using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Markup;
using Forms = System.Windows.Forms;

namespace CodexMeter {
sealed class QuotaColors {
    public readonly Color Start,Body,Tip;public readonly string Name;
    QuotaColors(string name,string start,string body,string tip){Name=name;Start=(Color)ColorConverter.ConvertFromString(start);Body=(Color)ColorConverter.ConvertFromString(body);Tip=(Color)ColorConverter.ConvertFromString(tip);}
    static readonly QuotaColors[] stages={
        new QuotaColors("翠绿","#08260F","#0F9D2A","#70F77D"),
        new QuotaColors("蓝色","#09173A","#004DD2","#31B1FF"),
        new QuotaColors("紫色","#1B0F34","#5727AE","#B688FF"),
        new QuotaColors("金黄","#312507","#AF900D","#FFED73"),
        new QuotaColors("橙黄","#412007","#DB7900","#FFDA53"),
        new QuotaColors("红橙","#3B0E1C","#C51F3E","#FF8153")
    };
    public static QuotaColors For(double remaining){return stages[remaining>80?0:remaining>60?1:remaining>40?2:remaining>20?3:remaining>10?4:5];}
}
static class Data {
    public static Dictionary<string,object> Obj(object x) { return x as Dictionary<string,object> ?? new Dictionary<string,object>(); }
    public static object Get(object x,string k) { object v; return Obj(x).TryGetValue(k,out v)?v:null; }
    public static string Str(object x) { return x == null ? "" : Convert.ToString(x,System.Globalization.CultureInfo.InvariantCulture); }
    public static double Num(object x,double fallback) { double v; return double.TryParse(Str(x),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out v)&&!double.IsInfinity(v)&&!double.IsNaN(v)?v:fallback; }
    public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength=32000000, RecursionLimit=120 }; }
    public static string Stage(double n) { return QuotaColors.For(n).Body.ToString(); }
    public static string StageName(double n) { return QuotaColors.For(n).Name; }
    public static DateTime Epoch(object x) { double s=Num(x,0); try{return new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(s).ToLocalTime();}catch{return DateTime.MinValue;} }
    public static void Atomic(string path,string value) { string tmp=path+".tmp"; File.WriteAllText(tmp,value,new UTF8Encoding(false)); if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path); }
    public static List<Quota> Quotas(object result) {
        var rows=new List<Quota>(); var buckets=Obj(Get(result,"rateLimitsByLimitId"));
        if(buckets.Count==0 && Get(result,"rateLimits")!=null) buckets["codex"]=Get(result,"rateLimits");
        foreach(var kv in buckets.OrderBy(x=>x.Key=="codex"?0:1)) {
            object bucket=kv.Value; string label=Str(Get(bucket,"limitName"));
            if(label=="") label=kv.Key=="codex"?"":kv.Key;
            foreach(string key in new[]{"primary","secondary"}) {
                object w=Get(bucket,key); double used=Num(Get(w,"usedPercent"),-1); if(w==null||used<0)continue;
                double mins=Num(Get(w,"windowDurationMins"),0);
                string period=mins==10080?"周额度":mins==300?"5 小时额度":mins>=1440?(mins/1440).ToString("0.#")+" 天额度":mins>0?(mins/60).ToString("0.#")+" 小时额度":"额度";
                rows.Add(new Quota { Label=(label==""?"":label+" · ")+period,Remaining=Math.Max(0,Math.Min(100,100-used)),Reset=Epoch(Get(w,"resetsAt")) });
            }
            object monthly=Get(bucket,"individualLimit");double left=Num(Get(monthly,"remainingPercent"),-1);
            if(left>=0)rows.Add(new Quota{Label=(label==""?"":label+" · ")+"月额度",Remaining=Math.Max(0,Math.Min(100,left)),Reset=Epoch(Get(monthly,"resetsAt"))});
        }
        return rows;
    }
}
class Quota { public string Label; public double Remaining; public DateTime Reset; }

sealed class Rpc : IDisposable {
    Process proc; int seq; public static string FindExe() {
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string root=System.IO.Path.Combine(local,"OpenAI","Codex","bin");
        if(Directory.Exists(root)) { var f=Directory.GetFiles(root,"codex.exe",SearchOption.AllDirectories).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault(); if(f!=null)return f; }
        foreach(string dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(';')) {try{string f=System.IO.Path.Combine(dir,"codex.exe"); if(File.Exists(f))return f;}catch{}}
        throw new Exception("未找到 Codex，请先安装并登录 Codex 桌面应用。");
    }
    public Rpc() {
        var si=new ProcessStartInfo(FindExe(),"app-server --stdio") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        proc=Process.Start(si);proc.ErrorDataReceived+=(s,e)=>{};proc.BeginErrorReadLine();
        try {Request("initialize",new {clientInfo=new {name="codex_usage_float",title="Codex Usage Float",version="1.0.0"}});
        proc.StandardInput.WriteLine("{\"method\":\"initialized\"}");proc.StandardInput.Flush();}catch{Dispose();throw;}
    }
    public object Request(string method,object parameters=null) {
        int id=++seq;var req=new Dictionary<string,object>{{"method",method},{"id",id}}; if(parameters!=null)req["params"]=parameters;
        proc.StandardInput.WriteLine(Data.Json().Serialize(req));proc.StandardInput.Flush();
        var read=Task.Run<object>(()=>{string line;while((line=proc.StandardOutput.ReadLine())!=null) {
            object msg;try{msg=Data.Json().DeserializeObject(line);}catch{continue;}
            if(Data.Num(Data.Get(msg,"id"),-1)!=id)continue;
            if(Data.Get(msg,"error")!=null)throw new Exception(Data.Str(Data.Get(Data.Get(msg,"error"),"message")));
            return Data.Get(msg,"result");
        }throw new Exception("Codex 数据连接已关闭。");});
        if(!read.Wait(TimeSpan.FromSeconds(25))) {Dispose();throw new Exception("读取超时，请检查 Codex 登录和网络。");}
        return read.Result;
    }
    public void Dispose() {try{if(proc!=null&&!proc.HasExited)proc.Kill();}catch{} if(proc!=null)proc.Dispose();proc=null;}
}

class LogCursor {public long Position; public string Pending=""; public string Id; public bool Subagent;}
class Completion {public string Id,Turn,Title;}
sealed class TaskMonitor {
    readonly string home; readonly DateTime started=DateTime.UtcNow;
    readonly Dictionary<string,LogCursor> cursors=new Dictionary<string,LogCursor>();
    readonly Dictionary<string,string> names=new Dictionary<string,string>();
    readonly HashSet<string> seen=new HashSet<string>();
    public int Watched {get{return cursors.Count;}}
    public TaskMonitor(string root) {home=root;}
    public List<Completion> Scan() {
        var completed=new List<Completion>(); string index=System.IO.Path.Combine(home,"session_index.jsonl");
        if(File.Exists(index))try {using(var f=new FileStream(index,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var r=new StreamReader(f,Encoding.UTF8)) {
            if(f.Length>2000000){f.Seek(-2000000,SeekOrigin.End);r.ReadLine();}
            string l;while((l=r.ReadLine())!=null)try{var d=Data.Json().DeserializeObject(l);names[Data.Str(Data.Get(d,"id"))]=Data.Str(Data.Get(d,"thread_name"));}catch{}
        }}catch{}
        string sessions=System.IO.Path.Combine(home,"sessions"); if(!Directory.Exists(sessions))return completed;
        foreach(string file in Directory.EnumerateFiles(sessions,"*.jsonl",SearchOption.AllDirectories)) {
            try {
                if(File.GetLastWriteTimeUtc(file)<started.AddDays(-7)&&!cursors.ContainsKey(file))continue;
                LogCursor cur;bool fresh=!cursors.TryGetValue(file,out cur);
                if(fresh) { cur=new LogCursor();string name=System.IO.Path.GetFileNameWithoutExtension(file);cur.Id=name.Length>=36?name.Substring(name.Length-36):name;
                    using(var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var r=new StreamReader(f,Encoding.UTF8)) {
                        string first=r.ReadLine();if(first!=null){object meta=Data.Get(Data.Json().DeserializeObject(first),"payload");object source=Data.Get(meta,"source");cur.Subagent=Data.Get(source,"subagent")!=null;}
                    }
                    cursors[file]=cur;
                }
                if(cur.Subagent)continue;
                using(var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                    if(f.Length<cur.Position){cur.Position=0;cur.Pending="";}
                    bool skipFirst=false;if(fresh&&f.Length>2000000){cur.Position=f.Length-2000000;skipFirst=true;}
                    f.Seek(cur.Position,SeekOrigin.Begin);
                    // Keep an incomplete UTF-8 line as bytes by advancing only through complete newlines.
                    using(var ms=new MemoryStream()) {
                        byte[] buf=new byte[65536];int n;while((n=f.Read(buf,0,buf.Length))>0)ms.Write(buf,0,n);
                        byte[] bytes=ms.ToArray();int end=Array.LastIndexOf(bytes,(byte)10);
                        if(end<0)continue;
                        string content=Encoding.UTF8.GetString(bytes,0,end+1);cur.Position+=end+1;
                        string[] lines=content.Split('\n');for(int i=skipFirst?1:0;i<lines.Length;i++) {
                            if(lines[i].IndexOf("\"task_complete\"",StringComparison.Ordinal)<0)continue;
                            try { object d=Data.Json().DeserializeObject(lines[i]);var c=Parse(d,cur.Id,started,seen);if(c!=null){string title; c.Title=names.TryGetValue(cur.Id,out title)?title:"任务 "+cur.Id.Substring(0,8);completed.Add(c);} }catch{}
                        }
                    }
                }
            }catch(IOException){}catch(UnauthorizedAccessException){}catch{}
        }
        return completed;
    }
    public static Completion Parse(object d,string id,DateTime since,HashSet<string> seen) {
        if(Data.Str(Data.Get(d,"type"))!="event_msg")return null;
        object e=Data.Get(d,"payload");if(Data.Str(Data.Get(e,"type"))!="task_complete"||Data.Get(e,"error")!=null)return null;
        string status=Data.Str(Data.Get(e,"status")); if(status!=""&&status!="completed")return null;
        string turn=Data.Str(Data.Get(e,"turn_id")),root=Data.Str(Data.Get(e,"root_turn_id")); if(root!=""&&root!=turn)return null;
        DateTime time;if(!DateTime.TryParse(Data.Str(Data.Get(d,"timestamp")),null,System.Globalization.DateTimeStyles.RoundtripKind,out time)||time.ToUniversalTime()<since)return null;
        if(turn=="")return null;string key=id+":"+turn;if(!seen.Add(key))return null;
        return new Completion{Id=id,Turn=turn};
    }
}

class EnergySurface : FrameworkElement {
    public double Remaining=0,DisplayedRemaining=double.NaN;public Color Tint;public double Intensity=0.8;
    static readonly Brush particleGlow=Frozen(new RadialGradientBrush(Color.FromArgb(70,153,225,255),Colors.Transparent));
    static readonly Brush particleCore=Frozen(new SolidColorBrush(Color.FromRgb(216,247,255)));
    static Brush Frozen(Brush brush){brush.Freeze();return brush;}
    readonly Stopwatch elapsed=new Stopwatch();readonly DispatcherTimer animator;double lastTime;
    public EnergySurface(){
        animator=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(33)};
        animator.Tick+=(s,e)=>{if(!IsVisible)return;double now=elapsed.Elapsed.TotalSeconds,dt=Math.Min(0.1,Math.Max(0,now-lastTime));lastTime=now;
            if(double.IsNaN(DisplayedRemaining))DisplayedRemaining=Remaining;else DisplayedRemaining+=(Remaining-DisplayedRemaining)*(1-Math.Exp(-dt*8));InvalidateVisual();};
        Loaded+=(s,e)=>Resume();Unloaded+=(s,e)=>{animator.Stop();elapsed.Stop();};IsVisibleChanged+=(s,e)=>{if(IsVisible)Resume();else{animator.Stop();elapsed.Stop();}};
    }
    void Resume(){if(!IsLoaded||!IsVisible)return;elapsed.Start();lastTime=elapsed.Elapsed.TotalSeconds;animator.Start();}
    static Color Alpha(Color c,byte a){return Color.FromArgb(a,c.R,c.G,c.B);}
    double Edge(double y,double t,double x){return x+Math.Sin(y*0.14+t*2.4)*2.4+Math.Sin(y*0.39-t*3.6)*0.9+Math.Sin(y*0.072-t*1.7)*1.4;}
    protected override void OnRender(DrawingContext dc){
        double w=ActualWidth,h=ActualHeight;if(w<=0||h<=0)return;
        double t=elapsed.Elapsed.TotalSeconds,p=double.IsNaN(DisplayedRemaining)?Remaining:DisplayedRemaining,x=w*Math.Max(0,Math.Min(100,p))/100;
        var palette=QuotaColors.For(Remaining);Color start=palette.Start,body=palette.Body,tip=palette.Tip;
        dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h),16,16));dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20,23,27)),null,new Rect(0,0,w,h));
        if(x>0.01){
            const int samples=49;var pts=new Point[samples];for(int i=0;i<samples;i++){double y=h*i/(samples-1);pts[i]=new Point(Edge(y,t,x),y);}
            var region=new StreamGeometry();using(var g=region.Open()){g.BeginFigure(new Point(0,0),true,true);g.LineTo(pts[0],true,false);for(int i=1;i<samples;i++)g.LineTo(pts[i],true,false);g.LineTo(new Point(0,h),true,false);}region.Freeze();
            var fill=new LinearGradientBrush();fill.StartPoint=new Point(0,0);fill.EndPoint=new Point(1,0);
            // A continuous quadratic color curve avoids a visible change in slope at the middle stop.
            for(int i=0;i<=16;i++){double u=i/16.0,v=1-u;var color=Color.FromRgb((byte)Math.Round(v*v*start.R+2*v*u*body.R+u*u*tip.R),(byte)Math.Round(v*v*start.G+2*v*u*body.G+u*u*tip.G),(byte)Math.Round(v*v*start.B+2*v*u*body.B+u*u*tip.B));fill.GradientStops.Add(new GradientStop(color,u));}
            dc.DrawGeometry(fill,null,region);
            dc.PushClip(region);
            dc.PushClip(new RectangleGeometry(new Rect(x*0.55,0,x*0.45+8,h)));
            for(int i=0;i<5;i++){double cy=h*(0.1+0.2*i)+Math.Sin(t*0.8+i)*6;var glow=new RadialGradientBrush();glow.GradientStops.Add(new GradientStop(Alpha(tip,35),0));glow.GradientStops.Add(new GradientStop(Alpha(tip,0),1));dc.DrawEllipse(glow,null,new Point(x-13+Math.Sin(t+i)*4,cy),24,15);}
            dc.Pop();
            // Stable, gently drifting stars span the whole fill; its clip keeps the spent area clear.
            const int particleColumns=8,particleRows=4;
            for(int row=0;row<particleRows;row++)for(int col=0;col<particleColumns;col++){
                int i=row*particleColumns+col;double seed=i*0.61803398875;
                double u=(col+0.20+((i*0.41421356237)%1)*0.60+Math.Sin(t*0.35+i*1.7)*0.10)/particleColumns;
                double v=(row+0.22+((i*0.73205080757)%1)*0.56+Math.Cos(t*0.27+i*2.1)*0.10)/particleRows;
                Point position=new Point(u*w,v*h);double life=(t*0.18+seed)%1,twinkle=Math.Sin(life*Math.PI),radius=0.55+(i%5)*0.12;
                dc.PushOpacity(0.30+0.45*twinkle*twinkle);dc.DrawEllipse(particleGlow,null,position,radius*3.2,radius*3.2);dc.DrawEllipse(particleCore,null,position,radius,radius);dc.Pop();
            }
            dc.Pop();
            var edge=new StreamGeometry();using(var g=edge.Open()){g.BeginFigure(pts[0],false,false);for(int i=1;i<samples;i++)g.LineTo(pts[i],true,false);}edge.Freeze();
            double pulse=(0.93+0.07*Math.Sin(t*2.1))*Intensity;
            for(int i=4;i>=1;i--){var pen=new Pen(new SolidColorBrush(Alpha(tip,(byte)Math.Min(255,(11+7*(5-i))*pulse))),i*2.7*Math.Min(1,x/24));pen.LineJoin=PenLineJoin.Round;dc.DrawGeometry(null,pen,edge);}
            dc.DrawGeometry(null,new Pen(new SolidColorBrush(Alpha(tip,(byte)(220*pulse))),2.3),edge);
            dc.DrawGeometry(null,new Pen(new SolidColorBrush(Alpha(Color.FromRgb(227,250,255),(byte)(255*pulse))),0.9),edge);
        }
        var sheen=new LinearGradientBrush(Color.FromArgb(9,255,255,255),Colors.Transparent,90);dc.DrawRectangle(sheen,null,new Rect(0,0,w,h));dc.Pop();
    }
}


class UniformRimBorder : Border {
    protected override void OnRender(DrawingContext dc){
        double w=ActualWidth,h=ActualHeight,t=BorderThickness.Left,r=CornerRadius.TopLeft;if(w<=0||h<=0)return;
        var outside=new RectangleGeometry(new Rect(0,0,w,h),r,r);
        dc.DrawGeometry(Brushes.Transparent,null,outside);
        if(w>2*t&&h>2*t){var inside=new RectangleGeometry(new Rect(t,t,w-2*t,h-2*t),Math.Max(0,r-t),Math.Max(0,r-t));var ring=new CombinedGeometry(GeometryCombineMode.Exclude,outside,inside);dc.DrawGeometry(BorderBrush,null,ring);}
    }
}

class FrameGlow : FrameworkElement {
    public double Radius=21,Inset=2.5;readonly Stopwatch elapsed=new Stopwatch();readonly DispatcherTimer timer;double duration=3;
    public bool Active {get;private set;}
    public double Phase {get{return elapsed.Elapsed.TotalSeconds/duration;}}
    public FrameGlow(){IsHitTestVisible=false;timer=new DispatcherTimer(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(25)};timer.Tick+=(s,e)=>{if(elapsed.Elapsed.TotalSeconds>=duration)Stop();else InvalidateVisual();};Unloaded+=(s,e)=>Stop();}
    public void Start(double seconds=3){duration=seconds;Active=true;elapsed.Restart();timer.Start();InvalidateVisual();}
    public void Stop(){Active=false;timer.Stop();elapsed.Stop();InvalidateVisual();}
    protected override void OnRender(DrawingContext dc){
        if(!Active||ActualWidth<12||ActualHeight<12)return;
        double time=elapsed.Elapsed.TotalSeconds;
        double fade=Math.Min(1,time/0.18)*Math.Min(1,Math.Max(0,(duration-time)/0.45));
        double strength=fade*(0.92+0.08*Math.Sin(time*5.4));
        var bounds=new Rect(0,0,ActualWidth,ActualHeight);double r=Math.Min(Radius,Math.Min(ActualWidth,ActualHeight)/2);
        dc.PushOpacity(strength);
        var outline=new Rect(Inset,Inset,ActualWidth-2*Inset,ActualHeight-2*Inset);
        for(int i=6;i>=1;i--){var glow=new SolidColorBrush(Color.FromArgb((byte)(7+(6-i)*3),255,32,52));dc.DrawRoundedRectangle(null,new Pen(glow,i*2.5),outline,Math.Max(1,r-Inset),Math.Max(1,r-Inset));}
        var wash=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(0.15,1)};
        wash.GradientStops.Add(new GradientStop(Color.FromArgb(134,255,46,70),0));
        wash.GradientStops.Add(new GradientStop(Color.FromArgb(108,255,24,53),0.48));
        wash.GradientStops.Add(new GradientStop(Color.FromArgb(144,255,42,64),1));
        dc.DrawRoundedRectangle(wash,null,bounds,r,r);
        dc.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromArgb(210,255,87,99)),1.6),outline,Math.Max(1,r-Inset),Math.Max(1,r-Inset));
        dc.Pop();
    }
}


class FloatWindow : Window {
    readonly string store=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexUsageFloat");
    readonly string codexHome=Environment.GetEnvironmentVariable("CODEX_HOME")??System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
    Border frame;StackPanel quotaPanel;Ellipse lamp;Forms.NotifyIcon tray;FrameGlow sweep;
    DispatcherTimer clock,scanTimer,autoStop;int unread,scanBusy;bool closing;DateTime updated;object latestResult;
    TaskMonitor monitor;AutoResetEvent refresh=new AutoResetEvent(false);CancellationTokenSource cancel=new CancellationTokenSource();Rpc rpc;readonly object rpcGate=new object();
    public bool IsFlashing {get{return sweep!=null&&sweep.Active;}}
    public Border Frame {get{return frame;}}
    public FrameGlow Glow {get{return sweep;}}
    public object CurrentQuota {get{return latestResult;}}
    static SolidColorBrush Brush(string color){return (SolidColorBrush)new BrushConverter().ConvertFromString(color);}
    static LinearGradientBrush WhiteRim(bool alert){var b=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(0.08,1)};string[] colors=alert?new[]{"#FFFFFF","#F3FFF9","#E0F7EA","#B5DACC","#FFFFFF","#E1F3EA","#9CBFB0"}:new[]{"#FFFFFF","#FFFFFF","#F0F3F7","#C5CFD9","#FFFFFF","#E5EBF0","#9CA9B7"};double[] offsets={0,0.18,0.40,0.54,0.66,0.84,1};for(int i=0;i<colors.Length;i++)b.GradientStops.Add(new GradientStop(Brush(colors[i]).Color,offsets[i]));return b;}
    readonly bool previewMode;
    public FloatWindow(bool preview=false){
        previewMode=preview;
        Title="Codex 用量胶囊";Width=298;Height=85.5;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;FontFamily=new FontFamily("Microsoft YaHei UI");
        frame=(Border)XamlReader.Parse(@"<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='Transparent' Opacity='0.92' BorderThickness='5' CornerRadius='21' Margin='16,9,16,15'>
          <Border.Effect><DropShadowEffect Color='#07111D' BlurRadius='16' ShadowDepth='5' Direction='270' Opacity='0.62'/></Border.Effect>
          <Grid Background='Transparent'>
            <StackPanel x:Name='Quotas' Orientation='Horizontal'/>
            <Ellipse x:Name='Lamp' Width='3' Height='3' Fill='#485466' Opacity='0' HorizontalAlignment='Right' VerticalAlignment='Top' Margin='0,9,10,0'/>
          </Grid></Border>");
        Border parsed=frame;UIElement body=parsed.Child;parsed.Child=null;
        frame=new UniformRimBorder{Child=body,Background=parsed.Background,Opacity=parsed.Opacity,BorderThickness=parsed.BorderThickness,CornerRadius=parsed.CornerRadius,Margin=parsed.Margin,Effect=parsed.Effect,BorderBrush=WhiteRim(false)};
        NameScope.SetNameScope(frame,NameScope.GetNameScope(parsed));var root=new Grid();root.Children.Add(frame);sweep=new FrameGlow{Radius=21,Margin=frame.Margin};root.Children.Add(sweep);Content=root;quotaPanel=(StackPanel)frame.FindName("Quotas");lamp=(Ellipse)frame.FindName("Lamp");
        frame.MouseLeftButtonDown+=(s,e)=>{try{DragMove();Save();}catch{}};
        var menu=new ContextMenu();ContextMenu=menu;
        Add(menu,"立即刷新额度",()=>refresh.Set());Add(menu,"测试整框红光（3秒）",()=>{if(SkinController.Running)SkinController.NotifyActive();else Notify("红光演示",1);});SkinController.AddMenu(menu,"capsule");
        var pin=new MenuItem{Header="始终置顶",IsCheckable=true,IsChecked=true};pin.Click+=(s,e)=>Topmost=pin.IsChecked;menu.Items.Add(pin);
        Add(menu,"收起到托盘",()=>Hide());menu.Items.Add(new Separator());Add(menu,"退出",()=>Close());
        Directory.CreateDirectory(store);Restore();RenderQuota(null,false);
        autoStop=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};autoStop.Tick+=(s,e)=>Ack();
        Closing+=(s,e)=>{closing=true;Save();autoStop.Stop();if(clock!=null)clock.Stop();if(scanTimer!=null)scanTimer.Stop();cancel.Cancel();refresh.Set();if(tray!=null)tray.Dispose();lock(rpcGate){if(rpc!=null)rpc.Dispose();rpc=null;}};
        if(preview)return;
        tray=new Forms.NotifyIcon{Icon=System.Drawing.SystemIcons.Information,Text="Codex 用量胶囊",Visible=true};tray.DoubleClick+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{Show();Activate();}));
        var trayMenu=new Forms.ContextMenuStrip();trayMenu.Items.Add("显示用量胶囊",null,(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{Show();Activate();})));trayMenu.Items.Add("退出",null,(s,e)=>Dispatcher.BeginInvoke(new Action(Close)));tray.ContextMenuStrip=trayMenu;
        try{string cache=System.IO.Path.Combine(store,"quota-cache.json");if(File.Exists(cache)){object c=Data.Json().DeserializeObject(File.ReadAllText(cache));DateTime.TryParse(Data.Str(Data.Get(c,"updatedAt")),out updated);latestResult=Data.Get(c,"result");RenderQuota(latestResult,true);}}catch{}
        monitor=SkinController.SharedMonitor;Task.Run(()=>QuotaLoop());
        scanTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};scanTimer.Tick+=(s,e)=>ScanTasks();scanTimer.Start();ScanTasks();
        clock=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};clock.Tick+=(s,e)=>{
            if(Program.ShowSignal!=null&&Program.ShowSignal.WaitOne(0)){Show();Activate();}

            if(updated!=DateTime.MinValue&&DateTime.Now-updated>TimeSpan.FromMinutes(3)&&!quotaPanel.Tag.Equals(true))RenderQuota(latestResult,true);
        };clock.Start();
    }
    void Add(ContextMenu m,string title,Action action){var item=new MenuItem{Header=title};item.Click+=(s,e)=>action();m.Items.Add(item);}
    void Restore(){Left=SystemParameters.WorkArea.Right-Width-26;Top=SystemParameters.WorkArea.Top+80;try{string file=System.IO.Path.Combine(store,"settings.json");if(File.Exists(file)){object d=Data.Json().DeserializeObject(File.ReadAllText(file));Left=Data.Num(Data.Get(d,"left"),Left);Top=Data.Num(Data.Get(d,"top"),Top);}}catch{}Left=Math.Max(SystemParameters.VirtualScreenLeft,Math.Min(Left,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-Width));Top=Math.Max(SystemParameters.VirtualScreenTop,Math.Min(Top,SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-Height));}
    void Save(){if(previewMode)return;try{Data.Atomic(System.IO.Path.Combine(store,"settings.json"),Data.Json().Serialize(new{left=Left,top=Top}));}catch{}}
    void SetEnergy(double intensity){foreach(Grid g in quotaPanel.Children.OfType<Grid>())foreach(EnergySurface e in g.Children.OfType<EnergySurface>()){e.Intensity=intensity;e.InvalidateVisual();}}
    public void Notify(string title,int count){unread+=count;ResumeAlert(3);}
    public void ResumeAlert(double seconds){lamp.Fill=Brush("#FF5D55");autoStop.Stop();autoStop.Interval=TimeSpan.FromSeconds(seconds);autoStop.Start();if(!IsVisible)Show();sweep.Start(seconds);}
    void Ack(){autoStop.Stop();unread=0;sweep.Stop();lamp.Fill=Brush("#485466");lamp.Effect=null;frame.BorderBrush=WhiteRim(false);SetEnergy(0.8);}
    void ScanTasks(){if(Interlocked.Exchange(ref scanBusy,1)==1)return;Task.Run(()=>{try{List<Completion> events;lock(monitor)events=monitor.Scan();Dispatcher.BeginInvoke(new Action(()=>{if(events.Count>0){if(SkinController.Running)SkinController.NotifyActive();else if(!closing)Notify(events.Last().Title,events.Count);if(!closing)refresh.Set();}}));}catch{}finally{Interlocked.Exchange(ref scanBusy,0);}});}
    void QuotaLoop(){while(!cancel.IsCancellationRequested){try{
        Rpc r;lock(rpcGate){if(closing)return;if(rpc==null)rpc=new Rpc();r=rpc;}
        object result=r.Request("account/rateLimits/read");DateTime now=DateTime.Now;
        var safe=new Dictionary<string,object>();safe["rateLimits"]=Data.Get(result,"rateLimits");safe["rateLimitsByLimitId"]=Data.Get(result,"rateLimitsByLimitId");
        Data.Atomic(System.IO.Path.Combine(store,"quota-cache.json"),Data.Json().Serialize(new{updatedAt=now.ToString("o"),result=safe}));
        Dispatcher.BeginInvoke(new Action(()=>{if(closing)return;updated=now;latestResult=safe;RenderQuota(safe,false);}));
    }catch(Exception ex){lock(rpcGate){if(rpc!=null)rpc.Dispose();rpc=null;}string reason=ex.GetBaseException().Message;try{Dispatcher.BeginInvoke(new Action(()=>{if(closing)return;RenderQuota(latestResult,true);frame.ToolTip="读取失败，将自动重试。\n"+reason+(latestResult==null?"":"\n星号表示缓存额度。");}));}catch{}}
    if(cancel.IsCancellationRequested)return;refresh.WaitOne(TimeSpan.FromSeconds(60));}}
    public void RenderQuota(object result,bool stale){var previous=quotaPanel.Children.OfType<Grid>().Where(g=>g.Tag is string).ToDictionary(g=>(string)g.Tag,g=>g.Children.OfType<EnergySurface>().First().DisplayedRemaining);quotaPanel.Children.Clear();quotaPanel.Tag=stale;var rows=Data.Quotas(result);
        if(rows.Count==0){Width=298;quotaPanel.Children.Add(new TextBlock{Text="CODEX      —",FontSize=17,Foreground=Brush("#CED5E0"),Margin=new Thickness(18,0,0,0),VerticalAlignment=VerticalAlignment.Center});frame.ToolTip=stale?"额度暂不可用，将自动重试":"正在读取真实额度…";return;}
        Width=42+rows.Count*256;
        foreach(var row in rows){
            var group=new Grid{Width=256,Tag=row.Label};var surface=new EnergySurface{Remaining=row.Remaining,DisplayedRemaining=previous.ContainsKey(row.Label)?previous[row.Label]:row.Remaining,Tint=QuotaColors.For(row.Remaining).Body,Intensity=unread>0?1:0.8};group.Children.Add(surface);
            var title=new StackPanel{Margin=new Thickness(16,0,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center};
            var shadow=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=4,ShadowDepth=0,Color=Colors.Black,Opacity=0.9};
            title.Children.Add(new TextBlock{Text="CODEX",FontFamily=new FontFamily("Segoe UI"),FontWeight=FontWeights.ExtraBold,FontSize=17,Foreground=Brush("#F0F4FA"),Effect=shadow});
            title.Children.Add(new TextBlock{Text=row.Label+" · 剩余额度",FontSize=11,FontWeight=FontWeights.Medium,Foreground=Brush("#C6CFDA"),Margin=new Thickness(0,3,0,0),Effect=shadow});group.Children.Add(title);
            var number=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,15,0),Effect=shadow};
            number.Children.Add(new TextBlock{Text=row.Remaining.ToString("0.#"),FontFamily=new FontFamily("Segoe UI"),FontSize=34,FontWeight=FontWeights.Bold,Foreground=Brush("#F1F4F9")});
            number.Children.Add(new TextBlock{Text="%"+(stale?"*":""),FontFamily=new FontFamily("Segoe UI"),FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=Brush("#E1E7EF"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(1,8,0,0)});group.Children.Add(number);quotaPanel.Children.Add(group);
        }
        frame.ToolTip=string.Join("\n",rows.Select(r=>r.Label+"剩余 "+r.Remaining.ToString("0.#")+"%"+(r.Reset==DateTime.MinValue?"":" · "+r.Reset.ToString("M/d HH:mm")+" 重置")))+"\n"+(stale?"* 缓存额度 · ":"已同步 ")+updated.ToString("HH:mm:ss")+"\n拖动移动 · 右键操作 · 完成整框红光3秒";
    }
    public void SetPreviewQuota(object result){latestResult=result;updated=DateTime.Now;RenderQuota(result,false);}
    public void LoadPreviewQuota(){using(var r=new Rpc()){latestResult=r.Request("account/rateLimits/read");}updated=DateTime.Now;RenderQuota(latestResult,false);}
    public void Preview(string file){LoadPreviewQuota();Show();Capture(file);CaptureScene(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file),"悬浮效果预览.png"));}
    public void CaptureScene(string file){UpdateLayout();var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brush("#E7EBF1"),null,new Rect(0,0,ActualWidth,ActualHeight));dc.DrawRectangle(new VisualBrush(this),null,new Rect(0,0,ActualWidth,ActualHeight));}var bmp=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth*2),(int)Math.Ceiling(ActualHeight*2),192,192,PixelFormats.Pbgra32);bmp.Render(visual);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(file))enc.Save(f);}
    public void Capture(string file){UpdateLayout();var bmp=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth*2),(int)Math.Ceiling(ActualHeight*2),192,192,PixelFormats.Pbgra32);bmp.Render(this);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(file))enc.Save(f);}
}


static class Program {
    static Mutex instance; public static EventWaitHandle ShowSignal;
    [STAThread] public static int Main(string[] args) {
        try{
            if(args.Contains("--compare-preview")||args.Contains("--ring-test"))return RingProgram.Main(args);
            if(args.Contains("--skin-test"))return SkinController.Test(args.Last());
            if(args.Contains("--stages-preview"))return RenderPreviews.Stages(args.Last());
            if(args.Contains("--all-skins-stages-preview"))return RenderPreviews.AllSkinsStages(args.Last());
            if(args.Contains("--alert-preview"))return RenderPreviews.Alert(args.Last());
            if(args.Contains("--skin-preview")||args.Contains("--demo-preview"))return SkinController.Preview(args.Last(),args.Contains("--demo-preview"));
            if(args.Contains("--self-test")){SelfTest(args.Last());return 0;}
            if(args.Contains("--diagnose")){using(var rpc=new Rpc()){object r=rpc.Request("account/rateLimits/read");var qs=Data.Quotas(r);Data.Atomic(args.Last(),Data.Json().Serialize(new{success=true,quotas=qs.Select(x=>new{label=x.Label,remaining=x.Remaining,reset=x.Reset.ToString("o")}).ToArray()}));}return 0;}
            if(args.Contains("--preview")){var a=new Application();var w=new FloatWindow(true);w.Preview(args.Last());w.Close();return 0;}
            if(args.Contains("--motion-preview")){string folder=args.Last();Directory.CreateDirectory(folder);var a=new Application();var w=new FloatWindow(true);w.LoadPreviewQuota();int frame=0;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};timer.Tick+=(s,e)=>{w.CaptureScene(System.IO.Path.Combine(folder,"frame-"+(frame++).ToString("000")+".png"));if(frame>=75){timer.Stop();w.Close();}};timer.Start();a.Run(w);return 0;}
            if(args.Contains("--smoke-test")){var a=new Application();var w=new FloatWindow();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(8)};timer.Tick+=(s,e)=>{timer.Stop();w.Capture(args.Last());w.Close();};timer.Start();a.Run(w);return 0;}
            if(args.Contains("--ui-test")){var a=new Application();var w=new FloatWindow();int ticks=0;var colors=new HashSet<string>();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};var duration=new Stopwatch();double stoppedAt=0;timer.Tick+=(s,e)=>{
                ticks++;var f=w.Frame;var light=(Ellipse)f.FindName("Lamp");if(ticks==2){w.Notify("闪灯联动检查",1);duration.Start();}if(ticks>2&&ticks<16)colors.Add(Math.Round(w.Glow.Phase,1).ToString());if(ticks>2&&!w.IsFlashing&&stoppedAt==0)stoppedAt=duration.Elapsed.TotalSeconds;
                if(ticks==8)w.CaptureScene(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(args.Last()),"capsule-red-sweep.png"));if(ticks==21){timer.Stop();bool stopped=!w.IsFlashing&&light.Fill.ToString()=="#FF485466";Data.Atomic(args.Last(),Data.Json().Serialize(new{frameGlowAnimating=colors.Count>=2,autoStopped=stopped,stopObservedSeconds=stoppedAt,width=w.ActualWidth,height=w.ActualHeight,observedColors=colors.ToArray()}));w.Close();}};timer.Start();a.Run(w);return 0;}
            ShowSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\CodexUsageFloat-Show-v1");
            bool created;instance=new Mutex(true,"Local\\CodexUsageFloat-v1",out created);if(!created){ShowSignal.Set();return 0;}
            var app=new Application();app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show("悬浮窗遇到错误："+e.Exception.Message,"Codex 用量悬浮窗");e.Handled=true;};SkinController.Start(app);app.Run();return 0;
        }catch(Exception ex){if(args.Length>0){try{File.WriteAllText(args.Last(),ex.ToString());}catch{}}else MessageBox.Show(ex.Message,"Codex 用量悬浮窗");return 1;}
    }
    static void Check(bool test,string name,List<string> passed){if(!test)throw new Exception("FAILED: "+name);passed.Add(name);}
    static void SelfTest(string output){var passed=new List<string>();
        Check(Data.Stage(100)=="#FF0F9D2A"&&Data.Stage(80.01)=="#FF0F9D2A"&&Data.Stage(80)=="#FF004DD2"&&Data.Stage(60.01)=="#FF004DD2"&&Data.Stage(60)=="#FF5727AE"&&Data.Stage(40.01)=="#FF5727AE"&&Data.Stage(40)=="#FFAF900D"&&Data.Stage(20.01)=="#FFAF900D"&&Data.Stage(20)=="#FFDB7900"&&Data.Stage(10.01)=="#FFDB7900"&&Data.Stage(10)=="#FFC51F3E"&&Data.Stage(0)=="#FFC51F3E","shared six-stage threshold boundaries",passed);
        var q=Data.Quotas(Data.Json().DeserializeObject("{\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"usedPercent\":4,\"windowDurationMins\":10080},\"secondary\":null}}}"));Check(q.Count==1&&q[0].Remaining==96&&q[0].Label=="周额度","weekly primary without secondary",passed);
        q=Data.Quotas(Data.Json().DeserializeObject("{\"rateLimits\":{\"primary\":{\"usedPercent\":120,\"windowDurationMins\":300},\"secondary\":{\"usedPercent\":10,\"windowDurationMins\":10080},\"individualLimit\":{\"remainingPercent\":68}}}"));Check(q.Count==3&&q[0].Remaining==0&&q[0].Label=="5 小时额度"&&q[2].Remaining==68,"legacy and monthly quotas",passed);
        Check(Data.Quotas(new {}).Count==0,"missing quota stays unknown",passed);
        var seen=new HashSet<string>();DateTime since=DateTime.UtcNow.AddSeconds(-1);
        Func<string,string,object> ev=(kind,extra)=>Data.Json().DeserializeObject("{\"timestamp\":\""+DateTime.UtcNow.ToString("o")+"\",\"type\":\"event_msg\",\"payload\":{\"type\":\""+kind+"\",\"turn_id\":\"t1\""+extra+"}}");
        Check(TaskMonitor.Parse(ev("item_completed",""),"id",since,seen)==null,"tool completion ignored",passed);
        Check(TaskMonitor.Parse(ev("task_complete",",\"error\":{\"message\":\"failed\"}"),"id",since,seen)==null,"failed turn ignored",passed);
        Check(TaskMonitor.Parse(ev("task_complete",",\"root_turn_id\":\"parent\""),"id",since,seen)==null,"child turn ignored",passed);
        Check(TaskMonitor.Parse(ev("task_complete",""),"id",since,seen)!=null,"successful completion detected",passed);
        Check(TaskMonitor.Parse(ev("task_complete",""),"id",since,seen)==null,"duplicate suppressed",passed);
        Check(TaskMonitor.Parse(ev("task_complete",""),"other",DateTime.UtcNow.AddHours(1),seen)==null,"historical event ignored",passed);
        string root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"codex-meter-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(System.IO.Path.Combine(root,"sessions"));
        try {string f=System.IO.Path.Combine(root,"sessions","rollout-00000000-0000-0000-0000-000000000001.jsonl");File.WriteAllText(f,"{\"type\":\"session_meta\",\"payload\":{}}\n",new UTF8Encoding(false));var m=new TaskMonitor(root);Check(m.Scan().Count==0,"startup does not alert",passed);
            string line=Data.Json().Serialize(ev("task_complete",""))+"\n";byte[] b=Encoding.UTF8.GetBytes(line);int split=b.Length/2;using(var fs=new FileStream(f,FileMode.Append)){fs.Write(b,0,split);}Check(m.Scan().Count==0,"partial log line waits",passed);using(var fs=new FileStream(f,FileMode.Append)){fs.Write(b,split,b.Length-split);}Check(m.Scan().Count==1,"completed log line alerts",passed);Check(m.Scan().Count==0,"next scan does not repeat",passed);
        }finally{Directory.Delete(root,true);}
        Data.Atomic(output,Data.Json().Serialize(new{passed=passed.Count,checks=passed}));
    }
}
}
