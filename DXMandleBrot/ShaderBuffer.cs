using SharpDX;
using System.Runtime.InteropServices;

namespace DXMandelBrot
{
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public struct ShaderBuffer
    {
        public Double2 Pan;
        public Vector3 Color;
        public int Iterations;
        public double Zoom;
        public int Width;
        public int Height;
        public float ModdedTime;
    }

    public struct Double2
    {
        public double X;
        public double Y;

        public Double2(double x, double y)
        {
            X = x;
            Y = y;
        }

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

        public static Double2 operator /(Double2 vec, double scalar)
        {
            return new Double2 { X = vec.X / scalar, Y = vec.Y / scalar };
        }

        public static explicit operator Decimal2(Double2 d)
        {
            return new Decimal2 { X = (decimal)d.X, Y = (decimal)d.Y };
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }

    public struct Decimal2
    {
        public decimal X;
        public decimal Y;

        public Decimal2(decimal x, decimal y)
        {
            X = x;
            Y = y;
        }

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

        public static Decimal2 operator /(Decimal2 vec, decimal scalar)
        {
            return new Decimal2 { X = vec.X / scalar, Y = vec.Y / scalar };
        }

        public static explicit operator Double2(Decimal2 d)
        {
            return new Double2 { X = (double)d.X, Y = (double)d.Y };
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }
}
