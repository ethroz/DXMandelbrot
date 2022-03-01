using System;
using Vortice;
using static Win32.WindowMessage;
using static Win32.User32;
using System.Runtime.InteropServices;

namespace Win32.Events;

public sealed class WindowEvents
{
    public EventHandler<SizeMessage> OnRestate;
    public EventHandler<SizeContainer> OnSize;
    public EventHandler<bool> OnFocus;
    public EventHandler<RawRect> OnRectChanged;
    public EventHandler OnSized;
    public EventHandler<POINT> OnMoved;

    internal IntPtr FireWindowEvents(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Size:
                int w = Utils.Loword(lParam);
                int h = Utils.Hiword(lParam);
                SizeContainer sc = new() { msg = (SizeMessage)wParam, width = w, height = h };
                OnRestate?.Invoke(hWnd, sc.msg);
                OnSize?.Invoke(hWnd, sc);
                //DXMandelbrot.Generator.print("activated");
                break;

            case SetFocus:
                OnFocus?.Invoke(hWnd, true);
                break;

            case KillFocus:
                OnFocus?.Invoke(hWnd, false);
                break;

            case Activate:
                OnFocus?.Invoke(hWnd, Utils.Loword(wParam) != 0);
                break;

            case Move:
                int x = Utils.Loword(lParam);
                int y = Utils.Hiword(lParam);
                OnMoved?.Invoke(hWnd, new POINT(x, y));
                break;

            case NcCalcSize:
                IntPtr def = DefWindowProc(hWnd, msg, wParam, lParam);
                RawRect rect = Marshal.PtrToStructure<RawRect>(lParam);
                OnRectChanged?.Invoke(hWnd, rect);
                return def;

            case ExitSizeMove:
                OnSized?.Invoke(hWnd, null);
                return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }
}
