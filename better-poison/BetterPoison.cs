using System;
using ImGuiNET;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.BetterPoison;

public sealed class BetterPoisonMod : IMod
{
    const string Owner = "lexer.kf2.better-poison";
    const string NoticeSetting = "lexer.kf2.betterpoison.notifications";
    const string IntensitySetting = "lexer.kf2.betterpoison.intensity";
    const string FrequencySetting = "lexer.kf2.betterpoison.frequency";
    static bool _notifications, _seen, _poisoned, _poisonFlash, _firstPulse, _newPulse;
    static float _intensity = 35f, _frequency = .25f, _phase;
    static int _flashTicks;

    public void OnLoad()
    {
        _notifications = Runtime.View.GetBool(NoticeSetting, true);
        _intensity = Bounded(Runtime.View.GetFloat(IntensitySetting, 35f), 35f, 100f);
        _frequency = Bounded(Runtime.View.GetFloat(FrequencySetting, .25f), .25f, .5f);
        Reset();
    }
    public void OnUnload()
    {
        Reset();
    }
    public void DrawSettings()
    {
        if (ImGui.Checkbox("Poison notifications", ref _notifications))
        {
            if (!_notifications) global::Kf2.NativeNotifications.Cancel(Owner);
            Runtime.View.SetBool(NoticeSetting, _notifications);
            Runtime.SaveView();
        }
        if (ImGui.SliderFloat("Flash peak intensity (%)", ref _intensity, 0f, 100f, "%.1f%%", ImGuiSliderFlags.AlwaysClamp))
        {
            _intensity = Bounded(_intensity, 35f, 100f);
            Runtime.View.SetFloat(IntensitySetting, _intensity);
            Runtime.SaveView();
        }
        if (ImGui.SliderFloat("Flash frequency (Hz)", ref _frequency, 0f, .5f, "%.2f", ImGuiSliderFlags.AlwaysClamp))
        {
            _frequency = Bounded(_frequency, .25f, .5f);
            Runtime.View.SetFloat(FrequencySetting, _frequency);
            Runtime.SaveView();
        }
    }

    static float Bounded(float value, float fallback, float max) =>
        float.IsFinite(value) ? Math.Clamp(value, 0f, max) : fallback;

    static void Reset()
    {
        global::Kf2.NativeNotifications.Cancel(Owner);
        _seen = _poisoned = _poisonFlash = _newPulse = false;
        _flashTicks = 0; _phase = 0f; _firstPulse = true;
    }

    [PostHook("game", Address = 0x8002A550)]
    public static void Player(CpuContext c, IMemory m)
    {
        // Every prefix runs even when pacing skips the native body; only here
        // has the pacing prefix made its final normal/fallback tick decision.
        if (!global::Kf2.FramePacing.TickedThisFrame) return;
        // 26 is maximum HP; current HP is28. A death can clear poison before
        // the next post-player hook, and must not be mistaken for a cure.
        if (m.ReadU16(0x80199428) == 0 || m.ReadU8(0x801994E1) == 0x11)
        { Reset(); return; }
        // The native tint occurs before the late poison damage event. A newly
        // accepted pulse must retain40 so its first tint sees the full peak.
        if (!_newPulse && _flashTicks > 0) _flashTicks--;
        _newPulse = false;
        bool poisoned = (short)m.ReadU16(0x80199468) > 0;
        if ((!_seen && poisoned) || (_seen && poisoned != _poisoned))
        {
            // These are current status notices. Replace this owner's obsolete
            // status rather than leaving POISONED ahead of a cure in the FIFO.
            // Other owners and vanilla pickups keep their own queue entries.
            global::Kf2.NativeNotifications.Cancel(Owner);
            if (_notifications)
                global::Kf2.NativeNotifications.PostText(Owner, poisoned ? "POISONED" : "POISON CURED");
        }
        _poisoned = poisoned; _seen = true;
        if (!poisoned) { _phase = 0f; _flashTicks = 0; _firstPulse = true; _newPulse = false; }
        global::Kf2.NativeNotifications.Flush(c, m);
    }

    [PostHook("game", Address = 0x80024F90)]
    public static void PoisonDamage(CpuContext c, IMemory m)
    {
        // Only the stage-3 poison tick's HP subtraction owns this flash.
        if (c.RA != 0x8002B100) return;
        if (m.ReadU16(0x80199428) == 0 || m.ReadU8(0x801994E1) == 0x11)
        { Reset(); return; }
        _poisonFlash = true;
        // The native poison event arrives every30 world ticks (2seconds).
        // Gate visuals only; HP damage and the poison counter remain native.
        bool first = _firstPulse;
        _firstPulse = false;
        if (_frequency <= 0) { _flashTicks = 0; return; }
        _phase = first ? 1f : _phase + _frequency * 2f;
        if (_phase >= 1f)
        {
            _flashTicks = 40;
            _newPulse = true;
            _phase -= 1f;
        }
    }

    [PreHook("game", Address = 0x8002A158)]
    [PreHook("game", Address = 0x8002A1EC)]
    public static void OtherDamage(CpuContext c, IMemory m) => _poisonFlash = false;

    [PreHook("game", Address = 0x80024FE0)]
    public static void Damage(CpuContext c, IMemory m)
    {
        // The damage routine ignores zero-damage calls and this player state.
        // Neither starts a replacement flash, so retain an existing poison wash.
        if (c.A1 != 0 && m.ReadU8(0x801994E1) != 0x11) _poisonFlash = false;
    }

    [PreHook("game", Address = 0x8003236C)]
    public static bool Tint(CpuContext c, IMemory m)
    {
        if (_poisonFlash && c.RA == 0x8002A67C && c.A0 == 60 && c.A1 == 0 && c.A2 == 0)
        {
            c.A0 = 0; c.A1 = 60;
            // Own envelope avoids skipped native events resetting the wash or
            // turning it red. Native A3 scales RGB in Q12; it is not primitive alpha.
            c.A3 = (uint)Math.Clamp((int)MathF.Round(4096f * (_intensity / 100f) * (_flashTicks / 40f)), 0, 4096);
            // A zero RGB contribution still increments the native averaging
            // count. Skip it entirely so a hidden poison wash cannot dim a
            // simultaneous unrelated tint.
            return 60u * c.A3 >= 4096;
        }
        return true;
    }

    [PostHook("game", Address = 0x8004A040)]
    [PreHook("game", Address = 0x8002A264)] // death, including native respawn
    [PreHook("game", Address = 0x80025A94)] // full status reset, not an antidote
    public static void Loaded(CpuContext c, IMemory m) => Reset();
}
