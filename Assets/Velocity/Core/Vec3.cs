using System;

namespace Velocity.Core
{
    [Serializable]
    public struct Vec3
    {
        public float X, Y, Z;
        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static readonly Vec3 Zero = new Vec3();
        public static readonly Vec3 Up = new Vec3(0, 1, 0);
        public float LengthSquared => X * X + Y * Y + Z * Z;
        public float Length => (float)Math.Sqrt(LengthSquared);
        public float HorizontalSpeed => (float)Math.Sqrt(X * X + Z * Z);
        public bool Finite => MathEx.Finite(X) && MathEx.Finite(Y) && MathEx.Finite(Z);
        public Vec3 Normalized => LengthSquared > 1e-12f ? this / Length : Zero;
        public Vec3 Horizontal => new Vec3(X, 0, Z);
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float f) => new Vec3(a.X * f, a.Y * f, a.Z * f);
        public static Vec3 operator *(float f, Vec3 a) => a * f;
        public static Vec3 operator /(Vec3 a, float f) => a * (1f / f);
        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * MathEx.Clamp(t, 0, 1);
        public override string ToString() => $"({X:0.000}, {Y:0.000}, {Z:0.000})";
    }

    public static class MathEx
    {
        public const float DegToRad = (float)Math.PI / 180f;
        public static float Clamp(float x, float low, float high) => Math.Max(low, Math.Min(x, high));
        public static int Clamp(int x, int low, int high) => Math.Max(low, Math.Min(x, high));
        public static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public static float WrapAngle(float a) { a %= 360; return a > 180 ? a - 360 : a < -180 ? a + 360 : a; }
        public static Vec3 Forward(float yaw) { float a = yaw * DegToRad; return new Vec3((float)Math.Sin(a), 0, (float)Math.Cos(a)); }
        public static Vec3 Right(float yaw) { float a = yaw * DegToRad; return new Vec3((float)Math.Cos(a), 0, -(float)Math.Sin(a)); }
        public static Vec3 Aim(float yaw, float pitch) => Forward(yaw) * (float)Math.Cos(pitch * DegToRad) - Vec3.Up * (float)Math.Sin(pitch * DegToRad);
    }
}
