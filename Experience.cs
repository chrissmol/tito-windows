using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
namespace Tito {
public partial class App {
 static bool doNotDisturb,dropHover,compactWide,saveChat=true,clipOn=true,shareContext=true;static double compactWidth=176;static string pillKey="";static int selectedMonitor;static TextBlock compactStatus;static bool focusWasRunning;static DateTime celebrateUntil;
 static MascotEyes headerMascot;static DateTime lastMascotInteraction=DateTime.UtcNow,gazeUntil,surprisedUntil;static Point lastGaze;static bool mediaWasPlaying;
 static DesktopAudio.PeakMonitor outputMeter;static System.Windows.Threading.DispatcherTimer waveTimer;static bool waveWasActive;
 sealed class ShelfItem {public string Name,Text,Folder;public string[] Files;public BitmapSource Image;public DateTime Added=DateTime.UtcNow;}
 static List<ShelfItem> shelf=new List<ShelfItem>();const int ShelfLimit=12;
 static Rect MonitorArea(){var screens=System.Windows.Forms.Screen.AllScreens;var s=screens[Math.Max(0,Math.Min(selectedMonitor,screens.Length-1))];var a=s.WorkingArea;var source=PresentationSource.FromVisual(w);var transform=source==null?Matrix.Identity:source.CompositionTarget.TransformFromDevice;var origin=transform.Transform(new Point(a.Left,a.Top));var size=transform.Transform(new Vector(a.Width,a.Height));return new Rect(origin.X,origin.Y,size.X,size.Y);}
 static void SetupExperience(){
  if(!uiTest){outputMeter=new DesktopAudio.PeakMonitor();waveTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};waveTimer.Tick+=(s,e)=>{bool active=!contextHidden&&(page=="music"||(!opened&&media.playing));outputMeter.Enabled=active;bool visible=page=="music"||(!opened&&media.playing);mascot.MediaMode=visible;mascot.ToolTip=visible?(outputMeter.Available?"Nível real da saída padrão · mistura de todos os aplicativos":"Medição de áudio indisponível · "+outputMeter.Error):"Tito";if(headerMascot!=null)headerMascot.MediaMode=page=="music";Color waves=glassTint.HasValue?glassTint.Value:Color.FromRgb(83,190,255);mascot.WaveColor=waves;if(headerMascot!=null)headerMascot.WaveColor=waves;double peak=active&&outputMeter.Available?outputMeter.Level:0;bool busy=active||waveWasActive||mascot.MediaBlend>.001||(headerMascot!=null&&headerMascot.MediaBlend>.001);if(busy){mascot.AudioPeak(peak);if(headerMascot!=null)headerMascot.AudioPeak(peak);}waveWasActive=active;waveTimer.Interval=TimeSpan.FromMilliseconds(busy?50:250);};waveTimer.Start();w.Closed+=(s,e)=>{waveTimer.Stop();outputMeter.Dispose();};}
  w.PreviewMouseMove+=(s,e)=>lastMascotInteraction=DateTime.UtcNow;w.PreviewKeyDown+=(s,e)=>lastMascotInteraction=DateTime.UtcNow;
  if(!secondary)try{File.Delete(SharedPath());Directory.Delete(Path.Combine(dataDir,"shelf"),true);}catch{}
  if(!secondary)selectedMonitor=Math.Max(0,Array.FindIndex(System.Windows.Forms.Screen.AllScreens,x=>x.Primary));
  try{string path=Path.Combine(dataDir,"experience.json");if(File.Exists(path)){var prefs=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));Func<string,bool,bool> get=(k,d)=>prefs.ContainsKey(k)?Convert.ToBoolean(prefs[k]):d;doNotDisturb=get("quiet",false);saveChat=get("saveChat",true);clipOn=get("clip",true);shareContext=get("context",true);pinOpen=get("pin",false);clickOnly=get("clickOnly",false);summaryOn=get("summary",true);if(prefs.ContainsKey("summaryAt"))summaryAt=Convert.ToString(prefs["summaryAt"]);if(prefs.ContainsKey("hotkey"))hotkeyKey=Convert.ToString(prefs["hotkey"]);calOn=get("cal",true);winOn=get("windows",true);notifOn=get("notif",true);pcOn=get("pc",true);Func<string,int,int> num=(k,d)=>prefs.ContainsKey(k)?Convert.ToInt32(prefs[k]):d;cpuLimit=num("cpuLimit",92);memLimit=num("memLimit",90);battLimit=num("battLimit",30);}string source=Path.Combine(dataDir,"media-source.txt");if(File.Exists(source))selectedSource=File.ReadAllText(source).Trim();}catch{}
  if(!secondary&&saveChat)LoadChat();
  w.Closed+=(s,e)=>SavePrefs();
  var face=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
  mascot.HorizontalAlignment=HorizontalAlignment.Center;restingShell.Child=null;face.Children.Add(mascot);compactStatus=T("",12);compactStatus.Margin=new Thickness(8,0,0,0);compactStatus.Visibility=Visibility.Collapsed;face.Children.Add(compactStatus);compactFace=face;compactCoverImg=new Image{Stretch=Stretch.UniformToFill,Width=22,Height=22,Clip=new RectangleGeometry(new Rect(0,0,22,22),7,7)};compactCover=new Border{Width=22,Height=22,CornerRadius=new CornerRadius(7),Margin=new Thickness(0,0,9,0),Background=B("#22FFFFFF"),Child=compactCoverImg,Visibility=Visibility.Collapsed};face.Children.Insert(0,compactCover);restingShell.Child=face;
  w.AllowDrop=true;w.PreviewDragOver+=(s,e)=>{bool valid=e.Data.GetDataPresent(DataFormats.FileDrop)||HasVirtualFiles(e.Data)||e.Data.GetDataPresent(DataFormats.UnicodeText)||e.Data.GetDataPresent(DataFormats.Bitmap);e.Effects=valid?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;if(valid){dropHover=true;leaveTimer.Stop();if(page!="shelf")Switch("shelf");if(!opened)Expand(true);}};
  w.DragLeave+=(s,e)=>{dropHover=false;leaveTimer.Start();};w.Drop+=(s,e)=>{dropHover=false;ReceiveDrop(e.Data);Switch("shelf");e.Handled=true;};
  restingShell.MouseLeftButtonUp+=(s,e)=>Expand(true);
 }
 static void ReceiveDrop(IDataObject data){
  var item=new ShelfItem();
  if(data.GetDataPresent(DataFormats.FileDrop)){item.Files=(string[])data.GetData(DataFormats.FileDrop);if(item.Files==null||item.Files.Length==0)return;item.Name=item.Files.Length==1?Path.GetFileName(item.Files[0]):item.Files.Length+" arquivos";}
  else if(HasVirtualFiles(data)&&ReceiveVirtualFiles(data,item)){}
  else if(data.GetDataPresent(DataFormats.Bitmap)){var bitmap=data.GetData(DataFormats.Bitmap) as BitmapSource;if(bitmap==null)return;if((long)bitmap.PixelWidth*bitmap.PixelHeight>32000000){error="Imagem muito grande. Arraste o arquivo da imagem.";ShowError();return;}item.Image=bitmap.Clone();item.Image.Freeze();item.Name="Imagem";}
  else if(data.GetDataPresent(DataFormats.UnicodeText)){item.Text=Convert.ToString(data.GetData(DataFormats.UnicodeText));if(String.IsNullOrWhiteSpace(item.Text))return;if(item.Text.Length>1000000){error="Texto muito grande para a prateleira.";ShowError();return;}item.Name=item.Text.Replace("\r"," ").Replace("\n"," ");if(item.Name.Length>48)item.Name=item.Name.Substring(0,48)+"…";}else return;
  shelf.Insert(0,item);if(shelf.Count>ShelfLimit)Forget(shelf[shelf.Count-1]);lastMascotInteraction=DateTime.UtcNow;surprisedUntil=DateTime.UtcNow.AddMilliseconds(450);celebrateUntil=DateTime.UtcNow.AddSeconds(2.5);if(item.Files!=null&&item.Files.Length==1)FlowStart(item);
 }
 static DataObject ShelfData(ShelfItem item){var d=new DataObject();if(item.Files!=null){d.SetData(DataFormats.FileDrop,item.Files);if(item.Files.Length==1&&new[]{".png",".jpg",".jpeg",".bmp",".gif"}.Contains(Path.GetExtension(item.Files[0]).ToLowerInvariant()))try{var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;using(var metadata=System.Drawing.Image.FromFile(item.Files[0])){if(metadata.Width>4096)image.DecodePixelWidth=4096;}image.UriSource=new Uri(item.Files[0]);image.EndInit();image.Freeze();d.SetData(DataFormats.Bitmap,image);}catch{}}else if(item.Image!=null)d.SetData(DataFormats.Bitmap,item.Image);else d.SetText(item.Text);return d;}
 static void Shelf(){
  bool mail=flowState==3&&flowItem!=null&&shelf.Contains(flowItem);ScrollViewer sc;
  Button action=mail?Btn("‹ Voltar",()=>{flowState=2;Switch("shelf");}):shelf.Count==0?null:Btn("Limpar",()=>{var all=shelf.ToArray();if(all.Length==0)return;shelf.Clear();Switch("shelf");Undoable("Prateleira limpa",()=>shelf.AddRange(all),()=>{foreach(var x in all)Forget(x);});},"Esvazia a prateleira (dá para desfazer)");
  var rows=Panel(mail?"Enviar por e-mail":"Prateleira temporária",out sc,action);
  var flowCard=FlowCard();if(flowCard!=null)rows.Children.Add(flowCard);
  if(mail){}else if(shelf.Count==0||dropHover){rows.Children.Add(DropZone());if(shelf.Count==0){var limit=T("Até 12 itens · expiram em 1 hora",Theme.Caption,Theme.Muted);limit.Margin=new Thickness(8,8,0,0);rows.Children.Add(limit);}}
  if(!mail)foreach(var item in shelf.ToArray()){var captured=item;var row=Stack(Orientation.Horizontal);var copy=Btn(item.Name,()=>{Clipboard.SetDataObject(ShelfData(captured),true);celebrateUntil=DateTime.UtcNow.AddSeconds(2);},"Copiar para colar em outro aplicativo");copy.MaxWidth=440;copy.PreviewMouseMove+=(s,e)=>{if(e.LeftButton==MouseButtonState.Pressed&&e.GetPosition(copy).Y>copy.ActualHeight)DragDrop.DoDragDrop(copy,ShelfData(captured),DragDropEffects.Copy);};row.Children.Add(copy);if(item.Text!=null)row.Children.Add(Btn("Resumir",()=>{string t="Resuma este texto em poucas linhas:\n"+(captured.Text.Length>6000?captured.Text.Substring(0,6000):captured.Text);chatDraft=t;Switch("robo");if(chatInput!=null)chatInput.Text=t;},"Leva o texto para o chat; só vai para a IA quando você apertar Enter"));row.Children.Add(Btn("×",()=>{int at=shelf.IndexOf(captured);shelf.Remove(captured);Switch("shelf");Undoable("Item removido",()=>shelf.Insert(Math.Min(at,shelf.Count),captured),()=>Forget(captured));},"Remover"));rows.Children.Add(row);}
 }
 static void AudioPanel(){
  ScrollViewer sc;var root=Panel("Áudio",out sc);
  try{var outputs=DesktopAudio.Outputs();var picker=new ComboBox{ItemsSource=outputs,SelectedItem=outputs.FirstOrDefault(x=>x.Current),Margin=new Thickness(0,10,0,8),MaxWidth=500,HorizontalAlignment=HorizontalAlignment.Left};picker.SelectionChanged+=(s,e)=>{var output=picker.SelectedItem as DesktopAudio.Output;if(output!=null)try{DesktopAudio.Select(output.Id);}catch(Exception ex){error="Não foi possível trocar a saída. "+ex.Message;ShowError();}};root.Children.Add(picker);float volume=DesktopAudio.Volume(null);var label=T("Volume · "+Math.Round(volume*100)+"%",13);root.Children.Add(label);var slider=new Slider{Minimum=0,Maximum=100,Value=volume*100,Margin=new Thickness(0,10,0,0),Width=470,HorizontalAlignment=HorizontalAlignment.Left};slider.ValueChanged+=(s,e)=>{try{float actual=DesktopAudio.Volume((float)(slider.Value/100));label.Text="Volume · "+Math.Round(actual*100)+"%";}catch(Exception ex){label.Text="Áudio indisponível: "+ex.Message;}};root.Children.Add(slider);}
  catch(Exception ex){root.Children.Add(T("Áudio indisponível: "+ex.Message,12));}
 }
 static void SourceMenu(){var menu=new ContextMenu();var automatic=new MenuItem{Header="Automático",IsCheckable=true,IsChecked=String.IsNullOrEmpty(selectedSource)};automatic.Click+=(s,e)=>SelectSource("");menu.Items.Add(automatic);foreach(var source in media.sessions??new MediaSource[0]){var entry=source;var item=new MenuItem{Header=(entry.playing?"▶ ":"")+(entry.title??"Mídia")+" · "+entry.source,IsCheckable=true,IsChecked=selectedSource==entry.source};item.Click+=(s,e)=>SelectSource(entry.source);menu.Items.Add(item);}menu.IsOpen=true;}
 static string selectedSource="";static string chatDraft="";
 static void SelectSource(string source){selectedSource=source;File.WriteAllText(Path.Combine(dataDir,"media-source.txt"),source);}
 static string syncSnap="";static DateTime syncStamp=DateTime.MinValue;
 // Chat history survives restarts (shared.json is reset at startup, so it lives in its own file).
 static string ChatPath(){return Path.Combine(dataDir,"chat.json");}
 static void LoadChat(){try{if(!File.Exists(ChatPath()))return;var list=new List<Dictionary<string,string>>();foreach(var o in (System.Collections.IEnumerable)json.Deserialize<List<object>>(File.ReadAllText(ChatPath()))){var m=(Dictionary<string,object>)o;list.Add(new Dictionary<string,string>{{"role",""+m["role"]},{"content",""+m["content"]}});}conversation=list.Count>100?list.GetRange(list.Count-100,100):list;}catch{}}
 static void SavePrefs(){if(secondary)return;try{File.WriteAllText(Path.Combine(dataDir,"experience.json"),json.Serialize(new{monitor=selectedMonitor,quiet=doNotDisturb,saveChat=saveChat,clip=clipOn,context=shareContext,pin=pinOpen,clickOnly=clickOnly,summary=summaryOn,summaryAt=summaryAt,hotkey=hotkeyKey,cal=calOn,windows=winOn,notif=notifOn,pc=pcOn,cpuLimit=cpuLimit,memLimit=memLimit,battLimit=battLimit}));}catch{}}
 static string SharedPath(){return Path.Combine(dataDir,"shared.json");}
 static string Snap(){return running+"|"+(running?end.Ticks:(long)Math.Round(remaining))+"|"+doNotDisturb+"|"+conversation.Count+"|"+(conversation.Count>0?conversation[conversation.Count-1]["content"].GetHashCode():0);}
 static void SyncTick(){
  try{
   string path=SharedPath();DateTime stamp=File.Exists(path)?File.GetLastWriteTimeUtc(path):DateTime.MinValue;
   if(stamp!=syncStamp&&stamp!=DateTime.MinValue){
    var d=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));bool was=running;int count=conversation.Count;
    running=Convert.ToBoolean(d["running"]);remaining=Convert.ToDouble(d["remaining"]);end=new DateTime(Convert.ToInt64(d["end"]),DateTimeKind.Utc);doNotDisturb=Convert.ToBoolean(d["dnd"]);
    var list=new List<Dictionary<string,string>>();foreach(var o in (System.Collections.IEnumerable)d["conversation"]){var m=(Dictionary<string,object>)o;list.Add(new Dictionary<string,string>{{"role",""+m["role"]},{"content",""+m["content"]}});}
    conversation=list;syncStamp=stamp;syncSnap=Snap();
    if((On("chat")&&list.Count!=count)||(page=="focus"&&was!=running))Render();
   }else if(Snap()!=syncSnap){
    File.WriteAllText(path,json.Serialize(new Dictionary<string,object>{{"running",running},{"remaining",remaining},{"end",end.Ticks},{"dnd",doNotDisturb},{"conversation",conversation}}));if(saveChat&&!secondary)try{SafeWrite(ChatPath(),json.Serialize(conversation));}catch{}syncStamp=File.GetLastWriteTimeUtc(path);syncSnap=Snap();
   }
  }catch{}
 }
 static void ExperienceTick(){
  SyncTick();FocusTick();CpuTick();ClipboardTick();PainelPoll();AgendaReminderTick();SummaryTick();CalTick();
  if(focusWasRunning&&!running&&remaining<=0)excitedUntil=DateTime.UtcNow.AddSeconds(5);focusWasRunning=running;
  var expired=shelf.Where(x=>DateTime.UtcNow-x.Added>TimeSpan.FromHours(1)).ToArray();foreach(var x in expired)Forget(x);bool removed=expired.Length>0;if(removed&&page=="shelf")Switch("shelf");
  if(mediaWasPlaying&&!media.playing)celebrateUntil=DateTime.UtcNow.AddSeconds(1);mediaWasPlaying=media.playing;
  var now=DateTime.UtcNow;if(chatWasBusy&&!chatBusy)chatDoneUntil=now.AddSeconds(1.5);chatWasBusy=chatBusy;string mood=now<dizzyUntil?"dizzy":dropHover||now<curiousUntil?"curious":now<surprisedUntil?"surprised":now<excitedUntil?"excited":now<loveUntil?"love":now<celebrateUntil||now<welcomeUntil||now<chatDoneUntil?"happy":chatBusy||flowState==1||now<agentBusyUntil?"thinking":painelHigh||cpuHigh||memHigh?"worry":batteryLow?"sleepy":running||now<typingUntil?"focus":media.playing?"music":now<winkUntil?"wink":DateTime.UtcNow-lastMascotInteraction>TimeSpan.FromSeconds(60)&&!keepAwake?"sleepy":"idle";
  if(lastMood=="sleepy"&&mood!="sleepy"&&mood!="dizzy"){surprisedUntil=now.AddMilliseconds(500);mood="surprised";} // acordou: abre os olhos com susto
  if(mood=="excited"&&lastMood!="excited")Confetti();lastMood=mood;
  if(mascot!=null){mascot.Mood=mood;mascot.InvalidateVisual();}if(headerMascot!=null)headerMascot.Mood=mood;
  if(compactStatus!=null){bool med=!running&&media.playing&&!String.IsNullOrEmpty(media.title);string text=running?Format(remaining):med?media.title:"";string battColor=null,battHex;string battText=BatteryText(out battHex),alertHex;string alert=PcAlert(out alertHex);if(alert!=null&&(battText==null||battHex==Theme.Ok)){battText=alert;battHex=alertHex;}if(text.Length==0&&battText!=null){text=battText;battColor=battHex;}compactStatus.MaxWidth=130;compactStatus.TextTrimming=TextTrimming.CharacterEllipsis;compactStatus.Text=text;compactStatus.Foreground=running?B("#BC83FF"):battColor!=null?B(battColor):Brushes.White;CapsuleMedia(med);compactStatus.Visibility=text.Length>0?Visibility.Visible:Visibility.Collapsed;bool wide=text.Length>0;double cw=med?270:running||battColor!=null?176:230;if(compactWide!=wide||(wide&&compactWidth!=cw)){compactWide=wide;compactWidth=cw;if(!opened)SetPose(false,true);}
  string key=media.playing&&glassTint.HasValue?"t"+glassTint.Value:"n";if(restingShell!=null&&key!=pillKey){pillKey=key;Color to=key=="n"?Color.FromRgb(5,5,6):Color.FromRgb((byte)(glassTint.Value.R*.62),(byte)(glassTint.Value.G*.62),(byte)(glassTint.Value.B*.62));var brush=new SolidColorBrush(restingShell.Background is SolidColorBrush?((SolidColorBrush)restingShell.Background).Color:to);restingShell.Background=brush;brush.BeginAnimation(SolidColorBrush.ColorProperty,new System.Windows.Media.Animation.ColorAnimation(to,TimeSpan.FromMilliseconds(450)));}}
 }
 static StackPanel compactFace;static Border compactCover;static Image compactCoverImg;static string compactCoverPath="";
 // Cápsula com mídia: capa pequena arredondada, título forte e artista discreto; os olhos (que seguem o áudio) ficam no canto direito.
 static void CapsuleMedia(bool med){
  if(compactFace==null)return;
  compactCover.Visibility=med&&!String.IsNullOrEmpty(media.cover)?Visibility.Visible:Visibility.Collapsed;
  bool eyesLast=compactFace.Children.IndexOf(mascot)>compactFace.Children.IndexOf(compactStatus);
  if(med!=eyesLast){compactFace.Children.Remove(mascot);compactFace.Children.Insert(med?compactFace.Children.IndexOf(compactStatus)+1:compactFace.Children.IndexOf(compactStatus),mascot);}
  compactStatus.Margin=med?new Thickness(0,0,10,0):new Thickness(8,0,0,0);compactStatus.MaxWidth=med?150:130;
  if(med){
   compactStatus.Inlines.Clear();compactStatus.Inlines.Add(new System.Windows.Documents.Run(media.title){FontWeight=FontWeights.SemiBold});
   if(!String.IsNullOrWhiteSpace(media.artist))compactStatus.Inlines.Add(new System.Windows.Documents.Run("  "+media.artist){FontSize=11,FontWeight=FontWeights.Normal,Foreground=B("#9E99A8")});
   string cover=media.cover??"";if(cover!=compactCoverPath){compactCoverPath=cover;compactCoverImg.Source=null;try{if(File.Exists(cover)){var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.DecodePixelWidth=64;bmp.UriSource=new Uri(cover);bmp.EndInit();bmp.Freeze();compactCoverImg.Source=bmp;}}catch{}}
  }
 }
 static void CheckExperience(){
  var wave=new MascotEyes{Width=36,Height=20,MediaMode=true};wave.BeginAnimation(MascotEyes.MediaBlendProperty,null);wave.MediaBlend=1;wave.AudioPeak(Double.NaN);wave.AudioPeak(Double.PositiveInfinity);foreach(double peak in new[]{0,.01,.04,.1,.4,.8,1.0})wave.AudioPeak(peak);wave.BeginAnimation(MascotEyes.WaveStepProperty,null);wave.WaveStep=1;var waveShell=new Border{Width=128,Height=34,Background=Brushes.Black,CornerRadius=new CornerRadius(17),Child=wave};wave.HorizontalAlignment=HorizontalAlignment.Center;wave.VerticalAlignment=VerticalAlignment.Center;waveShell.Measure(new Size(128,34));waveShell.Arrange(new Rect(0,0,128,34));waveShell.UpdateLayout();var waveBitmap=new RenderTargetBitmap(128,34,96,96,PixelFormats.Pbgra32);waveBitmap.Render(waveShell);SavePng(waveBitmap,"preview-mascot-media.png");
  var expressions=new HashSet<string>();foreach(string mood in new[]{"idle","curious","happy","focus","sleepy","surprised","love","wink","thinking","worry","excited","dizzy"}){var eyes=new MascotEyes{Width=36,Height=20,Mood=mood,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};eyes.BeginAnimation(MascotEyes.BlendProperty,null);eyes.Blend=1;var shell=new Border{Width=128,Height=34,Background=Brushes.Black,CornerRadius=new CornerRadius(17),Child=eyes};shell.Measure(new Size(128,34));shell.Arrange(new Rect(0,0,128,34));shell.UpdateLayout();var bitmap=new RenderTargetBitmap(128,34,96,96,PixelFormats.Pbgra32);bitmap.Render(shell);var pixels=new byte[128*34*4];bitmap.CopyPixels(pixels,128*4,0);using(var sha=System.Security.Cryptography.SHA256.Create())expressions.Add(Convert.ToBase64String(sha.ComputeHash(pixels)));SavePng(bitmap,"preview-mascot-"+mood+".png");}if(expressions.Count!=12)throw new Exception("Mascot expressions must be visually distinct");
  shelf.Clear();var text=new DataObject();text.SetText("Um texto temporário");ReceiveDrop(text);if(shelf.Count!=1||shelf[0].Text!="Um texto temporário")throw new Exception("Shelf text drop");
  var files=new DataObject(DataFormats.FileDrop,new[]{"C:\\sample.png"});ReceiveDrop(files);if(shelf[0].Files[0]!="C:\\sample.png")throw new Exception("Shelf file reference");
  for(int i=0;i<15;i++){var d=new DataObject();d.SetText("Item "+i);ReceiveDrop(d);}if(shelf.Count!=ShelfLimit||shelf[0].Text!="Item 14")throw new Exception("Shelf capacity/order");
  if(ShelfData(shelf[0]).GetText()!="Item 14")throw new Exception("Shelf clipboard payload");shelf.Clear();celebrateUntil=DateTime.MinValue;
  {string ad,at,tt;var n0=new DateTime(2026,11,19,10,0,0);if(!ParseAgenda("amanhã 9h30 Dentista",n0,out ad,out at,out tt)||ad!="2026-11-20"||at!="09:30"||tt!="Dentista"||!ParseAgenda("14:30 X",n0,out ad,out at,out tt)||ad!="2026-11-19"||ParseAgenda("25:00 X",n0,out ad,out at,out tt)||ParseAgenda("qualquer coisa",n0,out ad,out at,out tt))throw new Exception("Agenda parser");}
  {var cm=CommandMatches("lembrar amanhã 9h Dentista");if(cm.Count==0||cm[0].Key!="Criar lembrete · amanhã, 09:00 · Dentista")throw new Exception("Command box reminder");if(CommandMatches("xyzabc")[0].Key!="Perguntar ao Tito: xyzabc")throw new Exception("Command box fallback");}
  {var rr=ParseRoutines(DefaultRoutines);if(rr.Count!=2||rr[0][0]!="começar o dia"||rr[0][1]!="agenda ; foco 25"||rr[1][1]!="parar foco ; notas")throw new Exception("Routines parse");}
  CheckPdf();
  {string ra;if(RoboParse("Inicia um foco de 45 minutos",out ra)!="focus"||ra!="45"||RoboParse("anota: comprar leite",out ra)!="note"||ra!="comprar leite"||RoboParse("me lembra amanhã 9h Dentista",out ra)!="agenda"||ra!="amanhã 9h Dentista"||RoboParse("o que tenho hoje?",out ra)!="q:agenda"||RoboParse("pausa a música",out ra)!="play"||RoboParse("coloca o volume em 30%",out ra)!="vol"||ra!="30"||RoboParse("abre a calculadora",out ra)!="open"||ra!="calculadora"||RoboParse("quanta bateria eu tenho",out ra)!="q:battery"||RoboParse("conta uma piada",out ra)!=null)throw new Exception("Robo intents");}
  bool inter=false;foreach(var face in w.FontFamily.GetTypefaces()){GlyphTypeface glyph;if(face.TryGetGlyphTypeface(out glyph)&&glyph.FamilyNames.Values.Any(x=>x.IndexOf("Inter",StringComparison.OrdinalIgnoreCase)>=0)){inter=true;break;}}if(!inter)throw new Exception("Embedded Inter font did not resolve");
  foreach(string panel in new[]{"shelf","audio"}){Switch(panel);Capture(panel);}Switch("home");
 }
}
}
