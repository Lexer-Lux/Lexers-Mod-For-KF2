using System;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;
using Game = Recompiled.KingsField2_game;

namespace Lexer.Kf2.AutoPickUpAll;

public sealed class AutoPickUpAllMod : IMod
{
    const uint Objects = 0x80177714, Definitions = 0x80175914, Player = 0x801994EC;
    const int Count = 396, Stride = 0x44, Radius = 384, Height = 768;
    static readonly ushort[] Rejected = new ushort[Count];
    static bool _active;
    static long _started;
    static int _area = -1;

    public void OnLoad() { Array.Clear(Rejected); _area = -1; _active = false; Event.AddListener<PadReadEvent>(Confirm); }
    public void OnUnload() { _active = false; Event.RemoveListener<PadReadEvent>(Confirm); Array.Clear(Rejected); }

    static uint Record(int slot) => Objects + (uint)(slot * Stride);
    static bool Container(IMemory m, uint rec)
    {
        uint id = m.ReadU16(rec + 6);
        if (id >= 320 || id == 255 || m.ReadU8(rec + 4) == 255) return false;
        byte kind = m.ReadU8(Definitions + id * 24);
        return kind == 5 || kind == 8 || kind == 9 || kind == 21 || kind == 22;
    }
    static bool Linked(IMemory m, int slot)
    {
        for (int i = 0; i < Count; i++)
        {
            uint rec = Record(i);
            if (Container(m, rec) && m.ReadU16(rec + 0x3A) == slot) return true;
        }
        return false;
    }
    static bool Near(IMemory m, uint rec)
    {
        long dx = (int)m.ReadU32(rec + 0x14) - (long)(int)m.ReadU32(Player);
        long dy = (int)m.ReadU32(rec + 0x18) - (long)(int)m.ReadU32(Player + 4);
        long dz = (int)m.ReadU32(rec + 0x1C) - (long)(int)m.ReadU32(Player + 8);
        // Bound before squaring, including malformed or absent world records.
        return Math.Abs(dx) <= Radius && Math.Abs(dz) <= Radius && Math.Abs(dy) <= Height
            && dx * dx + dz * dz <= Radius * Radius;
    }
    static void Confirm(PadReadEvent e)
    {
        if (!_active || e.Port != 0 || Runtime.Mem == null) return;
        ushort use = Runtime.Mem.ReadU16(0x8006E568);
        // Only the pickup call owned by this mod receives synthetic confirmation.
        // A clean release then a press also works while physical movement is held.
        e.Buttons = (Environment.TickCount64 - _started) % 400 < 200 ? ushort.MaxValue : (ushort)~use;
    }

    [PostHook("game", Address = 0x8002A550)]
    public static void Tick(CpuContext c, IMemory m)
    {
        if (_active || FramePacing.Paused || m.ReadU8(0x801994E1) != 0 || m.ReadU16(0x80199424) == 0) return;
        int area = m.ReadU8(0x8017E060);
        if (_area != area) { Array.Clear(Rejected); _area = area; }
        for (int i = 0; i < Count; i++)
        {
            uint rec = Record(i), id = m.ReadU16(rec + 6);
            if (!Near(m, rec)) { Rejected[i] = 0; continue; }
            if (Rejected[i] == id + 1 || id >= 120 || m.ReadU8(rec + 4) == 255
                || m.ReadU8(Definitions + id * 24) != 0x40 || m.ReadU8(rec + 0x38) != 255 || Linked(m, i)) continue;
            bool gold = m.ReadU8(Definitions + id * 24 + 1) == 0x20;
            // Let the native pickup keep all its own gold, quest and special-item
            // handling. Capacity-full ordinary items do not spam a modal each tick.
            if (!gold && (m.ReadU8(0x8009B52C + id) >= 99 || m.ReadU16(0x8006E568) == 0)) continue;
            var cpu = c.Snapshot();
            _active = true; _started = Environment.TickCount64;
            try
            {
                c.SP -= 0x100;
                c.A0 = rec;
                Game.func_8004831C(c, m);
            }
            finally { c.Restore(cpu); _active = false; }
            Rejected[i] = m.ReadU16(rec + 6) != 255 ? (ushort)(id + 1) : (ushort)0;
            return; // one native pickup at a time
        }
    }

    [PostHook("game", Address = 0x8004A040)]
    public static void Loaded(CpuContext c, IMemory m) { Array.Clear(Rejected); _area = -1; }
    [PostHook("game", Address = 0x8004905C)]
    public static void NewGame(CpuContext c, IMemory m) { Array.Clear(Rejected); _area = -1; }
}
