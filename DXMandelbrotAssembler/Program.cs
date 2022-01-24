using System;
using System.IO;
using System.Reflection;

namespace DXMandelbrotAssembler
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string MandelbrotDir = Assembly.GetExecutingAssembly().Location;
            for (int i = 0; i < 4; i++)
                MandelbrotDir = Path.GetDirectoryName(MandelbrotDir);
            MandelbrotDir += @"\DXMandelbrot";
            string shaderBuffer = File.ReadAllText(MandelbrotDir + @"\ShaderBuffer.cs");
            string Shader1 = File.ReadAllText(MandelbrotDir + @"\Shaders.hlsl");
            string Shader2 = File.ReadAllText(MandelbrotDir + @"\Shaders.hlsl");
            int classLocation = shaderBuffer.IndexOf("public static class Shaders");
            shaderBuffer = shaderBuffer.Remove(classLocation) + "public static class Shaders\r\n    {\r\n        ";
            shaderBuffer += "public static string Shader1 = \r\n@\"" + Shader1 + "\";\r\n        ";
            shaderBuffer += "public static string Shader2 = \r\n@\"" + Shader2 + "\";\r\n    }\r\n}";
            File.WriteAllText(MandelbrotDir + @"\ShaderBuffer.cs", shaderBuffer);
        }
    }
}