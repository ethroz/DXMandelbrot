using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Windows;
using SharpDX.DirectInput;
using SharpDX.DXGI;
using D3D11 = SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using Point = System.Drawing.Point;
using Color = SharpDX.Color;
using Colour = System.Drawing.Color;
using System.Windows.Forms;
using System.Drawing;
using SharpDX.Direct3D11;
using SharpDX.D3DCompiler;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace DXMandelBrot
{
    public class Game : IDisposable
    {
        private RenderForm renderForm;
        private D3D11.Device device;
        private DeviceContext deviceContext;
        private SwapChain swapChain;
        private RenderTargetView renderTargetView;
        private Vector3[] vertices = new Vector3[]
        {
            new Vector3(-1.0f, -1.0f, 0.0f),
            new Vector3(-1.0f, 1.0f, 0.0f),
            new Vector3(1.0f, -1.0f, 0.0f),
            new Vector3(1.0f, 1.0f, 0.0f)
        };
        private VertexPositionTexture[] texturedVertices = new VertexPositionTexture[]
        {
            new VertexPositionTexture(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(0.0f, 1.0f)),
            new VertexPositionTexture(new Vector3(-1.0f, 1.0f, 0.0f), new Vector2(0.0f, 0.0f)),
            new VertexPositionTexture(new Vector3(1.0f, -1.0f, 0.0f), new Vector2(1.0f, 1.0f)),
            new VertexPositionTexture(new Vector3(1.0f, 1.0f, 0.0f), new Vector2(1.0f, 0.0f))
        };
        private D3D11.Buffer triangleVertexBuffer;
        private ShaderBuffer ShaderBuffer;
        private D3D11.Buffer ShaderBufferInstance;
        private VertexShader vertexShader;
        private PixelShader pixelShader;
        private InputElement[] inputElements = new InputElement[]
        {
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0)
        };
        private InputElement[] texturedInputElements = new InputElement[]
        {
            new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0, InputClassification.PerVertexData, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32B32A32_Float, 16, 0, InputClassification.PerVertexData, 0)
        };
        private ShaderSignature inputSignature;
        private InputLayout inputLayout;
        private Viewport viewport;
        private BufferDescription dbdescription;
        private Texture2DDescription td;
        private DirectBitmap dbmp;
        private Texture2D texture;

        public bool RenderWithCPU = false;
        public float elapsedTime;
        private bool OneFrame = false;
        private bool Debug = false;
        private bool SkipFirst = true;
        private System.Diagnostics.Stopwatch sw;
        private long t1, t2;
        private Vector3 Color = new Color(59, 131, 247).ToVector3();
        private int Itterations = 100;
        private Decimal2 Pan = new Decimal2 { X = -0.25M, Y = 0.0M };
        private decimal Zoom = 2.0M;
        private int Width;
        private int Height;
        private int SampleCount = 1;
        public enum WindowState { Normal, Minimized, Maximized, FullScreen };
        private WindowState State = WindowState.Maximized;
        private int ResolutionIndex = 0;
        private int[][] Resolutions = new int[8][]
        {
            new int[] { 3840, 2160 },
            new int[] { 2560, 1440 },
            new int[] { 1920, 1080 },
            new int[] { 1280, 720 },
            new int[] { 640, 360 },
            new int[] { 320, 180},
            new int[] { 128, 72},
            new int[] { 64, 36}
        };
        private int ScreenHeight;
        private List<long> Time = new List<long>();

        private Mouse mouse;
        private Button[] buttons;
        private POINT PrevMousePos;
        public Decimal2 DeltaMousePos;
        public int DeltaMouseScroll;
        private Keyboard keyboard;
        private Chey[] cheyArray;

        private void Test()
        {
            long sum = 0;
            for (int i = 0; i < Time.Count; i++)
            {
                sum += Time[i];
            }
            sum /= Time.Count;
            Console.WriteLine("Average Time Per Frame: " + sum);
        }

        public Game()
        {
            //Test();
            Width = Resolutions[ResolutionIndex][0];
            Height = Resolutions[ResolutionIndex][1];
            renderForm = new RenderForm("DXMandelBrot")
            {
                ClientSize = new Size(Width, Height),
                AllowUserResizing = false
                
            };
            if (State == WindowState.FullScreen)
            {
                renderForm.TopMost = true;
                renderForm.FormBorderStyle = FormBorderStyle.None;
                renderForm.WindowState = FormWindowState.Maximized;
            }
            else if (State == WindowState.Maximized)
            {
                renderForm.WindowState = FormWindowState.Maximized;
            }
            else if (State == WindowState.Minimized)
            {
                renderForm.TopMost = false;
                renderForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                renderForm.WindowState = FormWindowState.Minimized;
            }

            InitializeMouse();
            InitializeKeyboard();
            InitializeDeviceResources();
            if (RenderWithCPU) InitializeShaders2(); else InitializeShaders();
            InitializeTriangle();
            sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            t1 = sw.ElapsedTicks;
            dbmp = new DirectBitmap(Width, Height);
            td = new Texture2DDescription
            {
                Width = dbmp.Width,
                Height = dbmp.Height,
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                Usage = ResourceUsage.Immutable,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = Format.B8G8R8A8_UNorm,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SampleDescription(1, 0),
            };
            ScreenHeight = Screen.FromControl(renderForm).Bounds.Height;
        }

        private void InitializeMouse()
        {
            mouse = new Mouse(new DirectInput());
            mouse.Acquire();
            var state = mouse.GetCurrentState();
            var allButtons = state.Buttons;
            buttons = new Button[allButtons.Length];
            for (int i = 0; i < allButtons.Length; i++)
                buttons[i] = new Button();
            GetCursorPos(out PrevMousePos);
        }

        private void InitializeKeyboard()
        {
            keyboard = new Keyboard(new DirectInput());
            keyboard.Properties.BufferSize = 128;
            keyboard.Acquire();
            var state = keyboard.GetCurrentState();
            var allKeys = state.AllKeys;
            cheyArray = new Chey[allKeys.Count];
            for (int i = 0; i < allKeys.Count; i++)
                cheyArray[i] = new Chey(allKeys[i]);
        }

        private void InitializeDeviceResources()
        {
            SwapChainDescription swapChainDesc = new SwapChainDescription()
            {
                ModeDescription = new ModeDescription(Width, Height, new Rational(10000, 1), Format.R8G8B8A8_UNorm),
                SampleDescription = new SampleDescription(1, 0),
                Usage = Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = renderForm.Handle,
                IsWindowed = true
            };
            D3D11.Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.None, swapChainDesc, out device, out swapChain);
            deviceContext = device.ImmediateContext;
            using (Texture2D backBuffer = swapChain.GetBackBuffer<Texture2D>(0))
            {
                renderTargetView = new RenderTargetView(device, backBuffer);
            }
            viewport = new Viewport(0, 0, Width, Height);
            deviceContext.Rasterizer.SetViewport(viewport);
        }

        private void InitializeTriangle()
        {
            if (RenderWithCPU)
                triangleVertexBuffer = D3D11.Buffer.Create(device, BindFlags.VertexBuffer, texturedVertices);
            else
                triangleVertexBuffer = D3D11.Buffer.Create(device, BindFlags.VertexBuffer, vertices);
        }

        private void InitializeShaders()
        {
            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "vertexShader", "vs_5_0", ShaderFlags.Debug))
            {
                inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                vertexShader = new VertexShader(device, vertexShaderByteCode);
            }
            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders.hlsl", "pixelShader", "ps_5_0", ShaderFlags.Debug))
            {
                pixelShader = new PixelShader(device, pixelShaderByteCode);
            }
            deviceContext.VertexShader.Set(vertexShader);
            deviceContext.PixelShader.Set(pixelShader);

            AssignShaderBuffer();
            dbdescription = new BufferDescription(AssignShaderBufferSize(), ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, 0, 0);
            ShaderBufferInstance = D3D11.Buffer.Create(device, ref ShaderBuffer, dbdescription);
            deviceContext.VertexShader.SetConstantBuffer(0, ShaderBufferInstance);
            deviceContext.PixelShader.SetConstantBuffer(0, ShaderBufferInstance);
            //var vBB = new VertexBufferBinding(ShaderBufferInstance, Utilities.SizeOf<ShaderBuffer>(), 0);
            //deviceContext.InputAssembler.SetVertexBuffers(1, vBB);

            //deviceContext.VertexShader.SetConstantBuffer(1, ShaderBufferInstance);
            //deviceContext.PixelShader.SetConstantBuffer(1, ShaderBufferInstance);

            inputLayout = new InputLayout(device, inputSignature, inputElements);
            deviceContext.InputAssembler.InputLayout = inputLayout;

            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        }

        private void InitializeShaders2()
        {
            using (var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Shaders2.hlsl", "vertexShader", "vs_5_0", ShaderFlags.Debug))
            {
                inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                vertexShader = new VertexShader(device, vertexShaderByteCode);
            }
            using (var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Shaders2.hlsl", "pixelShader", "ps_5_0", ShaderFlags.Debug))
            {
                pixelShader = new PixelShader(device, pixelShaderByteCode);
            }
            deviceContext.VertexShader.Set(vertexShader);
            deviceContext.PixelShader.Set(pixelShader);

            var samplerStateDescription = new SamplerStateDescription
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                Filter = Filter.MinMagMipLinear
            };
            using (var samplerState = new SamplerState(device, samplerStateDescription))
                deviceContext.PixelShader.SetSampler(0, samplerState);

            inputLayout = new InputLayout(device, inputSignature, texturedInputElements);
            deviceContext.InputAssembler.InputLayout = inputLayout;

            deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        }

        private void AssignShaderBuffer()
        {
            ShaderBuffer = new ShaderBuffer
            {
                Pan = (Double2)Pan,
                Color = Color,
                Itterations = Itterations,
                Zoom = (double)Zoom,
                Width = Width,
                Height = Height,
                SampleCount = SampleCount
            };
        }

        private int AssignShaderBufferSize()
        {
            int size = Utilities.SizeOf<ShaderBuffer>();
            if (size / 16.0f != Math.Floor(size / 16.0f))
            {
                size += 16 - (size % 16);
            }
            return size;
        }

        private void GetMouseData()
        {
            mouse.Poll();
            var state = mouse.GetCurrentState();
            var butons = state.Buttons;
            for (int i = 0; i < butons.Length; i++)
            {
                bool pressed = butons[i];
                buttons[i].Down = buttons[i].Raised && pressed;
                buttons[i].Up = buttons[i].Held && !pressed;
                buttons[i].Held = pressed;
                buttons[i].Raised = !pressed;
            }
            POINT CurrentMousePos;
            GetCursorPos(out CurrentMousePos);
            if (State == WindowState.Normal)
                DeltaMousePos = new Decimal2 { X = (decimal)(CurrentMousePos.X - PrevMousePos.X) / Resolutions[ResolutionIndex][1], Y = (decimal)(CurrentMousePos.Y - PrevMousePos.Y) / Resolutions[ResolutionIndex][1] };
            else if (State == WindowState.FullScreen)
                DeltaMousePos = new Decimal2 { X = (decimal)(CurrentMousePos.X - PrevMousePos.X) / renderForm.Height, Y = (decimal)(CurrentMousePos.Y - PrevMousePos.Y) / renderForm.Height };
            else
                DeltaMousePos = new Decimal2 { X = (decimal)(CurrentMousePos.X - PrevMousePos.X) / ScreenHeight, Y = (decimal)(CurrentMousePos.Y - PrevMousePos.Y) / (ScreenHeight - 20) };
            PrevMousePos = CurrentMousePos;
            DeltaMouseScroll = state.Z / 120;
        }

        private void GetKeys()
        {
            keyboard.Poll();
            var state = keyboard.GetCurrentState();
            for (int i = 0; i < cheyArray.Length; i++)
            {
                bool pressed = state.IsPressed(cheyArray[i].key);
                cheyArray[i].Down = cheyArray[i].Raised && pressed;
                cheyArray[i].Up = cheyArray[i].Held && !pressed;
                cheyArray[i].Held = pressed;
                cheyArray[i].Raised = !pressed;
            }
        }

        public bool KeyDown(Key key)
        {
            return FindChey(key).Down;
        }

        public bool KeyUp(Key key)
        {
            return FindChey(key).Up;
        }

        public bool KeyHeld(Key key)
        {
            return FindChey(key).Held;
        }

        public bool KeyRaised(Key key)
        {
            return FindChey(key).Raised;
        }

        private Chey FindChey(Key key)
        {
            for (int i = 0; i < cheyArray.Length; i++)
            {
                if (cheyArray[i].key == key)
                    return cheyArray[i];
            }
            return null;
        }

        public bool ButtonDown(int button)
        {
            return buttons[button].Down;
        }

        public bool ButtonUp(int button)
        {
            return buttons[button].Up;
        }

        public bool ButtonHeld(int button)
        {
            return buttons[button].Held;
        }

        public bool ButtonRaised(int button)
        {
            return buttons[button].Raised;
        }

        public void CycleWindowState()
        {
            switch (State)
            {
                case WindowState.Minimized:
                    State = WindowState.Normal;
                    renderForm.TopMost = false;
                    renderForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                    renderForm.WindowState = FormWindowState.Normal;
                    break;
                case WindowState.Normal:
                    State = WindowState.Maximized;
                    renderForm.TopMost = false;
                    renderForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                    renderForm.WindowState = FormWindowState.Maximized;
                    break;
                case WindowState.Maximized:
                    State = WindowState.FullScreen;
                    renderForm.TopMost = true;
                    renderForm.FormBorderStyle = FormBorderStyle.None;
                    renderForm.WindowState = FormWindowState.Normal;
                    renderForm.WindowState = FormWindowState.Maximized;
                    break;
                case WindowState.FullScreen:
                    State = WindowState.Minimized;
                    renderForm.TopMost = false;
                    renderForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                    renderForm.WindowState = FormWindowState.Minimized;
                    break;
            }
        }

        private void GetTime()
        {
            t2 = sw.ElapsedTicks;
            if (!SkipFirst)
                Time.Add(t2 - t1);
            SkipFirst = false;
            elapsedTime = (t2 - t1) / 10000000.0f;
            t1 = t2;
            renderForm.Text = "DXMandelBrot   FPS: " + 1.0f / elapsedTime;
        }

        public void UserInput()
        {
            if (KeyDown(Key.Down))
                SampleCount = Math.Max(--SampleCount, 1);
            if (KeyDown(Key.Up))
                SampleCount++;
            if (KeyDown(Key.Left))
            {
                ResolutionIndex++;
                if (ResolutionIndex > Resolutions.Length - 1)
                    ResolutionIndex = Resolutions.Length - 1;
                else
                {
                    Width = Resolutions[ResolutionIndex][0];
                    Height = Resolutions[ResolutionIndex][1];
                    renderForm.Width = Width;
                    renderForm.Height = Height;
                    InitializeDeviceResources();
                    if (RenderWithCPU) InitializeShaders2(); else InitializeShaders();
                    dbmp = new DirectBitmap(Width, Height);
                    td = new Texture2DDescription
                    {
                        Width = dbmp.Width,
                        Height = dbmp.Height,
                        ArraySize = 1,
                        BindFlags = BindFlags.ShaderResource,
                        Usage = ResourceUsage.Immutable,
                        CpuAccessFlags = CpuAccessFlags.None,
                        Format = Format.B8G8R8A8_UNorm,
                        MipLevels = 1,
                        OptionFlags = ResourceOptionFlags.None,
                        SampleDescription = new SampleDescription(1, 0),
                    };
                }
            }
            if (KeyDown(Key.Right))
            {
                ResolutionIndex--;
                if (ResolutionIndex < 0)
                    ResolutionIndex = 0;
                else
                {
                    Width = Resolutions[ResolutionIndex][0];
                    Height = Resolutions[ResolutionIndex][1];
                    renderForm.Width = Width;
                    renderForm.Height = Height;
                    InitializeDeviceResources();
                    if (RenderWithCPU) InitializeShaders2(); else InitializeShaders();
                    dbmp = new DirectBitmap(Width, Height);
                    td = new Texture2DDescription
                    {
                        Width = dbmp.Width,
                        Height = dbmp.Height,
                        ArraySize = 1,
                        BindFlags = BindFlags.ShaderResource,
                        Usage = ResourceUsage.Immutable,
                        CpuAccessFlags = CpuAccessFlags.None,
                        Format = Format.B8G8R8A8_UNorm,
                        MipLevels = 1,
                        OptionFlags = ResourceOptionFlags.None,
                        SampleDescription = new SampleDescription(1, 0),
                    };
                }
            }
            if (KeyDown(Key.Return))
            {
                RenderWithCPU = !RenderWithCPU;
                if (RenderWithCPU) InitializeShaders2(); else InitializeShaders();
                InitializeTriangle();
            }

            /*if (KeyDown(Key.Comma))
                Itterations = Math.Max(Itterations - 10, 1);
            if (KeyDown(Key.Period))
                Itterations += 10;*/

            if (ButtonHeld(0))
            {
                Pan.X = Math.Min(Math.Max(Pan.X - DeltaMousePos.X * Zoom, -3.0M / Zoom), 3.0M / Zoom);
                Pan.Y = Math.Min(Math.Max(Pan.Y - DeltaMousePos.Y * Zoom, -2.0M / Zoom), 2.0M / Zoom);
            }

            if (DeltaMouseScroll != 0)
                Zoom = Math.Min(Zoom * (decimal)Math.Pow(1.1, -DeltaMouseScroll), 2.0M);
            Itterations = (int)(50.0 * Math.Pow(Math.Log(Width / (double)Zoom), 1.25));

            if (KeyDown(Key.R))
            {
                Pan = new Decimal2 { X = -0.25M, Y = 0.0M };
                Zoom = 2.0M;
            }

            if (KeyDown(Key.P))
                Test();
                //Console.WriteLine(Zoom);

            if (KeyDown(Key.LeftAlt))
                OneFrame = !OneFrame;
            if (KeyDown(Key.D))
                Debug = !Debug;

            if (KeyDown(Key.Escape))
                Environment.Exit(0);

            if (KeyDown(Key.Tab))
                CycleWindowState();
        }

        public void OnUpdate()
        {
            // vertex shader

            //pixel shader
            Decimal2 Offset = new Decimal2 { X = (decimal)Width / Height / 2.0M, Y = 0.5M };
            Parallel.For(0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    Color color = new Color();
                    Decimal2 C = (new Decimal2 { X = (decimal)x / Height, Y = (decimal)y / Height } - Offset) * Zoom + Pan;
                    Decimal2 v = C;

                    int prevItteration = Itterations;

                    for (int i = 0; i < prevItteration; i++)
                    {
                        v = new Decimal2 { X = (v.X * v.X) - (v.Y * v.Y), Y = v.X * v.Y * 2.0M } + C;

                        if ((prevItteration == Itterations) && ((v.X * v.X) + (v.Y * v.Y)) > 4.0M)
                        {
                            //float NIC = (float)((float)i - (Math.Log(Math.Log(Math.Sqrt((double)v.X * (double)v.X + (double)v.Y * (double)v.Y)))) / Math.Log(2.0)) / Itterations;
                            //color += new Vector3((float)Math.Sin(NIC * Color.X), (float)Math.Sin(NIC * Color.Y), (float)Math.Sin(NIC * Color.Z));
                            //int brightness = (int)((float)i / prevItteration * 255.0f);
                            //color = new Color(brightness, brightness, brightness);
                            int temp = (int)((float)Math.Sqrt((double)i / Itterations) * 255.0f);
                            color = new Color(temp / 4, temp / 2, temp);
                            prevItteration = i + 1;
                        }
                    }
                    dbmp.SetPixel(x, y, color);
                }
            });
        }

        public void OnDebug()
        {
            Decimal2 Offset = new Decimal2 { X = (decimal)Width / Height / 2.0M, Y = 0.5M };
            Parallel.For(0, Height, y =>
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector3 color = new Vector3(0, 0, 0);
                    for (int ys = 0; ys < SampleCount; ys++)
                    {
                        for (int xs = 0; xs < SampleCount; xs++)
                        {
                            Decimal2 C = (new Decimal2 { X = (x + (xs / (decimal)SampleCount)) / Height, Y = (y + (ys / (decimal)SampleCount)) / Height } - Offset) * Zoom + Pan;
                            Decimal2 v = C;

                            int prevItteration = Itterations;
                            int i = 0;
                            while (i < prevItteration)
                            {
                                v = new Decimal2 { X = (v.X * v.X) - (v.Y * v.Y), Y = v.X * v.Y * 2.0M } + C;

                                i++;

                                if ((prevItteration == Itterations) && ((v.X * v.X) + (v.Y * v.Y)) > 4.0M)
                                {
                                    //float t = (float)i / Itterations;
                                    //color = new Vector3(t);
                                    float NIC = (float)((float)i - (Math.Log(Math.Log(Math.Sqrt((double)v.X * (double)v.X + (double)v.Y * (double)v.Y)))) / Math.Log(2.0)) / Itterations;
                                    color += new Vector3((float)Math.Sin(NIC * Color.X), (float)Math.Sin(NIC * Color.Y), (float)Math.Sin(NIC * Color.Z));
                                    prevItteration = i + 1;
                                }
                            }
                        }
                    }
                    color /= (float)SampleCount * SampleCount;
                    Color col = new Color(color.X, color.Y, color.Z);
                    dbmp.SetPixel(x, y, col);
                }
                Console.WriteLine("Finsihed row " + y + "/" + Height);
            });
            Console.WriteLine("Done!");
        }

        private void RenderCallBack()
        {
            GetMouseData();
            GetKeys();
            UserInput();
            if (!KeyDown(Key.Space) && OneFrame)
                return;
            if (RenderWithCPU) DrawCPU(); else DrawGPU();
            GetTime();
        }

        private void DrawCPU()
        {
            if (Debug) OnDebug(); else OnUpdate();
            texture = new Texture2D(device, td, new DataRectangle(dbmp.BitsHandle.AddrOfPinnedObject(), Width * 4));
            ShaderResourceView textureView = new ShaderResourceView(device, texture);
            deviceContext.PixelShader.SetShaderResource(0, textureView);
            texture.Dispose();
            textureView.Dispose();

            deviceContext.OutputMerger.SetRenderTargets(renderTargetView);
            deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(triangleVertexBuffer, Utilities.SizeOf<VertexPositionTexture>(), 0));
            deviceContext.Draw(vertices.Length, 0);

            swapChain.Present(0, PresentFlags.None);
        }

        private void DrawGPU()
        {
            AssignShaderBuffer();
            dbdescription = new BufferDescription(AssignShaderBufferSize(), ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, 0, 0);
            ShaderBufferInstance = D3D11.Buffer.Create(device, ref ShaderBuffer, dbdescription);
            deviceContext.PixelShader.SetConstantBuffer(0, ShaderBufferInstance);
            ShaderBufferInstance.Dispose();

            deviceContext.OutputMerger.SetRenderTargets(renderTargetView);
            deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(triangleVertexBuffer, Utilities.SizeOf<Vector3>(), 0));
            deviceContext.Draw(vertices.Length, 0);

            swapChain.Present(1, PresentFlags.None);
        }

        public void Run()
        {
            RenderLoop.Run(renderForm, RenderCallBack);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool boolean)
        {
            mouse.Dispose();
            keyboard.Dispose();
            device.Dispose();
            deviceContext.Dispose();
            swapChain.Dispose();
            renderTargetView.Dispose();
            triangleVertexBuffer.Dispose();
            vertexShader.Dispose();
            pixelShader.Dispose();
            inputLayout.Dispose();
            inputSignature.Dispose();
            renderForm.Dispose();
        }

        private class Chey
        {
            public Key key;
            public bool Down, Up, Held, Raised;

            public Chey(Key key)
            {
                this.key = key;
                Down = Up = Held = false;
                Raised = true;
            }
        }

        private class Button
        {
            // 0 is left
            // 1 is right
            // 
            public bool Down, Up, Held, Raised;

            public Button()
            {
                Down = Up = Held = false;
                Raised = true;
            }
        }

        private class DirectBitmap : IDisposable
        {
            public Bitmap Bitmap { get; private set; }
            public int[] Bits { get; private set; }
            public bool Disposed { get; private set; }
            public int Height { get; private set; }
            public int Width { get; private set; }

            public GCHandle BitsHandle { get; private set; }

            public DirectBitmap(int width, int height)
            {
                Width = width;
                Height = height;
                Bits = new int[width * height];
                BitsHandle = GCHandle.Alloc(Bits, GCHandleType.Pinned);
                Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, BitsHandle.AddrOfPinnedObject());
            }

            public void SetPixel(int x, int y, Color color)
            {
                int index = x + (y * Width);
                int col = (int)(color.A << 24) + (int)(color.R << 16) + (int)(color.G << 8) + color.B;

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
                Bitmap.Dispose();
                BitsHandle.Free();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        public struct VertexPositionTexture
        {
            public VertexPositionTexture(Vector3 position, Vector2 textureUV)
            {
                Position = new Vector4(position, 1.0f);
                TextureUV = textureUV;
                padding = new Vector2();
            }

            public Vector4 Position;
            public Vector2 TextureUV;
            private Vector2 padding;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}