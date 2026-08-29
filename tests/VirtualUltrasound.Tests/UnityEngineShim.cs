#if !UNITY_5_3_OR_NEWER && !UNITY_2017_1_OR_NEWER
using System;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
    }

    public struct Vector2
    {
        public float x;
        public float y;

        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 1e-5f ? this / magnitude : zero;

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    public struct Quaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x; this.y = y; this.z = z; this.w = w;
        }

        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public static Quaternion Euler(float pitch, float yaw, float roll)
        {
            float p = pitch * (MathF.PI / 180f) * 0.5f;
            float y = yaw * (MathF.PI / 180f) * 0.5f;
            float r = roll * (MathF.PI / 180f) * 0.5f;

            float sinP = MathF.Sin(p), cosP = MathF.Cos(p);
            float sinY = MathF.Sin(y), cosY = MathF.Cos(y);
            float sinR = MathF.Sin(r), cosR = MathF.Cos(r);

            return new Quaternion(
                sinP * cosY * cosR + cosP * sinY * sinR,
                cosP * sinY * cosR - sinP * cosY * sinR,
                cosP * cosY * sinR - sinP * sinY * cosR,
                cosP * cosY * cosR + sinP * sinY * sinR
            );
        }

        public static Quaternion Inverse(Quaternion q)
        {
            return new Quaternion(-q.x, -q.y, -q.z, q.w);
        }

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float num = q.x * 2f;
            float num2 = q.y * 2f;
            float num3 = q.z * 2f;
            float num4 = q.x * num;
            float num5 = q.y * num2;
            float num6 = q.z * num3;
            float num7 = q.x * num2;
            float num8 = q.x * num3;
            float num9 = q.y * num3;
            float num10 = q.w * num;
            float num11 = q.w * num2;
            float num12 = q.w * num3;

            return new Vector3(
                (1f - (num5 + num6)) * v.x + (num7 - num12) * v.y + (num8 + num11) * v.z,
                (num7 + num12) * v.x + (1f - (num4 + num6)) * v.y + (num9 - num10) * v.z,
                (num8 - num11) * v.x + (num9 + num10) * v.y + (1f - (num4 + num5)) * v.z
            );
        }
    }

    public struct Color32
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }
    }

    public struct Vector3Int
    {
        public int x;
        public int y;
        public int z;

        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public Color(float r, float g, float b, float a = 1.0f)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }

        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color white => new Color(1f, 1f, 1f, 1f);
    }

    public struct Matrix4x4
    {
        public float m00, m01, m02, m03;
        public float m10, m11, m12, m13;
        public float m20, m21, m22, m23;
        public float m30, m31, m32, m33;

        public static Matrix4x4 identity => new Matrix4x4
        {
            m00 = 1, m11 = 1, m22 = 1, m33 = 1
        };

        public static Matrix4x4 Rotate(Quaternion q)
        {
            float num = q.x * 2f;
            float num2 = q.y * 2f;
            float num3 = q.z * 2f;
            float num4 = q.x * num;
            float num5 = q.y * num2;
            float num6 = q.z * num3;
            float num7 = q.x * num2;
            float num8 = q.x * num3;
            float num9 = q.y * num3;
            float num10 = q.w * num;
            float num11 = q.w * num2;
            float num12 = q.w * num3;

            Matrix4x4 result = identity;
            result.m00 = 1f - (num5 + num6);
            result.m01 = num7 - num12;
            result.m02 = num8 + num11;
            result.m10 = num7 + num12;
            result.m11 = 1f - (num4 + num6);
            result.m12 = num9 - num10;
            result.m20 = num8 - num11;
            result.m21 = num9 + num10;
            result.m22 = 1f - (num4 + num5);
            return result;
        }

        public Vector3 MultiplyPoint3x4(Vector3 point)
        {
            return new Vector3(
                m00 * point.x + m01 * point.y + m02 * point.z + m03,
                m10 * point.x + m11 * point.y + m12 * point.z + m13,
                m20 * point.x + m21 * point.y + m22 * point.z + m23
            );
        }

        public Vector3 MultiplyVector(Vector3 vector)
        {
            return new Vector3(
                m00 * vector.x + m01 * vector.y + m02 * vector.z,
                m10 * vector.x + m11 * vector.y + m12 * vector.z,
                m20 * vector.x + m21 * vector.y + m22 * vector.z
            );
        }
    }

    public static class Mathf
    {
        public const float Deg2Rad = MathF.PI / 180f;
        public const float Rad2Deg = 180f / MathF.PI;

        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Atan2(float y, float x) => MathF.Atan2(y, x);
        public static float Abs(float f) => MathF.Abs(f);
        public static float Round(float f) => MathF.Round(f);
        public static bool Approximately(float a, float b) => MathF.Abs(b - a) < MathF.Max(1e-6f * MathF.Max(MathF.Abs(a), MathF.Abs(b)), 1e-5f);
        public static float Sqrt(float f) => MathF.Sqrt(f);
        public static float Sin(float f) => MathF.Sin(f);
        public static float Cos(float f) => MathF.Cos(f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);
        public static int CeilToInt(float f) => (int)MathF.Ceiling(f);
        public static int FloorToInt(float f) => (int)MathF.Floor(f);
        public static float Exp(float f) => MathF.Exp(f);
        public static float Log(float f) => MathF.Log(f);
    }
}
#endif
