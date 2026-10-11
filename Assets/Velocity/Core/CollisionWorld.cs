using System;
using System.Collections.Generic;

namespace Velocity.Core
{
    public struct CollisionPlane
    {
        public Vec3 Normal;
        public float Distance;
        public CollisionPlane(Vec3 normal, Vec3 point) { Normal = normal.Normalized; Distance = Vec3.Dot(Normal, point); }
    }

    public struct Hull
    {
        public float Radius, Height;
        public Hull(float radius, float height) { Radius = radius; Height = height; }
        public Vec3 Extents => new Vec3(Radius, Height * .5f, Radius);
    }

    public struct Trace
    {
        public float Fraction;
        public Vec3 Normal;
        public bool StartSolid;
        public static Trace Clear => new Trace { Fraction = 1 };
    }

    public interface IMovementWorld
    {
        Trace Sweep(Vec3 feet, Vec3 motion, Hull hull);
        bool Overlaps(Vec3 feet, Hull hull);
    }

    // A convex brush is the single source for both collision planes and its rendered mesh.
    // Sweeps expand its planes by the axis-aligned player hull (Minkowski sum).
    public sealed class ConvexBrush
    {
        public CollisionPlane[] Planes;
        public Vec3[] Vertices;
        public int[] Triangles;
        public Vec3 Minimum, Maximum;
        public int Material;
        public ConvexBrush(CollisionPlane[] planes, Vec3[] vertices, int[] triangles, int material)
        {
            Planes = planes; Vertices = vertices; Triangles = triangles; Material = material;
            Minimum = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            Maximum = -Minimum;
            foreach (Vec3 v in vertices)
            {
                Minimum = new Vec3(Math.Min(Minimum.X, v.X), Math.Min(Minimum.Y, v.Y), Math.Min(Minimum.Z, v.Z));
                Maximum = new Vec3(Math.Max(Maximum.X, v.X), Math.Max(Maximum.Y, v.Y), Math.Max(Maximum.Z, v.Z));
            }
        }
        private float Expanded(CollisionPlane plane, Hull hull)
        {
            Vec3 n = plane.Normal, e = hull.Extents;
            return plane.Distance + Math.Abs(n.X) * e.X + Math.Abs(n.Y) * e.Y + Math.Abs(n.Z) * e.Z;
        }
        public bool Contains(Vec3 feet, Hull hull)
        {
            Vec3 center = feet + Vec3.Up * (hull.Height * .5f);
            foreach (CollisionPlane p in Planes)
                if (Vec3.Dot(p.Normal, center) - Expanded(p, hull) >= -.0001f) return false;
            return true;
        }
        public Trace Sweep(Vec3 feet, Vec3 motion, Hull hull)
        {
            Vec3 center = feet + Vec3.Up * (hull.Height * .5f);
            float enter = float.NegativeInfinity, leave = 1;
            Vec3 normal = Vec3.Zero;
            bool strictlyInside = true;
            foreach (CollisionPlane p in Planes)
            {
                float d0 = Vec3.Dot(p.Normal, center) - Expanded(p, hull);
                float d1 = d0 + Vec3.Dot(p.Normal, motion);
                if (d0 >= -.0001f) strictlyInside = false;
                if (d0 > 0 && d1 >= d0) return Trace.Clear;
                if (d0 > 0 && d1 > 0) return Trace.Clear;
                if (d0 <= 0 && d1 <= 0 && d0 < -.0001f) continue;
                if (Math.Abs(d0 - d1) < 1e-9f) continue;
                if (d0 > d1)
                {
                    float f = (d0 - .001f) / (d0 - d1);
                    if (f > enter) { enter = f; normal = p.Normal; }
                }
                else leave = Math.Min(leave, (d0 + .001f) / (d0 - d1));
            }
            if (strictlyInside) return new Trace { Fraction = 0, StartSolid = true };
            if (normal.LengthSquared > 0 && enter <= leave && enter < 1 && leave >= 0)
                return new Trace { Fraction = Math.Max(0, enter), Normal = normal };
            return Trace.Clear;
        }
    }

    public sealed class CollisionWorld : IMovementWorld
    {
        public readonly List<ConvexBrush> Brushes = new List<ConvexBrush>();
        // Broad-phase AABBs reject distant brushes before the plane sweep.
        public Trace Sweep(Vec3 feet, Vec3 motion, Hull hull)
        {
            Trace best = Trace.Clear;
            Vec3 end = feet + motion;
            Vec3 min = new Vec3(Math.Min(feet.X, end.X) - hull.Radius, Math.Min(feet.Y, end.Y), Math.Min(feet.Z, end.Z) - hull.Radius);
            Vec3 max = new Vec3(Math.Max(feet.X, end.X) + hull.Radius, Math.Max(feet.Y, end.Y) + hull.Height, Math.Max(feet.Z, end.Z) + hull.Radius);
            foreach (ConvexBrush b in Brushes)
            {
                if (min.X > b.Maximum.X + .002f || max.X < b.Minimum.X - .002f || min.Y > b.Maximum.Y + .002f || max.Y < b.Minimum.Y - .002f || min.Z > b.Maximum.Z + .002f || max.Z < b.Minimum.Z - .002f) continue;
                Trace t = b.Sweep(feet, motion, hull);
                if (t.StartSolid) { best.StartSolid = true; continue; }
                if (t.Fraction < best.Fraction - .000001f || (Math.Abs(t.Fraction - best.Fraction) < .000001f && Vec3.Dot(motion, t.Normal) < Vec3.Dot(motion, best.Normal)))
                { bool solid = best.StartSolid; best = t; best.StartSolid |= solid; }
            }
            return best;
        }
        public bool Overlaps(Vec3 feet, Hull hull)
        {
            foreach (ConvexBrush b in Brushes) if (b.Contains(feet, hull)) return true;
            return false;
        }
        public float Raycast(Vec3 origin, Vec3 direction, float range)
        {
            Trace t = Sweep(origin, direction.Normalized * range, new Hull(0, 0));
            return t.StartSolid ? 0 : t.Fraction * range;
        }
    }

    public static class BrushFactory
    {
        private static readonly int[] Faces = { 0,1,2, 0,2,3, 4,6,5, 4,7,6, 0,5,1, 0,4,5, 3,6,7, 3,2,6, 0,7,4, 0,3,7, 1,6,2, 1,5,6 };
        public static ConvexBrush Box(Vec3 center, Vec3 size, int material = 0, float yaw = 0) => Prism(center, size.X, size.Z, size.Y * .5f, size.Y * .5f, 0, material, yaw);
        public static ConvexBrush Ramp(Vec3 center, float width, float length, float angle, int material = 1, float yaw = 0)
            => Prism(center, width, length, width * .5f * (float)Math.Tan(Math.Abs(angle) * MathEx.DegToRad) + 1, 0, (float)Math.Tan(angle * MathEx.DegToRad), material, yaw);
        private static ConvexBrush Prism(Vec3 center, float width, float length, float bottom, float top, float slope, int material, float yaw)
        {
            float x = width * .5f, z = length * .5f;
            Vec3[] v = { new Vec3(-x,-bottom,-z),new Vec3(x,-bottom,-z),new Vec3(x,-bottom,z),new Vec3(-x,-bottom,z),new Vec3(-x,top+x*slope,-z),new Vec3(x,top-x*slope,-z),new Vec3(x,top-x*slope,z),new Vec3(-x,top+x*slope,z) };
            Vec3 right = MathEx.Right(yaw), forward = MathEx.Forward(yaw);
            for (int i = 0; i < v.Length; i++) v[i] = center + right * v[i].X + Vec3.Up * v[i].Y + forward * v[i].Z;
            CollisionPlane[] planes = new CollisionPlane[6];
            for (int f = 0; f < 6; f++)
            {
                int a = Faces[f*6], b = Faces[f*6+1], c = Faces[f*6+2];
                Vec3 n = Vec3.Cross(v[b]-v[a], v[c]-v[a]).Normalized;
                planes[f] = new CollisionPlane(n, v[a]);
            }
            return new ConvexBrush(planes, v, (int[])Faces.Clone(), material);
        }
    }
}
