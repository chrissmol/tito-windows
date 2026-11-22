using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace Tito {
public partial class App {
 static TextBlock dashTrack,dashArtist,dashTimer,dashReady,dashAgent,dashCount;
 static DateTime agentStamp;static Dictionary<string,string> agentEvent;static Image dashImage;static string dashCover="";
 static Button dashPlay,dashStart;static ScrollViewer chatScroll;static StackPanel chatMessages;static TextBox chatInput;static bool chatBusy;
 static List<Dictionary<string,string>> conversation=new List<Dictionary<string,string>>();
 static string localModel="";static TextBlock modelStatus;static ComboBox models;
 static Border Card(string top,string bottom,double width){return new Border{CornerRadius=new CornerRadius(12),Background=new LinearGradientBrush(B(top).Color,B(bottom).Color,90),Width=width,Padding=new Thickness(12),Cursor=Cursors.Hand};}
 static void Dashboard(){
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(110)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(110)});view.Children.Add(grid);
  var agent=Card("#FFFF6B1A","#FFFFA63D",104);agent.HorizontalAlignment=HorizontalAlignment.Left;agent.MouseLeftButtonUp+=(s,e)=>Switch("agent");
  var a=new Grid();agent.Child=a;a.Children.Add(new TextBlock{Text="CLAUDE",FontSize=9,Foreground=B("#E6E0D7"),VerticalAlignment=VerticalAlignment.Top});
  var info=Stack();info.VerticalAlignment=VerticalAlignment.Bottom;dashCount=T("—",29);info.Children.Add(dashCount);dashAgent=T("Sem sessão",9,"#F0D2BE");info.Children.Add(dashAgent);a.Children.Add(info);grid.Children.Add(agent);
  var music=Card("#FF2BB0FF","#FF2146E0",Double.NaN);music.Margin=new Thickness(4,0,4,0);music.Padding=new Thickness(0);music.ClipToBounds=true;music.SizeChanged+=(s,e)=>music.Clip=new RectangleGeometry(new Rect(0,0,music.ActualWidth,music.ActualHeight),12,12);Grid.SetColumn(music,1);grid.Children.Add(music);
  var m=new Grid();music.Child=m;var moon=new Ellipse{Width=80,Height=80,Fill=B("#BEE7E4D8"),VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,-10,32,0)};m.Children.Add(moon);
  m.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M0,90 L65,43 120,80 185,20 270,73 330,43 340,160 0,160 Z"),Fill=B("#70202B50"),Stretch=Stretch.Fill,Height=85,VerticalAlignment=VerticalAlignment.Bottom});
  dashImage=new Image{Stretch=Stretch.UniformToFill};m.Children.Add(dashImage);m.Children.Add(new Border{Background=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Colors.Transparent,0),new GradientStop(Color.FromArgb(215,7,12,24),1)},90)});
  var text=Stack();text.VerticalAlignment=VerticalAlignment.Bottom;text.Margin=new Thickness(14,0,48,13);dashTrack=T("Nenhuma mídia",20);dashTrack.FontWeight=FontWeights.SemiBold;dashTrack.TextTrimming=TextTrimming.CharacterEllipsis;text.Children.Add(dashTrack);dashArtist=T("Abra seu player",10,"#E7E4E1");dashArtist.Margin=new Thickness(0,3,0,0);text.Children.Add(dashArtist);m.Children.Add(text);
  dashPlay=Btn("▶",()=>{if(media.available)SendMedia("toggle");else Switch("music");},"Reproduzir / pausar");RoundTransport(dashPlay);dashPlay.Width=30;dashPlay.Height=30;dashPlay.Padding=new Thickness(0);dashPlay.Background=B("#F5F1EB");dashPlay.Foreground=B("#25212B");dashPlay.HorizontalAlignment=HorizontalAlignment.Right;dashPlay.VerticalAlignment=VerticalAlignment.Bottom;dashPlay.Margin=new Thickness(0,0,13,14);m.Children.Add(dashPlay);
  music.MouseLeftButtonUp+=(s,e)=>{if(!(e.OriginalSource is Button)&&!IsButtonSource(e.OriginalSource as DependencyObject))Switch("music");};
  var focus=Card("#FF7B3DFF","#FFC05CFF",104);focus.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(focus,2);grid.Children.Add(focus);var f=new Grid();focus.Child=f;var label=T("FOCUS",9,"#E8DFF8");label.VerticalAlignment=VerticalAlignment.Top;f.Children.Add(label);
  var timer=Stack();timer.VerticalAlignment=VerticalAlignment.Bottom;timer.Margin=new Thickness(0,0,0,2);dashTimer=T("25:00",24);System.Windows.Documents.Typography.SetNumeralAlignment(dashTimer,FontNumeralAlignment.Tabular);timer.Children.Add(dashTimer);dashReady=T("Pronto",9,"#D7CCF1");dashReady.Margin=new Thickness(0,4,0,0);timer.Children.Add(dashReady);f.Children.Add(timer);
  dashStart=Btn("▶",()=>{if(running){remaining=Math.Max(0,(end-DateTime.UtcNow).TotalSeconds);running=false;}else{if(remaining<=0)remaining=1500;end=DateTime.UtcNow.AddSeconds(remaining);running=true;}UpdateDashboard();},"Iniciar / pausar foco");RoundTransport(dashStart);dashStart.Width=23;dashStart.Height=23;dashStart.Padding=new Thickness(0);dashStart.HorizontalAlignment=HorizontalAlignment.Right;dashStart.VerticalAlignment=VerticalAlignment.Bottom;dashStart.Margin=new Thickness(0,-2,-4,0);dashStart.Foreground=B("#51467B");dashStart.Background=B("#F5F1EB");f.Children.Add(dashStart);
  focus.MouseLeftButtonUp+=(s,e)=>{if(!IsButtonSource(e.OriginalSource as DependencyObject))Switch("focus");};dashCover="";UpdateDashboard();
 }
 // Uma sessão por arquivo em Dados\sessions (o hook apaga no SessionEnd; ignora o que ficou parado há 2 h). Lê no máximo a cada 2 s.
 static DateTime csAt=DateTime.MinValue;static int[] csCache=new[]{0,0};
 static int[] ClaudeSessions(){
  if(DateTime.UtcNow-csAt<TimeSpan.FromSeconds(2))return csCache;csAt=DateTime.UtcNow;int total=0,waiting=0;
  try{string dir=System.IO.Path.Combine(dataDir,"sessions");if(Directory.Exists(dir))foreach(string f in Directory.GetFiles(dir,"*.json")){if(DateTime.UtcNow-File.GetLastWriteTimeUtc(f)>TimeSpan.FromHours(2))continue;var e=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(f));total++;if(e["event"]=="Notification")waiting++;}}catch{}
  csCache=new[]{total,waiting};return csCache;
 }
 static bool IsButtonSource(DependencyObject o){while(o!=null){if(o is Button)return true;try{o=VisualTreeHelper.GetParent(o);}catch{return false;}}return false;}
 static void UpdateDashboard(){
  if(page!="home"||dashTrack==null)return;
  dashTrack.Text=media.available&&!String.IsNullOrWhiteSpace(media.title)?media.title:"Nenhuma mídia";dashArtist.Text=media.available?(media.artist??""):"Abra seu player";dashPlay.Content=TransportIcon(media.playing?"pause":"play",true);dashTimer.Text=Format(Math.Ceiling(remaining));dashReady.Text=running?"Rodando":"Pronto";dashStart.Content=TransportIcon(running?"pause":"play",true);
  string cover=media.available?(media.cover??""):"";if(cover!=dashCover){dashCover=cover;dashImage.Source=null;try{if(File.Exists(cover)){var bmp=new BitmapImage();bmp.BeginInit();bmp.CacheOption=BitmapCacheOption.OnLoad;bmp.DecodePixelWidth=600;bmp.UriSource=new Uri(cover);bmp.EndInit();bmp.Freeze();dashImage.Source=bmp;Tint(bmp);}}catch{}}
  try{string path=System.IO.Path.Combine(dataDir,"agent.json");if(File.Exists(path)){var stamp=File.GetLastWriteTimeUtc(path);if(stamp!=agentStamp){agentStamp=stamp;agentEvent=json.Deserialize<Dictionary<string,string>>(File.ReadAllText(path));}var e=agentEvent;dashAgent.Text=e["event"]=="Stop"?"Concluído":e["event"]=="SessionEnd"?"Encerrado":"Conectado";dashCount.Text=e["event"]=="SessionEnd"?"0":"1";}else{dashCount.Text="0";dashAgent.Text="Sem sessão";}}catch{}
  var cs=ClaudeSessions();if(cs[0]>0){dashCount.Text=cs[0].ToString();dashAgent.Text=cs[1]>0?cs[1]+" aguardando você":cs[0]==1?"1 sessão":cs[0]+" sessões";}
 }
 static void Robo(){
  var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});view.Children.Add(layout);
  var chatHeader=new DockPanel{Margin=new Thickness(0,0,0,4)};chatHeader.Children.Add(T("via "+MotorLabel(),9,Theme.Muted));var reset=Btn("Nova conversa",()=>{if(!chatBusy){conversation.Clear();Switch("robo");}},"Nova conversa");reset.HorizontalAlignment=HorizontalAlignment.Right;reset.Padding=new Thickness(6,1,6,1);chatHeader.Children.Add(reset);layout.Children.Add(chatHeader);
  chatMessages=Stack();chatScroll=new ScrollViewer{Content=chatMessages,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(chatScroll,1);layout.Children.Add(chatScroll);
  if(conversation.Count==0){chatMessages.Children.Add(T("ROBO",9,"#C1B5CC"));var welcome=T("O que falta para hoje?",21);welcome.FontWeight=FontWeights.SemiBold;welcome.Margin=new Thickness(0,13,0,7);chatMessages.Children.Add(welcome);chatMessages.Children.Add(T(MotorStatusText(),11,"#BDB4C7"));}else foreach(var message in conversation)AddChatBubble(message["content"],message["role"]=="user");
  var entry=new Grid{Margin=new Thickness(0,9,0,0)};entry.ColumnDefinitions.Add(new ColumnDefinition());entry.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Grid.SetRow(entry,2);layout.Children.Add(entry);
  chatInput=new TextBox{Background=Brushes.Transparent,Foreground=B("#F2ECE7"),BorderBrush=B("#25FFFFFF"),BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,5,8,5),FontSize=12,ToolTip="Mensagem para o Robo"};chatInput.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){SendChat();e.Handled=true;}};entry.Children.Add(chatInput);
  var send=Btn("↑",SendChat,"Enviar");send.Width=29;send.Height=29;send.Padding=new Thickness(0);send.Background=B("#F5ECEA");send.Foreground=B("#28202E");Grid.SetColumn(send,1);entry.Children.Add(send);
 }
 static TextBlock AddChatBubble(string message,bool user){
  if(chatMessages==null)return null;var text=T(message,12,user?"#EEE8E6":"#D9D0DF");text.TextWrapping=TextWrapping.Wrap;
  var border=new Border{Child=text,CornerRadius=new CornerRadius(10),Padding=new Thickness(10,6,10,6),Margin=new Thickness(0,3,0,3),MaxWidth=450,Background=user?B("#25FFFFFF"):Brushes.Transparent,HorizontalAlignment=user?HorizontalAlignment.Right:HorizontalAlignment.Left};chatMessages.Children.Add(border);if(chatScroll!=null)chatScroll.ScrollToEnd();return text;
 }
 // Only the latest turns go to the model (max 12 messages / ~6000 chars, always at least the last one); the saved history is capped at 100.
 static List<Dictionary<string,string>> RecentChat(){
  var list=new List<Dictionary<string,string>>();int chars=0;
  for(int i=conversation.Count-1;i>=0&&list.Count<12;i--){chars+=conversation[i]["content"].Length;if(chars>6000&&list.Count>0)break;list.Insert(0,conversation[i]);}
  return list;
 }
 static readonly System.Globalization.CultureInfo PtBr=new System.Globalization.CultureInfo("pt-BR");
 // The small local model ignores the date in the system prompt, so plain date/time questions are answered here.
 static string DateAnswer(string text){
  var t=(text??"").ToLowerInvariant().Trim(' ','?','!','.');if(t.Length>60)return null;DateTime n=DateTime.Now;
  if(System.Text.RegularExpressions.Regex.IsMatch(t,@"\bque horas?\b|\bhoras? (são|e) agora|\bhora atual"))return "Agora são "+n.ToString("HH:mm",PtBr)+".";
  if(System.Text.RegularExpressions.Regex.IsMatch(t,@"\bque dia\b|\bqual (é )?a data\b|\bdata de hoje|\bdia da semana|\bem que dia estamos"))return "Hoje é "+n.ToString("dddd, d 'de' MMMM 'de' yyyy",PtBr)+".";
  return null;
 }
 static async void SendChat(){
  if(chatBusy||chatInput==null||String.IsNullOrWhiteSpace(chatInput.Text))return;
    string message=chatInput.Text;chatInput.Clear();conversation.Add(new Dictionary<string,string>{{"role","user"},{"content",message}});AddChatBubble(message,true);if(conversation.Count>100)conversation.RemoveRange(0,conversation.Count-100);var quick=DateAnswer(message)??RoboAction(message);if(quick!=null){conversation.Add(new Dictionary<string,string>{{"role","assistant"},{"content",quick}});if(On("chat"))AddChatBubble(quick,false);return;}
  chatBusy=true;
  TextBlock live=null;try{var context=new List<Dictionary<string,string>>{new Dictionary<string,string>{{"role","system"},{"content","Você é o Tito, um assistente dentro do app Tito, no Windows do usuário. Responda sempre em português do Brasil, de forma breve. Agora é "+DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy, HH:mm",PtBr)+" (horário local do PC); use isso para perguntas sobre data e hora. O timer está "+(running?"em andamento":"parado")+" e mostra "+Format(remaining)+". "+(shareContext?RoboContext()+" ":"")+"Use só esses dados reais e o que o usuário escreveu; não invente mais nada. O Tito executa por conta própria pedidos claros de abrir app ou link, lembrete, nota, foco, mídia e volume; para qualquer outra ação no PC, diga que não consegue."}}};context.AddRange(RecentChat());var last=context[context.Count-1];if(last["role"]=="user")context[context.Count-1]=new Dictionary<string,string>{{"role","user"},{"content",last["content"]+"\n\n[Data e hora atuais do PC: "+DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy, HH:mm",PtBr)+"]"}};
   if(On("chat"))live=AddChatBubble("…",false);Action<string> show=t=>{if(live!=null)live.Dispatcher.BeginInvoke(new Action(()=>{live.Text=t;if(chatScroll!=null)chatScroll.ScrollToEnd();}));};
   string content;if(IsOllama()){content=await ChatStream(ExtPort(),OllamaName(),context,show);}
   else{MotorProgress+=MotorUi;MotorShowProgress(true);try{content=await MotorChat(context,show);}finally{MotorProgress-=MotorUi;MotorShowProgress(false);}}
   if(live!=null&&live.Parent!=null)live.Text=content;else if(On("chat"))AddChatBubble(content,false);
   conversation.Add(new Dictionary<string,string>{{"role","assistant"},{"content",content}});
  }catch(Exception ex){var inner=ex is AggregateException&&ex.InnerException!=null?ex.InnerException:ex;if(live!=null)chatMessages.Children.Remove((UIElement)live.Parent);if(On("chat"))AddChatBubble(inner is MotorException?inner.Message:IsOllama()?"Não consegui falar com "+ExtSource(ExtPort())+". Confira se ele está aberto com o modelo carregado, ou escolha a IA embutida do Tito em Settings.":"Não consegui usar a IA embutida: "+inner.Message,false);}finally{chatBusy=false;}
 }
 // Servers already running on this PC are appended to the model list.
 static async void DetectModels(){
  var found=await ProbeLocalAIs();if(models==null)return;
  foreach(var f in found){string tag="ext:"+f.Key+":"+f.Value;models.Items.Add(new ComboBoxItem{Content=ExtSource(f.Key)+" · "+f.Value,Tag=tag});if(IsOllama()&&ExtPort()==f.Key&&OllamaName()==f.Value)models.SelectedIndex=models.Items.Count-1;}
 }
}
}
