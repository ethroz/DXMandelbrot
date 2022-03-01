using System;
using DXMandelbrot;

class Program
{
    [MTAThread]
    static void Main()
    {
        using (Generator gen = new Generator())
        {
            gen.Run();
        }
    }
}
