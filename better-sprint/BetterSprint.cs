using System;
using ImGuiNET;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.BetterSprint;

public sealed class BetterSprintMod : IMod
{
    const string Setting = "lexer.kf2.bettersprint.drain";
    const string FreeMagicSetting = "lexer.kf2.bettersprint.freemagic";
    const uint Power = 0x8019942E, MagicPower = 0x80199432, RunState = 0x8019941F;
    const uint Pad = 0x80199554, LastPad = 0x80199556, UseMask = 0x8006E568;
    const uint X = 0x801994EC, Z = 0x801994F4;
    const int FullPower = 5000, VanillaDrain = 500;
    const float DefaultRate = 25f;
    static float _rate = DefaultRate, _fraction;
    static bool _player, _inside, _eligible, _pending;
    static bool _freeMagic = true, _exhausted;
    static uint _x, _z;
    static ushort _physical, _magic, _mask, _now, _last;

    public void OnLoad()
    {
        float value = Runtime.View.GetFloat(Setting, DefaultRate);
        _rate = float.IsFinite(value) ? Math.Clamp(value, 0f, 300f) : DefaultRate;
        _freeMagic = Runtime.View.GetBool(FreeMagicSetting, true);
        Reset();
    }

    public void OnUnload()
    {
        if (Runtime.Mem != null) RestoreInput(Runtime.Mem);
        Reset();
    }

    public void DrawSettings()
    {
        if (ImGui.SliderFloat("Stamina drain (%/second)", ref _rate, 0f, 300f, "%.1f%%", ImGuiSliderFlags.AlwaysClamp))
        {
            _rate = float.IsFinite(_rate) ? Math.Clamp(_rate, 0f, 300f) : DefaultRate;
            Runtime.View.SetFloat(Setting, _rate);
            Runtime.SaveView();
        }
        if (ImGui.Checkbox("Running consumes no magic power", ref _freeMagic))
        {
            Runtime.View.SetBool(FreeMagicSetting, _freeMagic);
            Runtime.SaveView();
        }
    }

    static void Reset(bool requireRelease = false)
    {
        _player = _inside = _eligible = _pending = false;
        _mask = 0;
        _fraction = 0;
        _exhausted = requireRelease;
    }

    [PostHook("game", Address = 0x8004A040)]
    [PreHook("game", Address = 0x8002A264)] // death
    [PreHook("game", Address = 0x80025A94)] // native full status reset/respawn
    public static void Loaded(CpuContext c, IMemory m)
    {
        RestoreInput(m);
        Reset(true);
    }

    [PreHook("game", Address = 0x80022754)]
    public static void Menu(CpuContext c, IMemory m)
    {
        RestoreInput(m);
        _player = _inside = _eligible = _pending = false;
        // A modal interruption must not release an exhausted held run button.
        // Keep the latch and fractional charge until gameplay observes release.
    }

    [PreHook("game", Address = 0x8002A550)]
    public static void BeforePlayer(CpuContext c, IMemory m)
    {
        if (m.ReadU16(0x80199428) == 0 || m.ReadU8(0x801994E1) == 0x11)
        { Loaded(c, m); return; }
        StopEmptyRun(m);
        _x = m.ReadU32(X); _z = m.ReadU32(Z);
        _player = true;
        _pending = false;
    }

    [PreHook("game", Address = 0x8002957C)]
    public static void BeforeRun(CpuContext c, IMemory m)
    {
        _inside = true;
        _physical = m.ReadU16(Power); _magic = m.ReadU16(MagicPower);
        _now = m.ReadU16(Pad); _last = m.ReadU16(LastPad);
        ushort mask = (ushort)m.ReadU32(UseMask);
        bool held = (_now & mask) != 0 && (_last & mask) != 0;
        bool pressed = (_now & mask) != 0;
        if (!pressed) _exhausted = false;
        else if (_physical == 0) _exhausted = true;
        _eligible = _player && held && !_exhausted && m.ReadU8(0x801994D8) != 50 && _physical != 0;
        _mask = 0;
        // Runs after the fork translates its separate Run binding into the native
        // action bit. Scope only that bit; Interact and all other bindings survive.
        if (held && _exhausted)
            BlockRun(m, mask);
    }

    // Casting/quick item use precedes the run branch inside the native function.
    // Its legitimate changes must not be mistaken for running's fixed subtraction.
    [PostHook("game", Address = 0x80027DC0)]
    [PostHook("game", Address = 0x800474D0)]
    public static void AfterAction(CpuContext c, IMemory m)
    {
        if (!_inside) return;
        _physical = m.ReadU16(Power); _magic = m.ReadU16(MagicPower);
        if (_physical == 0)
        {
            _eligible = false; _exhausted = true;
            ushort mask = (ushort)m.ReadU32(UseMask);
            // An action may empty physical power after BeforeRun took its
            // snapshot. Stop the later native run branch as well, rather than
            // allowing it to remove magic power while physical power is empty.
            if ((_now & mask) != 0 && (_last & mask) != 0) BlockRun(m, mask);
        }
    }

    static void BlockRun(IMemory m, ushort mask)
    {
        _mask = mask;
        m.WriteU16(Pad, (ushort)(m.ReadU16(Pad) & ~mask));
        m.WriteU16(LastPad, (ushort)(m.ReadU16(LastPad) & ~mask));
        StopEmptyRun(m);
    }

    [PostHook("game", Address = 0x8002957C)]
    public static void AfterRun(CpuContext c, IMemory m)
    {
        RestoreInput(m);
        _inside = false;
        byte state = m.ReadU8(RunState);
        if (!_eligible || (state != 1 && state != 40)) return;
        if (m.ReadU16(Power) != Math.Max(0, _physical - VanillaDrain) ||
            m.ReadU16(MagicPower) != Math.Max(0, _magic - VanillaDrain)) return;
        // The game keeps its running behavior; charge at the end of collision-
        // resolved movement, so standing still or pushing a wall costs nothing.
        m.WriteU16(Power, _physical);
        m.WriteU16(MagicPower, _magic);
        m.WriteU8(RunState, 1);
        _pending = true;
    }

    static void RestoreInput(IMemory m)
    {
        if (_mask == 0) return;
        m.WriteU16(Pad, (ushort)((m.ReadU16(Pad) & ~_mask) | (_now & _mask)));
        m.WriteU16(LastPad, (ushort)((m.ReadU16(LastPad) & ~_mask) | (_last & _mask)));
        _mask = 0;
    }

    [PostHook("game", Address = 0x8002A550)]
    public static void AfterPlayer(CpuContext c, IMemory m)
    {
        _player = false;
        if (!_pending) return;
        _pending = false;
        if (m.ReadU32(X) == _x && m.ReadU32(Z) == _z) return;
        // The game world ticks at 15 Hz independently of rendering. Vanilla
        // removes 500/5000 per tick from both physical and magical power.
        float drain = _fraction + FullPower * (_rate / 100f) / 15f;
        int cost = (int)drain;
        _fraction = drain - cost;
        int left = Math.Max(0, m.ReadU16(Power) - cost);
        m.WriteU16(Power, (ushort)left);
        if (!_freeMagic)
            m.WriteU16(MagicPower, (ushort)Math.Max(0, m.ReadU16(MagicPower) - cost));
        if (left == 0)
        {
            _exhausted = true;
            m.WriteU8(RunState, 40);
            StopEmptyRun(m);
        }
    }

    static void StopEmptyRun(IMemory m)
    {
        if (m.ReadU16(Power) != 0 && !_exhausted) return;
        // Stage 3 derives this tick's walk speed from the previous run latch and
        // ramp before reading Run. Remove carried positive sprint acceleration;
        // leave negative environmental slowdowns intact.
        if ((short)m.ReadU16(0x80199422) > 0) m.WriteU16(0x80199422, 0);
        m.WriteU8(0x80199421, 1);
    }
}
