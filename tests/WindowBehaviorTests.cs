using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

class FullscreenHost : Forms.Form {
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
    public FullscreenHost(){Text="Codex fullscreen regression probe";Width=640;Height=360;StartPosition=Forms.FormStartPosition.CenterScreen;Controls.Add(new Forms.Label{Text="悬浮窗全屏避让验证 · 数秒后自动关闭",Dock=Forms.DockStyle.Fill,TextAlign=System.Drawing.ContentAlignment.MiddleCenter});}
    protected override void WndProc(ref Forms.Message m){
        if(m.Msg==0x8001){WindowState=Forms.FormWindowState.Normal;FormBorderStyle=Forms.FormBorderStyle.None;Bounds=Forms.Screen.FromHandle(Handle).Bounds;Activate();return;}
        if(m.Msg==0x8002){ShowWindow(Handle,1);WindowState=Forms.FormWindowState.Normal;FormBorderStyle=Forms.FormBorderStyle.Sizable;Width=640;Height=360;CenterToScreen();Activate();SetForegroundWindow(Handle);return;}
        if(m.Msg==0x8003){WindowState=Forms.FormWindowState.Minimized;return;}
        if(m.Msg==0x8004){ShowWindow(Handle,1);Activate();SetForegroundWindow(Handle);return;}
        if(m.Msg==0x8005){Close();return;}
        if(m.Msg==0x8006){FormBorderStyle=Forms.FormBorderStyle.Sizable;WindowState=Forms.FormWindowState.Maximized;Activate();return;}
        base.WndProc(ref m);
    }
}
static class WindowBehaviorTests {
    [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
    static readonly List<object> results=new List<object>();static int failures;
    static Assembly target;static Type presence;static IntPtr host;static Process probeProcess;
    static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static void Host(int command){PostMessage(host,command,IntPtr.Zero,IntPtr.Zero);Pump(200);probeProcess.Refresh();host=probeProcess.MainWindowHandle;}
    static void Check(string skin,string name,bool passed){results.Add(new{skin=skin,check=name,passed=passed});if(!passed)failures++;}
    static void Show(Window w){if(presence==null)w.Show();else presence.GetMethod("Show").Invoke(null,new object[]{w});}
    static void Hide(Window w){if(presence==null)w.Hide();else presence.GetMethod("Hide").Invoke(null,new object[]{w});}
    static void Pin(Window w,bool enabled){if(presence==null)w.Topmost=enabled;else presence.GetMethod("SetPinned").Invoke(null,new object[]{w,enabled});}
    static void Notify(Window w){if(w.GetType().Name=="FloatWindow")w.GetType().GetMethod("Notify").Invoke(w,new object[]{"regression",1});else w.GetType().GetMethod("Notify").Invoke(w,new object[]{3.0});}
    [STAThread]static int Main(string[] args){
        if(args.Length==1&&args[0]=="--host"){Forms.Application.EnableVisualStyles();Forms.Application.Run(new FullscreenHost());return 0;}
        Process probe=null;var windows=new List<Window>();var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        try{
            target=Assembly.LoadFrom(Path.GetFullPath(args[0]));presence=target.GetType("CodexMeter.WindowPresence");
            probe=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--host"){UseShellExecute=false});probeProcess=probe;probe.WaitForInputIdle(5000);Pump(300);probe.Refresh();host=probe.MainWindowHandle;if(host==IntPtr.Zero)throw new Exception("Probe did not create a window.");
            foreach(string skin in new[]{"capsule","ring","aurora"}){
                Host(0x8002);if(GetForegroundWindow()!=host)throw new Exception("Probe could not obtain foreground; test is inconclusive. Foreground="+GetForegroundWindow()+", probe="+host);
                Type type=target.GetType(skin=="capsule"?"CodexMeter.FloatWindow":"CodexMeter.RingWindow",true);
                var w=(Window)Activator.CreateInstance(type,skin=="capsule"?new object[]{true}:new object[]{true,skin=="aurora"});windows.Add(w);
                if(presence!=null)presence.GetMethod("Attach").Invoke(null,new object[]{w,false});
                var h=new WindowInteropHelper(w).EnsureHandle();var screen=Forms.Screen.FromHandle(host);SetWindowPos(h,IntPtr.Zero,screen.Bounds.Left+35,screen.Bounds.Top+50,0,0,0x15);
                Check(skin,"passive startup",!w.ShowActivated);Show(w);Pump(200);Check(skin,"show retains foreground",GetForegroundWindow()==host);
                Check(skin,"mouse activation refused",SendMessage(h,0x21,IntPtr.Zero,IntPtr.Zero)==new IntPtr(3));
                Check(skin,"native no-activate style",(GetWindowLong(h,-20)&0x08000000)!=0);
                Hide(w);Host(0x8004);Notify(w);Pump(150);Check(skin,"completion does not reopen tray-hidden window",!w.IsVisible);Check(skin,"completion retains foreground",GetForegroundWindow()==host);
                Show(w);Host(0x8006);Pump(400);Check(skin,"ordinary maximized window keeps widget",w.IsVisible);
                Host(0x8001);Pump(550);Check(skin,"same-monitor fullscreen yields",!w.IsVisible&&!w.Topmost);Notify(w);Pump(150);Check(skin,"fullscreen completion stays suppressed",!w.IsVisible&&GetForegroundWindow()==host);
                Show(w);Pump(100);Check(skin,"show command respects fullscreen",!w.IsVisible&&GetForegroundWindow()==host);
                var duringFullscreen=(Window)Activator.CreateInstance(type,skin=="capsule"?new object[]{true}:new object[]{true,skin=="aurora"});windows.Add(duringFullscreen);if(presence!=null)presence.GetMethod("Attach").Invoke(null,new object[]{duringFullscreen,false});var extraHandle=new WindowInteropHelper(duringFullscreen).EnsureHandle();SetWindowPos(extraHandle,IntPtr.Zero,screen.Bounds.Left+40,screen.Bounds.Top+60,0,0,0x15);Show(duringFullscreen);Pump(150);Check(skin,"startup during fullscreen stays suppressed",!duringFullscreen.IsVisible&&!duringFullscreen.Topmost&&GetForegroundWindow()==host);duringFullscreen.Close();
                Host(0x8002);Pump(550);Check(skin,"restore is visible without activation",w.IsVisible&&w.Topmost&&GetForegroundWindow()==host);
                Pin(w,false);Host(0x8001);Pump(400);Host(0x8002);Pump(400);Check(skin,"manual unpin survives fullscreen",w.IsVisible&&!w.Topmost);Pin(w,true);
                Host(0x8001);Pump(400);Host(0x8003);Pump(400);Check(skin,"minimizing fullscreen restores without activation",w.IsVisible&&GetForegroundWindow()!=h);Host(0x8002);
                Host(0x8001);Pump(400);Hide(w);Host(0x8002);Pump(400);Check(skin,"manual hide survives fullscreen exit",!w.IsVisible);Show(w);
                if(Forms.Screen.AllScreens.Length>1){foreach(var other in Forms.Screen.AllScreens)if(other.DeviceName!=screen.DeviceName){SetWindowPos(h,IntPtr.Zero,other.Bounds.Left+35,other.Bounds.Top+50,0,0,0x15);break;}Host(0x8001);Pump(450);Check(skin,"other-monitor fullscreen keeps widget",w.IsVisible&&GetForegroundWindow()==host);Host(0x8002);SetWindowPos(h,IntPtr.Zero,screen.Bounds.Left+35,screen.Bounds.Top+50,0,0,0x15);}
                Notify(w);Pump(3250);bool flashing=(bool)type.GetProperty(skin=="capsule"?"IsFlashing":"IsSweeping").GetValue(w,null);Check(skin,"completion still stops after three seconds",!flashing);w.Close();
            }
            File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(new{passed=failures==0,failures=failures,checks=results}));return failures==0?0:1;
        }catch(Exception ex){File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(new{passed=false,error=ex.ToString(),checks=results}));return 2;}
        finally{foreach(var w in windows)if(w.IsVisible)w.Close();if(host!=IntPtr.Zero)PostMessage(host,0x8005,IntPtr.Zero,IntPtr.Zero);if(probe!=null){if(!probe.WaitForExit(2000))probe.Kill();probe.Dispose();}app.Shutdown();}
    }
}
