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
    static bool _notifications, _seen, _poisoned, _poisonFlash;

    public void OnLoad()
    {
        _notifications = Runtime.View.GetBool(NoticeSetting, true);
        _seen = _poisonFlash = false;
    }
    public void OnUnload()
    {
        global::Kf2.NativeNotifications.Cancel(Owner);
        _seen = _poisonFlash = false;
    }
    public void DrawSettings()
    {
        if (ImGui.Checkbox("Poison notifications", ref _notifications))
        {
            Runtime.View.SetBool(NoticeSetting, _notifications);
            Runtime.SaveView();
        }
    }

    [PostHook("game", Address = 0x8002A550)]
    public static void Player(CpuContext c, IMemory m)
    {
        if (m.ReadU16(0x80199426) == 0) return;
        bool poisoned = (short)m.ReadU16(0x80199468) > 0;
        if (_notifications && ((!_seen && poisoned) || (_seen && poisoned != _poisoned)))
            global::Kf2.NativeNotifications.PostText(Owner, poisoned ? "POISONED" : "POISON CURED");
        _poisoned = poisoned; _seen = true;
        global::Kf2.NativeNotifications.Flush(c, m);
    }

    [PostHook("game", Address = 0x80024F90)]
    public static void PoisonDamage(CpuContext c, IMemory m)
    {
        // Only the stage-3 poison tick's HP subtraction owns this flash.
        if (c.RA == 0x8002B100) _poisonFlash = true;
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
    public static void Tint(CpuContext c, IMemory m)
    {
        if (_poisonFlash && c.RA == 0x8002A67C && c.A0 == 60 && c.A1 == 0 && c.A2 == 0)
        { c.A0 = 0; c.A1 = 60; }
    }

    [PostHook("game", Address = 0x8004A040)]
    public static void Loaded(CpuContext c, IMemory m)
    {
        global::Kf2.NativeNotifications.Cancel(Owner);
        _seen = _poisonFlash = false;
    }
}
