using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Hardware;
using RecompOne.Runtime.Memory;
using Game = Recompiled.KingsField2_game;

namespace Lexer.Kf2.BetterCrystals;

/// <summary>The game's own confirmation boxes, label font, number font and
/// native menu frame/input routines. No host window or overlay imitation.</summary>
static class NativeChoice
{
    const uint LabelFont = 0x80064BF0, NumberFont = 0x80064BE4;
    // Both original font routines advance seven pixels and stop at24 glyphs.
    const int Advance = 7, NativeRun = 24;
    const uint Ok = 0x8006E568, Cancel = 0x8006E56C, CancelAlt = 0x8006E57C;
    static readonly string[] Buttons = { "Select", "L3", "R3", "Start", "Up", "Right", "Down", "Left", "L2", "R2", "L1", "R1", "Triangle", "Circle", "Cross", "Square" };
    static readonly MethodInfo PadLabel = typeof(ConfigManager).Assembly.GetType("RecompOne.Runtime.Host.Window.InputSettingsSection")!
        .GetMethod("PadLabel", BindingFlags.NonPublic | BindingFlags.Static)!;

    public static int Show(CpuContext c, IMemory m, Func<bool> active, string error)
    {
        var saved = c.Snapshot();
        uint stack = c.SP - 0x600;
        if (m is not PSMemory ram || (ulong)(stack & 0x1FFFFFFF) + 0x600 > (ulong)ram.Ram.Length)
            throw new InvalidOperationException("Native choice stack is outside RAM");
        c.SP = stack;
        uint first = stack + 0x80, second = stack + 0xC0, text = stack + 0x100;
        for (uint i = 0; i < 0x400; i++) m.WriteU8(stack + 0x80 + i, 0);
        int choice = -1;
        try
        {
            while (active())
            {
                c.SP = stack;
                Game.func_80022E90(c, m);
                Game.func_80022E58(c, m);
                uint pressed = c.V0;
                uint confirm = m.ReadU32(Ok), cancel = m.ReadU32(Cancel) | m.ReadU32(CancelAlt);
                if ((pressed & confirm) != 0) choice = 0;
                else if ((pressed & cancel) != 0) choice = 1;
                // Draw twice as the native confirmation loop does, so both
                // display buffers contain the original game's menu elements.
                for (int buffer = 0; buffer < 2; buffer++)
                {
                    c.SP = stack;
                    Game.func_80022530(c, m);
                    c.A0 = 0;
                    Game.func_8002156C(c, m);
                    DrawText(c, m, text, 32, 40, "YOU HAVE");
                    int count = m.ReadU8(BetterCrystalsMod.Count);
                    int x = 32 + 9 * Advance;
                    DrawNumber(c, m, text, x, 40, count);
                    x += (count.ToString().Length + 1) * Advance;
                    DrawText(c, m, text, x, 40, count == 1 ? "DRAGON CRYSTAL" : "DRAGON CRYSTALS");
                    DrawText(c, m, text, 32, 60, "REVIVE OR RELOAD YOUR LAST SAVE?");
                    Record(m, first, 32, 100, "REVIVE");
                    Record(m, second, 32, 148, "RELOAD");
                    c.A0 = first; c.A1 = second; c.A2 = choice == 1 ? 1u : 0u; c.A3 = choice >= 0 ? 1u : 0u;
                    Game.func_80021478(c, m);
                    DrawBindings(c, m, text, 100, 100, Describe(m.ReadU32(Ok)));
                    DrawBindings(c, m, text, 100, 148, Describe(m.ReadU32(Cancel) | m.ReadU32(CancelAlt)));
                    if (error.Length != 0) DrawText(c, m, text, 32, 196, error);
                    Game.func_800226A8(c, m);
                }
                if (choice >= 0)
                {
                    // Native release prevents this press leaking into the
                    // loaded game or the original revival branch.
                    Game.func_80022EFC(c, m);
                    return choice;
                }
            }
            return -1;
        }
        finally { c.Restore(saved); }
    }

    static void DrawBindings(CpuContext c, IMemory m, uint record, int x, int y, string labels)
    {
        int columns = Math.Max(1, (312 - x) / Advance);
        var lines = new List<string>();
        string line = "";
        foreach (string word in labels.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length != 0 && line.Length + word.Length + 1 > columns)
            {
                lines.Add(line);
                line = "";
            }
            line += (line.Length == 0 ? "" : " ") + word;
        }
        if (line.Length != 0) lines.Add(line);
        // Large custom binding sets cycle explicitly labelled pages instead
        // of silently dropping bindings that do not fit beside the button.
        int rows = lines.Count > 3 ? 2 : 3;
        int pages = Math.Max(1, (lines.Count + rows - 1) / rows);
        int page = (int)((Environment.TickCount64 / 3000) % pages);
        for (int row = 0; row < rows && page * rows + row < lines.Count; row++)
            DrawText(c, m, record, x, y + row * 15, lines[page * rows + row]);
        if (pages > 1) DrawText(c, m, record, x, y + 30, $"PAGE {page + 1} OF {pages}");
    }

    public static string Describe(uint nativeMask)
    {
        ushort mask = (ushort)((nativeMask >> 8) | (nativeMask << 8));
        var labels = new List<string>();
        for (int i = 0; i < Buttons.Length; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            string name = Buttons[i];
            foreach (object keys in new object[] { ConfigManager.Game.Keys, ConfigManager.Game.Keys2 })
            {
                string? key = keys.GetType().GetProperty(name)?.GetValue(keys) as string;
                if (!string.IsNullOrWhiteSpace(key)) labels.Add(key);
            }
            object pad = ConfigManager.Game.PadFor(0);
            if (pad.GetType().GetProperty(name)?.GetValue(pad) is int[] bindings)
                foreach (int button in bindings) labels.Add((string)PadLabel.Invoke(null, new object[] { button })!);
        }
        // Keep the port's own configured physical labels (including paired
        // PS/Xbox names), rather than guessing a connected controller family.
        return string.Join(" OR ", labels.Select(Normalize).Where(s => s.Length != 0).Distinct()).Trim() is { Length: > 0 } value ? value : "UNBOUND";
    }

    static string Normalize(string value) => string.Join(" ", new string(value.ToUpperInvariant().Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    static void Record(IMemory m, uint address, int x, int y, string value)
    {
        m.WriteU16(address, (ushort)x); m.WriteU16(address + 2, (ushort)y);
        int length = Math.Min(value.Length, 100);
        for (int i = 0; i < length; i++) m.WriteU8(address + 4 + (uint)i, value[i] == ' ' ? (byte)127 : value[i] == '?' ? (byte)58 : (byte)(value[i] - 'A'));
        m.WriteU8(address + 4 + (uint)length, 255);
    }
    static void DrawText(CpuContext c, IMemory m, uint record, int x, int y, string value)
    {
        int offset = 0;
        while (offset < value.Length)
        {
            int end = offset + 1;
            bool number = char.IsAsciiDigit(value[offset]);
            while (end < value.Length && end - offset < NativeRun && char.IsAsciiDigit(value[end]) == number) end++;
            string part = value.Substring(offset, end - offset);
            if (number)
            {
                m.WriteU16(record, (ushort)x); m.WriteU16(record + 2, (ushort)y);
                for (int i = 0; i < part.Length; i++) m.WriteU8(record + 4 + (uint)i, (byte)(part[i] - '0'));
                m.WriteU8(record + 4 + (uint)part.Length, 255);
            }
            else Record(m, record, x, y, part);
            c.A0 = number ? NumberFont : LabelFont; c.A1 = record;
            if (number) Game.func_80021FCC(c, m); else Game.func_80021E10(c, m);
            x += part.Length * Advance;
            offset = end;
        }
    }
    static void DrawNumber(CpuContext c, IMemory m, uint record, int x, int y, int number) => DrawText(c, m, record, x, y, number.ToString());
}
