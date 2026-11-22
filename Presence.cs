using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace Tito {
public static class DesktopContext {
 [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] struct MonitorInfo {public int Size;public Rect Monitor,Work;public uint Flags;}
 [StructLayout(LayoutKind.Sequential)] struct CursorPoint {public int X,Y;}
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd,System.Text.StringBuilder name,int length);
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint point);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attr,out Rect rect,int size);
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
 [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)] public static extern int SetCurrentProcessExplicitAppUserModelID(string id);
 static uint cachedPid;static string cachedName="",cachedPath="";
 public static string ForegroundApp="";
 public static void HideFromSwitcher(Window window){var hwnd=new WindowInteropHelper(window).Handle;int style=GetWindowLong(hwnd,-20);SetWindowLong(hwnd,-20,(style|0x80)&~0x40000);}
 public static bool CursorInside(Window window,double dipWidth,double dipHeight,double margin){
  CursorPoint p;Rect r;var hwnd=new WindowInteropHelper(window).Handle;if(hwnd==IntPtr.Zero||!GetCursorPos(out p)||!GetWindowRect(hwnd,out r))return false;
  double s=(r.Right-r.Left)/640.0,cx=(r.Left+r.Right)/2.0;
  return p.X>=cx-(dipWidth/2+margin)*s&&p.X<=cx+(dipWidth/2+margin)*s&&p.Y>=r.Top+(8-margin)*s&&p.Y<=r.Top+(8+dipHeight+margin)*s;
 }
 static IntPtr lastFocus;
 // Janela em foco mudou: devolve para onde os olhos devem olhar (na direção dela).
 public static bool FocusChanged(Window own,out Point gaze){
  gaze=new Point();IntPtr h=GetForegroundWindow();if(h==lastFocus)return false;lastFocus=h;
  Rect me,r;if(h==IntPtr.Zero||h==new WindowInteropHelper(own).Handle||IsIconic(h)||!GetWindowRect(h,out r)||!GetWindowRect(new WindowInteropHelper(own).Handle,out me))return false;
  double cx=(me.Left+me.Right)/2.0,cy=me.Top+25*(me.Bottom-me.Top)/300.0;
  gaze=new Point(2.6*Math.Tanh(((r.Left+r.Right)/2.0-cx)/260.0),1.4*Math.Tanh(((r.Top+r.Bottom)/2.0-cy)/160.0));return true;
 }
 public static bool IsToolWindow(IntPtr hwnd){int style=GetWindowLong(hwnd,-20);return (style&0x80)!=0&&(style&0x40000)==0;}
 // Fullscreen = the shell says so (game, presentation) or a window covers the monitor; a maximized window that keeps its title bar is not fullscreen.
 public static bool IsFullscreen(bool covers,bool zoomed,bool caption,int state){return state==2||state==3||state==4||(covers&&!(zoomed&&caption));}
 public static bool CoversMonitor(Rect rect,Rect monitor){return rect.Left<=monitor.Left+2&&rect.Top<=monitor.Top+2&&rect.Right>=monitor.Right-2&&rect.Bottom>=monitor.Bottom-2;}
 public static bool Suppress(IntPtr ownWindow,ISet<string> hiddenApps){
  var hwnd=GetForegroundWindow();if(hwnd==IntPtr.Zero||hwnd==ownWindow||!IsWindowVisible(hwnd)||IsIconic(hwnd))return false;
  var cls=new System.Text.StringBuilder(256);GetClassName(hwnd,cls,cls.Capacity);string name=cls.ToString();
  if(new[]{"Progman","WorkerW","Shell_TrayWnd","Shell_SecondaryTrayWnd","XamlExplorerHostIslandWindow"}.Contains(name))return false;
  uint pid;GetWindowThreadProcessId(hwnd,out pid);
  if(pid!=cachedPid){cachedPid=pid;cachedName="";cachedPath="";try{using(var p=Process.GetProcessById((int)pid)){cachedName=p.ProcessName;try{cachedPath=p.MainModule.FileName;}catch{}}}catch{}}
  if(!String.Equals(cachedName,"explorer",StringComparison.OrdinalIgnoreCase)&&!String.Equals(cachedName,"ShellExperienceHost",StringComparison.OrdinalIgnoreCase)&&!String.IsNullOrEmpty(cachedName))ForegroundApp=cachedName;
  if(MonitorFromWindow(hwnd,2)!=MonitorFromWindow(ownWindow,2))return false;
  if(hiddenApps.Contains(cachedName))return true;
  if(name=="UnrealWindow"||name=="UnityWndClass")return true;
  if(cachedPath.IndexOf("\\steamapps\\common\\",StringComparison.OrdinalIgnoreCase)>=0)return true;
  if(cachedPath.IndexOf("\\Epic Games\\",StringComparison.OrdinalIgnoreCase)>=0&&cachedName.IndexOf("Launcher",StringComparison.OrdinalIgnoreCase)<0)return true;
  string[] known={"cs2","dota2","VALORANT-Win64-Shipping","FortniteClient-Win64-Shipping","Minecraft.Windows","RobloxPlayerBeta","RocketLeague","GenshinImpact","League of Legends","Overwatch","r5apex","RainbowSix","cod"};if(known.Contains(cachedName,StringComparer.OrdinalIgnoreCase))return true;
  var monitor=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(!GetMonitorInfo(MonitorFromWindow(hwnd,2),ref monitor))return false;
  Rect rect;if(DwmGetWindowAttribute(hwnd,9,out rect,Marshal.SizeOf(typeof(Rect)))!=0&&!GetWindowRect(hwnd,out rect))return false;
  int state;if(SHQueryUserNotificationState(out state)!=0)state=-1;
  return IsFullscreen(CoversMonitor(rect,monitor.Monitor),IsZoomed(hwnd),(GetWindowLong(hwnd,-16)&0xC00000)==0xC00000,state);
 }
 public static Point Gaze(Window window){
  CursorPoint point;if(!GetCursorPos(out point))return new Point();
  Rect rect;if(!GetWindowRect(new WindowInteropHelper(window).Handle,out rect))return new Point();
  double cx=(rect.Left+rect.Right)/2.0,cy=rect.Top+25*(rect.Bottom-rect.Top)/300.0;
  return new Point(2.6*Math.Tanh((point.X-cx)/260.0),1.4*Math.Tanh((point.Y-cy)/160.0));
 }
}
public partial class App {
 static Border restingShell;static MascotEyes mascot;static Grid windowHost,island;
 static RectangleGeometry islandClip;static DispatcherTimer hoverTimer;
 static bool secondary;static int ownerPid;static Process ownerProc;static string suffix="";
 static bool contextHidden,autoHideContext=true;
 static HashSet<string> hiddenApps=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 static DateTime nextBlink=DateTime.UtcNow.AddSeconds(3);static Random random=new Random();
 static System.Threading.EventWaitHandle openSignal,restartSignal;
 // Dashboard size: 0 = automatic (monitor width in DIPs vs 1920, so a 4K at 100% gets a bigger Tito), else a fixed percent from Settings.
 // The percent lives in size.txt; every island rereads it when it changes, like the monitor switches.
 static int sizePct;static DateTime sizeStamp;
 static string SizeFile(){return System.IO.Path.Combine(dataDir,"size.txt");}
 static void ApplyScale(Rect area){double s=sizePct>0?sizePct/100.0:Math.Round(Math.Max(.85,Math.Min(2,area.Width/1920))*20)/20;w.Width=640*s;w.Height=300*s;}
 static void SyncSize(){try{var t=File.Exists(SizeFile())?File.GetLastWriteTimeUtc(SizeFile()):DateTime.MinValue;if(t==sizeStamp)return;sizeStamp=t;int p=t==DateTime.MinValue?0:int.Parse(File.ReadAllText(SizeFile()).Trim());if(p!=sizePct){sizePct=p;SetPose(opened,false);}}catch{}}
 static void SetPose(bool expanded,bool animated){
  if(!SystemParameters.ClientAreaAnimation)animated=false;
  opened=expanded;ApplyBackdrop();int revision=++poseRevision;glass.Width=600;glass.Height=270;glass.HorizontalAlignment=HorizontalAlignment.Center;glass.VerticalAlignment=VerticalAlignment.Top;
  if(expanded){view.Visibility=Visibility.Visible;nav.Visibility=Visibility.Visible;date.Visibility=Visibility.Visible;time.Visibility=Visibility.Visible;compact.Visibility=Visibility.Collapsed;}
  double width=expanded?600:compactWide?compactWidth:128,height=expanded?270:34;
  var area=MonitorArea();ApplyScale(area);w.Left=area.Left+(area.Width-w.Width)/2;w.Top=area.Top;
  if(!expanded){restingShell.Width=width;restingShell.Height=height;}
  glass.IsHitTestVisible=expanded;restingShell.IsHitTestVisible=!expanded;
  if(animated){
   glass.Visibility=Visibility.Visible;restingShell.Visibility=Visibility.Visible;
   MorphTo(width,height,expanded);
   var scale=new ScaleTransform(expanded?.94:1,expanded?.94:1);glass.RenderTransformOrigin=new Point(.5,0);glass.RenderTransform=scale;
   if(expanded){var grow=new System.Windows.Media.Animation.DoubleAnimation(1,TimeSpan.FromMilliseconds(460)){BeginTime=TimeSpan.FromMilliseconds(60),EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut}};scale.BeginAnimation(ScaleTransform.ScaleXProperty,grow);scale.BeginAnimation(ScaleTransform.ScaleYProperty,grow);}
   var panelFade=new System.Windows.Media.Animation.DoubleAnimation(expanded?1:0,TimeSpan.FromMilliseconds(expanded?240:90)){BeginTime=expanded?TimeSpan.FromMilliseconds(110):TimeSpan.Zero};
   panelFade.Completed+=(s,e)=>{if(revision==poseRevision&&!expanded)glass.Visibility=Visibility.Collapsed;};glass.BeginAnimation(UIElement.OpacityProperty,panelFade);
   var faceFade=new System.Windows.Media.Animation.DoubleAnimation(expanded?0:1,TimeSpan.FromMilliseconds(180));
   faceFade.Completed+=(s,e)=>{if(revision==poseRevision&&expanded)restingShell.Visibility=Visibility.Collapsed;};restingShell.BeginAnimation(UIElement.OpacityProperty,faceFade);
  }else{
   islandClip.BeginAnimation(RectangleGeometry.RectProperty,null);islandClip.BeginAnimation(RectangleGeometry.RadiusXProperty,null);islandClip.BeginAnimation(RectangleGeometry.RadiusYProperty,null);SetClip(width,height);
   glass.RenderTransform=null;glass.BeginAnimation(UIElement.OpacityProperty,null);restingShell.BeginAnimation(UIElement.OpacityProperty,null);glass.Opacity=expanded?1:0;restingShell.Opacity=expanded?0:1;
   glass.Visibility=expanded?Visibility.Visible:Visibility.Collapsed;restingShell.Visibility=expanded?Visibility.Collapsed:Visibility.Visible;
  }
 }
 static void SetupPresence(){
  string apps=System.IO.Path.Combine(dataDir,"hidden-apps.txt");if(File.Exists(apps))foreach(var line in File.ReadAllLines(apps))if(!String.IsNullOrWhiteSpace(line))hiddenApps.Add(line.Trim());
  openSignal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\TitoGlassOpen"+suffix);
  restartSignal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\TitoGlassRestart"+suffix);
  if(!secondary)File.WriteAllLines(System.IO.Path.Combine(dataDir,"active.txt"),new[]{Process.GetCurrentProcess().Id.ToString(),Process.GetCurrentProcess().MainModule.FileName});
 }

 static DateTime lastGazeTick=DateTime.UtcNow;static Point gazeNow;
 static void GazeTick(){
  var now=DateTime.UtcNow;double dt=Math.Min(.1,(now-lastGazeTick).TotalSeconds);lastGazeTick=now;
  var m=opened?headerMascot:mascot;var other=opened?mascot:headerMascot;if(m==null)return;
  if(other!=null&&(other.GazeX!=0||other.GazeY!=0)){other.GazeX=0;other.GazeY=0;other.InvalidateVisual();}
  Point target=DesktopContext.Gaze(w);if((target-lastGaze).Length>.05){gazeUntil=now.AddMilliseconds(1600);lastGaze=target;}
  if(now>gazeUntil)target=new Point();if(now<quirkUntil)target=quirkGaze;
  double k=1-Math.Exp(-dt/(target.X!=0||target.Y!=0?.07:.24));var next=new Point(gazeNow.X+(target.X-gazeNow.X)*k,gazeNow.Y+(target.Y-gazeNow.Y)*k);
  if((next-gazeNow).Length>.004){gazeNow=next;m.GazeX=next.X;m.GazeY=next.Y;m.InvalidateVisual();}
 }
 static bool hoverArmed; // só abre por hover depois de o mouse ter saído da área uma vez (ex.: Tito reaberto com o mouse em cima)
 static DateTime hoverSince=DateTime.MinValue,outsideSince=DateTime.MinValue;static bool pinOpen,clickOnly;static readonly TimeSpan HoverDelay=TimeSpan.FromMilliseconds(350);
 static void HoverTick(){
  if(uiTest||contextHidden||!w.IsVisible){if(hoverTimer!=null)hoverTimer.Interval=TimeSpan.FromMilliseconds(250);return;}
  GazeTick();
  double tw=opened?600:(compactWide?compactWidth:128),th=opened?270:34;bool inside=DesktopContext.CursorInside(w,tw,th,opened?2:0);
  // Fecha só depois de 300 ms fora (atravessar um vão não fecha); fixado ou digitando num campo, fica aberto.
  if(opened){bool hold=inside||pinOpen||dropHover||System.Windows.Input.Mouse.Captured!=null||(w.IsKeyboardFocusWithin&&System.Windows.Input.Keyboard.FocusedElement is TextBox);
   if(hold)outsideSince=DateTime.MinValue;else if(outsideSince==DateTime.MinValue)outsideSince=DateTime.UtcNow;else if(DateTime.UtcNow-outsideSince>=TimeSpan.FromMilliseconds(300)){outsideSince=DateTime.MinValue;Expand(false);}}
  else if(!inside){hoverSince=DateTime.MinValue;hoverArmed=true;}else if(!doNotDisturb&&!clickOnly&&hoverArmed){if(hoverSince==DateTime.MinValue)hoverSince=DateTime.UtcNow;else if(DateTime.UtcNow-hoverSince>=HoverDelay){hoverSince=DateTime.MinValue;Expand(true);}} // abre só depois de um instante com o mouse em cima
  // 33 ms só quando há o que animar; em repouso 100 ms bastam para detectar o mouse.
  hoverTimer.Interval=TimeSpan.FromMilliseconds(opened||inside||gazeNow.X!=0||gazeNow.Y!=0||DateTime.UtcNow<gazeUntil?16:100);
 }
 static void PresenceTick(){
  SyncFullscreenHide();
  SyncSize();
  SyncMonitors(false);
  if(secondary&&ownerProc!=null&&ownerProc.HasExited){w.Close();return;}
  if(restartSignal!=null&&restartSignal.WaitOne(0)){w.Close();return;}
  bool suppress=monitorOff||autoHideContext&&DesktopContext.Suppress(new WindowInteropHelper(w).Handle,hiddenApps);
  if(suppress&&!contextHidden){contextHidden=true;leaveTimer.Stop();SetPose(false,false);w.Hide();}
  else if(!suppress&&contextHidden){contextHidden=false;SetPose(false,false);w.Show();}
  if(openSignal!=null&&openSignal.WaitOne(0)&&!contextHidden){w.Show();Expand(true);}
  if(!opened&&mascot!=null&&!contextHidden){
   
   if(DateTime.UtcNow>=nextQuirk){nextQuirk=DateTime.UtcNow.AddSeconds(14+random.NextDouble()*16);if(mascot.Mood=="idle"){int q=agendaSoon?3:random.Next(3);if(q==0)winkUntil=DateTime.UtcNow.AddMilliseconds(800);else if(q==1){quirkGaze=new Point(random.Next(2)==0?-2:2,0);quirkUntil=DateTime.UtcNow.AddMilliseconds(1300);}else if(q==3){quirkGaze=new Point(3,-1.4);quirkUntil=DateTime.UtcNow.AddMilliseconds(900);}else mascot.Hop(1);}} // lembrete chegando: olha de relance para o relógio
   Point glance;if(mascot.Mood=="idle"&&DesktopContext.FocusChanged(w,out glance)){quirkGaze=glance;quirkUntil=DateTime.UtcNow.AddMilliseconds(1100);}
   if(DateTime.UtcNow>=nextBlink){mascot.BlinkOnce(random.NextDouble()<.2);nextBlink=DateTime.UtcNow.AddSeconds(3+random.NextDouble()*4);}
  }
 }
 static void Restart(){
  string launcher=System.IO.Path.Combine(baseDir,"Tito.exe");
  Process.Start(new ProcessStartInfo(launcher,"--wait "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true});w.Close();
 }
 static void HideCurrentApp(){
  string app=DesktopContext.ForegroundApp;if(String.IsNullOrEmpty(app))return;
  hiddenApps.Add(app);File.WriteAllLines(System.IO.Path.Combine(dataDir,"hidden-apps.txt"),hiddenApps);PresenceTick();
 }
}
}
