using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace CodexMeter {
class RingSurface : FrameworkElement {
    public double Remaining;public bool Dark;
    static SolidColorBrush B(string c){return (SolidColorBrush)new BrushConverter().ConvertFromString(c);}
    void DrawDark(DrawingContext dc){
        const double c=100,r=79;double p=Math.Max(0,Math.Min(100,Remaining))/100;
        var core=new RadialGradientBrush(Color.FromRgb(23,20,49),Color.FromRgb(13,17,36));dc.DrawEllipse(core,null,new Point(c,c),66,66);
        var track=new LinearGradientBrush(Color.FromRgb(37,34,68),Color.FromRgb(21,28,47),90);dc.DrawEllipse(null,new Pen(track,18),new Point(c,c),r,r);
        if(p<=0)return;
        var arc=new StreamGeometry();using(var s=arc.Open()){s.BeginFigure(new Point(c,c-r),false,false);if(p>=0.9999){s.ArcTo(new Point(c,c+r),new Size(r,r),0,false,SweepDirection.Clockwise,true,false);s.ArcTo(new Point(c,c-r),new Size(r,r),0,false,SweepDirection.Clockwise,true,false);}else{double a=-Math.PI/2+p*2*Math.PI;s.ArcTo(new Point(c+r*Math.Cos(a),c+r*Math.Sin(a)),new Size(r,r),0,p>0.5,SweepDirection.Clockwise,true,false);}}arc.Freeze();
        var color=new LinearGradientBrush{StartPoint=new Point(0.8,0),EndPoint=new Point(0.15,1)};
        string head=Remaining>50?"#9DFF7B":Remaining>30?"#81DAFF":Remaining>10?"#FFE286":"#FF9DAD";
        color.GradientStops.Add(new GradientStop(B(head).Color,0));color.GradientStops.Add(new GradientStop(B(Remaining>30?"#8FE0C2":"#E9A0B7").Color,0.36));color.GradientStops.Add(new GradientStop(B("#9CB6E0").Color,0.65));color.GradientStops.Add(new GradientStop(B("#B478F3").Color,1));
        dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(20,173,139,255)),23){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},arc);
        dc.DrawGeometry(null,new Pen(color,18){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},arc);
        dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(20,255,255,255)),16){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},arc);
        for(int i=0;i<(p>0.995?1:2);i++){double a=-Math.PI/2+(i==0?0:p*2*Math.PI);Point dot=new Point(c+r*Math.Cos(a),c+r*Math.Sin(a));dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(40,8,9,28)),null,new Point(dot.X,dot.Y+1),7.2,7.2);dc.DrawEllipse(B("#F6FAF7"),new Pen(B("#D9E4E1"),0.7),dot,6,6);dc.DrawEllipse(B(i==0?"#A5E9B1":"#CAB0E8"),null,dot,2.9,2.9);dc.DrawEllipse(Brushes.White,null,new Point(dot.X-0.8,dot.Y-1.1),1,1);}
    }
    protected override void OnRender(DrawingContext dc){
        if(Dark){DrawDark(dc);return;}
        const double c=100,r=96,inner=62;
        var track=new RadialGradientBrush();track.Center=new Point(0.57,0.58);track.GradientOrigin=new Point(0.6,0.63);track.RadiusX=0.54;track.RadiusY=0.54;
        track.GradientStops.Add(new GradientStop(Color.FromRgb(255,255,255),0.50));track.GradientStops.Add(new GradientStop(Color.FromRgb(235,238,235),0.77));track.GradientStops.Add(new GradientStop(Color.FromRgb(216,222,217),0.92));track.GradientStops.Add(new GradientStop(Color.FromRgb(246,248,246),1));
        dc.DrawEllipse(track,null,new Point(c,c),r,r);
        double p=Math.Max(0,Math.Min(100,Remaining))/100;
        for(int i=0;i<12;i++){double angle=-Math.PI/2+i*Math.PI/6;if(i/12.0>p+0.03){var pos=new Point(c+79*Math.Cos(angle),c+79*Math.Sin(angle));dc.PushTransform(new RotateTransform(angle*180/Math.PI,pos.X,pos.Y));dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(179,188,179)),null,new Rect(pos.X-1.3,pos.Y-1.3,2.6,2.6),0.6,0.6);dc.Pop();}}
        if(p>0){
            Geometry arc;
            if(p>=0.99999)arc=new CombinedGeometry(GeometryCombineMode.Exclude,new EllipseGeometry(new Point(c,c),r,r),new EllipseGeometry(new Point(c,c),inner,inner));
            else{double a=-Math.PI/2+p*Math.PI*2;Point outerEnd=new Point(c+r*Math.Cos(a),c+r*Math.Sin(a)),innerEnd=new Point(c+inner*Math.Cos(a),c+inner*Math.Sin(a));var g=new StreamGeometry();using(var s=g.Open()){s.BeginFigure(new Point(c,c-r),true,true);s.ArcTo(outerEnd,new Size(r,r),0,p>0.5,SweepDirection.Clockwise,true,false);s.LineTo(innerEnd,true,false);s.ArcTo(new Point(c,c-inner),new Size(inner,inner),0,p>0.5,SweepDirection.Counterclockwise,true,false);}g.Freeze();arc=g;}
            Color light,dark;
            if(Remaining>50){light=Color.FromRgb(112,255,103);dark=Color.FromRgb(18,187,64);}
            else if(Remaining>30){light=Color.FromRgb(132,215,255);dark=Color.FromRgb(41,121,228);}
            else if(Remaining>10){light=Color.FromRgb(255,230,109);dark=Color.FromRgb(229,159,25);}
            else{light=Color.FromRgb(255,156,144);dark=Color.FromRgb(231,64,75);}
            var fill=new LinearGradientBrush();fill.StartPoint=new Point(0,0);fill.EndPoint=new Point(0.8,1);fill.GradientStops.Add(new GradientStop(light,0));fill.GradientStops.Add(new GradientStop(dark,0.57));fill.GradientStops.Add(new GradientStop(light,1));
            dc.PushTransform(new TranslateTransform(0,1.6));dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(25,dark.R,dark.G,dark.B)),new Pen(new SolidColorBrush(Color.FromArgb(15,dark.R,dark.G,dark.B)),4),arc);dc.Pop();
            dc.DrawGeometry(fill,new Pen(new SolidColorBrush(Color.FromArgb(120,210,255,209)),1),arc);
            dc.PushClip(arc);var glint=new RadialGradientBrush();glint.Center=new Point(0.5,0.18);glint.GradientOrigin=new Point(0.5,0.18);glint.RadiusX=0.60;glint.RadiusY=0.80;glint.GradientStops.Add(new GradientStop(Color.FromArgb(80,255,255,255),0));glint.GradientStops.Add(new GradientStop(Colors.Transparent,1));dc.DrawRectangle(glint,null,new Rect(c-r,c-r,2*r,2*r));dc.Pop();
        }
        for(int i=12;i>=1;i--)dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(i<5?12:4),33,43,35)),null,new Point(c,c+4),inner+i*0.9,inner+i*0.9);
        var white=new LinearGradientBrush(Colors.White,Color.FromRgb(251,252,251),90);dc.DrawEllipse(white,new Pen(new SolidColorBrush(Color.FromRgb(250,252,250)),1),new Point(c,c),inner,inner);
    }
}

class AuroraShine : FrameworkElement {
    protected override void OnRender(DrawingContext dc){double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h),47,47));
        var top=new RadialGradientBrush();top.GradientStops.Add(new GradientStop(Color.FromArgb(100,102,99,199),0));top.GradientStops.Add(new GradientStop(Colors.Transparent,1));dc.DrawEllipse(top,null,new Point(w*0.46,0),w*0.68,31);
        var side=new RadialGradientBrush();side.GradientStops.Add(new GradientStop(Color.FromArgb(45,146,139,243),0));side.GradientStops.Add(new GradientStop(Colors.Transparent,1));dc.DrawEllipse(side,null,new Point(0,h*0.35),23,h*0.4);
        var foot=new RadialGradientBrush();foot.GradientStops.Add(new GradientStop(Color.FromArgb(55,104,91,189),0));foot.GradientStops.Add(new GradientStop(Colors.Transparent,1));dc.DrawEllipse(foot,null,new Point(w*0.54,h),w*0.55,22);dc.Pop();
    }
}

class RingWindow : Window {
    readonly string store;readonly bool dark;
    readonly string shared=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexUsageFloat","quota-cache.json");
    Border card;RingSurface ring;TextBlock percent,countdown;PerimeterSweep sweep;Forms.NotifyIcon tray;
    DispatcherTimer clock,scanTimer;TaskMonitor monitor;Rpc rpc;object rpcLock=new object();AutoResetEvent refresh=new AutoResetEvent(false);CancellationTokenSource cancel=new CancellationTokenSource();bool closing;int scanBusy;DateTime reset=DateTime.MinValue;DateTime updated;object current;
    public bool IsSweeping {get{return sweep.Active;}}public double SweepPhase {get{return sweep.Phase;}}public bool IsDark {get{return dark;}}
    static SolidColorBrush B(string color){return (SolidColorBrush)new BrushConverter().ConvertFromString(color);}
    public RingWindow(bool preview=false,bool darkStyle=false){
        dark=darkStyle;store=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),dark?"CodexUsageAurora":"CodexUsageRing");
        Title=dark?"Codex 极光圆环":"Codex 用量圆环";Width=130;Height=130;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;FontFamily=new FontFamily("Microsoft YaHei UI");
        var root=new Grid{Width=260,Height=260,LayoutTransform=new ScaleTransform(0.5,0.5)};card=new Border{Background=Brushes.White,BorderBrush=B("#E4E8E6"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(dark?48:20),Margin=new Thickness(16),Opacity=dark?0.98:0.97,Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=dark?Color.FromRgb(10,11,36):Color.FromRgb(21,31,27),BlurRadius=dark?17:12,ShadowDepth=dark?5:4,Opacity=dark?0.36:0.20}};
        if(dark){var body=new LinearGradientBrush();body.StartPoint=new Point(0.15,0);body.EndPoint=new Point(0.85,1);body.GradientStops.Add(new GradientStop(B("#21195A").Color,0));body.GradientStops.Add(new GradientStop(B("#10122C").Color,0.46));body.GradientStops.Add(new GradientStop(B("#090F1C").Color,1));card.Background=body;card.BorderBrush=new LinearGradientBrush(B("#A09ACA").Color,B("#202C3D").Color,90);card.BorderThickness=new Thickness(1.2);}
        var grid=new Grid();if(dark)grid.Children.Add(new AuroraShine());
        var host=new Grid{Width=200,Height=200};ring=new RingSurface{Dark=dark};host.Children.Add(ring);
        var center=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,-2,0,0)};
        var line=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};percent=new TextBlock{Text="—",FontFamily=new FontFamily("Segoe UI"),FontSize=dark?54:39,FontWeight=dark?FontWeights.SemiBold:FontWeights.Bold,Foreground=B(dark?"#F7F6FD":"#101813")};line.Children.Add(percent);line.Children.Add(new TextBlock{Text="%",FontSize=dark?18:13,FontWeight=FontWeights.SemiBold,Foreground=B(dark?"#E0DBEF":"#1C2720"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(1,13,0,0)});center.Children.Add(line);
        countdown=new TextBlock{Text="读取重置时间…",FontSize=dark?16:11,FontWeight=FontWeights.Medium,Foreground=B(dark?"#B8B9D0":"#637369"),Margin=new Thickness(0,3,0,0),HorizontalAlignment=HorizontalAlignment.Center};center.Children.Add(countdown);
        host.Children.Add(center);var scale=new Viewbox{Width=200,Height=200,Child=host,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};grid.Children.Add(scale);card.Child=grid;root.Children.Add(card);
        sweep=new PerimeterSweep{Radius=dark?48:20,Inset=0.6,Margin=card.Margin};root.Children.Add(sweep);Content=root;
        card.MouseLeftButtonDown+=(s,e)=>{try{DragMove();Save();}catch{}};
        var menu=new ContextMenu();ContextMenu=menu;Add(menu,"立即刷新额度",()=>{forceRead=true;refresh.Set();});Add(menu,"测试红色扫光（3秒）",()=>{if(SkinController.Running)SkinController.NotifyActive();else Notify();});SkinController.AddMenu(menu,dark?"aurora":"ring");var pin=new MenuItem{Header="始终置顶",IsCheckable=true,IsChecked=true};pin.Click+=(s,e)=>Topmost=pin.IsChecked;menu.Items.Add(pin);Add(menu,"收起到托盘",()=>Hide());menu.Items.Add(new Separator());Add(menu,"退出悬浮窗",Close);
        Directory.CreateDirectory(store);Restore();
        clock=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};clock.Tick+=(s,e)=>{UpdateCountdown();if(Program.ShowSignal!=null&&Program.ShowSignal.WaitOne(0)){Show();Activate();}};clock.Start();
        Closing+=(s,e)=>{closing=true;Save();clock.Stop();if(scanTimer!=null)scanTimer.Stop();sweep.Stop();cancel.Cancel();refresh.Set();if(tray!=null)tray.Dispose();lock(rpcLock){if(rpc!=null)rpc.Dispose();rpc=null;}};
        if(preview)return;
        tray=new Forms.NotifyIcon{Icon=System.Drawing.SystemIcons.Information,Text="Codex 用量圆环版",Visible=true};tray.DoubleClick+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{Show();Activate();}));var tm=new Forms.ContextMenuStrip();tm.Items.Add("显示圆环版",null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{Show();Activate();})));tm.Items.Add("退出圆环版",null,(s,e)=>Dispatcher.BeginInvoke(new Action(Close)));tray.ContextMenuStrip=tm;
        try{DateTime time;object value;if(ReadCache(out value,out time)){ApplyData(value,DateTime.Now-time>TimeSpan.FromMinutes(3),time);}}catch{}
        monitor=SkinController.SharedMonitor;
        scanTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};scanTimer.Tick+=(s,e)=>Scan();scanTimer.Start();Scan();Task.Run(()=>QuotaLoop());
    }
    volatile bool forceRead;
    void Add(ContextMenu m,string name,Action action){var item=new MenuItem{Header=name};item.Click+=(s,e)=>action();m.Items.Add(item);}
    void Restore(){Left=SystemParameters.WorkArea.Right-Width-350;Top=SystemParameters.WorkArea.Top+80;try{string file=Path.Combine(store,"settings.json");if(File.Exists(file)){var d=Data.Json().DeserializeObject(File.ReadAllText(file));Left=Data.Num(Data.Get(d,"left"),Left);Top=Data.Num(Data.Get(d,"top"),Top);}else{string main=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexUsageFloat","settings.json");if(File.Exists(main)){var d=Data.Json().DeserializeObject(File.ReadAllText(main));Left=Data.Num(Data.Get(d,"left"),Left)-Width-12;Top=Data.Num(Data.Get(d,"top"),Top);}}}catch{}Left=Math.Max(SystemParameters.VirtualScreenLeft,Math.Min(Left,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-Width));Top=Math.Max(SystemParameters.VirtualScreenTop,Math.Min(Top,SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-Height));}
    void Save(){try{Data.Atomic(Path.Combine(store,"settings.json"),Data.Json().Serialize(new{left=Left,top=Top}));}catch{}}
    public static string Countdown(DateTime target,DateTime now){if(target==DateTime.MinValue)return "重置时间暂未提供";double seconds=Math.Ceiling((target-now).TotalSeconds);if(seconds<=0)return "等待额度重置…";if(seconds<3600)return "不足1小时";var t=TimeSpan.FromSeconds(seconds);return (t.Days>0?t.Days+"天 ":"")+t.Hours+"小时";}
    void UpdateCountdown(){countdown.Text=Countdown(reset,DateTime.Now);countdown.ToolTip=reset==DateTime.MinValue?"额度接口未返回重置时间":reset.ToString("yyyy-MM-dd HH:mm:ss")+" 重置（本地时间）";}
    public void ApplyData(object data,bool stale,DateTime time){current=data;updated=time;var rows=Data.Quotas(data);if(rows.Count==0){percent.Text="—";reset=DateTime.MinValue;UpdateCountdown();return;}var q=rows.First();ring.Remaining=q.Remaining;ring.InvalidateVisual();percent.Text=q.Remaining.ToString("0.#")+(stale?"*":"");reset=q.Reset;card.ToolTip=(stale?"缓存额度 · ":"已同步 ")+time.ToString("HH:mm:ss")+"\n"+string.Join("\n",rows.Select(r=>r.Label+"剩余 "+r.Remaining.ToString("0.#")+"%"));UpdateCountdown();}
    public void Notify(double seconds=3){if(!IsVisible)Show();sweep.Start(seconds);}
    void Scan(){if(Interlocked.Exchange(ref scanBusy,1)==1)return;Task.Run(()=>{try{List<Completion> events;lock(monitor)events=monitor.Scan();if(events.Count>0)Dispatcher.BeginInvoke(new Action(()=>{if(SkinController.Running)SkinController.NotifyActive();else if(!closing)Notify();}));}catch{}finally{Interlocked.Exchange(ref scanBusy,0);}});}
    bool ReadCache(out object result,out DateTime time){result=null;time=DateTime.MinValue;if(!File.Exists(shared))return false;var cached=Data.Json().DeserializeObject(File.ReadAllText(shared));if(!DateTime.TryParse(Data.Str(Data.Get(cached,"updatedAt")),out time))return false;result=Data.Get(cached,"result");return result!=null;}
    void QuotaLoop(){while(!cancel.IsCancellationRequested){try{object result;DateTime time;bool forced=forceRead;forceRead=false;if(forced||!ReadCache(out result,out time)||DateTime.Now-time>TimeSpan.FromSeconds(75)){lock(rpcLock){if(closing)return;if(rpc==null)rpc=new Rpc();}result=rpc.Request("account/rateLimits/read");time=DateTime.Now;}object value=result;DateTime stamp=time;Dispatcher.BeginInvoke(new Action(()=>{if(!closing)ApplyData(value,false,stamp);}));}catch{lock(rpcLock){if(rpc!=null)rpc.Dispose();rpc=null;}try{Dispatcher.BeginInvoke(new Action(()=>{if(!closing)ApplyData(current,true,updated);}));}catch{}}if(cancel.IsCancellationRequested)return;refresh.WaitOne(TimeSpan.FromSeconds(60));}}
    public void Capture(string file){UpdateLayout();var bmp=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth*2),(int)Math.Ceiling(ActualHeight*2),192,192,PixelFormats.Pbgra32);bmp.Render(this);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(file))e.Save(f);}
}

static class RingProgram {
    public static EventWaitHandle ShowSignal;static Mutex single;
    [STAThread] public static int Main(string[] args){try{
        if(args.Contains("--compare-preview")){var a=new Application();var capsule=new FloatWindow(true);capsule.LoadPreviewQuota();var ring=new RingWindow(true);ring.ApplyData(capsule.CurrentQuota,false,DateTime.Now);capsule.Show();ring.Show();capsule.UpdateLayout();ring.UpdateLayout();double width=capsule.ActualWidth+ring.ActualWidth+36,height=ring.ActualHeight+55;var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(239,242,245)),null,new Rect(0,0,width,height));var face=new Typeface("Microsoft YaHei UI");dc.DrawText(new FormattedText("A · 动态能量胶囊",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,face,13,Brushes.DimGray,1.0),new Point(18,18));dc.DrawText(new FormattedText("B · 白色立体圆环",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,face,13,Brushes.DimGray,1.0),new Point(capsule.ActualWidth+30,18));dc.DrawRectangle(new VisualBrush(capsule),null,new Rect(0,55+(ring.ActualHeight-capsule.ActualHeight)/2,capsule.ActualWidth,capsule.ActualHeight));dc.DrawRectangle(new VisualBrush(ring),null,new Rect(capsule.ActualWidth+24,55,ring.ActualWidth,ring.ActualHeight));}var bmp=new RenderTargetBitmap((int)(width*2),(int)(height*2),192,192,PixelFormats.Pbgra32);bmp.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(args.Last()))encoder.Save(f);ring.Capture(Path.Combine(Path.GetDirectoryName(args.Last()),"圆环样式预览.png"));capsule.Close();ring.Close();return 0;}
        if(args.Contains("--ring-test")){var checks=new List<string>();DateTime now=DateTime.Now;if(RingWindow.Countdown(DateTime.MinValue,now)!="重置时间暂未提供")throw new Exception("missing reset");checks.Add("unknown reset");if(RingWindow.Countdown(now.AddSeconds(-1),now)!="等待额度重置…")throw new Exception("expired reset");checks.Add("expired reset");if(RingWindow.Countdown(now.AddSeconds(65),now)!="不足1小时")throw new Exception("minute rollover");checks.Add("minute rollover");if(RingWindow.Countdown(now.AddDays(1).AddHours(2).AddMinutes(3).AddSeconds(4),now)!="1天 2小时")throw new Exception("day rollover");checks.Add("day rollover");var app=new Application();var w=new RingWindow(true);using(var rpc=new Rpc())w.ApplyData(rpc.Request("account/rateLimits/read"),false,DateTime.Now);var t=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};int ticks=0;double previous=0;bool moved=false;t.Tick+=(s,e)=>{ticks++;if(ticks==2)w.Notify();if(ticks>2&&ticks<12){if(w.SweepPhase>previous)moved=true;previous=w.SweepPhase;}if(ticks==8)w.Capture(Path.Combine(Path.GetDirectoryName(args.Last()),"ring-red-sweep.png"));if(ticks==21){t.Stop();Data.Atomic(args.Last(),Data.Json().Serialize(new{countdownChecks=checks,redSweepMoving=moved,autoStopped=!w.IsSweeping}));w.Close();}};t.Start();app.Run(w);return 0;}
        ShowSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\CodexUsageRing-Show");bool created;single=new Mutex(true,"Local\\CodexUsageRing-v1",out created);if(!created){ShowSignal.Set();return 0;}var application=new Application();application.Run(new RingWindow());return 0;
    }catch(Exception e){if(args.Length>0)File.WriteAllText(args.Last(),e.ToString());else MessageBox.Show(e.Message,"Codex 圆环版");return 1;}}
}
}
