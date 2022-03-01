using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Color = Vortice.Mathematics.Color;

namespace DXMandelbrot;

[StructLayout(LayoutKind.Sequential)]
public struct VertexPositionTexture
{
    public Vector4 Position;
    public Vector2 TextureUV;
    private Vector2 padding;

    public VertexPositionTexture(Vector3 position, Vector2 textureUV)
    {
        Position = new Vector4(position, 1.0f);
        TextureUV = textureUV;
        padding = new Vector2();
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 16)]
public struct ShaderBuffer
{
    public Double2 Pan;
    public Vector3 Color;
    public int Iterations;
    public double Zoom;
    public int Width;
    public int Height;
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

internal class QuickBitmap : IDisposable
{
    public IntPtr BitsHandle { get; private set; }
    public int[] Bits { get; private set; }
    public bool Disposed { get; private set; }
    public int Height { get; private set; }
    public int Width { get; private set; }

    public unsafe QuickBitmap(int width, int height)
    {
        Width = width;
        Height = height;
        Bits = new int[width * height];
        fixed (int* temp = &Bits[0])
            BitsHandle = (IntPtr)temp;
        
        //Bits = GC.AllocateArray<int>(width * height, true);
        //BitsHandle = Marshal.UnsafeAddrOfPinnedArrayElement(Bits, 0);
    }

    public void SetPixel(int x, int y, Color color)
    {
        int index = x + (y * Width);
        int col = (color.A << 24) + (color.R << 16) + (color.G << 8) + color.B;

        Bits[index] = col;
    }

    public Color GetPixel(int x, int y)
    {
        int index = x + (y * Width);
        int col = Bits[index];
        Color result = new Color((col >> 16) & 0xFF, (col >> 8) & 0xFF, col & 0xFF);

        return result;
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool boolean)
    {
        if (Disposed) return;
        Disposed = true;
        Bits = null;
        BitsHandle = IntPtr.Zero;
        Height = Width = 0;
    }
}