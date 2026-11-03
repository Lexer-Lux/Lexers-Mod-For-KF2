using System;
using System.Reflection;
using Kf2;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.BetterCrystals;

public sealed class BetterCrystalsMod : IMod
{
    public const uint Count = 0x8009B578, DragonFlag = 0x801B3089;
    public const uint State = 0x801994E1, Hp = 0x80199428, MaxHp = 0x80199426;
    public const uint DeathFrames = 0x8019951A, CurrentSlot = 0x8006E5D4;
    static IDisposable? _reloadHook;
    static bool _enabled;
    public static bool Choosing { get; private set; }
    static readonly MethodInfo Load = typeof(AutoReload).GetMethod("LoadSlot", BindingFlags.Static | BindingFlags.NonPublic)!;

    public void OnLoad()
    {
        Choosing = false;
        // The port's automatic reload otherwise clamps death at frame31 and
        // prevents the original Dragon Crystal branch at frame65 from running.
        // Hold only that eligible death, without editing the player's setting.
        // The source compiler takes its references from assemblies already loaded.
        // Resolve the detour at runtime so compilation needs no MonoMod reference.
        var hookType = Assembly.Load("MonoMod.RuntimeDetour").GetType("MonoMod.RuntimeDetour.Hook", throwOnError: true)!;
        _reloadHook = (IDisposable)hookType.GetConstructor(new[] { typeof(MethodBase), typeof(MethodInfo) })!
            .Invoke(new object[] { typeof(AutoReload).GetMethod(nameof(AutoReload.AfterPlayerStage))!,
                typeof(BetterCrystalsMod).GetMethod(nameof(AllowNativeDecision))! });
        _enabled = true;
    }

    public void OnUnload()
    {
        _enabled = false;
        _reloadHook?.Dispose();
        _reloadHook = null;
        // The modal checks _enabled on each native frame and then hands the
        // unmodified death back to the game. No synthetic button remains held.
    }

    public static bool Eligible(IMemory m) => m.ReadU8(State) == 0x11 &&
        m.ReadU16(Hp) == 0 && m.ReadU16(MaxHp) != 0 && m.ReadU8(Count) != 0 &&
        (m.ReadU8(DragonFlag) & 8) != 0;

    public static void AllowNativeDecision(Action<CpuContext, IMemory> original, CpuContext c, IMemory m)
    {
        if (!_enabled || !Eligible(m)) original(c, m);
    }

    [Replace("game", Address = 0x8002A550)]
    public static void Player(Action<CpuContext, IMemory> original, CpuContext c, IMemory m)
    {
        if (Choosing) return;
        // Earlier native death branches (including the frame31 special case)
        // execute normally. This is the final tick before the actual frame65
        // count/flag checks and native consumption/area transition.
        if (!_enabled || !Eligible(m) || m.ReadU16(DeathFrames) != 64)
        {
            original(c, m);
            return;
        }
        Choosing = true;
        try
        {
            string error = "";
            while (_enabled && Eligible(m))
            {
                int choice = NativeChoice.Show(c, m, () => _enabled && Eligible(m), error);
                if (choice != 1) break;
                byte slot = m.ReadU8(CurrentSlot);
                if (slot < 1 || slot > 3)
                {
                    error = "NO SAVE HAS BEEN LOADED OR SAVED";
                    continue;
                }
                if (Reload(c, m, slot)) return;
                error = "COULD NOT LOAD THAT SAVE";
            }
        }
        finally { Choosing = false; }
        // Only the original death branch consumes one crystal, restores native
        // HP/MP/status and travels to the native revival location.
        original(c, m);
    }

    public static bool Reload(CpuContext c, IMemory m, byte slot)
    {
        if (slot < 1 || slot > 3) return false;
        var saved = c.Snapshot();
        try
        {
            object[] args = { c, m, slot, 0u };
            return (uint)Load.Invoke(null, args)! == 0;
        }
        catch (TargetInvocationException) { return false; }
        finally { c.Restore(saved); }
    }
}
