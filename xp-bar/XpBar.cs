using System;
using Kf2;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.XpBar;

public sealed class XpBarMod : IMod
{
    const uint Experience = 0x80199414, Next = 0x80199418, Level = 0x8019941C;
    const uint LevelTable = 0x8007682C;
    const uint ActiveDescriptor = 0x8017E0A4, OrderingTable = 0x8018E0A8;
    const uint PacketBytes = 24;

    public void OnLoad() { }

    public static double Progress(IMemory m)
    {
        uint level = m.ReadU8(Level), xp = m.ReadU32(Experience), next = m.ReadU32(Next);
        if (level == 0 || next == 0) return 0;
        if (level == 255 || xp >= 999999) return 1;
        uint previous;
        if (level == 1) previous = 0;
        else if (level <= 100) previous = m.ReadU32(LevelTable + (level - 2) * 12 + 8);
        else
        {
            // Above the table's hundred levels, the native level-up routine
            // advances the threshold by its last table interval each time.
            uint last = m.ReadU32(LevelTable + 99 * 12 + 8);
            uint beforeLast = m.ReadU32(LevelTable + 98 * 12 + 8);
            uint interval = last >= beforeLast ? last - beforeLast : 0;
            previous = next >= interval ? next - interval : next;
        }
        if (next <= previous) return xp >= next ? 1 : 0;
        return Math.Clamp(((double)xp - previous) / (next - previous), 0d, 1d);
    }

    static bool InRam(IMemory m, uint address, uint bytes) =>
        m is PSMemory ram && (ulong)(address & 0x1FFFFFFF) + bytes <= (ulong)ram.Ram.Length;

    [PostHook("game", Address = 0x80031D5C)]
    public static void Draw(CpuContext c, IMemory m)
    {
        // Share the native HUD's visibility. A world pass behind a menu/message
        // is not another HUD; leave loading, title and death screens alone.
        if (m.ReadU16(0x80199426) == 0 || m.ReadU16(0x80199428) == 0 ||
            m.ReadU8(0x801994DD) != 1 || m.ReadU8(Level) == 0 || !MenuWorld.Quiet(c, m)) return;
        uint descriptor = m.ReadU32(ActiveDescriptor), table = m.ReadU32(OrderingTable);
        if (!InRam(m, descriptor, 12) || !InRam(m, table, 8)) return;
        uint cursor = m.ReadU32(descriptor + 8), end = m.ReadU32(descriptor + 4);
        int margin = Display.WideMargin(320), left = -margin, width = 320 + 2 * margin;
        int filled = Math.Clamp((int)Math.Floor(width * Progress(m)), 0, width);
        uint bytes = filled == 0 ? PacketBytes : 2 * PacketBytes;
        if ((cursor & 3) != 0 || cursor > end || end - cursor < bytes || !InRam(m, cursor, bytes)) return;
        // AddPrim prepends: link the fill first, then the background, so the
        // ordering-table walk draws the dark track followed by its gold fill.
        uint entry = table + 4;
        if (filled > 0)
        {
            Quad(m, cursor, entry, left, left + filled, 156, 132, 76);
            cursor += PacketBytes;
        }
        Quad(m, cursor, entry, left, left + width, 28, 24, 20);
        m.WriteU32(descriptor + 8, cursor + PacketBytes);
        m.WriteU32(0x8006E914, cursor + PacketBytes);
    }

    static void Quad(IMemory m, uint packet, uint entry, int left, int right, byte r, byte g, byte b)
    {
        uint head = m.ReadU32(entry);
        m.WriteU32(packet, 0x05000000 | (head & 0x00FFFFFF));
        m.WriteU32(packet + 4, 0x28000000 | (uint)(r | g << 8 | b << 16));
        m.WriteU16(packet + 8, unchecked((ushort)left)); m.WriteU16(packet + 10, 238);
        m.WriteU16(packet + 12, unchecked((ushort)right)); m.WriteU16(packet + 14, 238);
        m.WriteU16(packet + 16, unchecked((ushort)left)); m.WriteU16(packet + 18, 240);
        m.WriteU16(packet + 20, unchecked((ushort)right)); m.WriteU16(packet + 22, 240);
        m.WriteU32(entry, (head & 0xFF000000) | (packet & 0x00FFFFFF));
    }
}
