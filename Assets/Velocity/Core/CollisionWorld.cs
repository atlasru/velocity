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
            Vertices = vertices; Triangles = triangles; Material = material;
            Planes = AddHullBevels(planes,vertices,triangles);
            Minimum = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            Maximum = -Minimum;
            foreach (Vec3 v in vertices)
            {
                Minimum = new Vec3(Math.Min(Minimum.X, v.X), Math.Min(Minimum.Y, v.Y), Math.Min(Minimum.Z, v.Z));
                Maximum = new Vec3(Math.Max(Maximum.X, v.X), Math.Max(Maximum.Y, v.Y), Math.Max(Maximum.Z, v.Z));
            }
        }
        private static CollisionPlane[] AddHullBevels(CollisionPlane[] originals,Vec3[] vertices,int[] triangles)
        {
            var result=new List<CollisionPlane>(originals);
            void Add(Vec3 direction)
            {
                if(direction.LengthSquared<1e-8f) return;Vec3 n=direction.Normalized;
                foreach(CollisionPlane p in result) if(Vec3.Dot(p.Normal,n)>.99999f) return;
                float distance=float.NegativeInfinity;foreach(Vec3 v in vertices)distance=Math.Max(distance,Vec3.Dot(n,v));
                result.Add(new CollisionPlane{Normal=n,Distance=distance});
            }
            Vec3[] axes={new Vec3(1,0,0),Vec3.Up,new Vec3(0,0,1)};
            foreach(Vec3 axis in axes){Add(axis);Add(-axis);}
            var edges=new Dictionary<long,Vec3>();
            var bevel=new HashSet<long>();
            for(int t=0;t<triangles.Length;t+=3)
            {
                int a=triangles[t],b=triangles[t+1],c=triangles[t+2];Vec3 normal=Vec3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).Normalized;
                int[] ids={a,b,c};
                for(int k=0;k<3;k++)
                {
                    int first=Math.Min(ids[k],ids[(k+1)%3]),second=Math.Max(ids[k],ids[(k+1)%3]);long key=((long)first<<32)|(uint)second;
                    if(edges.TryGetValue(key,out Vec3 other)){if(Vec3.Dot(other,normal)<.99999f)bevel.Add(key);}else edges.Add(key,normal);
                }
            }
            var orderedEdges=new List<long>(bevel);orderedEdges.Sort();
            foreach(long key in orderedEdges)
            {
                int a=(int)(key>>32),b=(int)(key&uint.MaxValue);Vec3 edge=vertices[b]-vertices[a];
                foreach(Vec3 axis in axes){Vec3 n=Vec3.Cross(edge,axis);Add(n);Add(-n);}
            }
            return result.ToArray();
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
        public static ConvexBrush TrianglePrism(Vec3 a,Vec3 b,Vec3 c,float thickness=1,int material=1)
        {
            Vec3[] v={a,b,c,a-Vec3.Up*thickness,b-Vec3.Up*thickness,c-Vec3.Up*thickness};
            Vec3 center=(a+b+c)/3-Vec3.Up*thickness*.5f;
            var faces=new List<int>();var planes=new List<CollisionPlane>();
            void Face(int x,int y,int z,bool plane=true)
            {
                Vec3 n=Vec3.Cross(v[y]-v[x],v[z]-v[x]).Normalized;
                if(Vec3.Dot(n,center-v[x])>0){int swap=y;y=z;z=swap;n=-n;}
                faces.Add(x);faces.Add(y);faces.Add(z);if(plane)planes.Add(new CollisionPlane(n,v[x]));
            }
            Face(3,4,5);Face(0,1,2);
            for(int i=0;i<3;i++){int j=(i+1)%3;Face(i,j,j+3);Face(i,j+3,i+3,false);}
            return new ConvexBrush(planes.ToArray(),v,faces.ToArray(),material);
        }
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
