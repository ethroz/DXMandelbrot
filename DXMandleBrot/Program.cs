using System;

namespace DXMandelBrot
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            using (Generator game = new Generator())
            {
                game.Run();
            }
        }
    }
}
