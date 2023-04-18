using System.Runtime.InteropServices;

public class MouseHook
{
    public enum MouseEvents
    {
        LeftDown = 0x201,
        LeftUp = 0x202,
        LeftDoubleClick = 0x203,
        RightDown = 0x204,
        RightUp = 0x205,
        RightDoubleClick = 0x206,
        MiddleDown = 0x207,
        MiddleUp = 0x208,
        MiddleDoubleClick = 0x209,
        MouseScroll = 0x20A
    }
    public enum MouseWheelEvents
    {
        ScrollUp = 7864320,
        ScrollDown = -7864320
    }
    
    private const int HC_ACTION = 0;
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x200;
    private const int WM_LBUTTONDOWN = 0x201;
    private const int WM_LBUTTONUP = 0x202;
    private const int WM_LBUTTONDBLCLK = 0x203;
    private const int WM_RBUTTONDOWN = 0x204;
    private const int WM_RBUTTONUP = 0x205;
    private const int WM_RBUTTONDBLCLK = 0x206;
    private const int WM_MBUTTONDOWN = 0x207;
    private const int WM_MBUTTONUP = 0x208;
    private const int WM_MBUTTONDBLCLK = 0x209;
    private const int WM_MOUSEWHEEL = 0x20A;

    public struct POINT
    {
        public int x;
        public int y;
    }

    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public int mouseData;
    }

    // 'API Functions
    [DllImport("user32")]
    private static extern int SetWindowsHookEx(int idHook, MouseProcDelegate lpfn, int hmod, int dwThreadId);
    [DllImport("user32")]
    private static extern int UnhookWindowsHookEx(int hHook);

    // 'Our Mouse Delegate
    private delegate int MouseProcDelegate(int nCode, int wParam, ref MSLLHOOKSTRUCT lParam);
    // 'The Mouse events
    public static event MouseMoveEventHandler MouseMove;

    public delegate void MouseMoveEventHandler(POINT ptLocat);

    public static event MouseEventEventHandler MouseEvent;

    public delegate void MouseEventEventHandler(MouseEvents mEvent);

    public static event WheelEventEventHandler WheelEvent;

    public delegate void WheelEventEventHandler(MouseWheelEvents wEvent);

    // 'The identifyer for our MouseHook
    private static int Mousehook;
    // 'MouseHookDelegate
    private static MouseProcDelegate MouseHookDelegate;
    public static void InstallHook()
    {
        MouseHookDelegate = new MouseProcDelegate(MouseProc);
        Mousehook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookDelegate, 0, 0);
    }
    public static void UninstallHook()
    {
        UnhookWindowsHookEx(Mousehook);
    }

    private static int MouseProc(int nCode, int wParam, ref MSLLHOOKSTRUCT lParam)
    {
        if ((nCode == HC_ACTION))
        {
            if (wParam == WM_MOUSEMOVE)
                MouseMove?.Invoke(lParam.pt);
            else if (wParam == WM_LBUTTONDOWN | wParam == WM_LBUTTONUP | wParam == WM_LBUTTONDBLCLK | wParam == WM_RBUTTONDOWN | wParam == WM_RBUTTONUP | wParam == WM_RBUTTONDBLCLK | wParam == WM_MBUTTONDOWN | wParam == WM_MBUTTONUP | wParam == WM_MBUTTONDBLCLK)
                MouseEvent?.Invoke((MouseEvents)wParam);
            else if (wParam == WM_MOUSEWHEEL)
                WheelEvent?.Invoke((MouseWheelEvents)lParam.mouseData);
        }
        return 0;
    }

    ~MouseHook()
    {
        UnhookWindowsHookEx(Mousehook);
    }
}
