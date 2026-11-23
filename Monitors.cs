using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace Tito {
public partial class App {
 // Multi-monitor control: one island per screen (a process each). Turn screens on/off, follow plug/unplug, open the island on the screen under the mouse with Ctrl+Alt+T.
 static Dictionary<int,Process> spawned=new Dictionary<int,Process>();static DispatcherTimer displayTimer;
 [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
 [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd,int id);
 static string MonitorsFile(){return Path.Combine(dataDir,"monitors.txt");}
 static HashSet<int> OffMonitors(){var off=new HashSet<int>();try{if(File.Exists(MonitorsFile()))foreach(var line in File.ReadAllLines(MonitorsFile())){int n;if(Int32.TryParse(line.Trim(),out n))off.Add(n);}}catch{}return off;}
 static string MonitorLabel(int i){var screens=Forms.Screen.AllScreens;var b=screens[i].Bounds;return "Tela "+(i+1)+" · "+b.Width+"×"+b.Height+(screens[i].Primary?" · principal":"");}
 static void SignalIsland(string name,string suffix){try{using(var signal=System.Threading.EventWaitHandle.OpenExisting("Local\\"+name+suffix))signal.Set();}catch{}}
 // Primary only: starts islands for enabled screens that have none, closes the ones switched off.
 static void SyncIslands(){
  var off=OffMonitors();var screens=Forms.Screen.AllScreens;string exe=Process.GetCurrentProcess().MainModule.FileName;
  for(int i=0;i<screens.Length;i++){
   if(i==selectedMonitor)continue;
   Process p;spawned.TryGetValue(i,out p);bool running=p!=null&&!p.HasExited;
   if(off.Contains(i)){if(running)SignalIsland("TitoGlassRestart","m"+i);}
   else if(!running)try{spawned[i]=Process.Start(new ProcessStartInfo(exe,"--monitor "+i+" --owner "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=baseDir});}catch{}
  }
 }
 static string FsFile(){return Path.Combine(dataDir,"monitors-fullscreen-off.txt");}
 static DateTime fsStamp;
 static HashSet<int> FullscreenOff(){var off=new HashSet<int>();try{if(File.Exists(FsFile()))foreach(var line in File.ReadAllLines(FsFile())){int n;if(Int32.TryParse(line.Trim(),out n))off.Add(n);}}catch{}return off;}
 // "Hide in fullscreen" is per screen; every island rereads the file when it changes (fullscreen detection already only looks at the island's own screen).
 static void SyncFullscreenHide(){try{var t=File.Exists(FsFile())?File.GetLastWriteTimeUtc(FsFile()):DateTime.MinValue;if(t==fsStamp)return;fsStamp=t;autoHideContext=!FullscreenOff().Contains(selectedMonitor);}catch{}}
 static void SetFullscreenHide(int i,bool on){
  var off=FullscreenOff();if(on)off.Remove(i);else off.Add(i);
  try{File.WriteAllLines(FsFile(),off.Select(x=>x.ToString()).ToArray());}catch{}
  SyncFullscreenHide();PresenceTick();
 }
 // Any island can switch any screen: the file is the truth and every island rereads it when it changes. An "off" primary only hides (it owns the tray and the media bridge).
 static DateTime monStamp;static bool monitorOff;
 static void SyncMonitors(bool force){try{
  var t=File.Exists(MonitorsFile())?File.GetLastWriteTimeUtc(MonitorsFile()):DateTime.MinValue;if(t==monStamp&&!force)return;monStamp=t;
  monitorOff=OffMonitors().Contains(selectedMonitor);
  if(secondary){if(monitorOff)w.Close();}else SyncIslands();
 }catch{}}
 static void SetMonitor(int i,bool on){
  var off=OffMonitors();var count=Forms.Screen.AllScreens.Length;
  if(!on&&Enumerable.Range(0,count).Count(x=>x!=i&&!off.Contains(x))==0){error="Mantenha pelo menos uma tela ligada.";ShowError();return;}
  if(on)off.Remove(i);else off.Add(i);
  try{File.WriteAllLines(MonitorsFile(),off.Select(x=>x.ToString()).ToArray());}catch{}
  SyncMonitors(true);
 }
 static void SetupMonitors(){
  if(uiTest)return;
  SyncFullscreenHide();
  Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=(s,e)=>w.Dispatcher.BeginInvoke(new Action(()=>{if(displayTimer!=null){displayTimer.Stop();displayTimer.Start();}}));
  // Debounced: Windows raises several display events per plug/unplug.
  displayTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};
  displayTimer.Tick+=(s,e)=>{displayTimer.Stop();
   if(secondary&&selectedMonitor>=Forms.Screen.AllScreens.Length){w.Close();return;}
   SetPose(opened,false);if(!secondary)SyncIslands();};
  if(secondary)return;
  var helper=new WindowInteropHelper(w);IntPtr hwnd=helper.EnsureHandle();
  HwndSource.FromHwnd(hwnd).AddHook((IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool handled)=>{
   if(msg==0x312&&wp.ToInt32()==0x7157){handled=true;var screens=Forms.Screen.AllScreens;int i=Array.IndexOf(screens,Forms.Screen.FromPoint(Forms.Cursor.Position));if(i>=0&&OffMonitors().Contains(i))SetMonitor(i,true);else SignalIsland("TitoGlassOpen",i<0||i==selectedMonitor?"":"m"+i);}
   if(msg==0x312&&wp.ToInt32()==0x7158){handled=true;CommandBox();}
   if(shellMsg!=0&&(uint)msg==shellMsg)ShellHook(wp.ToInt32(),lp);
   return IntPtr.Zero;});
  RegisterHotKey(hwnd,0x7157,0x3|0x4000,0x54); // Ctrl+Alt+T
  hotHwnd=hwnd;ApplyHotkey();ConnectSetup(hwnd);
  w.Closed+=(s,e)=>{UnregisterHotKey(hwnd,0x7157);UnregisterHotKey(hwnd,0x7158);};
 }
 // Atalho da caixa de comando. Win+Espaço fica de fora: o Windows usa para trocar o idioma do teclado.
 static readonly string[][] Hotkeys={new[]{"ctrl-alt-space","Ctrl+Alt+Espaço","3","32"},new[]{"ctrl-shift-space","Ctrl+Shift+Espaço","6","32"},new[]{"ctrl-alt-k","Ctrl+Alt+K","3","75"},new[]{"ctrl-alt-j","Ctrl+Alt+J","3","74"},new[]{"off","Desligado","0","0"}};
 static string hotkeyKey="ctrl-alt-space";static IntPtr hotHwnd;
 static string[] HotkeyDef(){return Hotkeys.FirstOrDefault(h=>h[0]==hotkeyKey)??Hotkeys[0];}
 // Registra o atalho escolhido; false quando outro programa já usa a combinação.
 static bool ApplyHotkey(){
  if(hotHwnd==IntPtr.Zero)return true;UnregisterHotKey(hotHwnd,0x7158);var h=HotkeyDef();if(h[0]=="off")return true;
  return RegisterHotKey(hotHwnd,0x7158,UInt32.Parse(h[2])|0x4000,UInt32.Parse(h[3]));
 }
 static Forms.ToolStripMenuItem MonitorsMenu(){
  var root=new Forms.ToolStripMenuItem("Telas (Ctrl+Alt+T abre onde está o mouse)");
  root.DropDownOpening+=(s,e)=>{
   root.DropDownItems.Clear();var off=OffMonitors();var fsOff=FullscreenOff();var screens=Forms.Screen.AllScreens;
   for(int i=0;i<screens.Length;i++){int n=i;var sub=new Forms.ToolStripMenuItem(MonitorLabel(i));
    var on=new Forms.ToolStripMenuItem("Tito nesta tela"){Checked=!off.Contains(i),Enabled=off.Contains(i)||Enumerable.Range(0,screens.Length).Count(x=>x!=n&&!off.Contains(x))>0};on.Click+=(a,b)=>SetMonitor(n,off.Contains(n));sub.DropDownItems.Add(on);
    var fs=new Forms.ToolStripMenuItem("Ocultar em tela cheia"){Checked=!fsOff.Contains(i)};fs.Click+=(a,b)=>SetFullscreenHide(n,fsOff.Contains(n));sub.DropDownItems.Add(fs);root.DropDownItems.Add(sub);}
  };
  root.DropDownItems.Add("…");return root;
 }
 static UIElement MonitorsCard(){
  var box=Stack();var title=T("Telas",12,Theme.Heading);title.Margin=new Thickness(8,4,0,4);box.Children.Add(title);
  var off=OffMonitors();var fsOff=FullscreenOff();var screens=Forms.Screen.AllScreens;
  for(int i=0;i<screens.Length;i++){int n=i;
   var cap=T(MonitorLabel(i),10.5,Theme.Soft);cap.Margin=new Thickness(8,4,8,2);box.Children.Add(cap);
   box.Children.Add(ToggleRow("Mostrar Tito",!off.Contains(i),v=>SetMonitor(n,v)));
   box.Children.Add(ToggleRow("Ocultar em tela cheia",!fsOff.Contains(i),v=>SetFullscreenHide(n,v)));}
  var hint=T("Ctrl+Alt+T abre o Tito na tela do mouse (ou religa, se estiver desligado).  "+HotkeyDef()[1]+" abre a caixa de comando.",10.5,Theme.Soft);hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(8,4,8,4);box.Children.Add(hint);
  return CardBox(box);
 }
}
}
