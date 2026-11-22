using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Web.Script.Serialization;
namespace Tito {
public class MediaSource {public string source,title;public bool playing;}
public class MediaState {
 public MediaSource[] sessions;
 public bool available,playing,canSeek,canNext,canPrevious,canToggle;
 public string title,artist,album,source,cover,error,updated;
 public double position,duration;
}
public partial class App {
 static string baseDir=Environment.GetEnvironmentVariable("TITO_ROOT")??AppDomain.CurrentDomain.BaseDirectory;
 static string dataDir=System.IO.Path.Combine(baseDir,"Dados");
 static JavaScriptSerializer json=new JavaScriptSerializer();
 static MediaState media=new MediaState();
 static bool useSystem=true; static Process mediaWorker; static int poll;
 static TextBlock mediaArtist,mediaHint,agentStatus,agentDetail,agentAge;
 static Image albumImage;static string lastCover="",lastAgent="";static DateTime lastAgentStamp,lastMediaStamp;static TextBlock lastAgentUi;
 static Slider systemSeek;static Button systemPlay,systemNext,systemPrevious;static Line rulerMarker;static Canvas rulerCanvas;
 static DispatcherTimer leaveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
 static string Accent(string n){string t=ThemeAccent(n);if(t!=null)return t;return n=="music"?"#FF375F":n=="focus"?"#BF5AF2":n=="ia"?Theme.Warn:n=="agenda"?Theme.Bad:n=="notes"?"#FFD60A":n=="clipboard"?Theme.Ok:"#FFFFFF";}
 static StackPanel NavIcon(string name,string label){
  var p=new StackPanel{Orientation=Orientation.Horizontal};
  string path=name=="home"?"M1,7 L7,1 13,7 M3,6 L3,13 11,13 11,6 M6,13 L6,9 8,9 8,13":name=="music"?"M5,11 L5,3 12,1 12,9 M5,4 L12,2 M5,11 C5,14 0,14 1,11 C2,9 5,9 5,11 M12,9 C12,12 7,12 8,9 C9,7 12,7 12,9":name=="focus"?"M7,1 A6,6 0 1 1 6.99,1 M7,4 L7,7 10,8":name=="ia"?"M2,2 L12,2 Q14,2 14,4 L14,10 Q14,12 12,12 L5,12 2,14 2,12 Q0,12 0,10 L0,4 Q0,2 2,2 M4,6 L4,8 M10,6 L10,8":name=="painel"?"M2,13 L2,8 M7,13 L7,2 M12,13 L12,6":name=="agenda"?"M1,3 L13,3 13,13 1,13 Z M1,6 L13,6 M4,1 L4,4 M10,1 L10,4":name=="notes"?"M2,1 L12,1 12,13 2,13 Z M4,4 L10,4 M4,7 L10,7 M4,10 L8,10":name=="clipboard"?"M3,2 L11,2 11,13 3,13 Z M5,0 L9,0 9,3 5,3 Z M5,7 L9,7 M5,10 L9,10":name=="settings"?"M7,0 L9,3 12,2 12,5 14,7 12,9 12,12 9,11 7,14 5,11 2,12 2,9 0,7 2,5 2,2 5,3 Z M7,4 A3,3 0 1 1 6.99,4":"M7,0 L7,14 M0,7 L14,7 M2,2 L12,12 M2,12 L12,2";
  p.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(path),Stroke=B(label!=null?Accent(name):"#EDE6E0"),StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=15,Height=15,VerticalAlignment=VerticalAlignment.Center,Stretch=Stretch.Uniform});
  if(label!=null){var text=T(label,10,Accent(name));text.Margin=new Thickness(6,0,0,0);p.Children.Add(text);}return p;
 }
 static void SendMedia(string action,double pos=0){
  if(!media.available)return;
  string path=System.IO.Path.Combine(dataDir,"media-command.json");string temp=path+".tmp";
  File.WriteAllText(temp,json.Serialize(new{ id=Guid.NewGuid().ToString("N"),action=action,position=pos,source=media.source}),new System.Text.UTF8Encoding(false));
  if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
 }
 static void SystemMusic(){
  if(!useSystem){LocalMusic();return;}
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(196)});grid.ColumnDefinitions.Add(new ColumnDefinition());view.Children.Add(grid);coverImage=new Image{Stretch=Stretch.UniformToFill,Clip=new RectangleGeometry(new Rect(0,0,176,176),20,20)};albumImage=coverImage;coverCard=new Border{Width=176,Height=176,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,0,0),Child=coverImage,Effect=CoverGlow()};grid.Children.Add(coverCard);
  var sources=Btn("Origem ▾",SourceMenu,"Escolher aplicativo de mídia");sources.HorizontalAlignment=HorizontalAlignment.Left;sources.VerticalAlignment=VerticalAlignment.Bottom;sources.Margin=new Thickness(10,0,0,8);sources.Background=B("#A6000000");grid.Children.Add(sources);
  var info=new Grid();Grid.SetColumn(info,1);grid.Children.Add(info);
  mediaArtist=T("",10,"#CFC8C2");mediaArtist.VerticalAlignment=VerticalAlignment.Top;mediaArtist.TextTrimming=TextTrimming.CharacterEllipsis;info.Children.Add(mediaArtist);
  var words=new Grid{Height=100,Margin=new Thickness(0,17,0,0),VerticalAlignment=VerticalAlignment.Top,ClipToBounds=true};words.RowDefinitions.Add(new RowDefinition{Height=new GridLength(20)});words.RowDefinitions.Add(new RowDefinition{Height=new GridLength(58)});words.RowDefinitions.Add(new RowDefinition{Height=new GridLength(20)});info.Children.Add(words);
  lyricBefore=T("",17,"#55EEE8E3");lyricBefore.TextTrimming=TextTrimming.CharacterEllipsis;words.Children.Add(lyricBefore);
  lyricCurrent=T("",22);lyricCurrent.FontWeight=FontWeights.SemiBold;lyricCurrent.TextWrapping=TextWrapping.Wrap;lyricCurrent.MaxHeight=58;Grid.SetRow(lyricCurrent,1);words.Children.Add(lyricCurrent);
  lyricAfter=T("",16,"#88DAD1C9");lyricAfter.TextTrimming=TextTrimming.CharacterEllipsis;Grid.SetRow(lyricAfter,2);words.Children.Add(lyricAfter);
  musicTitle=lyricCurrent;mediaHint=T("",9,"#BDB4AE");mediaHint.Visibility=Visibility.Collapsed;info.Children.Add(mediaHint);
  var timeline=new Grid{Height=17,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,33)};timeline.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(35)});timeline.ColumnDefinitions.Add(new ColumnDefinition());timeline.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});
  elapsed=T("0:00",9,"#DFD3CB");timeline.Children.Add(elapsed);systemSeek=new Slider{Minimum=0,Maximum=100,IsMoveToPointEnabled=true,VerticalAlignment=VerticalAlignment.Center,Height=15};Grid.SetColumn(systemSeek,1);timeline.Children.Add(systemSeek);
  systemSeek.Style=(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'><Grid Background='Transparent'><Border Height='3' Background='#35FFFFFF' CornerRadius='2'/><Track x:Name='PART_Track'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Background='#F0DACE' BorderThickness='0' Height='3'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='{TemplateBinding Background}' Height='3' CornerRadius='2'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.Thumb><Thumb Width='6' Height='6'><Thumb.Template><ControlTemplate><Ellipse Fill='#F5E4D8'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Opacity='0' Height='10'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>");
  systemSeek.PreviewMouseDown+=(s,e)=>seeking=true;systemSeek.PreviewMouseUp+=(s,e)=>{SeekTo(media.duration*systemSeek.Value/100);seeking=false;};systemSeek.LostMouseCapture+=(s,e)=>seeking=false;
  mediaDuration=T("−0:00",9,"#DFD3CB");mediaDuration.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(mediaDuration,2);timeline.Children.Add(mediaDuration);info.Children.Add(timeline);
  var transport=Stack(Orientation.Horizontal);transport.HorizontalAlignment=HorizontalAlignment.Center;transport.VerticalAlignment=VerticalAlignment.Bottom;
  systemPrevious=Btn("",()=>SendMedia("previous"),"Anterior");systemPrevious.Content=TransportIcon("previous");transport.Children.Add(systemPrevious);
  systemPlay=Btn("",()=>SendMedia("toggle"),"Reproduzir / pausar");systemPlay.Background=B("#F9F2E9");systemPlay.Width=30;systemPlay.Height=30;systemPlay.Padding=new Thickness(0);RoundTransport(systemPlay);transport.Children.Add(systemPlay);
  systemNext=Btn("",()=>SendMedia("next"),"Próxima");systemNext.Content=TransportIcon("next");transport.Children.Add(systemNext);info.Children.Add(transport);lastCover="";UpdateSystemMusic();UpdateLyrics();
 }
 static TextBlock mediaDuration;static Image coverImage;static Border coverCard;
 static void UpdateSystemMusic(){
  if(page!="music"||!useSystem||mediaArtist==null)return;

  mediaArtist.Text=media.available?(media.playing?"TOCANDO":"PAUSADO")+" · "+(String.IsNullOrWhiteSpace(media.album)?(media.source??""):media.album).ToUpperInvariant():"MÍDIA";mediaArtist.Foreground=glassTint.HasValue?new SolidColorBrush(glassTint.Value):B("#CFC8C2");mediaArtist.FontWeight=FontWeights.SemiBold;coverCard.Opacity=media.playing?1:.6;
  mediaHint.Text=media.available?(media.album??media.source??""):media.artist??"Reproduza áudio ou vídeo em um aplicativo ou navegador compatível.";
  mediaHint.ToolTip=media.error;systemPlay.Content=TransportIcon(media.playing?"pause":"play",true);systemPlay.IsEnabled=media.available&&media.canToggle;systemNext.IsEnabled=media.available&&media.canNext;systemPrevious.IsEnabled=media.available&&media.canPrevious;systemSeek.IsEnabled=media.available&&media.canSeek&&media.duration>0;
  double position=MediaPosition();elapsed.Text=Format(position);mediaDuration.Text="−"+Format(media.duration-position);if(!seeking)systemSeek.Value=media.duration>0?Math.Max(0,Math.Min(100,position/media.duration*100)):0;
  string cover=media.available?(media.cover??""):"";if(cover!=lastCover){lastCover=cover;albumImage.Source=Landscape();backgroundArtwork.Source=Landscape();GlassTint(null);try{if(File.Exists(cover)){var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.DecodePixelWidth=480;bmp.UriSource=new Uri(cover);bmp.EndInit();bmp.Freeze();albumImage.Source=bmp;var tiny=new BitmapImage();tiny.BeginInit();tiny.CacheOption=BitmapCacheOption.OnLoad;tiny.DecodePixelWidth=32;tiny.UriSource=new Uri(cover);tiny.EndInit();tiny.Freeze();backgroundArtwork.Source=tiny;Tint(bmp);}}catch{}}
 }
 static void Tint(BitmapSource source){
  var small=new TransformedBitmap(source,new ScaleTransform(24.0/source.PixelWidth,24.0/source.PixelHeight));var rgba=new FormatConvertedBitmap(small,PixelFormats.Bgra32,null,0);int stride=rgba.PixelWidth*4;var pixels=new byte[stride*rgba.PixelHeight];rgba.CopyPixels(pixels,stride,0);long r=0,g=0,b=0,total=0;for(int i=0;i<pixels.Length;i+=4){int weight=Math.Max(1,Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2]))-Math.Min(pixels[i],Math.Min(pixels[i+1],pixels[i+2])));r+=pixels[i+2]*weight;g+=pixels[i+1]*weight;b+=pixels[i]*weight;total+=weight;}Color c=Color.FromArgb(255,(byte)(r/total),(byte)(g/total),(byte)(b/total));
  double mean=(c.R+c.G+c.B)/3.0;Func<double,byte> vivid=v=>(byte)Math.Max(0,Math.Min(255,mean+(v-mean)*1.55));Color vc=Color.FromRgb(vivid(c.R),vivid(c.G),vivid(c.B));double peak=Math.Max(vc.R,Math.Max(vc.G,vc.B));double lift=peak<200&&peak>0?200.0/peak:1;Color lit=Color.FromRgb((byte)Math.Min(255,vc.R*lift),(byte)Math.Min(255,vc.G*lift),(byte)Math.Min(255,vc.B*lift));GlassTint(lit);CoverColor(lit);
 }
 static LinearGradientBrush glassBrush;
 static Color[] GlassColors(Nullable<Color> tint){
  Color black=Color.FromRgb(5,5,6);return new Color[]{black,black,black};
 }
 static LinearGradientBrush NeutralGlass(){
  Color[] c=GlassColors(null);glassBrush=new LinearGradientBrush(new GradientStopCollection{new GradientStop(c[0],0),new GradientStop(c[1],.5),new GradientStop(c[2],1)},90);return glassBrush;
 }
 static Nullable<Color> glassTint;
 static void GlassTint(Nullable<Color> tint){
  glassTint=tint;
  Color[] c=GlassColors(tint);
  for(int i=0;i<3;i++){var stop=glassBrush.GradientStops[i];if(SystemParameters.ClientAreaAnimation)stop.BeginAnimation(GradientStop.ColorProperty,new System.Windows.Media.Animation.ColorAnimation(c[i],TimeSpan.FromMilliseconds(600)){EasingFunction=new System.Windows.Media.Animation.CubicEase()});else{stop.BeginAnimation(GradientStop.ColorProperty,null);stop.Color=c[i];}}
 }
 static void UpdateAgent(){
  if(!On("geral")||agentStatus==null)return;
  string path=System.IO.Path.Combine(dataDir,"agent.json");if(!File.Exists(path)){agentDetail.Text="Conecte uma sessão para acompanhar eventos reais.";return;}
  DateTime agentStamp=File.GetLastWriteTimeUtc(path);if(agentStamp==lastAgentStamp&&ReferenceEquals(lastAgentUi,agentStatus))return;lastAgentStamp=agentStamp;lastAgentUi=agentStatus;
  try{var e=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(path));string ev=e.ContainsKey("event")?e["event"]:"";string tool=e.ContainsKey("tool")?e["tool"]:"";
   agentStatus.Text=ev=="Stop"?"Concluído":ev=="Notification"?"Aguardando sua atenção":ev=="SessionEnd"?"Sessão encerrada":ev=="PreToolUse"?"Executando · "+tool:ev=="PostToolUse"?"Ferramenta concluída · "+tool:ev=="UserPromptSubmit"?"Trabalhando":"Sessão conectada";
   agentDetail.Text=(e.ContainsKey("project")?e["project"]:"")+(e.ContainsKey("file")?"  /  "+e["file"]:"");DateTime stamp;if(DateTime.TryParse(e["updated"],out stamp))agentAge.Text="Último evento às "+stamp.ToLocalTime().ToString("HH:mm:ss");
   if(lastAgent!=e["updated"]){if(lastAgent!=""){if(ev=="Stop"){excitedUntil=DateTime.UtcNow.AddSeconds(4);agentBusyUntil=DateTime.MinValue;}else if(ev=="Notification")surprisedUntil=DateTime.UtcNow.AddMilliseconds(1500);else if(ev=="PreToolUse"||ev=="PostToolUse"||ev=="UserPromptSubmit")agentBusyUntil=DateTime.UtcNow.AddSeconds(8);}if(ev=="Stop"&&lastAgent!="")if(!contextHidden&&!doNotDisturb)System.Media.SystemSounds.Asterisk.Play();lastAgent=e["updated"];}
  }catch{}
 }
 static void PollIntegrations(){
  if(++poll%4!=0)return;
  try{string path=System.IO.Path.Combine(dataDir,"media.json");if(File.Exists(path)){DateTime written=File.GetLastWriteTimeUtc(path);if(written!=lastMediaStamp){lastMediaStamp=written;media=json.Deserialize<MediaState>(File.ReadAllText(path));}DateTime stamp;if(!DateTime.TryParse(media.updated,out stamp)||DateTime.UtcNow-stamp.ToUniversalTime()>TimeSpan.FromSeconds(12)){media.available=false;media.playing=false;media.cover="";media.artist="A conexão de mídia foi interrompida.";}UpdateSystemMusic();}}
  catch{}

  UpdateAgent();UpdateDashboard();
 }
 static bool uiTest;
 static void StartMediaBridge(){
  Directory.CreateDirectory(dataDir);WriteHookSettings();if(!uiTest)InstallUserHooks();string modelFile=System.IO.Path.Combine(dataDir,"model.txt");if(File.Exists(modelFile))localModel=File.ReadAllText(modelFile).Trim();else if(!uiTest)AutoPickModel();MotorKillStale();leaveTimer.Tick+=(s,e)=>{leaveTimer.Stop();if(!w.IsMouseOver&&w.IsEnabled&&Mouse.Captured==null&&!dropHover&&!pinOpen)Expand(false);};
  try{var info=new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+System.IO.Path.Combine(baseDir,"MediaBridge.ps1")+"\" -OwnerPid "+Process.GetCurrentProcess().Id){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};mediaWorker=Process.Start(info);}catch(Exception ex){media.error=ex.Message;}
 }
 static void WriteHookSettings(){
  var hooks=new Dictionary<string,object>();string command="powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+System.IO.Path.Combine(baseDir,"ClaudeHook.ps1").Replace("\\","/")+"\"";
  foreach(var ev in new[]{"SessionStart","SessionEnd","UserPromptSubmit","PreToolUse","PostToolUse","Notification","Stop"})hooks[ev]=new[]{new{matcher="",hooks=new[]{new{type="command",command=command,timeout=5}}}};
  File.WriteAllText(System.IO.Path.Combine(baseDir,"claude-settings.json"),json.Serialize(new{hooks=hooks}),new System.Text.UTF8Encoding(false));
 }
 // Instala os hooks em ~/.claude/settings.json sem apagar o resto; troca entradas antigas do ClaudeHook.ps1 e guarda backup.
 static void InstallUserHooks(){
  try{
   string file=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","settings.json");Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
   var root=new Dictionary<string,object>();if(File.Exists(file)){string text=File.ReadAllText(file);if(text.Trim().Length>0)root=json.Deserialize<Dictionary<string,object>>(text);if(!File.Exists(file+".tito-backup"))File.Copy(file,file+".tito-backup");}
   var ours=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(System.IO.Path.Combine(baseDir,"claude-settings.json")));
   var mine=(Dictionary<string,object>)ours["hooks"];var hooks=root.ContainsKey("hooks")?root["hooks"] as Dictionary<string,object>:null;if(hooks==null)hooks=new Dictionary<string,object>();
   foreach(var ev in mine.Keys){var list=new List<object>();var old=hooks.ContainsKey(ev)?hooks[ev] as System.Collections.IEnumerable:null;if(old!=null)foreach(var item in old)if(!json.Serialize(item).Contains("ClaudeHook.ps1"))list.Add(item);foreach(var item in (System.Collections.IEnumerable)mine[ev])list.Add(item);hooks[ev]=list;}
   root["hooks"]=hooks;string output=json.Serialize(root);if(!File.Exists(file)||File.ReadAllText(file)!=output)File.WriteAllText(file,output,new System.Text.UTF8Encoding(false));
  }catch{}
 }
 public static void Test(){
  SetKeepAwake(true);if((SetThreadExecutionState(0x80000003u)&3)!=3)throw new Exception("Keep awake was not applied");SetKeepAwake(false);PainelSelfCheck();
  var descriptor=new byte[4+592];BitConverter.GetBytes(1).CopyTo(descriptor,0);System.Text.Encoding.Unicode.GetBytes("foto.jpg").CopyTo(descriptor,4+72);if(DescriptorNames(descriptor).Length!=1||DescriptorNames(descriptor)[0]!="foto.jpg"||SafeName("a/b:c.png")!="a_b_c.png")throw new Exception("Dropped file names");
  var virtualDrop=new DataObject();virtualDrop.SetData("FileGroupDescriptorW",new MemoryStream(descriptor));virtualDrop.SetData("FileContents",new MemoryStream(new byte[]{1,2,3,4}));shelf.Clear();ReceiveDrop(virtualDrop);
  if(shelf.Count!=1||shelf[0].Files==null||!File.ReadAllBytes(shelf[0].Files[0]).SequenceEqual(new byte[]{1,2,3,4}))throw new Exception("Virtual file drop");Forget(shelf[0]);
  if(!DesktopContext.IsFullscreen(true,true,false,1)||DesktopContext.IsFullscreen(true,true,true,1)||!DesktopContext.IsFullscreen(false,false,false,3)||DesktopContext.IsFullscreen(false,true,true,1)||!DesktopContext.IsFullscreen(true,false,true,1))throw new Exception("Fullscreen rule");
  Directory.CreateDirectory(dataDir);if(Format(1500)!="25:00"||Format(-1)!="00:00"||Format(3600)!="60:00")throw new Exception("Timer roundtrip");Build();CheckExperience();CheckThemes();Switch("focus");start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(!running)throw new Exception("Timer start failed");start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(running||remaining<1490||remaining>1500)throw new Exception("Timer pause failed");remaining=1500;var saved=media;media=new MediaState{available=true};SendMedia("seek",42.5);var command=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(System.IO.Path.Combine(dataDir,"media-command.json")));if(Convert.ToDouble(command["position"])!=42.5)throw new Exception("Media command failed");media=saved;
  foreach(string p in new[]{"home","music","agent","focus","robo","settings"}){Switch(p);Capture(p);}
  VisualChecks();
  SetPose(false,false);if(w.ShowInTaskbar||islandClip.Rect.Width!=128||islandClip.Rect.Height!=34||glass.Visibility!=Visibility.Collapsed)throw new Exception("Mascot rest pose");restingShell.Measure(new Size(128,34));restingShell.Arrange(new Rect(0,0,128,34));restingShell.UpdateLayout();var restBitmap=new RenderTargetBitmap(128,34,96,96,PixelFormats.Pbgra32);restBitmap.Render(restingShell);var restEncoder=new PngBitmapEncoder();restEncoder.Frames.Add(BitmapFrame.Create(restBitmap));using(var f=File.Create(System.IO.Path.Combine(dataDir,"preview-rest.png")))restEncoder.Save(f);if(!DesktopContext.CoversMonitor(new DesktopContext.Rect{Left=0,Top=0,Right=1920,Bottom=1080},new DesktopContext.Rect{Left=0,Top=0,Right=1920,Bottom=1080})||DesktopContext.CoversMonitor(new DesktopContext.Rect{Left=0,Top=0,Right=1920,Bottom=1040},new DesktopContext.Rect{Left=0,Top=0,Right=1920,Bottom=1080}))throw new Exception("Fullscreen classification");WriteHookSettings();File.WriteAllText(System.IO.Path.Combine(dataDir,"test-result.txt"),"PASS: six panels, timer start/pause, media command, hook config, mascot 128x34, switcher disabled, fullscreen rectangle classification. "+DateTime.UtcNow.ToString("o"));w.Close();
 }
 [STAThread] public static void Main(string[] args){
  try{DesktopContext.SetCurrentProcessExplicitAppUserModelID("Tito.Desktop");Environment.SetEnvironmentVariable("TITO_DATA",dataDir);Directory.CreateDirectory(dataDir);if(args.Contains("--test")){Test();return;}if(args.Contains("--motor-test")){Environment.ExitCode=MotorSelfTest();return;}int mi=Array.IndexOf(args,"--monitor"),oi=Array.IndexOf(args,"--owner");secondary=mi>=0&&mi+1<args.Length&&Int32.TryParse(args[mi+1],out selectedMonitor);if(secondary){suffix="m"+selectedMonitor;if(oi<0||oi+1>=args.Length||!Int32.TryParse(args[oi+1],out ownerPid))return;try{ownerProc=Process.GetProcessById(ownerPid);}catch{return;}}bool created;using(var mutex=new Mutex(!secondary,"Local\\TitoGlassDesktopFinal"+suffix,out created)){if(secondary){try{if(!mutex.WaitOne(4000))return;}catch(AbandonedMutexException){}}else if(!created){try{using(var signal=System.Threading.EventWaitHandle.OpenExisting("Local\\TitoGlassOpen")){signal.Set();}}catch{}return;}labMode=args.Contains("--lab-frames")?2:args.Contains("--lab")?1:0;uiTest=args.Contains("--ui-test")||labMode>0;Run();}}
  catch(Exception ex){try{File.WriteAllText(System.IO.Path.Combine(dataDir,"startup-error.txt"),ex.ToString());}catch{}Environment.ExitCode=1;if(!args.Contains("--test"))MessageBox.Show("Não foi possível abrir a Tito.\n"+ex.Message,"Tito Glass");}
 }
}
}
