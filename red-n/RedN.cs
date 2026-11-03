using System;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Lexer.Kf2.RedN;

public sealed class RedNMod : IMod
{
    const uint ActiveDescriptor = 0x8017E0A4, Compass = 0x80067774;
    const uint MaxHudBytes = 65536;
    static uint _descriptor, _begin;

    public void OnLoad() => Reset();
    public void OnUnload() => Reset();
    static void Reset() { _descriptor = _begin = 0; }

    static bool InRam(IMemory m, uint address, uint bytes) =>
        m is PSMemory ram && address >= 0x80000000 && address < 0xA0000000 &&
        (ulong)(address & 0x1FFFFFFF) + bytes <= (ulong)ram.Ram.Length;

    [PreHook("game", Address = 0x80031D5C)]
    public static void BeforeHud(CpuContext c, IMemory m)
    {
        Reset();
        if (m.ReadU8(Compass) != 1 || m.ReadU16(Compass + 4) != 0) return;
        uint descriptor = m.ReadU32(ActiveDescriptor);
        if (!InRam(m, descriptor, 12)) return;
        uint begin = m.ReadU32(descriptor + 8);
        if ((begin & 3) != 0 || !InRam(m, begin, 4)) return;
        _descriptor = descriptor; _begin = begin;
    }

    [PostHook("game", Address = 0x80031D5C)]
    public static void AfterHud(CpuContext c, IMemory m)
    {
        uint descriptor = _descriptor, begin = _begin;
        Reset();
        if (descriptor == 0 || m.ReadU32(ActiveDescriptor) != descriptor) return;
        uint end = m.ReadU32(descriptor + 8), capacity = m.ReadU32(descriptor + 4);
        if (end < begin || end > capacity || end - begin > MaxHudBytes || !InRam(m, begin, end - begin)) return;
        // Validate the whole new packet span before changing any bytes. At most
        // four model0 quads sample the N cell; a different layout fails closed.
        Span<uint> targets = stackalloc uint[4];
        int count = 0;
        for (uint packet = begin; packet < end;)
        {
            if (end - packet < 8) return;
            uint words = m.ReadU8(packet + 3), bytes = (words + 1) * 4;
            if (words == 0 || bytes > end - packet) return;
            if (words == 12 && IsNorth(m, packet))
            {
                if (count == targets.Length) return;
                targets[count++] = packet;
            }
            packet += bytes;
        }
        for (int i = 0; i < count; i++)
            for (uint vertex = 0; vertex < 4; vertex++)
                // Native textured GT4 modulation: retain red and its lighting,
                // zero green/blue. UVs, palette, sphere and other letters stay.
                m.WriteU16(targets[i] + 5 + vertex * 12, 0);
    }

    static bool IsNorth(IMemory m, uint packet)
    {
        if (m.ReadU8(packet + 7) != 0x3C ||
            (m.ReadU16(packet + 14) & 0x7FFF) != 0x7B40 ||
            (m.ReadU16(packet + 26) & 0x19F) != 9) return false;
        for (uint vertex = 0; vertex < 4; vertex++)
        {
            uint uv = packet + 12 + vertex * 12;
            if (m.ReadU8(uv) >= 64 || m.ReadU8(uv + 1) < 128 || m.ReadU8(uv + 1) >= 192) return false;
        }
        return true;
    }
}
