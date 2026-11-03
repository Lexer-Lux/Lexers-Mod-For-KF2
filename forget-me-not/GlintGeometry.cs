using System;
using System.Numerics;

namespace Lexer.Kf2.ForgetMeNot;

// Pure geometry shared by native rendering and the focused fixture checks.
internal static class GlintGeometry
{
    static readonly float[] Heights = { 0.35f, 0.65f }, Widths = { 0.5f, 0.3f, 0.7f };
    public static Vector3 Transform(Vector3 p, ReadOnlySpan<short> r, Vector3 origin) => origin + new Vector3(
        (r[0] * p.X + r[1] * p.Y + r[2] * p.Z) / 4096f,
        (r[3] * p.X + r[4] * p.Y + r[5] * p.Z) / 4096f,
        (r[6] * p.X + r[7] * p.Y + r[8] * p.Z) / 4096f);

    public static bool Surface(ReadOnlySpan<Vector3> vertices, ReadOnlySpan<int> triangles, Vector3 eye, out Vector3 anchor)
    {
        anchor = default;
        if (vertices.Length == 0 || triangles.Length == 0) return false;
        Vector3 lo = vertices[0], hi = vertices[0];
        foreach (Vector3 v in vertices) { lo = Vector3.Min(lo, v); hi = Vector3.Max(hi, v); }
        Vector3 centre = (lo + hi) * 0.5f;
        // Actual transformed mesh bounds, not the object's origin (a door's hinge).
        if (Ray(vertices, triangles, eye, centre, out anchor)) return true;
        foreach (float fy in Heights)
            foreach (float fx in Widths)
                if (Ray(vertices, triangles, eye, new Vector3(lo.X + (hi.X - lo.X) * fx,
                    lo.Y + (hi.Y - lo.Y) * fy, centre.Z), out anchor)) return true;
        // Hollow/irregular geometry may have no face across its box's middle.
        // A front-facing face centroid supplies a real surface target instead.
        Vector3 best = default;
        float score = float.PositiveInfinity;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            if (Vector3.Dot(a - eye, Vector3.Cross(b - a, c - a)) <= 0) continue;
            Vector3 candidate = (a + b + c) / 3f;
            float distance = Vector3.DistanceSquared(candidate, centre);
            if (distance < score) { score = distance; best = candidate; }
        }
        return float.IsFinite(score) && Ray(vertices, triangles, eye, best, out anchor);
    }

    public static bool Ray(ReadOnlySpan<Vector3> vertices, ReadOnlySpan<int> triangles, Vector3 eye, Vector3 target, out Vector3 anchor)
    {
        anchor = default;
        Vector3 ray = target - eye;
        if (ray.LengthSquared() < 1e-6f) return false;
        ray = Vector3.Normalize(ray);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i + 2 < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]], e1 = vertices[triangles[i + 1]] - a, e2 = vertices[triangles[i + 2]] - a;
            // Same facing sign as the game's model assembler / retained shader.
            if (Vector3.Dot(a - eye, Vector3.Cross(e1, e2)) <= 0) continue;
            Vector3 p = Vector3.Cross(ray, e2);
            float determinant = Vector3.Dot(e1, p);
            if (MathF.Abs(determinant) < 1e-6f) continue;
            float inverse = 1f / determinant;
            Vector3 from = eye - a;
            float u = Vector3.Dot(from, p) * inverse;
            if (u < -1e-5f || u > 1.00001f) continue;
            Vector3 q = Vector3.Cross(from, e1);
            float v = Vector3.Dot(ray, q) * inverse;
            if (v < -1e-5f || u + v > 1.00001f) continue;
            float t = Vector3.Dot(e2, q) * inverse;
            if (t > 0 && t < nearest) nearest = t;
        }
        if (!float.IsFinite(nearest)) return false;
        anchor = eye + ray * nearest;
        return true;
    }
}
