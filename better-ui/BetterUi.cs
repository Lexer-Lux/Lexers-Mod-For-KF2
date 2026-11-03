using System.Collections.Generic;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.BetterUi;

public sealed class BetterUiMod : IMod
{
    const string Owner = "lexer.kf2.better-ui";
    public void OnLoad() { }
    public void OnUnload() => global::Kf2.NativeNotifications.Cancel(Owner);

    [PreHook("game", Address = 0x80046C64)]
    public static void Pickup(CpuContext c, IMemory m)
    {
        // These are the native pickup presentation's two initial placements,
        // after its lock/quest guards. Rejected interactions never reach them.
        if (c.RA != 0x8004857C && c.RA != 0x80048610) return;
        uint record = m.ReadU32(c.SP + 0x80);
        uint item = m.ReadU16(record + 6);
        if (item >= 120 || m.ReadU8(0x80175914 + item * 24 + 1) == 0x20) return;
        var name = new List<byte>(24);
        for (uint i = 0; i < 24; i++)
        {
            byte value = m.ReadU8(0x80065B24 + item * 24 + i);
            if (value == 0xFF) break;
            name.Add(value);
        }
        if (name.Count == 1 && name[0] == 0) return;
        global::Kf2.NativeNotifications.Post(Owner, name.ToArray());
        global::Kf2.NativeNotifications.Flush(c, m);
    }
}
