using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DirectInput;
using SharpDX.DXGI;
using SharpDX.Windows;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Color = SharpDX.Color;
using D3D11 = SharpDX.Direct3D11;

namespace DXMandelBrot
{
    public class Generator : IDisposable
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
        private Queue<Action> ToDo = new Queue<Action>();

        public bool Running = true;
        public bool RenderWithCPU = false;
        public double elapsedTime;
        private bool OneFrameMode = false;
        private bool RenderOnce = false;
        private System.Diagnostics.Stopwatch sw;
        private long t1, t2, t3, t4;
        private Vector3 SelectedColor = new Color(59, 131, 247).ToVector3();
        private int Iterations = 150;
        private double IterationScale = 1.0;
        private double StartingZoom = 2.0;
        private Double2 StartingPosition = new Double2(0.00164372197625241, -0.822467633314415);
        private Double2 Pan;
        private double Zoom;
        private int Width;
        private int Height;
        public enum WindowState { Minimized, Normal, Maximized, FullScreen, NotSet };
        private WindowState prevState = WindowState.NotSet;
        public WindowState State = WindowState.Maximized;
        private int ResolutionIndex = 0;
        private Double2 StartingPan;
        private POINT StartingMousePos;
        private bool MouseOnWindow;
        private bool HasFocus;
        private bool PanAllowed;
        private bool MouseScrollMode = false;

        private Mouse mouse;
        private Button[] buttons;
        private POINT CurrentMousePos;
        public POINT DeltaMousePos;
        public int DeltaMouseScroll;
        private Keyboard keyboard;
        private Chey[] cheyArray;

        private void Test()
        {
        }

        public Generator()
        {
            SetDimensions(ResolutionIndex);
            renderForm = new RenderForm("DXMandelbrot")
            {
                ClientSize = new Size(Width, Height),
                AllowUserResizing = false,
                MaximizeBox = true
            };
            ReassignWindowState();
            renderForm.FormClosing += RenderForm_FormClosing;
            renderForm.GotFocus += RenderForm_GotFocus;
            renderForm.LostFocus += RenderForm_LostFocus;
            renderForm.Resize += RenderForm_Resize;
            Zoom = 2.0 / Height;
            Pan = new Double2(-0.5 * Width, -0.5 * Height) + new Double2(StartingPosition.X, -StartingPosition.Y) * Height / 2;
            double zoomBefore = Zoom;
            Zoom = StartingZoom / Height;
            Double2 offset = new Double2(0.5 * Width, 0.5 * Height);
            Pan = (Pan + offset) * zoomBefore / Zoom - offset;

            sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            InitializeMouse();
            InitializeKeyboard();
            ChangeResolution(ResolutionIndex);
            InitializeTriangle();
        }

        private void RenderForm_Resize(object sender, EventArgs e)
        {
            switch (renderForm.WindowState)
            {
                case FormWindowState.Minimized:
                    State = WindowState.Minimized;
                    break;
                case FormWindowState.Normal:
                    State = WindowState.Normal;
                    break;
                case FormWindowState.Maximized:
                    if (renderForm.FormBorderStyle == FormBorderStyle.None)
                        State = WindowState.FullScreen;
                    else
                        State = WindowState.Maximized;
                    break;
            }
        }

        private void RenderForm_LostFocus(object sender, EventArgs e)
        {
            HasFocus = false;
        }

        private void RenderForm_GotFocus(object sender, EventArgs e)
        {
            HasFocus = true;
        }

        private void RenderForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Running = false;
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
            GetCursorPos(out CurrentMousePos);
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
            deviceContext.OutputMerger.SetRenderTargets(renderTargetView);
        }

        private void InitializeTriangle()
        {
            if (triangleVertexBuffer != null)
                triangleVertexBuffer.Dispose();
            if (RenderWithCPU)
                triangleVertexBuffer = D3D11.Buffer.Create(device, BindFlags.VertexBuffer, texturedVertices);
            else
                triangleVertexBuffer = D3D11.Buffer.Create(device, BindFlags.VertexBuffer, vertices);
        }

        private void InitializeShaders()
        {
            using (var vertexShaderByteCode = ShaderBytecode.Compile(Shaders.Shader1, "vertexShader", "vs_5_0", ShaderFlags.OptimizationLevel3))
            {
                inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                vertexShader = new VertexShader(device, vertexShaderByteCode);
            }
            using (var pixelShaderByteCode = ShaderBytecode.Compile(Shaders.Shader1, "pixelShader", "ps_5_0", ShaderFlags.OptimizationLevel3))
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
            using (var vertexShaderByteCode = ShaderBytecode.Compile(Shaders.Shader2, "vertexShader", "vs_5_0", ShaderFlags.Debug))
            {
                inputSignature = ShaderSignature.GetInputSignature(vertexShaderByteCode);
                vertexShader = new VertexShader(device, vertexShaderByteCode);
            }
            using (var pixelShaderByteCode = ShaderBytecode.Compile(Shaders.Shader2, "pixelShader", "ps_5_0", ShaderFlags.Debug))
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
                Color = SelectedColor,
                Iterations = Iterations,
                Zoom = (double)Zoom,
                Width = Width,
                Height = Height,
                ModdedTime = sw.ElapsedTicks % 60L,
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
            DeltaMousePos = new POINT(state.X, state.Y);
            GetCursorPos(out CurrentMousePos);
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
            State = (WindowState)(((int)State + 1) % 4);
        }

        private void ReassignWindowState()
        {
            if (State == prevState)
                return;
            prevState = State;
            switch (State)
            {
                case WindowState.Minimized:
                    renderForm.TopMost = false;
                    renderForm.WindowState = FormWindowState.Minimized;
                    break;
                case WindowState.Normal:
                    renderForm.TopMost = false;
                    renderForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                    renderForm.WindowState = FormWindowState.Maximized;
                    renderForm.WindowState = FormWindowState.Normal;
                    break;
                case WindowState.Maximized:
                    renderForm.TopMost = false;
                    renderForm.FormBorderStyle = FormBorderStyle.FixedSingle;
                    renderForm.WindowState = FormWindowState.Maximized;
                    break;
                case WindowState.FullScreen:
                    renderForm.TopMost = true;
                    renderForm.FormBorderStyle = FormBorderStyle.None;
                    renderForm.WindowState = FormWindowState.Normal;
                    renderForm.WindowState = FormWindowState.Maximized;
                    break;
            }
        }

        private void SetDimensions(int power)
        {
            power = Math.Min(2, Math.Max(power, 0));
            Width = Screen.PrimaryScreen.Bounds.Width;
            Height = Screen.PrimaryScreen.Bounds.Height;
            int i = 0;
            while (i < power)
            {
                if (Width % 2 != 0 || Height % 2 != 0)
                    break;
                Width /= 2;
                Height /= 2;
                i++;
            }
            ResolutionIndex = i;
        }

        private void ChangeResolution(int power)
        {
            double heightBefore = Height;
            SetDimensions(power);
            renderForm.Width = Width;
            renderForm.Height = Height;
            if (device != null)
                DisposeDXGI();
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
            Zoom *= heightBefore / Height;
            Pan *= Height / heightBefore;
        }

        private void SwitchDevice()
        {
            RenderWithCPU = !RenderWithCPU;
            if (RenderWithCPU) InitializeShaders2(); else InitializeShaders();
            InitializeTriangle();
        }

        private void GetTime()
        {
            t2 = sw.ElapsedTicks;
            elapsedTime = (t2 - t1) / 10000000.0;
            t1 = t2;
            renderForm.Text = "DXMandelbrot   FPS: " + (1.0 / elapsedTime).ToString("0.00") + "   Iterations: " + Iterations + "   Zoom: " + Zoom * Height
                + "   Width: " + Width + "   Pan: " + new Double2(Pan.X + Width / 2, -Pan.Y - Height / 2) * Zoom;
        }

        private void ControlLoop()
        {
            t3 = sw.ElapsedTicks;
            while (Running)
            {
                t4 = sw.ElapsedTicks;
                while (10000000.0f / (t4 - t3) > 250.0f)
                {
                    t4 = sw.ElapsedTicks;
                }
                t3 = t4;
                GetMouseData();
                GetKeys();
                UserInput();
            }
        }

        public void UserInput()
        {
            if (KeyDown(Key.Tab))
                ToDo.Enqueue(CycleWindowState);
            if (!HasFocus)
                return;

            if (KeyDown(Key.Left))
            {
                ToDo.Enqueue(() => ChangeResolution(ResolutionIndex + 1));
            }
            if (KeyDown(Key.Right))
            {
                ToDo.Enqueue(() => ChangeResolution(ResolutionIndex - 1));
            }
            if (KeyDown(Key.Return))
            {
                ToDo.Enqueue(SwitchDevice);
            }

            System.Drawing.Rectangle rect = renderForm.ClientRectangle;
            if (State != WindowState.FullScreen)
                rect.Location = new System.Drawing.Point(renderForm.DesktopLocation.X + 8, renderForm.DesktopLocation.Y + 31);
            MouseOnWindow = rect.Contains(CurrentMousePos.X, CurrentMousePos.Y);

            if (MouseOnWindow && ButtonDown(0))
            {
                StartingMousePos = CurrentMousePos;
                StartingPan = Pan;
                PanAllowed = true;
            }
            else if (ButtonUp(0))
                PanAllowed = false;
            if (PanAllowed && ButtonHeld(0))
            {
                // correct for rendersize vs size on display
                Pan.X = StartingPan.X - (CurrentMousePos.X - StartingMousePos.X) * Width / renderForm.ClientSize.Width;
                Pan.Y = StartingPan.Y - (CurrentMousePos.Y - StartingMousePos.Y) * Height / renderForm.ClientSize.Height;
            }
            else if (MouseOnWindow && DeltaMouseScroll != 0)
            {
                double zoomBefore = Zoom;
                Zoom = Math.Max(Math.Min(Zoom * (double)Math.Pow(1.05, -DeltaMouseScroll), 2.0 / Height), 1.0 / 3000000000000.0 / Height);
                Double2 offset = MouseScrollMode ? new Double2((CurrentMousePos.X - renderForm.Location.X) * Width / (double)renderForm.ClientSize.Width,
                    (CurrentMousePos.Y - renderForm.Location.Y) * Height / (double)renderForm.ClientSize.Height) : new Double2(0.5 * Width, 0.5 * Height);
                Pan = (Pan + offset) * zoomBefore / Zoom - offset;
            }

            if (KeyHeld(Key.Period))
                IterationScale *= 1.01;
            if (KeyHeld(Key.Comma))
                IterationScale *= 1.0 / 1.01;

            if (KeyHeld(Key.RightShift) && KeyDown(Key.R))
            {
                Pan = new Double2(-0.65 * Width, -0.5 * Height);
                Zoom = 2.0 / Height;
                IterationScale = 1.0;
            }
            else if (KeyDown(Key.R))
            {
                IterationScale = 1.0;
                Zoom = 2.0 / Height;
                Pan = new Double2(-0.5 * Width, -0.5 * Height) + new Double2(StartingPosition.X, -StartingPosition.Y) * Height / 2;
                double zoomBefore = Zoom;
                Zoom = StartingZoom / Height;
                Double2 offset = new Double2(0.5 * Width, 0.5 * Height);
                Pan = (Pan + offset) * zoomBefore / Zoom - offset;
            }

            // preferred iterations
            Iterations = (int)((82.686213896 * Math.Pow(1 / Zoom / Height, 0.634440905501) + 97.3183020129) * renderForm.ClientSize.Height / 1440.0 * IterationScale);
            if (Iterations < 0 || Iterations > 10000)
                Iterations = 10000;

            if (KeyDown(Key.T))
                Test();

            if (KeyDown(Key.O))
                OneFrameMode = !OneFrameMode;
            if (KeyDown(Key.Space))
                RenderOnce = true;
            if (KeyDown(Key.M))
                MouseScrollMode = !MouseScrollMode;

            if (KeyDown(Key.Escape))
                Running = false;
        }

        public void OnUpdate()
        {
            int iterations = Iterations;
            double zoom = Zoom;
            Double2 pan = Pan;
            Parallel.For(0, Height, y =>
            {
                double yy = y + 0.5;
                for (int x = 0; x < Width; x++)
                {
                    Color color = new Color();
                    Double2 C = (new Double2(x + 0.5, yy) + pan) * zoom;

                    double y2 = C.Y * C.Y;
                    double xt = (C.X - 0.25);
                    double q = xt * xt + y2;
                    if ((q * (q + xt) <= 0.25 * y2) || (C.X + 1) * (C.X + 1) + y2 <= 0.0625)
                    {
                        dbmp.SetPixel(x, y, Color.Black);
                        continue;
                    }

                    Double2 v = C;
                    for (int i = 0; i < iterations; i++)
                    {
                        v = new Double2(v.X * v.X - v.Y * v.Y, v.X * v.Y * 2.0) + C;

                        if (v.X * v.X + v.Y * v.Y > 4.0)
                        {
                            color = NICColor(i, iterations, v);
                            break;
                        }
                    }
                    dbmp.SetPixel(x, y, color);
                }
            });
        }

        private Color SqrtColor(int i, int iterations)
        {
            int temp = (int)((float)Math.Sqrt((double)i / iterations) * 255.0f);
            return new Color(temp / 4, temp / 2, temp);
        }

        private Color NICColor(int i, int iterations, Double2 v)
        {
            float NIC = (float)(i + 1.0 - Math.Log(Math.Log(v.X * v.X + v.Y * v.Y) / 2.0 / Math.Log(2.0)) / Math.Log(2.0)) / 20.0f;
            return new Color((float)Math.Sin(NIC * SelectedColor.X), (float)Math.Sin(NIC * SelectedColor.Y), (float)Math.Sin(NIC * SelectedColor.Z));
        }

        private Color BrightnessColor(int i, int iterations)
        {
            int brightness = (int)((float)i / iterations * 255.0f);
            return new Color(brightness, brightness, brightness);
        }

        private void RenderCallBack()
        {
            while (ToDo.Count > 0)
                ToDo.Dequeue().Invoke();
            ReassignWindowState();
            if (!Running)
                renderForm.Close();
            if ((!RenderOnce && OneFrameMode) || !HasFocus)
                return;
            RenderOnce = false;
            if (RenderWithCPU) DrawCPU(); else DrawGPU();
            GetTime();
        }

        private void DrawCPU()
        {
            OnUpdate();
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

            deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(triangleVertexBuffer, Utilities.SizeOf<Vector3>(), 0));
            deviceContext.Draw(vertices.Length, 0);

            swapChain.Present(0, PresentFlags.None);
        }

        public void Run()
        {
            Thread t = new Thread(() => ControlLoop());
            t.Start();
            t1 = sw.ElapsedTicks;
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
            DisposeDXGI();
            triangleVertexBuffer.Dispose();
            renderForm.Dispose();
        }

        private void DisposeDXGI()
        {
            device.Dispose();
            deviceContext.Dispose();
            swapChain.Dispose();
            renderTargetView.Dispose();
            vertexShader.Dispose();
            pixelShader.Dispose();
            inputSignature.Dispose();
            inputLayout.Dispose();
            dbmp.Dispose();
            if (texture != null)
                texture.Dispose();
            GC.Collect();
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

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private static void print(object message)
        {
            Console.WriteLine(message.ToString());
        }
    }
}