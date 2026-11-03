using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.ForgetMeNot;

public sealed class ForgetMeNotMod : IMod
{
    const uint Objects = 0x80177714, Definitions = 0x80175914, Area = 0x8017E060;
    const int Count = 396, Stride = 0x44, MaxStates = 16384, MaxBytes = 8 * 1024 * 1024;
    static readonly Dictionary<string, byte> States = new Dictionary<string, byte>();
    static readonly HashSet<int> Candidates = new HashSet<int>();
    static bool _using;
    static uint _loadSlot, _saveSlot, _pickup;
    static readonly uint[] Colours = new uint[8];
    static uint _rgbc, _gteRgbc;
    static bool _tinted;
    // 1 = searched/opened/emptied, 2 = an actual locked attempt. Absence = unchecked.
    public void OnLoad() { States.Clear(); Candidates.Clear(); _using = false; }
    public void OnUnload() { if (Runtime.Mem != null) RestoreLight(null!, Runtime.Mem); States.Clear(); Candidates.Clear(); _using = false; }

    static uint Record(int slot) => Objects + (uint)(slot * Stride);
    static int Slot(uint record) => record >= Objects && record < Objects + Count * Stride
        && (record - Objects) % Stride == 0 ? (int)((record - Objects) / Stride) : -1;
    static byte Kind(IMemory m, uint rec)
    {
        uint id = m.ReadU16(rec + 6);
        return id < 320 && id != 255 ? m.ReadU8(Definitions + id * 24) : (byte)255;
    }
    static bool Door(IMemory m, uint rec)
    {
        // These are the use handler's opening arms. The secret-door behaviour is
        // FF, and is deliberately never eligible, including before discovery.
        byte kind = Kind(m, rec), behaviour = m.ReadU8(rec + 4);
        return (kind == 2 || kind == 3) && (behaviour == 2 || behaviour == 3);
    }
    static bool Container(IMemory m, uint rec)
    {
        if (m.ReadU8(rec + 4) == 255) return false;
        byte kind = Kind(m, rec);
        return kind == 5 || kind == 8 || kind == 9 || kind == 21 || kind == 22;
    }
    static string Key(IMemory m, uint rec) => m.ReadU8(Area) + ":" + Slot(rec) + ":" + m.ReadU16(rec + 6);
    static void Set(IMemory m, uint rec, byte value)
    {
        if (Slot(rec) < 0 || (!Door(m, rec) && !Container(m, rec))) return;
        string key = Key(m, rec);
        if (States.Count < MaxStates || States.ContainsKey(key)) States[key] = value;
    }
    static bool Empty(IMemory m, uint rec)
    {
        uint child = m.ReadU16(rec + 0x3A);
        return child == 65535 || child < Count && m.ReadU16(Record((int)child) + 6) == 255;
    }
    public static byte Highlight(IMemory m, uint rec)
    {
        if (Slot(rec) < 0 || (!Door(m, rec) && !Container(m, rec))) return 0;
        if (Door(m, rec) && m.ReadU16(rec + 8) != 0) return 0;
        byte kind = Kind(m, rec);
        if ((kind == 5 || kind == 8 || kind == 22) && m.ReadU8(rec + 0x38) == 254 && Empty(m, rec)) return 0;
        byte state;
        if (States.TryGetValue(Key(m, rec), out state)) return state == 1 ? (byte)0 : (byte)2;
        return 1;
    }

    [PostHook("game", Address = 0x80034834)]
    public static void Glint(CpuContext c, IMemory m)
    {
        if (_tinted || c.RA != 0x800329A0 || ModelWalk.SubmitKind != ModelKind.Object) return;
        uint rec = ModelWalk.SubmitRecord;
        byte highlight = Highlight(m, rec);
        long phase = Environment.TickCount64 % 1800;
        if (highlight == 0 || phase >= 220) return;
        for (int i = 0; i < Colours.Length; i++) Colours[i] = Gte.ReadControl(13 + i);
        _rgbc = m.ReadU32(0x8006E604);
        _gteRgbc = Gte.Read(6);
        _tinted = true;
        // Brief surface glint in the model's own lighting, shared by the original
        // packets and retained GPU instances. It never draws through other geometry.
        double strength = 1.0 - Math.Abs(phase - 110) / 110.0;
        // Blend from the original lighting, so the pulse never begins or ends
        // with an unintended black flash.
        for (int i = 0; i < 3; i++)
        {
            int target = i == 0 || highlight == 1 ? 4096 : 0;
            Gte.WriteControl(13 + i, unchecked((uint)(int)((int)Colours[i] * (1 - strength) + target * strength)));
        }
        for (int i = 16; i <= 20; i++)
        {
            uint value = Colours[i - 13];
            short lo = (short)((short)value * (1 - strength));
            short hi = (short)((short)(value >> 16) * (1 - strength));
            Gte.WriteControl(i, (ushort)lo | (uint)(ushort)hi << 16);
        }
        uint tint = 0;
        for (int channel = 0; channel < 3; channel++)
        {
            int original = (int)(_rgbc >> (channel * 8) & 255);
            int target = channel == 0 || highlight == 1 ? 255 : 0;
            tint |= (uint)(original * (1 - strength) + target * strength) << (channel * 8);
        }
        m.WriteU32(0x8006E604, (_rgbc & 0xFF000000u) | tint);
    }

    [PostHook("game", Address = 0x80032588)]
    public static void RestoreLight(CpuContext c, IMemory m)
    {
        if (!_tinted) return;
        for (int i = 0; i < Colours.Length; i++) Gte.WriteControl(13 + i, Colours[i]);
        m.WriteU32(0x8006E604, _rgbc);
        Gte.Write(6, _gteRgbc);
        _tinted = false;
    }

    [PreHook("game", Address = 0x800489FC)]
    public static void BeginUse(CpuContext c, IMemory m) { _using = true; Candidates.Clear(); }

    [PostHook("game", Address = 0x80036EC8)]
    public static void Candidate(CpuContext c, IMemory m)
    {
        if (_using && c.RA == 0x80048B74 && c.V0 < Count) Candidates.Add((int)c.V0);
    }

    [PreHook("game", Address = 0x80033F08)]
    public static void Notification(CpuContext c, IMemory m)
    {
        if (!_using || Slot(c.S1) < 0) return;
        if (c.RA == 0x80048ECC && c.A0 == 6 && Container(m, c.S1)) Set(m, c.S1, 1);
        else if (c.RA == 0x80048F0C && (Door(m, c.S1) || Container(m, c.S1)))
        {
            byte kind = Kind(m, c.S1);
            Set(m, c.S1, (byte)((kind == 9 || kind == 21) && Empty(m, c.S1) ? 1 : 2));
        }
    }

    [PostHook("game", Address = 0x800489FC)]
    public static void EndUse(CpuContext c, IMemory m)
    {
        foreach (int slot in Candidates)
        {
            uint rec = Record(slot);
            if (Door(m, rec) && m.ReadU16(rec + 8) != 0) Set(m, rec, 1);
            if (Container(m, rec) && Empty(m, rec))
            {
                byte kind = Kind(m, rec);
                // A closed chest must actually be opened before its empty link is
                // knowledge. The exposed contents arms themselves count as a search.
                if (kind == 9 || kind == 21 || m.ReadU8(rec + 0x38) == 254) Set(m, rec, 1);
            }
        }
        _using = false;
        Candidates.Clear();
    }

    [PreHook("game", Address = 0x8004831C)]
    public static void BeginPickup(CpuContext c, IMemory m) { _pickup = c.A0; }
    [PostHook("game", Address = 0x8004831C)]
    public static void PickedUp(CpuContext c, IMemory m)
    {
        // The game's grant can refuse (capacity / declined pickup). Only an absent
        // linked item clears its container, never merely entering the pickup modal.
        int picked = Slot(_pickup);
        if (picked < 0 || m.ReadU16(_pickup + 6) != 255) return;
        for (int i = 0; i < Count; i++)
        {
            uint rec = Record(i);
            if (Container(m, rec) && m.ReadU16(rec + 0x3A) == picked) Set(m, rec, 1);
        }
    }

    [PostHook("game", Address = 0x8004905C)]
    public static void NewGame(CpuContext c, IMemory m) { States.Clear(); _using = false; Candidates.Clear(); }

    public sealed class Snapshot
    {
        public string Card { get; set; } = "";
        public uint Slot { get; set; }
        public string Hash { get; set; } = "";
        public Dictionary<string, byte> Values { get; set; } = new Dictionary<string, byte>();
    }
    static string DataFile => Path.Combine(Environment.CurrentDirectory, "mod-data", "lexer.kf2.forget-me-not.json");
    static string CardId() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        Path.GetFullPath(string.IsNullOrWhiteSpace(ConfigManager.Game.CardAPath) ? "carda.sav" : ConfigManager.Game.CardAPath).ToUpperInvariant())));
    static string Hash(IMemory m, uint buffer)
    {
        var bytes = new byte[0x3C00];
        for (uint i = 0; i < bytes.Length; i++) bytes[i] = m.ReadU8(buffer + i);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
    static List<Snapshot> Read()
    {
        try
        {
            if (!File.Exists(DataFile) || new FileInfo(DataFile).Length > MaxBytes) return new List<Snapshot>();
            var entries = JsonSerializer.Deserialize<List<Snapshot>>(File.ReadAllText(DataFile)) ?? new List<Snapshot>();
            return entries.Where(e => e.Slot >= 1 && e.Slot <= 3 && e.Values != null && e.Values.Count <= MaxStates
                && e.Card.Length == 64 && e.Hash.Length == 64).TakeLast(12).ToList();
        }
        catch (Exception e) { Console.Error.WriteLine("[Forget Me Not] Could not read exploration state: " + e.Message); return new List<Snapshot>(); }
    }
    static void Restore(IMemory m, uint buffer, uint slot)
    {
        States.Clear();
        string card = CardId(), hash = Hash(m, buffer);
        var entry = Read().LastOrDefault(e => e.Card == card && e.Slot == slot && e.Hash == hash);
        if (entry != null) foreach (var pair in entry.Values) if (pair.Value == 1 || pair.Value == 2) States[pair.Key] = pair.Value;
    }
    static void Save(IMemory m, uint buffer, uint slot)
    {
        if (slot < 1 || slot > 3) return;
        string card = CardId(), hash = Hash(m, buffer);
        var entries = Read();
        var matching = entries.Where(e => e.Card == card && e.Slot == slot).ToList();
        // Original plus newest per card/slot; at most twelve snapshots overall.
        entries.RemoveAll(e => e.Card == card && e.Slot == slot && (e.Hash == hash || e != matching.FirstOrDefault()));
        entries.Add(new Snapshot { Card = card, Slot = slot, Hash = hash, Values = new Dictionary<string, byte>(States) });
        entries = entries.TakeLast(12).ToList();
        string text = JsonSerializer.Serialize(entries);
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) { Console.Error.WriteLine("[Forget Me Not] Exploration state exceeds its storage limit; save unchanged."); return; }
        string temporary = DataFile + ".new";
        try { Directory.CreateDirectory(Path.GetDirectoryName(DataFile)!); File.WriteAllText(temporary, text); File.Move(temporary, DataFile, true); }
        catch (Exception e) { Console.Error.WriteLine("[Forget Me Not] Could not save exploration state: " + e.Message); }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception e) { Console.Error.WriteLine("[Forget Me Not] Could not remove temporary exploration state: " + e.Message); }
        }
    }

    [PreHook("game", Address = 0x80023638)]
    public static void BeginLoad(CpuContext c, IMemory m) { _loadSlot = c.A0; }
    [PreHook("game", Address = 0x8004A040)]
    public static void Loaded(CpuContext c, IMemory m) { Restore(m, c.A0, _loadSlot); _using = false; Candidates.Clear(); }
    [PreHook("game", Address = 0x80023764)]
    public static void BeginSave(CpuContext c, IMemory m) { _saveSlot = c.A0; }
    [PostHook("game", Address = 0x80023764)]
    public static void Saved(CpuContext c, IMemory m)
    {
        if (c.V0 == 0) Save(m, m.ReadU32(0x8006E98C) + 0x400, _saveSlot);
    }
}
