using System;
using System.Runtime.InteropServices;
using Vortice;
using Win32.Events;
using static Win32.ShowWindowCommand;
using static Win32.SizeMessage;
using static Win32.User32;
using static Win32.WindowClassStyles;
using static Win32.WindowExStyles;
using static Win32.WindowStyles;

namespace Win32;

public class Application : IDisposable
{
    public string Title { get; private set; }
    public RawRect Rectangle { get; private set; }
    public readonly IntPtr Handle;
    public WindowStyles Style { get; private set; }
    public WindowExStyles ExStyle { get; private set; }
    public ShowWindowCommand State { get; private set; }
    public bool HasFocus { get; private set; }
    public bool Fullscreen;

    public static readonly IntPtr Module = GetModuleHandle(null);
    public static readonly WindowEvents WindowEvents = new WindowEvents();
    private delegate IntPtr WndProcDel(IntPtr hwnd, WindowMessage msg, IntPtr wparam, IntPtr lparam);
    private WndProcDel WndProc;

    private const string className = "WndClass";
    private const WindowExStyles normalExStyle = WS_EX_APPWINDOW | WS_EX_WINDOWEDGE;

    public Application(string title, int width = 1280, int height = 720, int x = -1, int y = -1, ShowWindowCommand startState = Normal, WindowStyles style = WS_TILEDWINDOW)
    {
        Title = title;
        Style = style;

        WndProc = WindowProc;

        WNDCLASSEX wndClass = new WNDCLASSEX
        {
            Size = Marshal.SizeOf<WNDCLASSEX>(),
            Styles = CS_HREDRAW | CS_VREDRAW | CS_OWNDC,
            WindowProc = Marshal.GetFunctionPointerForDelegate(WndProc),
            InstanceHandle = Module,
            CursorHandle = LoadCursor(IntPtr.Zero, SystemCursor.IDC_ARROW),
            BackgroundBrushHandle = IntPtr.Zero,
            IconHandle = IntPtr.Zero,
            ClassName = className
        };

        RegisterClassEx(ref wndClass);

        if (x == -1)
        {
            x = (GetSystemMetrics(SystemMetrics.SM_CXSCREEN) - width) / 2;
            y = (GetSystemMetrics(SystemMetrics.SM_CYSCREEN) - height) / 2;
        }


        ExStyle = normalExStyle;

        RawRect rect = new RawRect(x, y, x + width, y + height);
        AdjustWindowRectEx(ref rect, Style, false, ExStyle);

        int windowWidth = rect.Right - rect.Left;
        int windowHeight = rect.Bottom - rect.Top;

        Handle = CreateWindowEx(ExStyle, className, Title, (int)Style, x, y, 
            windowWidth, windowHeight, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 
            IntPtr.Zero);

        ShowWindow(Handle, startState);
        State = startState;

        POINT p = new POINT();
        ClientToScreen(Handle, ref p);
        Rectangle = new RawRect(p.X, p.Y, p.X + width, p.Y + height);

        WindowEvents.OnRestate += (o, e) =>
        {
            switch (e)
            {
                case SIZE_MINIMIZED:
                    State = Minimize;
                    break;
                case SIZE_RESTORED:
                    State = Normal;
                    break;
                case SIZE_MAXIMIZED:
                    State = Maximize;
                    break;
            }
        };

        WindowEvents.OnFocus += (o, e) =>
        {
            HasFocus = e;
        };

        WindowEvents.OnMoved += (o, e) =>
        {
            Rectangle = new RawRect(e.X, e.Y, Rectangle.Right - Rectangle.Left + e.X, 
                Rectangle.Bottom - Rectangle.Top + e.Y);
        };

        WindowEvents.OnRectChanged += (o, e) =>
        {
            Rectangle = e;
        };
    }

    public void MessageLoop()
    {
        while (GetMessage(out Message msg, Handle, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private static IntPtr WindowProc(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WindowMessage.Destroy:
                PostQuitMessage(0);
                return IntPtr.Zero;

            case WindowMessage.GetMinMaxInfo:
                MINMAXINFO info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                RawRect rect = new RawRect(0, 0, 160, 90);
                AdjustWindowRectEx(ref rect, WS_TILEDWINDOW, false, normalExStyle);
                info.ptMinTrackSize = new POINT(rect.Right - rect.Left, rect.Bottom - rect.Top);
                Marshal.StructureToPtr(info, lParam, true);
                return IntPtr.Zero;

            default:
                return WindowEvents.FireWindowEvents(hWnd, msg, wParam, lParam);
        }
    }

    public void SetTitle(string title)
    {
        Title = title;
        SetWindowText(Handle, title);
    }

    public void Show()
    {
        ShowWindow(Handle, Restore);
    }

    public void Stop()
    {
        PostMessage(Handle, WindowMessage.Close, IntPtr.Zero, IntPtr.Zero);
    }

    public void Show(ShowWindowCommand command)
    {
        ShowWindow(Handle, command);
    }

    public void Dispose()
    {
        UnregisterClass(className, Module);
    }
}
