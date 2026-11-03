using System;
using System.Numerics;
using Kf2;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Hle;
using RecompOne.Runtime.Memory;

namespace Lexer.Kf2.ForgetMeNot;

internal static class GlintSprites
{
    const int MaxVertices = 4096, MaxTriangles = 8192, MaxPackets = 4096, PacketBytes = 24;
    const uint Descriptor = 0x8017E0A4, Cursor = 0x8006E914, OrderingTable = 0x8018E0A8;
    const uint ModelTable = 0x8018E19C, VertexBase = 0x8018EAA0;
    static readonly Vector3[] Vertices = new Vector3[MaxVertices];
    static readonly int[] Triangles = new int[MaxTriangles * 3];
    static readonly uint[] OwnedPackets = new uint[MaxPackets];
    static int _owned;
    static readonly string[][] Art = {
        new[] { ".2.", "252", ".2." },
        new[] { "..1..", "..3..", "13531", "..3..", "..1.." },
        new[] { "...1...", "...2...", "..131..", "1235321", "..131..", "...2...", "...1..." },
        new[] { "....1....", "....2....", "..1.3.1..", "...242...", "123454321", "...242...", "..1.3.1..", "....2....", "....1...." },
    };

    public static void Reset()
    {
        for (int i = 0; i < _owned; i++) GtePacketDepth.Slot(OwnedPackets[i]).Cmd = 0;
        _owned = 0;
    }

    static bool InRam(PSMemory m, uint address, ulong bytes) => address >= 0x80010000
        && (ulong)(address & 0x1FFFFFFF) + bytes <= (ulong)m.Ram.Length;

    public static void Draw(CpuContext c, IMemory memory, uint record, byte state)
    {
        if (state == 0 || memory is not PSMemory m || !GteDepth.ZBuffer || !GtePacketDepth.Active
            || !MenuWorld.Quiet(c, m) || m.ReadU16(0x80199426) == 0) return;
        int slot = (int)((record - 0x80177714) / 0x44);
        long phase = (Environment.TickCount64 + slot * 97L) % 1300;
        if (phase >= 800) return;
        var camera = Camera.Read(m);
        Vector3 eye = new Vector3(camera.X, camera.Y, camera.Z);
        Vector3 origin = new Vector3(ModelWalk.PlacedX, ModelWalk.PlacedY, ModelWalk.PlacedZ);
        if (Vector3.DistanceSquared(origin, eye) > 32768f * 32768f) return;
        if (!Mesh(m, record, origin, out int vertexCount, out int triangleCount)
            || !GlintGeometry.Surface(Vertices.AsSpan(0, vertexCount), Triangles.AsSpan(0, triangleCount * 3), eye, out Vector3 anchor)) return;
        Vector3 relative = anchor - eye;
        if (relative.LengthSquared() > 16384f * 16384f) return;
        uint view = CameraBlock.ViewMatrix;
        float X(int row) => ((short)m.ReadU16(view + (uint)(row * 6)) * relative.X
            + (short)m.ReadU16(view + (uint)(row * 6 + 2)) * relative.Y
            + (short)m.ReadU16(view + (uint)(row * 6 + 4)) * relative.Z) / 4096f
            + (int)m.ReadU32(view + 20 + (uint)row * 4);
        float z = X(2), h = Math.Max(GteDepth.ProjH, 1);
        if (!float.IsFinite(z) || z <= Math.Max(16f, h * 0.5f) || z > 16384) return;
        float x = GteDepth.ProjCx + h * X(0) / z, y = GteDepth.ProjCy + h * X(1) / z;
        int margin = Display.WideMargin(320);
        if (!float.IsFinite(x) || !float.IsFinite(y) || x < -margin - 5 || x > 320 + margin + 5 || y < -5 || y > 245) return;
        float twinkle = MathF.Sin((float)phase * MathF.PI / 800);
        int radius = Math.Clamp((int)MathF.Round(6000f / z * (0.65f + 0.35f * twinkle)), 1, 4);
        // A constant-depth billboard intersects a curved lid and loses most of its
        // pixels. Follow the actual mesh at every corner instead. This one native
        // depth unit only avoids coplanar rounding; walls still occlude the star.
        Vector3 Inverse(Vector3 p) => new Vector3(
            ((short)m.ReadU16(view) * p.X + (short)m.ReadU16(view + 6) * p.Y + (short)m.ReadU16(view + 12) * p.Z) / 4096f,
            ((short)m.ReadU16(view + 2) * p.X + (short)m.ReadU16(view + 8) * p.Y + (short)m.ReadU16(view + 14) * p.Z) / 4096f,
            ((short)m.ReadU16(view + 4) * p.X + (short)m.ReadU16(view + 10) * p.Y + (short)m.ReadU16(view + 16) * p.Z) / 4096f);
        Vector3 rayEye = eye - Inverse(new Vector3((int)m.ReadU32(view + 20), (int)m.ReadU32(view + 24), (int)m.ReadU32(view + 28)));
        float Corner(int px, int py)
        {
            // Project at the anchor's distance: a one-unit ray endpoint loses
            // subpixel direction precision at the map's large world coordinates.
            Vector3 target = rayEye + Inverse(new Vector3((px - GteDepth.ProjCx) * z / h, (py - GteDepth.ProjCy) * z / h, z));
            if (!GlintGeometry.Ray(Vertices.AsSpan(0, vertexCount), Triangles.AsSpan(0, triangleCount * 3), rayEye, target, out Vector3 at)) return float.NaN;
            Vector3 from = at - eye;
            float depth = ((short)m.ReadU16(view + 12) * from.X + (short)m.ReadU16(view + 14) * from.Y
                + (short)m.ReadU16(view + 16) * from.Z) / 4096f + (int)m.ReadU32(view + 28);
            return depth > 1 ? depth - 1 : float.NaN;
        }
        Emit(m, (int)MathF.Round(x), (int)MathF.Round(y), z, radius, state == 2, twinkle, Corner);
    }

    static bool Mesh(PSMemory m, uint record, Vector3 origin, out int vertexCount, out int triangleCount)
    {
        vertexCount = triangleCount = 0;
        uint table = m.ReadU32(ModelTable), clip = m.ReadU8(record + 1), sub = clip >= 128 ? clip & 127 : 0;
        if (!InRam(m, table, 12 + (sub + 1) * 28UL)) return false;
        uint header = table + 12 + sub * 28;
        uint count = m.ReadU32(header + 4), faces = m.ReadU32(header + 0x14);
        if (count == 0 || count > MaxVertices || faces == 0 || faces > 4096) return false;
        // A retained MO instance may have left this exact pose deferred. Read the
        // actual rendered pose, not stale scratch vertices from an earlier model.
        MoPose.Materialize(null, m);
        uint vertices = m.ReadU32(VertexBase);
        if (!InRam(m, vertices, count * 8UL)) return false;
        for (uint i = 0; i < count; i++)
        {
            uint p = vertices + i * 8;
            Vertices[i] = GlintGeometry.Transform(new Vector3((short)m.ReadU16(p), (short)m.ReadU16(p + 2),
                (short)m.ReadU16(p + 4)), ModelWalk.PlacedRot, origin);
        }
        uint faceOffset = m.ReadU32(header + 0x10);
        if ((ulong)table + 12 + faceOffset > uint.MaxValue) return false;
        uint face = table + 12 + faceOffset;
        for (uint i = 0; i < faces; i++)
        {
            if (!InRam(m, face, 4)) return false;
            uint word = m.ReadU32(face), body = face + 4, bytes = (word >> 6) & 0x3FC;
            if (bytes == 0 || !InRam(m, body, bytes)) return false;
            uint cmd = word >> 24 & 0xFD;
            int corners, offset, stride;
            switch (cmd)
            {
                case 0x24: corners = 3; offset = 0x0E; stride = 2; break;
                case 0x2C: corners = 4; offset = 0x12; stride = 2; break;
                case 0x34: corners = 3; offset = 0x0E; stride = 4; break;
                case 0x3C: corners = 4; offset = 0x12; stride = 4; break;
                default: face = body + bytes; continue;
            }
            if (offset + (corners - 1) * stride + 2 > bytes) return false;
            int a = Index(0), b = Index(1), d = Index(2), e = corners == 4 ? Index(3) : 0;
            if (a < 0 || b < 0 || d < 0 || e < 0 || triangleCount + 2 > MaxTriangles) return false;
            Triangles[triangleCount * 3] = a; Triangles[triangleCount * 3 + 1] = b; Triangles[triangleCount * 3 + 2] = d;
            triangleCount++;
            if (corners == 4)
            { Triangles[triangleCount * 3] = b; Triangles[triangleCount * 3 + 1] = e; Triangles[triangleCount * 3 + 2] = d; triangleCount++; }
            face = body + bytes;
            int Index(int corner)
            {
                uint value = m.ReadU16(body + (uint)(offset + corner * stride));
                return (value & 7) == 0 && value / 8 < count ? (int)(value / 8) : -1;
            }
        }
        vertexCount = (int)count;
        return triangleCount > 0;
    }

    static void Emit(PSMemory m, int x, int y, float z, int radius, bool red, float twinkle, Func<int, int, float> cornerDepth)
    {
        string[] pixels = Art[radius - 1];
        int count = 0;
        foreach (string row in pixels) foreach (char p in row) if (p != '.') count++;
        if (_owned + count > MaxPackets) return;
        uint descriptor = m.ReadU32(Descriptor), table = m.ReadU32(OrderingTable);
        if (!InRam(m, descriptor, 12) || !InRam(m, table, 0x8000)) return;
        uint cursor = m.ReadU32(descriptor + 8), end = m.ReadU32(descriptor + 4), bytes = (uint)(count * PacketBytes);
        if ((cursor & 3) != 0 || cursor > end || end - cursor < bytes || !InRam(m, cursor, bytes)) return;
        int first = _owned;
        uint next = cursor;
        for (int yy = 0; yy < pixels.Length; yy++) for (int xx = 0; xx < pixels[yy].Length; xx++)
        {
            char pixel = pixels[yy][xx];
            if (pixel == '.') continue;
            int value = (int)((48 + (pixel - '0') * 41) * (0.8f + 0.2f * twinkle));
            byte r = (byte)Math.Clamp(value, 0, 255), g = red ? (byte)(r / 10) : r, b = red ? (byte)(r / 16) : r;
            uint command = 0x28000000 | (uint)(r | g << 8 | b << 16);
            int left = x + xx - radius, top = y + yy - radius;
            float z0 = cornerDepth(left, top), z1 = cornerDepth(left + 1, top),
                z2 = cornerDepth(left, top + 1), z3 = cornerDepth(left + 1, top + 1);
            if (!float.IsFinite(z0) || !float.IsFinite(z1) || !float.IsFinite(z2) || !float.IsFinite(z3)) continue;
            m.WriteU32(next + 4, command);
            m.WriteU16(next + 8, unchecked((ushort)left)); m.WriteU16(next + 10, unchecked((ushort)top));
            m.WriteU16(next + 12, unchecked((ushort)(left + 1))); m.WriteU16(next + 14, unchecked((ushort)top));
            m.WriteU16(next + 16, unchecked((ushort)left)); m.WriteU16(next + 18, unchecked((ushort)(top + 1)));
            m.WriteU16(next + 20, unchecked((ushort)(left + 1))); m.WriteU16(next + 22, unchecked((ushort)(top + 1)));
            ref var depth = ref GtePacketDepth.Slot(next);
            depth = new GtePacketDepth.Rec { Cmd = command, Xy0 = m.ReadU32(next + 8), XyLast = m.ReadU32(next + 20),
                Z0 = z0, Z1 = z1, Z2 = z2, Z3 = z3, Model = true, NotRect = true };
            OwnedPackets[_owned++] = next;
            if (!GtePacketDepth.Peek(next + 4, command, depth.Xy0, depth.XyLast, out _))
            { while (_owned > first) GtePacketDepth.Slot(OwnedPackets[--_owned]).Cmd = 0; return; }
            next += PacketBytes;
        }
        uint entry = table + (uint)Math.Clamp((int)(z / 4), 16, 0x1FFD) * 4;
        for (uint packet = cursor; packet < next; packet += PacketBytes)
        {
            uint head = m.ReadU32(entry);
            m.WriteU32(packet, 0x05000000 | (head & 0x00FFFFFF));
            m.WriteU32(entry, (head & 0xFF000000) | (packet & 0x00FFFFFF));
        }
        m.WriteU32(descriptor + 8, next);
        m.WriteU32(Cursor, next);
        GtePacketDepth.Recorded += _owned - first;
    }
}
