using SharpDX;
using System.Runtime.InteropServices;

namespace DXMandelBrot
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct ShaderBuffer
    {
        public Double2 Pan;
        public Vector3 Color;
        public int Itterations;
        public double Zoom;
        public float Width;
        public float Height;
        public int SampleCount;
    }

    public struct Double2
    {
        public double X;
        public double Y;

        public static Double2 operator +(Double2 vec1, Double2 vec2)
        {
            return new Double2 { X = vec1.X + vec2.X, Y = vec1.Y + vec2.Y };
        }

        public static Double2 operator -(Double2 vec1, Double2 vec2)
        {
            return new Double2 { X = vec1.X - vec2.X, Y = vec1.Y - vec2.Y };
        }

        public static Double2 operator *(Double2 vec, double scalar)
        {
            return new Double2 { X = vec.X * scalar, Y = vec.Y * scalar };
        }

        public static explicit operator Decimal2(Double2 d)
        {
            return new Decimal2 { X = (decimal)d.X, Y = (decimal)d.Y };
        }
    }

    public struct Decimal2
    {
        public decimal X;
        public decimal Y;

        public static Decimal2 operator +(Decimal2 vec1, Decimal2 vec2)
        {
            return new Decimal2 { X = vec1.X + vec2.X, Y = vec1.Y + vec2.Y };
        }

        public static Decimal2 operator -(Decimal2 vec1, Decimal2 vec2)
        {
            return new Decimal2 { X = vec1.X - vec2.X, Y = vec1.Y - vec2.Y };
        }

        public static Decimal2 operator *(Decimal2 vec, decimal scalar)
        {
            return new Decimal2 { X = vec.X * scalar, Y = vec.Y * scalar };
        }

        public static explicit operator Double2(Decimal2 d)
        {
            return new Double2 { X = (double)d.X, Y = (double)d.Y };
        }
    }
}
