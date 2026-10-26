using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CodexMeter {
// Keep visual reminders separate from keyboard focus and the user's explicit visibility choice.
static class WindowPresence {
    static readonly ConditionalWeakTable<Window,State> states=new ConditionalWeakTable<Window,State>();
    public static void Attach(Window window,bool preview){State state;if(!states.TryGetValue(window,out state)){state=new State(window,preview);states.Add(window,state);}else if(!preview)state.StartMonitoring();}
    static State Get(Window window){State state;if(!states.TryGetValue(window,out state)){Attach(window,false);states.TryGetValue(window,out state);}return state;}
    public static void Show(Window window){Get(window).Show();}
    public static void Hide(Window window){Get(window).Hide();}
    public static void SetPinned(Window window,bool pinned){Get(window).SetPinned(pinned);}
    public static bool IsPinned(Window window){return Get(window).Pinned;}
    public static bool CoversMonitor(Rect client,Rect monitor,string className,bool minimized){
        if(minimized||client.IsEmpty||monitor.IsEmpty||client.Width<=0||client.Height<=0||monitor.Width<=0||monitor.Height<=0)return false;
        if(className=="Progman"||className=="WorkerW"||className=="Shell_TrayWnd"||className=="Shell_SecondaryTrayWnd")return false;
        const double tolerance=2;return client.Left<=monitor.Left+tolerance&&client.Top<=monitor.Top+tolerance&&client.Right>=monitor.Right-tolerance&&client.Bottom>=monitor.Bottom-tolerance;
    }
    sealed class State {
        readonly Window window;readonly DispatcherTimer timer;IntPtr handle;HwndSource source;bool observeFullscreen,requestedVisible=true,suppressed,closed;
        public bool Pinned=true;
        public State(Window window,bool preview){
            this.window=window;window.ShowActivated=false;
            timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(250)};timer.Tick+=(s,e)=>Update();
            window.SourceInitialized+=(s,e)=>InitializeHandle();
            window.IsVisibleChanged+=(s,e)=>{if(window.IsVisible)Update();};
            window.Closed+=(s,e)=>{closed=true;timer.Stop();if(source!=null)source.RemoveHook(Hook);};
            if(!preview)StartMonitoring();
        }
        public void StartMonitoring(){observeFullscreen=true;timer.Start();}
        void InitializeHandle(){handle=new WindowInteropHelper(window).Handle;source=HwndSource.FromHwnd(handle);if(source!=null)source.AddHook(Hook);int style=GetWindowLong(handle,-20);SetWindowLong(handle,-20,style|0x08000000|0x00000080);}
        IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled){if(message==0x0021){handled=true;return new IntPtr(3);}return IntPtr.Zero;}
        void Pin(bool value){if(window.Topmost!=value)window.Topmost=value;}
        public void SetPinned(bool pinned){Pinned=pinned;Update();if(!suppressed)Pin(pinned&&requestedVisible);}
        public void Show(){if(closed)return;requestedVisible=true;if(handle==IntPtr.Zero)new WindowInteropHelper(window).EnsureHandle();Update();if(suppressed)return;Pin(Pinned);window.ShowActivated=false;window.Show();}
        public void Hide(){requestedVisible=false;if(!closed)window.Hide();}
        void Update(){
            if(closed||handle==IntPtr.Zero)return;
            bool block=observeFullscreen&&ForegroundFullscreen(handle);
            if(block){suppressed=true;Pin(false);if(window.IsVisible)window.Hide();return;}
            if(suppressed){suppressed=false;if(requestedVisible){Pin(Pinned);window.ShowActivated=false;window.Show();}}
            else if(window.IsVisible)Pin(Pinned);
        }
    }
    [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;public Rect Rect{get{return new Rect(Left,Top,Math.Max(0,Right-Left),Math.Max(0,Bottom-Top));}}}
    [StructLayout(LayoutKind.Sequential)]struct NativePoint {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;}
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr window,out NativeRect rect);
    [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr window,ref NativePoint point);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder name,int count);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetWindowLong(IntPtr window,int index,int value);
    static bool ForegroundFullscreen(IntPtr widget){
        IntPtr foreground=GetForegroundWindow();if(foreground==IntPtr.Zero||foreground==widget||IsIconic(foreground))return false;
        uint process;GetWindowThreadProcessId(foreground,out process);if(process==(uint)Process.GetCurrentProcess().Id)return false;
        IntPtr monitor=MonitorFromWindow(foreground,2);if(monitor==IntPtr.Zero||monitor!=MonitorFromWindow(widget,2))return false;
        var info=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};NativeRect client;var origin=new NativePoint();if(!GetMonitorInfo(monitor,ref info)||!GetClientRect(foreground,out client)||!ClientToScreen(foreground,ref origin))return false;
        var name=new StringBuilder(128);GetClassName(foreground,name,name.Capacity);
        return CoversMonitor(new Rect(origin.X,origin.Y,Math.Max(0,client.Right-client.Left),Math.Max(0,client.Bottom-client.Top)),info.Monitor.Rect,name.ToString(),false);
    }
}
}
