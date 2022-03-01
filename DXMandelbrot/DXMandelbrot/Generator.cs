using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectInput;
using Vortice.DXGI;
using Vortice.Mathematics;
using Win32;
using static Vortice.D3DCompiler.Compiler;
using static Vortice.Direct3D11.D3D11;

namespace DXMandelbrot;

public class Generator : IDisposable
{
    private Application window;
    private IDXGIFactory5 factory;
    private IDXGIAdapter4 adapter;
    private ID3D11Device5 device;
    private ID3D11DeviceContext3 context;
    private IDXGISwapChain4 swapChain;
    private ID3D11RenderTargetView1 renderTargetView;
    private ID3D11Buffer vertexBuffer;
    private ID3D11VertexShader vertexShader;
    private ID3D11PixelShader pixelShader;
    private VertexPositionTexture[] vertices = new VertexPositionTexture[]
    {
        new VertexPositionTexture(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(0.0f, 1.0f)),
        new VertexPositionTexture(new Vector3(-1.0f, 1.0f, 0.0f), new Vector2(0.0f, 0.0f)),
        new VertexPositionTexture(new Vector3(1.0f, -1.0f, 0.0f), new Vector2(1.0f, 1.0f)),
        new VertexPositionTexture(new Vector3(1.0f, 1.0f, 0.0f), new Vector2(1.0f, 0.0f))
    };
    private InputElementDescription[] inputElements = new InputElementDescription[]
    {
        new InputElementDescription("POSITION", 0, Format.R32G32B32A32_Float, 0, 0, InputClassification.PerVertexData, 0),
        new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 16, 0, InputClassification.PerVertexData, 0)
    };
    private ID3D11InputLayout inputLayout;
    private ShaderBuffer ShaderBuffer;
    private ID3D11Buffer ShaderBufferInstance;
    private BufferDescription sbdesc;
    private Texture2DDescription1 td;
    private QuickBitmap quickBit;
    private ID3D11Texture2D1 texture;
    private ID3D11ShaderResourceView1 textureView;
    private Queue<Action> ToDo = new Queue<Action>();
    private bool RenderFrame = true;

    public bool Running { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool RenderWithCPU = false;
    public double elapsedTime;
    private Stopwatch sw;
    private long t1, t2;
    private Vector3 SelectedColor = new Vector3(59, 131, 247) / 255.0f;
    private int Iterations = 150;
    private double IterationScale = 1.0;
    private Double2 StartingPosition = new Double2(0.00164372197625241, -0.822467633314415);
    private double StartingZoom = 2.0;
    private Double2 Pan;
    private double Zoom;
    private Double2 StartingPan;
    private POINT StartingMousePos;
    private bool MouseOnWindow;
    private bool PanAllowed;
    private bool MouseScrollMode = false;
    private SizeMessage previousSize;

    private IDirectInputDevice8 mouse;
    private Button[] buttons;
    private POINT CurrentMousePos;
    public POINT DeltaMousePos;
    public int DeltaMouseScroll;
    private IDirectInputDevice8 keyboard;
    private const int KeyboardBufferSize = 256;
    private const double maxElapsed = 1.0 / 250.0;
    private Chey[] cheyArray;

    public Generator()
    {
        Running = true;
        sw = new Stopwatch();
        sw.Start();
        Width = 1280;
        Height = 720;
        window = new Application("DXMandelbrot", Width, Height, 50, 50);

        Zoom = StartingZoom;
        Pan = StartingPosition;

        InitializeInputs();
        InitializeDeviceResources();

        Application.WindowEvents.OnSized += (o, e) =>
        {
            int w = window.Rectangle.Right - window.Rectangle.Left;
            int h = window.Rectangle.Bottom - window.Rectangle.Top;
            CheckResize(w, h);
        };

        Application.WindowEvents.OnSize += (o, e) =>
        {
            if (e.msg == previousSize)
                return;
            previousSize = e.msg;
            CheckResize(e.width, e.height);
        };

        Application.WindowEvents.OnRectChanged += (o, e) =>
        {
            RenderFrame = true;
        };
    }

    private void InitializeDeviceResources()
    {
        DeviceCreationFlags flags = DeviceCreationFlags.None;
#if DEBUG
        flags = DeviceCreationFlags.Debug;
#endif
        D3D11CreateDevice(null, DriverType.Hardware, flags, new FeatureLevel[] { FeatureLevel.Level_11_1 }, out ID3D11Device device0);
        device = new ID3D11Device5(device0.NativePointer);
        context = device.ImmediateContext3;
        factory = device.QueryInterface<IDXGIDevice>().GetParent<IDXGIAdapter>().GetParent<IDXGIFactory5>();
        adapter = new IDXGIAdapter4(factory.GetAdapter1(0).NativePointer);

        SwapChainDescription1 scd = new SwapChainDescription1()
        {
            BufferCount = 1,
            Format = Format.R8G8B8A8_UNorm,
            Height = Height,
            Width = Width,
            SampleDescription = new SampleDescription(1, 0),
            SwapEffect = SwapEffect.Discard,
            BufferUsage = Usage.RenderTargetOutput
        };
        swapChain = new IDXGISwapChain4(factory.CreateSwapChainForHwnd(device, window.Handle, scd).NativePointer);

        AssignRenderTarget();

        InitializeShaders();

        InitializeVertices();
        context.IASetVertexBuffer(0, vertexBuffer, Marshal.SizeOf<VertexPositionTexture>());
    }

    private void AssignRenderTarget()
    {
        using (ID3D11Texture2D1 backBuffer = swapChain.GetBuffer<ID3D11Texture2D1>(0))
            renderTargetView = device.CreateRenderTargetView1(backBuffer);
        context.RSSetViewport(0, 0, Width, Height);
        context.OMSetRenderTargets(renderTargetView);
    }

    private void InitializeVertices()
    {
        BufferDescription bd = new BufferDescription(vertices.Length * Marshal.SizeOf<VertexPositionTexture>(), BindFlags.VertexBuffer);
        vertexBuffer = device.CreateBuffer(vertices, bd);
    }

    private void InitializeShaders()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = "DXMandelbrot.DXMandelbrot.Shaders.hlsl";
        string shader;

        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        using (StreamReader reader = new StreamReader(stream))
        {
             shader = reader.ReadToEnd();
        }

        Compile(shader, null, null, "vertexShader", "VertexShader", "vs_5_0", ShaderFlags.OptimizationLevel3, out Blob shaderCode, out Blob errorCode);
        if (shaderCode == null)
            throw new Exception("HLSL vertex shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));
        vertexShader = device.CreateVertexShader(shaderCode);

        inputLayout = device.CreateInputLayout(inputElements, shaderCode);
        context.IASetInputLayout(inputLayout);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleStrip);

        shaderCode.Dispose();

        Compile(shader, null, null, "pixelShader", "PixelShader", "ps_5_0", ShaderFlags.OptimizationLevel3, out shaderCode, out errorCode);
        if (shaderCode == null)
            throw new Exception("HLSL pixel shader compilation error:\r\n" + Encoding.ASCII.GetString(errorCode.GetBytes()));
        pixelShader = device.CreatePixelShader(shaderCode);

        shaderCode.Dispose();

        context.VSSetShader(vertexShader);
        context.PSSetShader(pixelShader);

        SamplerDescription ssd = new SamplerDescription()
        {
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            Filter = Filter.MinMagMipLinear
        };
        context.PSSetSampler(0, device.CreateSamplerState(ssd));

        SetBufferValues();
        sbdesc = new BufferDescription(AssignShaderBufferSize(), ResourceUsage.Default, BindFlags.ConstantBuffer, 0);
        ShaderBufferInstance = device.CreateBuffer(ref ShaderBuffer, sbdesc);

        quickBit = new QuickBitmap(Width, Height);
        td = new Texture2DDescription1
        {
            Width = quickBit.Width,
            Height = quickBit.Height,
            ArraySize = 1,
            BindFlags = BindFlags.ShaderResource,
            Usage = ResourceUsage.Immutable,
            CpuAccessFlags = CpuAccessFlags.None,
            Format = Format.B8G8R8A8_UNorm,
            MipLevels = 1,
            OptionFlags = ResourceOptionFlags.None,
            SampleDescription = new SampleDescription(1, 0)
        };

        SetTexture();
    }

    private void SetBufferValues()
    {
        ShaderBuffer = new ShaderBuffer
        {
            Pan = new Double2(Pan.X, -Pan.Y),
            Color = SelectedColor,
            Iterations = Iterations,
            Zoom = (double)Zoom,
            Width = RenderWithCPU ? -1 : Width,
            Height = Height
        };
    }

    private void SetTexture()
    {
        texture = device.CreateTexture2D1(td, new SubresourceData[] { new SubresourceData(quickBit.BitsHandle, Width * 4) });
        textureView = device.CreateShaderResourceView1(texture);
        context.PSSetShaderResource(0, textureView);
    }

    private void Resize()
    {
        renderTargetView.Release();
        swapChain.ResizeBuffers(0, Width, Height);

        AssignRenderTarget();

        quickBit.Dispose();
        quickBit = new QuickBitmap(Width, Height);
        td.Width = Width;
        td.Height = Height;
    }

    private void UpdateShaderBuffer()
    {
        SetBufferValues();
        context.UpdateSubresource(ref ShaderBuffer, ShaderBufferInstance);
        context.PSSetConstantBuffer(0, ShaderBufferInstance);
    }

    private int AssignShaderBufferSize()
    {
        int size = Marshal.SizeOf<ShaderBuffer>();
        if (size / 16.0f != Math.Floor(size / 16.0f))
        {
            size += 16 - (size % 16);
        }
        return size;
    }

    private void InitializeInputs()
    {
        IDirectInput8 di8 = DInput.DirectInput8Create(Application.Module);
        foreach (DeviceInstance di in di8.GetDevices())
        {
            switch (di.Type)
            {
                case DeviceType.Keyboard:
                    keyboard = di8.CreateDevice(di.ProductGuid);
                    keyboard.SetDataFormat<RawKeyboardState>();
                    keyboard.SetCooperativeLevel(window.Handle, CooperativeLevel.Foreground | CooperativeLevel.NonExclusive);
                    keyboard.Acquire();
                    break;
                case DeviceType.Mouse:
                    mouse = di8.CreateDevice(di.ProductGuid);
                    mouse.SetDataFormat<RawMouseState>();
                    mouse.SetCooperativeLevel(window.Handle, CooperativeLevel.Foreground | CooperativeLevel.NonExclusive);
                    mouse.Acquire();
                    break;
            }
        }
        InitializeKeyboard();
        InitializeMouse();
    }

    private void InitializeMouse()
    {
        mouse.Acquire();
        MouseState state = mouse.GetCurrentMouseState();
        var allButtons = state.Buttons;
        buttons = new Button[allButtons.Length];
        for (int i = 0; i < allButtons.Length; i++)
            buttons[i] = new Button();
        User32.GetCursorPos(out CurrentMousePos);
    }

    private void InitializeKeyboard()
    {
        keyboard.Acquire();
        cheyArray = new Chey[KeyboardBufferSize];
        for (int i = 0; i < KeyboardBufferSize; i++)
        {
            char[] keySpelling = ((Key)i).ToString().ToCharArray();
            bool containsLetter = false;
            foreach (char c in keySpelling)
            {
                if (char.IsLetter(c))
                {
                    containsLetter = true;
                    break;
                }
            }
            if (containsLetter)
            {
                cheyArray[i] = new Chey((Key)i);
            }
        }
    }

    private void GetMouseData()
    {
        var state = mouse.GetCurrentMouseState();
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
        User32.GetCursorPos(out CurrentMousePos);
        DeltaMouseScroll = state.Z / 120;
    }

    private void GetKeys()
    {
        KeyboardState state = keyboard.GetCurrentKeyboardState();
        for (int i = 0; i < KeyboardBufferSize; i++)
        {
            if (cheyArray[i] == null)
                continue;
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
        return cheyArray[(int)key];
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

    private void CheckResize(int w, int h)
    {
        if ((w != Width || h != Height) && w != 0)
        {
            Width = w;
            Height = h;
            ToDo.Enqueue(() => Resize());
        }
    }

    private void ToggleFullscreen()
    {
        window.Fullscreen = !window.Fullscreen;
        swapChain.SetFullscreenState(window.Fullscreen, null);
        Width = User32.GetSystemMetrics(SystemMetrics.SM_CXSCREEN);
        Height = User32.GetSystemMetrics(SystemMetrics.SM_CYSCREEN);

        Resize();
    }

    private double GetTime(ref long t)
    {
        double elapsed = (sw.ElapsedTicks - t) / 10000000.0;
        double ms = (maxElapsed - elapsed) * 1000.0;
        if (ms > 1.0)
            Thread.Sleep(4 - (int)ms);
        while (elapsed < maxElapsed)
        {
            elapsed = (sw.ElapsedTicks - t) / 10000000.0;
        }
        t = sw.ElapsedTicks;
        return elapsed;
    }

    private void ControlLoop()
    {
        while (Running)
        {
            GetTime(ref t2);
            if (mouse.Acquire().Success && keyboard.Acquire().Success)
            {
                GetMouseData();
                GetKeys();
                UserInput();
            }
        }
    }

    public void UserInput()
    {
        if (KeyDown(Key.Return))
        {
            RenderWithCPU = !RenderWithCPU;
            RenderFrame = true;
        }

        System.Drawing.Rectangle rect = window.Rectangle;
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
            Pan = StartingPan + new Double2(StartingMousePos.X - CurrentMousePos.X, 
                CurrentMousePos.Y - StartingMousePos.Y) * Zoom / Height;
            RenderFrame = true;
        }
        else if (MouseOnWindow && DeltaMouseScroll != 0)
        {
            double zoomAfter = Zoom * Math.Pow(1.05, -DeltaMouseScroll);
            if (zoomAfter > 2.0)
            {
                zoomAfter = 2.0;
            }
            else if (zoomAfter < 1.0 / 3000000000000.0)
            {
                zoomAfter = 1.0 / 3000000000000.0;
            }
            Double2 offset = MouseScrollMode ? new Double2(CurrentMousePos.X - window.Rectangle.Left - Width / 2,
                window.Rectangle.Top - CurrentMousePos.Y + Height / 2) / Height: new Double2();
            Pan += offset * (Zoom - zoomAfter);
            Zoom = zoomAfter;
            RenderFrame = true;
        }

        if (KeyHeld(Key.Period))
        {
            IterationScale *= 1.01;
            RenderFrame = true;
        }
        else if (KeyHeld(Key.Comma))
        {
            IterationScale /= 1.01;
            RenderFrame = true;
        }

        if (KeyDown(Key.R))
        {
            IterationScale = 1.0;
            if (KeyHeld(Key.LeftShift))
            {
                Zoom = 2.0;
                Pan = new Double2(0.0, 0.0);
            }
            else
            {
                Zoom = StartingZoom;
                Pan = StartingPosition;
            }
            RenderFrame = true;
        }

        if (KeyDown(Key.M))
            MouseScrollMode = !MouseScrollMode;

        if (KeyDown(Key.F11))
            ToDo.Enqueue(ToggleFullscreen);
    }

    public void OnUpdate()
    {
        int iterations = ShaderBuffer.Iterations;
        double zoom = ShaderBuffer.Zoom;
        Double2 pan = ShaderBuffer.Pan;
        Double2 offset = new Double2(Width / 2, Height / 2);
        Parallel.For(0, Height, y =>
        {
            double yy = y + 0.5;
            for (int x = 0; x < Width; x++)
            {
                Color color = new Color();
                Double2 C = (new Double2(x + 0.5, yy) - offset) * zoom / Height + pan;

                double y2 = C.Y * C.Y;
                double xt = (C.X - 0.25);
                double q = xt * xt + y2;
                if ((q * (q + xt) <= 0.25 * y2) || (C.X + 1) * (C.X + 1) + y2 <= 0.0625)
                {
                    quickBit.SetPixel(x, y, (Color)Colors.Black);
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
                quickBit.SetPixel(x, y, color);
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

    private void RenderLoop()
    {
        while (Running)
        {
            elapsedTime = GetTime(ref t1);

            window.SetTitle("DXMandelbrot   " + (RenderWithCPU ? "C" : "G") + "PU FPS: " +
                (1.0 / elapsedTime).ToString("0") + "   Iterations: " + Iterations +
                "   Zoom: " + Zoom + "   Width: " + Width + "   Pan: " + Pan);
            if (ToDo.Count == 0 && !RenderFrame)
                continue;
            while (ToDo.Count > 0)
                ToDo.Dequeue().Invoke();
            RenderFrame = false;

            // preferred iterations
            Iterations = (int)((82.686213896 * Math.Pow(1 / Zoom, 0.634440905501) + 97.3183020129) * IterationScale);
            if (Iterations < 0 || Iterations > 10000)
                Iterations = 10000;
            UpdateShaderBuffer();

            if (RenderWithCPU)
            {
                OnUpdate();
                textureView.Dispose();
                texture.Dispose();
                SetTexture();
            }

            context.Draw(vertices.Length, 0);
            swapChain.Present(1);
        }
    }

    public void Run()
    {
        t1 = t1 = t2 = sw.ElapsedTicks;

        Thread r = new Thread(() => RenderLoop());
        Thread c = new Thread(() => ControlLoop());
        r.Start();
        c.Start();

        window.MessageLoop();

        Running = false;

        r.Join();
        c.Join();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool boolean)
    {
        mouse.Unacquire();
        mouse.Release();
        keyboard.Unacquire();
        keyboard.Release();
        textureView.Release();
        texture.Release();
        quickBit.Dispose();
        ShaderBufferInstance.Release();
        inputLayout.Release();
        vertexShader.Release();
        pixelShader.Release();
        vertexBuffer.Release();
        renderTargetView.Release();
        swapChain.Release();
        adapter.Release();
        factory.Release();
        context.Release();
        device.Release();
        window.Dispose();
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

    public static void print(object message)
    {
        Trace.WriteLine(message.ToString());
    }
}
