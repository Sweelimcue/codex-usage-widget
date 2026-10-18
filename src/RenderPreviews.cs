using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CodexMeter {
static class RenderPreviews {
    static object Quota(int remaining){var q=new{rateLimits=new{primary=new{usedPercent=100-remaining,windowDurationMins=10080,resetsAt=(long)(DateTime.UtcNow.AddDays(3).AddHours(12)-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds}}};return Data.Json().DeserializeObject(Data.Json().Serialize(q));}
    static FloatWindow Make(int remaining){var w=new FloatWindow(true){ShowActivated=false,Left=SystemParameters.VirtualScreenLeft-2000,Top=SystemParameters.VirtualScreenTop-2000};w.SetPreviewQuota(Quota(remaining));w.Show();w.UpdateLayout();return w;}
    static void Save(Visual visual,double width,double height,string file){var bmp=new RenderTargetBitmap((int)Math.Ceiling(width*2),(int)Math.Ceiling(height*2),192,192,PixelFormats.Pbgra32);bmp.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(file))encoder.Save(f);}
    static void Text(DrawingContext dc,string value,double x,double y,double size,Brush color,bool bold=false){dc.DrawText(new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),size,color,1),new Point(x,y));}
    public static int AllSkinsStages(string path){
        string folder=Path.GetFullPath(path);Directory.CreateDirectory(folder);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var windows=new List<Window>();int[] levels={100,80,60,40,20,10};
        try{var scene=new DrawingVisual();const double width=700,height=1090;using(var dc=scene.RenderOpen()){
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(237,240,245)),null,new Rect(0,0,width,height));Text(dc,"三种皮肤 · 六档额度配色",24,16,19,Brushes.DarkSlateGray,true);
            Text(dc,"能量胶囊",28,53,12,Brushes.DimGray);Text(dc,"白色立体圆环",350,53,12,Brushes.DimGray);Text(dc,"极光圆环",530,53,12,Brushes.DimGray);
            for(int i=0;i<levels.Length;i++){int value=levels[i];double y=80+i*166;Text(dc,value+"% · "+QuotaColors.For(value).Name,28,y,12,Brushes.DarkSlateGray,true);
                var capsule=Make(value);windows.Add(capsule);capsule.CaptureScene(Path.Combine(folder,"capsule-"+value+".png"));dc.DrawRectangle(new VisualBrush(capsule),null,new Rect(10,y+20+(130-capsule.ActualHeight)/2,capsule.ActualWidth,capsule.ActualHeight));
                for(int style=0;style<2;style++){var ring=new RingWindow(true,style==1){ShowActivated=false,Left=SystemParameters.VirtualScreenLeft-2000,Top=SystemParameters.VirtualScreenTop-2000};windows.Add(ring);ring.ApplyData(Quota(value),false,DateTime.Now);ring.Show();ring.UpdateLayout();ring.Capture(Path.Combine(folder,(style==0?"white-":"aurora-")+value+".png"));dc.DrawRectangle(new VisualBrush(ring),null,new Rect(style==0?330:510,y+20,130,130));}
            }
        }Save(scene,width,height,Path.Combine(folder,"三皮肤六档配色.png"));
        }finally{foreach(var w in windows)w.Close();app.Shutdown();}return 0;
    }
    public static int Stages(string path){string folder=Path.GetFullPath(path);Directory.CreateDirectory(folder);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int[] levels={100,90,80,75,60,50,40,30,20,10,5,0};var windows=new List<FloatWindow>();
        try{foreach(int level in levels){var w=Make(level);windows.Add(w);w.CaptureScene(Path.Combine(folder,"quota-"+level.ToString("000")+".png"));}
            double cellWidth=298,width=3*cellWidth+4*18,height=80+Math.Ceiling(levels.Length/3.0)*150;var scene=new DrawingVisual();using(var dc=scene.RenderOpen()){
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(237,240,245)),null,new Rect(0,0,width,height));Text(dc,"皮肤 1 · 剩余额度阶段",22,16,18,Brushes.DarkSlateGray,true);Text(dc,"翠绿 >80% · 蓝 >60% · 紫 >40% · 金黄 >20% · 橙黄 >10% · 红橙 ≤10%",22,46,11,Brushes.DimGray);
                for(int i=0;i<levels.Length;i++){int p=levels[i];double x=18+(i%3)*(cellWidth+18),y=80+(i/3)*150;string stage=p==0?"已耗尽":p>80?"翠绿":p>60?"蓝色":p>40?"紫色":p>20?"金黄":p>10?"橙黄":"红橙";Text(dc,"剩余 "+p+"% · "+stage,x+16,y,12,Brushes.DarkSlateGray);dc.DrawRectangle(new VisualBrush(windows[i]),null,new Rect(x,y+20,windows[i].ActualWidth,windows[i].ActualHeight));}
            }Save(scene,width,height,Path.Combine(folder,"皮肤1额度阶段总览.png"));Data.Atomic(Path.Combine(folder,"stages.json"),Data.Json().Serialize(new{exampleValues=true,remainingPercent=levels,thresholds=new[]{80,60,40,20,10},scale=2}));
        }finally{foreach(var w in windows)w.Close();app.Shutdown();}return 0;
    }
    public static int Alert(string path){string folder=Path.GetFullPath(path);Directory.CreateDirectory(folder);var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var w=Make(75);w.CaptureScene(Path.Combine(folder,"normal.png"));var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};var time=Stopwatch.StartNew();var stamps=new List<double>();int frame=0;double stoppedAt=0;
        w.ResumeAlert(3);timer.Tick+=(s,e)=>{try{double now=time.Elapsed.TotalSeconds;if(!w.IsFlashing&&stoppedAt==0)stoppedAt=now;w.CaptureScene(Path.Combine(folder,"frame-"+(frame++).ToString("000")+".png"));stamps.Add(now);if(now<3.6)return;timer.Stop();
            var concat=new StringBuilder();for(int i=0;i<stamps.Count;i++){concat.Append("file 'frame-").Append(i.ToString("000")).Append(".png'\n");if(i+1<stamps.Count)concat.Append("duration ").Append((stamps[i+1]-stamps[i]).ToString("0.000000",CultureInfo.InvariantCulture)).Append('\n');}File.WriteAllText(Path.Combine(folder,"frames.txt"),concat.ToString(),new UTF8Encoding(false));
            Data.Atomic(Path.Combine(folder,"alert-check.json"),Data.Json().Serialize(new{autoStopped=!w.IsFlashing,stopObservedSeconds=stoppedAt,frames=frame,exampleRemaining=75}));w.Close();app.Shutdown();
        }catch(Exception ex){timer.Stop();File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());w.Close();app.Shutdown();}};timer.Start();app.Run();return 0;
    }
}
}
