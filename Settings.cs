using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace Tito {
public partial class App {
 // Settings: grouped cards with switches and link rows.
 static Border CardBox(UIElement child){return new Border{Background=B(Theme.Card),BorderBrush=B("#14FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Padding=new Thickness(4),Margin=new Thickness(0,0,0,6),Child=child};}
 static Border RowBox(UIElement child,Action click){
  var row=new Border{Height=32,CornerRadius=new CornerRadius(12),Background=Brushes.Transparent,Cursor=System.Windows.Input.Cursors.Hand,Child=child,Focusable=true,BorderBrush=Brushes.White};
  var panel=child as Panel;var text=panel!=null&&panel.Children.Count>0?panel.Children[0] as TextBlock:null;if(text!=null)System.Windows.Automation.AutomationProperties.SetName(row,text.Text);
  row.MouseEnter+=(s,e)=>row.Background=B("#14FFFFFF");row.MouseLeave+=(s,e)=>row.Background=Brushes.Transparent;
  row.GotKeyboardFocus+=(s,e)=>row.BorderThickness=new Thickness(1);row.LostKeyboardFocus+=(s,e)=>row.BorderThickness=new Thickness(0); // foco visível no teclado
  Action run=()=>{try{click();}catch(Exception ex){error=ex.Message;ShowError();}};
  row.MouseLeftButtonUp+=(s,e)=>run();row.KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter||e.Key==System.Windows.Input.Key.Space){run();e.Handled=true;}};return row;
 }
 static UIElement ToggleRow(string label,bool on,Action<bool> set){
  var grid=new Grid();grid.Children.Add(new TextBlock{Text=label,Foreground=B(Theme.Text),FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0)});
  var knob=new System.Windows.Shapes.Ellipse{Width=16,Height=16,Fill=Brushes.White,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(2),RenderTransform=new TranslateTransform(on?14:0,0)};
  var track=new Border{Width=34,Height=20,CornerRadius=new CornerRadius(10),HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,6,0),Background=B(on?"#30D158":"#4A4A50"),Child=knob};grid.Children.Add(track);
  bool state=on;
  return RowBox(grid,()=>{state=!state;set(state);track.Background=B(state?"#30D158":"#4A4A50");((TranslateTransform)knob.RenderTransform).BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(state?14:0,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});});
 }
 static UIElement LinkRow(string label,Action go){
  var grid=new Grid();grid.Children.Add(new TextBlock{Text=label,Foreground=B(Theme.Text),FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0)});
  grid.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M0,0 L4,4 0,8"),Stroke=B("#80FFFFFF"),StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)});
  return RowBox(grid,go);
 }
 static Border PillButton(string label,Action go){
  var pill=new Border{Background=B("#0A84FF"),CornerRadius=new CornerRadius(13),Padding=new Thickness(14,5,14,5),Margin=new Thickness(4,0,4,0),HorizontalAlignment=HorizontalAlignment.Left,Cursor=System.Windows.Input.Cursors.Hand,Child=new TextBlock{Text=label,Foreground=Brushes.White,FontSize=12,FontWeight=FontWeights.SemiBold}};
  pill.MouseLeftButtonUp+=(s,e)=>{try{go();}catch(Exception ex){error=ex.Message;ShowError();}};return pill;
 }
 static UIElement SizeCard(){
  var box=Stack();var title=T("Tamanho do Tito",12,Theme.Heading);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(8,6,0,6);box.Children.Add(title);
  var sizes=new[]{0,80,100,125,150,200};var pick=new ComboBox{Height=28,Margin=new Thickness(4,0,4,6)};
  foreach(var p in sizes)pick.Items.Add(new ComboBoxItem{Content=p==0?"Automático (pela tela)":p+"%",Tag=p});pick.SelectedIndex=Math.Max(0,Array.IndexOf(sizes,sizePct));
  pick.SelectionChanged+=(s,e)=>{var it=pick.SelectedItem as ComboBoxItem;if(it!=null)File.WriteAllText(SizeFile(),((int)it.Tag).ToString());};
  box.Children.Add(pick);return CardBox(box);
 }
 // Privacidade: o que fica no disco e o que vai para a IA. Detalhes em PRIVACY.md.
 static UIElement PrivacyCard(){
  var box=Stack();var title=T("Privacidade",12,Theme.Heading);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(8,6,0,6);box.Children.Add(title);
  box.Children.Add(ToggleRow("Salvar conversas no disco",saveChat,v=>{saveChat=v;if(!v)try{File.Delete(ChatPath());}catch{}SavePrefs();}));
  box.Children.Add(ToggleRow("Histórico da área de transferência",clipOn,v=>{clipOn=v;if(!v)clipItems.Clear();SavePrefs();}));
  box.Children.Add(ToggleRow("Enviar dados do PC à IA",shareContext,v=>{shareContext=v;SavePrefs();}));
  box.Children.Add(LinkRow("Apagar conversas",()=>{conversation.Clear();try{File.Delete(ChatPath());}catch{}celebrateUntil=DateTime.UtcNow.AddSeconds(1.5);}));
  box.Children.Add(LinkRow("Abrir pasta de dados",()=>System.Diagnostics.Process.Start("explorer.exe","\""+dataDir+"\"")));
  var note=T("Dados do PC: bateria, volume, app em foco, mídia e agenda de hoje. Data, hora e timer vão sempre. A IA embutida roda neste PC; um servidor externo em 127.0.0.1 pode repassar para a nuvem.",10.5,Theme.Soft);note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(8,4,8,4);box.Children.Add(note);
  return CardBox(box);
 }
 static void Settings(){
  var grid=new Grid{Margin=new Thickness(0,0,0,-8)};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
  var scroll=new ScrollViewer{Content=grid,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};view.Children.Add(scroll);
  var left=Stack();left.Margin=new Thickness(0,0,5,0);
  var switches=Stack();switches.Children.Add(ToggleRow("Não incomodar",doNotDisturb,v=>doNotDisturb=v));switches.Children.Add(ToggleRow("Iniciar com o Windows",AutostartOn(),SetAutostart));switches.Children.Add(ToggleRow("Manter acordado",keepAwake,SetKeepAwake));left.Children.Add(CardBox(switches));
  var open=Stack();open.Children.Add(ToggleRow("Abrir ao passar o mouse",!clickOnly,v=>{clickOnly=!v;SavePrefs();}));open.Children.Add(ToggleRow("Fixar aberto",pinOpen,v=>{pinOpen=v;SavePrefs();}));left.Children.Add(CardBox(open));
  left.Children.Add(PrivacyCard());
  var links=Stack();links.Children.Add(LinkRow("Prateleira",()=>Switch("shelf")));links.Children.Add(LinkRow("Rever apresentação",TourStart));links.Children.Add(LinkRow("Áudio",()=>Switch("audio")));links.Children.Add(LinkRow("Conectar Claude",()=>Switch("agent")));left.Children.Add(CardBox(links));left.Children.Add(MonitorsCard());
  var right=Stack();right.Margin=new Thickness(5,0,0,0);Grid.SetColumn(right,1);
  var robo=Stack();var title=T("Robo · IA local",12,Theme.Heading);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(8,6,0,6);robo.Children.Add(title);
  models=new ComboBox{Height=28,Margin=new Thickness(4,0,4,6)};int sel=0;foreach(var m in motorModels){models.Items.Add(new ComboBoxItem{Content="Tito · "+m.Name+" · "+GB(m.Size),Tag="tito:"+m.Id});if(!IsOllama()&&MotorChoice()==m)sel=models.Items.Count-1;}models.SelectedIndex=sel;
  models.SelectionChanged+=(s,e)=>{var it=models.SelectedItem as ComboBoxItem;if(it!=null){localModel=(string)it.Tag;File.WriteAllText(System.IO.Path.Combine(dataDir,"model.txt"),localModel);if(modelStatus!=null)modelStatus.Text=MotorStatusText();}};robo.Children.Add(models);
  robo.Children.Add(PillButton("Preparar IA agora",PrepareMotor));
  modelStatus=T(MotorStatusText(),10.5,Theme.Soft);modelStatus.TextWrapping=TextWrapping.Wrap;modelStatus.Margin=new Thickness(8,7,8,4);robo.Children.Add(modelStatus);DetectModels();right.Children.Add(CardBox(robo));
  var media_=Stack();var status=T("Mídia · "+(media.available?"conectada ao Windows":String.IsNullOrEmpty(media.error)?"aguardando player":media.error.Trim()),10.5,Theme.Soft);status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(8,5,8,3);media_.Children.Add(status);
  media_.Children.Add(LinkRow("Voltar ao player Windows",()=>{useSystem=true;player.Pause();playing=false;Switch("music");}));right.Children.Add(CardBox(media_));
  right.Children.Add(ThemeCard());right.Children.Add(SizeCard());grid.Children.Add(left);grid.Children.Add(right);
 }
}
}
