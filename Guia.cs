using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Tito {
public partial class App {
 // Desfazer: um aviso por 6 s depois de apagar nota, lembrete ou item da prateleira.
 // "commit" roda quando o aviso some sem desfazer (ex.: apagar de vez a pasta do item).
 static Border undoBar;static Action undoDo,undoCommit;static System.Windows.Threading.DispatcherTimer undoTimer;
 static void Undoable(string text,Action undo,Action commit=null){
  HideUndo();var host=view.Parent as Grid;undoDo=undo;undoCommit=commit;if(host==null)return;
  if(undoTimer==null){undoTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(6)};undoTimer.Tick+=(a,b)=>HideUndo();}
  var row=Stack(Orientation.Horizontal);row.Children.Add(T(text,12));var back=Btn("Desfazer",UndoNow,"Desfazer");back.Padding=new Thickness(8,2,8,2);row.Children.Add(back);
  undoBar=new Border{Background=B("#EE232328"),CornerRadius=new CornerRadius(16),Padding=new Thickness(12,2,4,2),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,2),Child=row};
  Grid.SetRow(undoBar,1);Panel.SetZIndex(undoBar,5);host.Children.Add(undoBar);undoTimer.Start();
 }
 static void UndoNow(){var u=undoDo;undoDo=null;undoCommit=null;HideUndo();if(u!=null){u();Switch(page);}}
 static void HideUndo(){
  if(undoTimer!=null)undoTimer.Stop();var c=undoCommit;undoCommit=null;undoDo=null;
  if(undoBar!=null){var h=undoBar.Parent as Panel;if(h!=null)h.Children.Remove(undoBar);undoBar=null;}
  if(c!=null)try{c();}catch{}
 }
 // Primeiro uso guiado: abre uma vez (primeiro-uso.txt) e dá para rever em Ajustes.
 static string TourPath(){return Path.Combine(dataDir,"primeiro-uso.txt");}
 static int tourStep;
 static readonly string[][] tourSteps={
  new[]{"Oi, eu sou o Tito","Sou os olhinhos no topo da tela. Fico pequeno na cápsula e abro só quando você clica em mim. Esc fecha."},
  new[]{"O painel","Início, Mídia, Foco, IA, Agenda, Notas e Colar ficam nos ícones de baixo. O + tem ações rápidas e a engrenagem, os ajustes."},
  new[]{"Caixa de comando","Aperte Ctrl+Alt+Espaço em qualquer lugar e escreva: “lembrar”, “hoje”, “buscar” ou o nome de uma aba. Enter resolve."},
  new[]{"Lembretes","Escreva “amanhã 9h Dentista” na caixa de comando. Eu aviso na hora, e se você estava longe, aviso depois (até 24 h)."},
  new[]{"Prateleira","Arraste arquivos, textos ou imagens para mim. Ficam aqui por 1 hora e dá para resumir, perguntar à IA ou mandar por e-mail."},
  new[]{"Privacidade","Tudo fica neste computador. Só vai para a IA o que você manda com Enter. Os detalhes estão no cartão Privacidade."}};
 static void TourEnd(){try{File.WriteAllText(TourPath(),DateTime.Now.ToString("yyyy-MM-dd"));}catch{}tourStep=0;Switch("home");}
 static void TourStart(){tourStep=0;Switch("tour");}
 static void TourPage(){
  var s=tourSteps[tourStep];bool last=tourStep==tourSteps.Length-1;
  var box=Stack();box.VerticalAlignment=VerticalAlignment.Center;box.HorizontalAlignment=HorizontalAlignment.Center;box.MaxWidth=440;
  var step=T((tourStep+1)+" de "+tourSteps.Length,11,Theme.Muted);step.HorizontalAlignment=HorizontalAlignment.Center;box.Children.Add(step);
  var title=T(s[0],24,Theme.Text);title.FontWeight=FontWeights.SemiBold;title.HorizontalAlignment=HorizontalAlignment.Center;title.Margin=new Thickness(0,4,0,8);box.Children.Add(title);
  var text=T(s[1],13.5,Theme.Soft);text.TextWrapping=TextWrapping.Wrap;text.TextAlignment=TextAlignment.Center;box.Children.Add(text);
  var dots=Stack(Orientation.Horizontal);dots.HorizontalAlignment=HorizontalAlignment.Center;dots.Margin=new Thickness(0,12,0,10);
  for(int i=0;i<tourSteps.Length;i++)dots.Children.Add(new System.Windows.Shapes.Ellipse{Width=6,Height=6,Margin=new Thickness(3,0,3,0),Fill=B(i==tourStep?Theme.Text:"#40FFFFFF")});box.Children.Add(dots);
  var row=Stack(Orientation.Horizontal);row.HorizontalAlignment=HorizontalAlignment.Center;
  if(!last)row.Children.Add(Btn("Pular",TourEnd));
  row.Children.Add(PillButton(last?"Começar":"Próximo",()=>{if(last)TourEnd();else{tourStep++;Switch("tour");}}));box.Children.Add(row);
  view.Children.Add(box);
 }
}
}
