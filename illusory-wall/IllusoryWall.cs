using System;
using System.Collections.Generic;
using ImGuiNET;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;
using Game = Recompiled.KingsField2_game;

namespace Lexer.Kf2.IllusoryWall;

public sealed class IllusoryWallMod : IMod
{
    const uint Objects = 0x80177714, Definitions = 0x80175914, Map = 0x801C8484;
    const uint DoorPattern = 0x8006886C;
    const string Prefix = "lexer.kf2.illusorywall.";
    const int Count = 396, Stride = 0x44, PatternLimit = 16;
    static bool _loaded, _onUse = true, _onAttack, _walkThrough, _retrying;
    readonly record struct Query(uint Position, uint Radius, uint Height);
    readonly record struct Change(uint Address, byte Before, byte During);
    static readonly Stack<Query> Queries = new();
    static readonly List<Change> Changes = new(Count * PatternLimit * 4);
    static int _changesArea = -1;

    public void OnLoad()
    {
        Reset();
        _onUse = Runtime.View.GetBool(Prefix + "onuse", true);
        _onAttack = Runtime.View.GetBool(Prefix + "onattack", false);
        _walkThrough = Runtime.View.GetBool(Prefix + "walkthrough", false);
        _loaded = true;
        Event.AddListener<OverlayLoadedEvent>(Overlay);
    }
    public void OnUnload()
    {
        _loaded = false;
        Event.RemoveListener<OverlayLoadedEvent>(Overlay);
        Reset();
    }
    public void DrawSettings()
    {
        if (ImGui.Checkbox("On use", ref _onUse)) Save("onuse", _onUse);
        if (ImGui.Checkbox("On attack", ref _onAttack)) Save("onattack", _onAttack);
        if (ImGui.Checkbox("Walk through", ref _walkThrough)) Save("walkthrough", _walkThrough);
    }
    static void Save(string key, bool value) { Runtime.View.SetBool(Prefix + key, value); Runtime.SaveView(); }
    static void Overlay(OverlayLoadedEvent e) => Reset();
    static void Reset()
    {
        if (Runtime.Mem != null) Restore(Runtime.Mem);
        Queries.Clear(); _retrying = false;
    }
    [PreHook("game", Address = 0x8004A040)]
    [PreHook("game", Address = 0x8002A264)]
    [PreHook("game", Address = 0x80025A94)]
    public static void Loaded(CpuContext c, IMemory m) => Reset();

    // Deliberately exact native wall definitions, not kind2, behaviorFF, or the
    // renderer's historical kind0E label (kind0E actually opens the save menu).
    // 82 and CD have the same wall geometry; native CD use was observed opening
    // its continuous rock panel into a passage. 84 is a visible barred gate.
    internal static bool Hidden(IMemory m, int slot)
    {
        if ((uint)slot >= Count) return false;
        uint record = Objects + (uint)(slot * Stride);
        uint id = m.ReadU16(record + 6);
        return id is 0x82 or 0xCD && m.ReadU8(record + 4) == 2
            && m.ReadU8(Definitions + id * 24) == 2;
    }
    static bool Closed(IMemory m, uint record) => m.ReadU16(record + 8) == 0 && m.ReadU8(record + 0x38) == 255;

    // Only the real use handler's candidate scan is filtered. Calling the native
    // scan again with the next index retains its facing/range/height decisions.
    [PreHook("game", Address = 0x80036EC8)]
    public static void BeforeUseQuery(CpuContext c, IMemory m)
    {
        if (_loaded && !_retrying && c.RA == 0x80048B74)
            Queries.Push(new(c.A1, c.A2, c.A3));
    }
    [PostHook("game", Address = 0x80036EC8)]
    public static void AfterUseQuery(CpuContext c, IMemory m)
    {
        if (!_loaded || _retrying || c.RA != 0x80048B74 || Queries.Count == 0) return;
        var query = Queries.Pop();
        if (_onUse || !Hidden(m, (int)c.V0)) return;
        var saved = c.Snapshot();
        uint found = c.V0;
        _retrying = true;
        try
        {
            for (int i = 0; i < Count && Hidden(m, (int)found); i++)
            {
                c.Restore(saved);
                c.A0 = found + 1; c.A1 = query.Position; c.A2 = query.Radius; c.A3 = query.Height;
                Game.func_80036EC8(c, m);
                if (c.V0 <= found && c.V0 != uint.MaxValue) { found = uint.MaxValue; break; }
                found = c.V0;
            }
        }
        finally { c.Restore(saved); c.V0 = found; _retrying = false; }
    }

    static bool Gameplay(CpuContext c, IMemory m) => m.ReadU16(0x80199428) != 0 && MenuWorld.Quiet(c, m);

    // RA27BE0 is the real weapon's spatial hit sample in func271D0, not an
    // attack-button edge; charging, menus and rejected swing starts do nothing.
    [PreHook("game", Address = 0x8003B72C)]
    public static void WeaponSample(CpuContext c, IMemory m)
    {
        if (!_loaded || !_onAttack || c.RA != 0x80027BE0 || !Gameplay(c, m)) return;
        for (int i = 0; i < Count; i++)
        {
            uint record = Objects + (uint)(i * Stride);
            if (!Hidden(m, i) || !Closed(m, record) || !NearHeight(m, record, (int)c.A1)) continue;
            if (FootprintContains(c, m, record, (int)c.A0 >> 11, (int)c.A2 >> 11))
                m.WriteU16(record + 8, 1); // native kind2 opening path; all motion/sound/save handling stays native
        }
    }

    static bool NearHeight(IMemory m, uint record, int y)
        => Math.Abs((long)(int)m.ReadU32(record + 0x18) - y) <= 2500;

    static (int Cos, int Sin) Rotation(CpuContext c, IMemory m, uint record)
    {
        var saved = c.Snapshot();
        try
        {
            c.A0 = m.ReadU16(record + 0x26); Game.rcos(c, m); int cos = (int)c.V0;
            c.A0 = m.ReadU16(record + 0x26); Game.rsin(c, m);
            return (cos, (int)c.V0);
        }
        finally { c.Restore(saved); }
    }
    static uint PatternCell(IMemory m, uint record, uint pattern, int cos, int sin)
    {
        int dx = (sbyte)m.ReadU8(pattern + 8), dz = (sbyte)m.ReadU8(pattern + 9);
        int x = ((int)m.ReadU32(record + 0x14) >> 11) + ((dx * cos - dz * sin) >> 12);
        int z = ((int)m.ReadU32(record + 0x1C) >> 11) + ((dz * cos + dx * sin) >> 12);
        if ((uint)x >= 80 || (uint)z >= 80) return 0;
        // func35CC8: record+0 ==1 selects lower; all other native halves upper.
        return Map + (uint)(z * 800 + x * 10) + (m.ReadU8(record) == 1 ? 0u : 5u);
    }
    static bool ValidPattern(IMemory m)
    {
        for (int i = 0; i < PatternLimit; i++) if (m.ReadU8(DoorPattern + (uint)i * 10) == 255) return i > 0;
        return false; // uninitialized/malformed RAM is not a door footprint
    }
    static bool FootprintContains(CpuContext c, IMemory m, uint record, int x, int z)
    {
        if (!ValidPattern(m) || (uint)x >= 80 || (uint)z >= 80) return false;
        var rotation = Rotation(c, m, record);
        uint candidate = Map + (uint)(z * 800 + x * 10);
        for (int i = 0; i < PatternLimit; i++)
        {
            uint pattern = DoorPattern + (uint)i * 10;
            if (m.ReadU8(pattern) == 255) break;
            uint cell = PatternCell(m, record, pattern, rotation.Cos, rotation.Sin);
            if (cell == candidate || cell == candidate + 5) return true;
        }
        return false;
    }

    // Native horizontal slide, WallGuard's same-player preflight, and the
    // player's vertical floor/step queries in28560. The opened floor must be
    // queried too: a closed door footprint has no walkable floor inside it.
    // Do not replace28080: the fork's WallGuard already owns it.
    static bool MovementQuery(CpuContext c) => c.RA is 0x80028150 or 0x800283BC or 0x80028080
        or 0x800285A0 or 0x800286FC or 0x80028848 or 0x800289D8;
    [Replace("game", Address = 0x8002C700)]
    [Replace("game", Address = 0x8002C330)]
    public static void Collision(Action<CpuContext, IMemory> orig, CpuContext c, IMemory m)
    {
        if (!_loaded || !_walkThrough || !MovementQuery(c)) { orig(c, m); return; }
        try { PrepareCollision(c, m); orig(c, m); }
        finally { Restore(m); }
    }
    internal static void PrepareCollision(CpuContext c, IMemory m)
    {
        if (!_loaded || !_walkThrough || !MovementQuery(c) || !Gameplay(c, m) || !ValidPattern(m)) return;
        Restore(m);
        int x = (int)c.A0 >> 11, z = (int)c.A2 >> 11;
        for (int i = 0; i < Count; i++)
        {
            uint record = Objects + (uint)(i * Stride);
            if (!Hidden(m, i) || !Closed(m, record) || !NearHeight(m, record, (int)c.A1)) continue;
            int rx = (int)m.ReadU32(record + 0x14) >> 11, rz = (int)m.ReadU32(record + 0x1C) >> 11;
            if (Math.Abs((long)rx - x) > 2 || Math.Abs((long)rz - z) > 2) continue;
            var rotation = Rotation(c, m, record);
            for (int j = 0; j < PatternLimit; j++)
            {
                uint pattern = DoorPattern + (uint)j * 10;
                if (m.ReadU8(pattern) == 255) break;
                uint cell = PatternCell(m, record, pattern, rotation.Cos, rotation.Sin);
                if (cell == 0) continue;
                uint other = m.ReadU8(record) == 1 ? cell + 5 : cell - 5;
                // func35CC8's pattern pairs the selected and opposite halves.
                // FE shape / FF model retain that field, not a guessed value.
                Replace(m, cell + 3, m.ReadU8(pattern), m.ReadU8(pattern + 4), 254);
                Replace(m, cell, m.ReadU8(pattern + 2), m.ReadU8(pattern + 6), 255);
                Replace(m, other + 3, m.ReadU8(pattern + 1), m.ReadU8(pattern + 5), 254);
                Replace(m, other, m.ReadU8(pattern + 3), m.ReadU8(pattern + 7), 255);
            }
        }
    }
    static void Replace(IMemory m, uint address, byte closed, byte open, byte sentinel)
    {
        if (closed == sentinel || open == sentinel || closed == open || m.ReadU8(address) != closed) return;
        if (Changes.Count == 0) _changesArea = m.ReadU8(0x8017E060);
        Changes.Add(new(address, closed, open)); m.WriteU8(address, open);
    }
    static void Restore(IMemory m)
    {
        for (int i = Changes.Count - 1; i >= 0 && _changesArea == m.ReadU8(0x8017E060); i--)
        {
            var change = Changes[i];
            if (m.ReadU8(change.Address) == change.During) m.WriteU8(change.Address, change.Before);
        }
        Changes.Clear(); _changesArea = -1;
    }
}
