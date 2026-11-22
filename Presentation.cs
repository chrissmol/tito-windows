using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace Tito {
public sealed class LyricLine {public double Time;public string Text;}
public static class Lrc {
 public static List<LyricLine> Parse(string content){
  var result=new List<LyricLine>();
  foreach(string raw in content.Split('\n')){
   var stamps=Regex.Matches(raw,@"\[(\d{1,3}):(\d{2}(?:[.,]\d{1,3})?)\]");
   if(stamps.Count==0)continue;
   string text=Regex.Replace(raw,@"\[[^\]]*\]","").Trim();
   if(text.Length==0)continue;
   foreach(Match stamp in stamps){double seconds=Double.Parse(stamp.Groups[2].Value.Replace(',','.'),CultureInfo.InvariantCulture);if(seconds>=60)continue;result.Add(new LyricLine{Time=Int32.Parse(stamp.Groups[1].Value)*60+seconds,Text=text});}
  }
  return result.OrderBy(x=>x.Time).ToList();
 }
 public static int At(List<LyricLine> lines,double position){int low=0,high=lines.Count-1,best=-1;while(low<=high){int mid=(low+high)/2;if(lines[mid].Time<=position){best=mid;low=mid+1;}else high=mid-1;}return best;}
}
public partial class App {
 static Image backgroundArtwork;static Border artworkShade;static Grid panelSurface;
 static TextBlock lyricBefore,lyricCurrent,lyricAfter;static string displayedLyric="";
 static List<LyricLine> lyrics=new List<LyricLine>();static string lyricsFor="";
 static int poseRevision;static double pendingSeek=-1;static DateTime pendingSeekUntil;
 static string TrackKey(){return (media.source??"")+"|"+(media.artist??"")+"|"+(media.title??"");}
 static double MediaPosition(){
  double position=media.position;DateTime stamp;
  if(media.playing&&DateTime.TryParse(media.updated,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out stamp))position+=Math.Max(0,Math.Min(3,(DateTime.UtcNow-stamp.ToUniversalTime()).TotalSeconds));
  if(pendingSeek>=0){if(DateTime.UtcNow<pendingSeekUntil&&Math.Abs(position-pendingSeek)>2)position=pendingSeek;else pendingSeek=-1;}
  return Math.Max(0,media.duration>0?Math.Min(media.duration,position):position);
 }
 static void SeekTo(double seconds){pendingSeek=seconds;pendingSeekUntil=DateTime.UtcNow.AddSeconds(2);SendMedia("seek",seconds);}
 static FrameworkElement TransportIcon(string name,bool dark=false){
  string path=name=="pause"?"M2,1 L6,1 6,15 2,15 Z M10,1 L14,1 14,15 10,15 Z":name=="previous"?"M1,2 L3,2 3,14 1,14 Z M14,2 L4,8 14,14 Z":name=="next"?"M13,2 L15,2 15,14 13,14 Z M2,2 L12,8 2,14 Z":"M3,1 L15,8 3,15 Z";
  return new System.Windows.Shapes.Path{Data=Geometry.Parse(path),Fill=dark?B("#242126"):B("#EEE8E1"),Stretch=Stretch.Uniform,Width=12,Height=12,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
 }
 static void RoundTransport(Button button){
  button.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' CornerRadius='100'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
 }
 static void QuickMenu(Button source){
  var menu=new ContextMenu();
  Action<string,Action> add=(label,action)=>{var item=new MenuItem{Header=label};item.Click+=(s,e)=>action();menu.Items.Add(item);};
  add("Abrir música local…",Choose);add("Carregar letra sincronizada (.lrc)…",LoadLyrics);
  add("Prateleira temporária",()=>Switch("shelf"));add("Volume e saída de áudio",()=>Switch("audio"));add("Novo foco",()=>Switch("focus"));add("Nova conversa",()=>{if(!chatBusy){conversation.Clear();Switch("robo");}});
  menu.PlacementTarget=source;menu.IsOpen=true;
 }
 static void LoadLyrics(){
  if(!media.available){error="Abra uma música no player do Windows antes de associar uma letra.";ShowError();return;}
  var dialog=new Microsoft.Win32.OpenFileDialog{Title="Letra sincronizada para "+media.title,Filter="Letras LRC|*.lrc"};
  if(dialog.ShowDialog(w)!=true)return;
  if(new FileInfo(dialog.FileName).Length>256*1024){error="A letra excede o limite de 256 KB.";ShowError();return;}
  var parsed=Lrc.Parse(File.ReadAllText(dialog.FileName));if(parsed.Count==0){error="O arquivo não contém linhas com marcações [mm:ss].";ShowError();return;}
  lyrics=parsed;lyricsFor=TrackKey();displayedLyric="";Switch("music");
 }
 static void UpdateLyrics(){
  if(page!="music"||!useSystem||lyricCurrent==null)return;
  bool has=lyrics.Count>0&&lyricsFor==TrackKey();int index=has?Lrc.At(lyrics,MediaPosition()):-1;
  string current=has?(index>=0?lyrics[index].Text:lyrics[0].Text):(String.IsNullOrWhiteSpace(media.title)?"Nada tocando agora":media.title);
  lyricBefore.Text=has&&index>0?lyrics[index-1].Text:"";
  lyricAfter.Text=has&&index>=0&&index+1<lyrics.Count?lyrics[index+1].Text:has?"":media.artist??"";
  lyricAfter.Foreground=has||!glassTint.HasValue?B("#88DAD1C9"):new SolidColorBrush(glassTint.Value);lyricAfter.FontWeight=has?FontWeights.Normal:FontWeights.SemiBold;
  lyricCurrent.Text=current;
  if(displayedLyric!=current){displayedLyric=current;lyricCurrent.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.45,1,TimeSpan.FromMilliseconds(200)));}
 }
 static BitmapSource Landscape(int width=600,int height=270){
  var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
   dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(22,121,177),Color.FromRgb(7,28,66),90),null,new Rect(0,0,width,height));
   var far=Geometry.Parse("M0,180 L130,95 230,150 350,65 470,155 600,95 600,300 0,300 Z");
   dc.PushTransform(new ScaleTransform(width/600.0,height/270.0));dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(16,75,113)),null,far);dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(11,47,83)),null,Geometry.Parse("M0,235 L160,130 310,220 450,130 600,230 600,300 0,300 Z"));dc.Pop();
   dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(205,224,218)),null,new Point(width*.69,height*.23),height*.22,height*.22);
  }
  var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bmp.Render(visual);bmp.Freeze();return bmp;
 }
 static void ConfigureArtwork(){
  bool visible=page=="music"&&useSystem;backgroundArtwork.Visibility=visible?Visibility.Visible:Visibility.Collapsed;artworkShade.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
 }
 static void Capture(string name){
  glass.BeginAnimation(UIElement.OpacityProperty,null);glass.Opacity=1;
  view.BeginAnimation(UIElement.OpacityProperty,null);view.Opacity=1;view.RenderTransform=null;
  glass.Width=600;glass.Height=270;glass.Measure(new Size(600,270));glass.Arrange(new Rect(0,0,600,270));glass.UpdateLayout();
  var bmp=new RenderTargetBitmap(600,270,96,96,PixelFormats.Pbgra32);bmp.Render(glass);var matte=new DrawingVisual();using(var dc=matte.RenderOpen()){dc.DrawRoundedRectangle(B("#101014"),null,new Rect(0,0,600,270),30,30);dc.DrawImage(bmp,new Rect(0,0,600,270));}var composited=new RenderTargetBitmap(600,270,96,96,PixelFormats.Pbgra32);composited.Render(matte);SavePng(composited,"preview-"+name+".png");
 }
 static void CaptureCapsule(string name){var shell=restingShell;double cw=compactWide?compactWidth:128;var host=(Panel)shell.Parent;int at=host.Children.IndexOf(shell);host.Children.Remove(shell);shell.Width=cw;shell.Measure(new Size(cw,34));shell.Arrange(new Rect(0,0,cw,34));shell.UpdateLayout();var bmp=new RenderTargetBitmap((int)cw*2,68,192,192,PixelFormats.Pbgra32);bmp.Render(shell);host.Children.Insert(at,shell);SavePng(bmp,"preview-"+name+".png");}
 static void SavePng(BitmapSource image,string name){var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(System.IO.Path.Combine(dataDir,name)))enc.Save(file);}
 static List<BitmapSource> morphFrames=new List<BitmapSource>();
 static void CaptureMorphFrame(int index){
  var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
   dc.DrawRectangle(B("#20252E"),null,new Rect(0,0,640,302));
   if(w.IsVisible){double width=Math.Max(1,islandClip.Rect.Width),height=Math.Max(1,islandClip.Rect.Height);var rect=new Rect((640-width)/2,12,width,height);dc.DrawRoundedRectangle(B("#101014"),null,rect,Math.Min(24,height/2),Math.Min(24,height/2));dc.DrawRectangle(new VisualBrush(island){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=islandClip.Rect,Stretch=Stretch.Fill},null,rect);}
   dc.DrawRectangle(new VisualBrush(confettiLayer){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top},null,new Rect(0,4,640,300));
  }
  var bmp=new RenderTargetBitmap(640,302,96,96,PixelFormats.Pbgra32);bmp.Render(visual);bmp.Freeze();morphFrames.Add(bmp);
 }
 static void SaveMorphFrames(){for(int i=0;i<morphFrames.Count;i++)SavePng(morphFrames[i],"morph/frame-"+i.ToString("000")+".png");morphFrames.Clear();}
 static void VisualChecks(){
  var parsed=Lrc.Parse("[00:01.20]First\n[00:03][00:05.500]Second\n[ar:Ignored]\n[00:77]Invalid");
  if(parsed.Count!=3||Lrc.At(parsed,.5)!=-1||Lrc.At(parsed,1.2)!=0||Lrc.At(parsed,5.5)!=2)throw new Exception("LRC parsing or timeline failed");
  var saved=media;var savedLyrics=lyrics;var savedKey=lyricsFor;var cover=System.IO.Path.Combine(dataDir,"fixture-cover.png");SavePng(Landscape(),"fixture-cover.png");
  media=new MediaState{available=true,playing=true,title="Blue Hours",artist="Tide Club",album="Night session",source="visual-test-fixture",cover=cover,position=42,duration=180,updated=DateTime.UtcNow.ToString("o"),canToggle=true,canNext=true,canPrevious=true,canSeek=true};
  lyrics=Lrc.Parse("[00:00]Lights settle on the skyline\n[00:35]A quiet moment, just for us\n[00:55]The city slows into the night");lyricsFor=TrackKey();
  foreach(string p in new[]{"home","music","focus"}){Switch(p);Capture("reference-"+p);}
  var savedChat=conversation;conversation=new List<Dictionary<string,string>>{new Dictionary<string,string>{{"role","user"},{"content","What is left tonight?"}},new Dictionary<string,string>{{"role","assistant"},{"content","One focus session, then time to rest."}}};Switch("robo");Capture("reference-robo");conversation=savedChat;
  var savedPainel=painel;var savedState=painelState;PainelSet(json.Deserialize<Dictionary<string,object>>(@"{""atividade"":{""2026-11-10"":{""claude"":394,""codex"":20},""2026-11-03"":{""claude"":120},""2026-10-20"":{""codex"":80},""2026-10-06"":{""claude"":946},""2026-09-29"":{""claude"":300,""codex"":629},""2026-11-16"":{""claude"":613}},""limites"":{""codex"":[{""nome"":""Sessão"",""pct"":13.0},{""nome"":""Semana"",""pct"":72.0}],""claude"":[{""nome"":""5 horas"",""pct"":93.0}]},""claude_uso"":{""h5"":12300000,""msgs5"":214,""semana"":88000000,""hoje"":9100000},""tokens_por_modelo"":[{""ia"":""claude"",""modelo"":""claude-sonnet-5-5"",""tokens"":125747485},{""ia"":""claude"",""modelo"":""claude-fable-5-2"",""tokens"":102482569},{""ia"":""codex"",""modelo"":""gpt-5.6-luna"",""tokens"":1722320}]}"));Switch("painel");Capture("reference-painel");painelState=0;Switch("painel");Capture("reference-painel-loading");painel=savedPainel;painelState=savedState;painelHigh=false;
  {restingShell.Visibility=Visibility.Visible;bool cwd=compactWide;double cwi=compactWidth;compactStatus.Text=media.title;compactStatus.Visibility=Visibility.Visible;compactWide=true;compactWidth=270;mascot.MediaMode=true;CapsuleMedia(true);CaptureCapsule("reference-capsule-media");mascot.MediaMode=false;compactStatus.Text="";compactStatus.Visibility=Visibility.Collapsed;compactWide=false;CapsuleMedia(false);CaptureCapsule("reference-capsule-idle");compactWide=cwd;compactWidth=cwi;restingShell.Visibility=Visibility.Collapsed;}
  media=saved;lyrics=savedLyrics;lyricsFor=savedKey;
  File.WriteAllText(System.IO.Path.Combine(dataDir,"preview-notes.txt"),"reference-* images use explicit visual test fixtures. They do not show live playback or a live AI response. Offscreen renders use a neutral dark matte; native desktop acrylic is not included.");
 }
}
}
