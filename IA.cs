using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace Tito {
public partial class App {
 // IA drawer: one place for every local AI (Claude Code, Codex, local model chat). Tabs reuse the former Claude, Robo and Painel IA pages.
 static string iaTab="geral";
 static bool On(string tab){return page=="ia"&&iaTab==tab;}
 static void IAPage(){
  var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());view.Children.Add(root);
  var tabs=Stack(Orientation.Horizontal);tabs.Margin=new Thickness(0,0,0,6);
  foreach(var t in new[]{new[]{"geral","Em uso"},new[]{"uso","Uso"},new[]{"chat","Chat"}}){string id=t[0];var b=Btn(t[1],()=>{if(id==iaTab)return;if(On("chat")&&chatInput!=null)chatDraft=chatInput.Text;iaTab=id;Render();});if(id==iaTab)b.Background=B("#33FFFFFF");tabs.Children.Add(b);}
  root.Children.Add(tabs);
  var body=new Grid();Grid.SetRow(body,1);root.Children.Add(body);
  var saved=view;view=body;
  try{if(iaTab=="chat"){Robo();if(chatInput!=null)chatInput.Text=chatDraft;}else if(iaTab=="uso")PainelPage();else IAOverview();}finally{view=saved;}
 }
 static Border IACard(string title,string status,string statusColor,UIElement content,int column,int columns){
  var stack=Stack();var head=T(title,9.5,Theme.Muted);head.Margin=new Thickness(0,0,0,4);stack.Children.Add(head);
  var s=T(status,15,statusColor);s.Margin=new Thickness(0,0,0,6);s.TextTrimming=TextTrimming.CharacterEllipsis;stack.Children.Add(s);if(content!=null)stack.Children.Add(content);
  var card=new Border{Background=B(Theme.Card),BorderBrush=B("#14FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Padding=new Thickness(11,9,11,9),Margin=new Thickness(column==0?0:4,0,column==columns-1?0:4,0),Child=stack};Grid.SetColumn(card,column);return card;
 }
 static void IAOverview(){
  var cards=new System.Collections.Generic.List<Func<int,int,Border>>();
  // Only AIs in use show up: Claude when a hook session exists or the logs show use in the last 14 days; local model once one is chosen.
  bool claudeOn=File.Exists(System.IO.Path.Combine(dataDir,"agent.json"))||painel==null||PainelActive("claude");
  if(claudeOn)cards.Add((c,n)=>{
   var body=Stack();agentStatus=T("Aguardando conexão",12,"#E2D1C4");agentDetail=T("",10.5,"#BEB2AC");agentDetail.TextTrimming=TextTrimming.CharacterEllipsis;agentAge=T("",10,"#ADA099");
   body.Children.Add(agentStatus);body.Children.Add(agentDetail);body.Children.Add(agentAge);
   if(painel!=null&&PainelActive("claude")){body.Children.Add(new Border{Height=6});foreach(var l in PainelLimits().Where(r=>Convert.ToString(r["ia"])=="claude"))body.Children.Add(LimitRow(Convert.ToString(l["nome"]),Num(l["pct"])));}
   var copy=Btn("Conectar sessão",()=>{WriteHookSettings();InstallUserHooks();agentDetail.Text="Conectado. Abra uma nova sessão do Claude Code.";},"Conectar o Claude Code");copy.HorizontalAlignment=HorizontalAlignment.Left;copy.Margin=new Thickness(-10,4,0,0);body.Children.Add(copy);
   return IACard("CLAUDE CODE","✳  Claude","#FFB36B",body,c,n);});
  if(painel!=null&&PainelActive("codex"))cards.Add((c,n)=>{
   var body=Stack();foreach(var l in PainelLimits().Where(r=>Convert.ToString(r["ia"])=="codex"))body.Children.Add(LimitRow(Convert.ToString(l["nome"]),Num(l["pct"]),l.ContainsKey("reinicia")?Num(l["reinicia"]):0));
   return IACard("CODEX","Em uso",Theme.Info,body,c,n);});
  cards.Add((c,n)=>{
   var body=Stack();var hint=T("Modelo: "+MotorLabel()+(IsOllama()?" · servidor local deste PC":" · embutida no Tito"),10.5,"#BDB4C7");hint.TextWrapping=TextWrapping.Wrap;body.Children.Add(hint);
   var open=Btn("Conversar",()=>{iaTab="chat";Render();});open.HorizontalAlignment=HorizontalAlignment.Left;open.Margin=new Thickness(-10,6,0,0);body.Children.Add(open);
   return IACard("IA LOCAL · ROBO","Conectada",Theme.Ok,body,c,n);});
  if(cards.Count==0){var none=T("Nenhuma IA em uso agora",16);none.HorizontalAlignment=HorizontalAlignment.Center;none.VerticalAlignment=VerticalAlignment.Center;view.Children.Add(none);return;}
  var grid=new Grid();for(int i=0;i<cards.Count;i++){grid.ColumnDefinitions.Add(new ColumnDefinition());grid.Children.Add(cards[i](i,cards.Count));}
  view.Children.Add(new ScrollViewer{Content=grid,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
  UpdateAgent();UpdateDashboard();
 }
}
}
