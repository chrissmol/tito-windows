﻿﻿﻿using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;
namespace Tito {
public partial class App {
 // Caixa de comando (Ctrl+Alt+Espaço): digite e Enter. Aparece na tela do mouse, some ao perder o foco.
 static Window cmdWin;
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int cx,int cy,uint flags);
 static readonly string[][] cmdPages={new[]{"home","início"},new[]{"music","mídia"},new[]{"music","música"},new[]{"focus","foco"},new[]{"ia","ia"},new[]{"agenda","agenda"},new[]{"notes","notas"},new[]{"clipboard","colar"},new[]{"shelf","prateleira"},new[]{"audio","áudio"},new[]{"settings","configurações"}};
 static void CmdDone(){celebrateUntil=DateTime.UtcNow.AddSeconds(2);}
 static void CmdOpen(string target){try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception ex){MessageBox.Show("Não consegui abrir “"+target+"”.\n"+ex.Message,"Tito",MessageBoxButton.OK,MessageBoxImage.Information);}}
 static void CmdFocus(int minutes){running=false;remaining=minutes*60;end=DateTime.UtcNow.AddSeconds(remaining);running=true;Tick();CmdDone();}
 static void CmdPauseFocus(){if(running){remaining=Math.Max(0,(end-DateTime.UtcNow).TotalSeconds);running=false;Tick();}}
 static List<KeyValuePair<string,Action>> CommandMatches(string text){
  var list=new List<KeyValuePair<string,Action>>();string t=(text??"").Trim();if(t.Length==0)return list;
  Action<string,Action> add=(label,run)=>list.Add(new KeyValuePair<string,Action>(label,run));
  Match m;
  if((m=Regex.Match(t,@"^(?:foco|focus)(?:\s+(\d{1,3}))?$",RegexOptions.IgnoreCase)).Success){int min=m.Groups[1].Success?Math.Max(1,Math.Min(MaxFocusMin,Int32.Parse(m.Groups[1].Value))):25;add("Iniciar foco de "+min+" min",()=>CmdFocus(min));}
  if(Regex.IsMatch(t,@"^(?:parar|pausar|pause)\s+foco$",RegexOptions.IgnoreCase))add("Pausar o foco",CmdPauseFocus);
  if((m=Regex.Match(t,@"^(?:nota|anotar)\s+(.+)$",RegexOptions.IgnoreCase)).Success){string note=m.Groups[1].Value;add("Anotar: "+note,()=>{try{AddNote(note);CmdDone();}catch(Exception ex){MessageBox.Show(ex.Message,"Tito");}});}
  // Mesmo entendimento de datas da agenda e do Robo: "lembrar amanhã 9h dentista", "agenda 25/11 18h30 jantar".
  if((m=Regex.Match(t,@"^(?:agenda|lembrar|lembrete|lembra)\s+(?!em\s)(.+)$",RegexOptions.IgnoreCase)).Success){string date,time,title;if(ParseAgenda(m.Groups[1].Value,DateTime.Now,out date,out time,out title)){string day=date==DateTime.Now.ToString("yyyy-MM-dd")?"hoje":date==DateTime.Now.AddDays(1).ToString("yyyy-MM-dd")?"amanhã":date.Substring(8,2)+"/"+date.Substring(5,2);string tt=title.Replace("|"," ");add("Criar lembrete · "+day+", "+time+" · "+tt,()=>{try{AddAgenda(date,time,tt);CmdDone();}catch(Exception ex){MessageBox.Show(ex.Message,"Tito");}});}}
  if((m=Regex.Match(t,@"^pausa(?:\s+(\d{1,2}))?$",RegexOptions.IgnoreCase)).Success){int min=m.Groups[1].Success?Math.Max(1,Int32.Parse(m.Groups[1].Value)):5;add("Pausa de "+min+" min",()=>{CmdFocus(min);onBreak=true;});}
  if((m=Regex.Match(t,@"^(?:lembrar|lembrete)\s+em\s+(\d{1,3})\s*(?:min|m)\s+(.+)$",RegexOptions.IgnoreCase)).Success){int min=Int32.Parse(m.Groups[1].Value);string title=m.Groups[2].Value;add("Lembrar em "+min+" min: "+title,()=>{try{var at=DateTime.Now.AddMinutes(min);AddAgenda(at.ToString("yyyy-MM-dd"),at.ToString("HH:mm"),title);CmdDone();}catch(Exception ex){MessageBox.Show(ex.Message,"Tito");}});}
  if((m=Regex.Match(t,@"^volume\s+(\d{1,3})$",RegexOptions.IgnoreCase)).Success){int v=Math.Min(100,Int32.Parse(m.Groups[1].Value));add("Volume em "+v+"%",()=>{DesktopAudio.Volume(v/100f);CmdDone();});}
  if(Regex.IsMatch(t,@"^(?:não incomodar|nao incomodar|dnd)$",RegexOptions.IgnoreCase))add(doNotDisturb?"Desligar não incomodar":"Ligar não incomodar",()=>{doNotDisturb=!doNotDisturb;CmdDone();});
  if(Regex.IsMatch(t,@"^(?:tocar|pausar|play|pause)$",RegexOptions.IgnoreCase))add("Tocar / pausar mídia",()=>SendMedia("toggle"));
  if(Regex.IsMatch(t,@"^(?:próxima|proxima|next)$",RegexOptions.IgnoreCase))add("Próxima faixa",()=>SendMedia("next"));
  if(Regex.IsMatch(t,@"^(?:hoje|resumo|resumo do dia)$",RegexOptions.IgnoreCase))add(DaySummary(),()=>Switch("agenda"));
  if(Regex.IsMatch(t,@"^resumo\s+agora$",RegexOptions.IgnoreCase))add("Gerar o resumo do dia agora",RunSummary);
  if((m=Regex.Match(t,@"^(?:buscar|procurar)\s+(.+)$",RegexOptions.IgnoreCase)).Success)foreach(var hit in LocalSearch(m.Groups[1].Value.Trim()))list.Add(hit);
  string low=t.ToLowerInvariant();
  if(low=="rotinas"||low=="editar rotinas")add("Editar rotinas (abre o arquivo)",EditRoutines);
  if(low.Length>=3)foreach(var r in Routines())if(r[0].ToLowerInvariant().StartsWith(low)){string steps=r[1];add("Rotina · "+r[0]+" ("+steps+")",()=>RunRoutine(steps));}
  foreach(var p in cmdPages)if(p[1].StartsWith(low)){string target=p[0];add("Abrir página: "+p[1],()=>Switch(target));}
  if((m=Regex.Match(t,@"^(?:ir\s+(?:para|pra|pro|a|ao)|alternar(?:\s+para)?|trocar\s+para|janela|go\s+to|switch\s+to)\s+(.+)$",RegexOptions.IgnoreCase)).Success)list.AddRange(WindowCommands(m.Groups[1].Value.Trim()));
  if(Regex.IsMatch(t,@"^(?:janelas|windows)$",RegexOptions.IgnoreCase))list.AddRange(WindowCommands(""));
  if((m=Regex.Match(t,@"^(?:abrir|open)\s+(.+)$",RegexOptions.IgnoreCase)).Success){string target=m.Groups[1].Value.Trim();
   if(winOn){var open=WinFilter(OpenWindows(),target,true);if(open.Count>0){var first=open[0];add("Ir para · "+WinLabel(first)+" (já aberto)",()=>FocusWindow(first.H));}}
   add("Aplicativo · abrir "+target,()=>CmdOpen(target));}
  else if(Regex.IsMatch(t,@"^https?://",RegexOptions.IgnoreCase))add("Link · abrir "+t,()=>CmdOpen(t));
  else if(list.Count==0){add("Perguntar ao Tito: "+t,()=>{chatDraft=t;Switch("robo");if(chatInput!=null){chatInput.Text=t;chatInput.CaretIndex=t.Length;}}); // não entendido: primeiro conversar, depois tentar como programa
   add("Aplicativo · abrir “"+t+"”",()=>CmdOpen(t));}
  return list;
 }

 // Rotinas: arquivo rotinas.txt, uma por linha, "nome = comando ; comando". Cada passo é um comando da caixa de comando.
 const string DefaultRoutines="# Rotinas do Tito: uma por linha, no formato  nome = passo ; passo\n# Cada passo é um comando da caixa de comando (Ctrl+Alt+Espaço). Edite à vontade.\ncomeçar o dia = agenda ; foco 25\nterminar o dia = parar foco ; notas\n";
 static int routineDepth;
 static string RoutinesPath(){return Path.Combine(dataDir,"rotinas.txt");}
 static List<string[]> ParseRoutines(string text){
  var list=new List<string[]>();
  foreach(var line in (text??"").Split('\n')){string l=line.Trim();int eq=l.IndexOf('=');if(l.Length==0||l[0]=='#'||eq<1)continue;string name=l.Substring(0,eq).Trim(),steps=l.Substring(eq+1).Trim();if(name.Length>0&&steps.Length>0)list.Add(new[]{name,steps});}
  return list;
 }
 static List<string[]> Routines(){
  try{if(!File.Exists(RoutinesPath())){Directory.CreateDirectory(dataDir);File.WriteAllText(RoutinesPath(),DefaultRoutines.Replace("\n",Environment.NewLine),new System.Text.UTF8Encoding(true));}return ParseRoutines(File.ReadAllText(RoutinesPath()));}
  catch{return ParseRoutines(DefaultRoutines);}
 }
 static void EditRoutines(){Routines();CmdOpen(RoutinesPath());}
 static void RunRoutine(string steps){
  if(routineDepth>2)return; // uma rotina que chama a si mesma não trava o Tito
  routineDepth++;
  try{foreach(var s in steps.Split(';')){string step=s.Trim();if(step.Length==0)continue;var m=CommandMatches(step);if(m.Count>0&&!m[0].Key.StartsWith("Perguntar ao Tito:"))m[0].Value();}CmdDone();}
  finally{routineDepth--;}
 }
 static string DaySummary(){
  var now=DateTime.Now;var today=AgendaEntries().Where(p=>AgendaAt(p).Date==now.Date).ToList();var next=today.FirstOrDefault(p=>AgendaAt(p)>now);int late=today.Count(p=>AgendaAt(p)<=now);
  return "Hoje · "+(today.Count==0?"nada na agenda":today.Count+" na agenda")+(next!=null?" · próximo "+next[1]+" "+next[2]:"")+(late>0?" · "+late+" vencido(s)":"")+" · "+FocusTodayText().Replace("Hoje: ","");
 }
 // Resumo do dia: no horário escolhido (18h), a IA local resume agenda, foco e notas de hoje; sem IA, sai a versão simples.
 static bool summaryOn=true,summaryBusy;static string summaryAt="18:00",summaryDay,summaryText="";
 static string SummaryPath(){return Path.Combine(dataDir,"resumo.txt");}
 static void SummaryLoad(){summaryDay="";summaryText="";try{if(File.Exists(SummaryPath())){var l=File.ReadAllText(SummaryPath()).Split(new[]{'\n'},2);summaryDay=l[0].Trim();summaryText=l.Length>1?l[1].Trim():"";}}catch{}}
 static string SummaryToday(){if(summaryDay==null)SummaryLoad();return summaryDay==DateTime.Now.ToString("yyyy-MM-dd")&&summaryText!=""?summaryText:null;}
 static void SaveSummary(string day,string text){summaryDay=day;summaryText=text;try{SafeWrite(SummaryPath(),day+"\n"+text);}catch{}}
 static List<string> NotesToday(){var l=new List<string>();FlushNotes();for(int n=1;n<=NotePages();n++){try{if(File.GetLastWriteTime(NotesPath(n)).Date==DateTime.Now.Date&&new FileInfo(NotesPath(n)).Length>0)l.Add(NoteTitle(n));}catch{}}return l;}
 static string SimpleSummary(){var n=NotesToday();return DaySummary()+(n.Count>0?" · notas de hoje: "+String.Join(", ",n):"");}
 static string DayFacts(){
  var now=DateTime.Now;var today=AgendaEntries().Where(p=>AgendaAt(p).Date==now.Date).ToList();var sb=new System.Text.StringBuilder();
  sb.Append("Agenda de hoje: "+(today.Count==0?"nada":String.Join("; ",today.Select(p=>p[1]+" "+p[2]+(AgendaAt(p)>now?" (ainda pendente)":" (já passou)"))))+"\n");
  sb.Append("Foco hoje: "+FocusTodayText().Replace("Hoje: ","").Replace(" de foco","")+"\n");
  FlushNotes();int shown=0;for(int n=1;n<=NotePages()&&shown<3;n++){try{if(File.GetLastWriteTime(NotesPath(n)).Date!=now.Date)continue;string t=File.ReadAllText(NotesPath(n)).Trim();if(t.Length==0)continue;shown++;sb.Append("Nota mexida hoje: "+(t.Length>300?t.Substring(0,300):t)+"\n");}catch{}}
  return sb.ToString();
 }
 static void SummaryTick(){
  if(secondary||uiTest||!summaryOn||summaryBusy||doNotDisturb||contextHidden)return;
  TimeSpan at;if(!TimeSpan.TryParse(summaryAt,out at))at=new TimeSpan(18,0,0);
  if(summaryDay==null)SummaryLoad();if(DateTime.Now.TimeOfDay<at||summaryDay==DateTime.Now.ToString("yyyy-MM-dd"))return;
  RunSummary();
 }
 static async void RunSummary(){
  if(summaryBusy)return;summaryBusy=true;string day=DateTime.Now.ToString("yyyy-MM-dd"),text=null,source="";
  try{
   if(shareContext&&(IsOllama()||MotorReady())){
    var msgs=new List<Dictionary<string,string>>{new Dictionary<string,string>{{"role","system"},{"content","Você é o Tito. Escreva em português do Brasil um resumo do dia do usuário em no máximo 4 linhas curtas, usando só os dados abaixo. Não invente nada e diga o que ficou pendente."}},new Dictionary<string,string>{{"role","user"},{"content",DayFacts()}}};
    var task=IsOllama()?ChatStream(ExtPort(),OllamaName(),msgs,null):MotorChat(msgs);
    if(await System.Threading.Tasks.Task.WhenAny(task,System.Threading.Tasks.Task.Delay(90000))==task){text=(await task).Trim();if(text.Length>0)source="IA local";}
   }
  }catch{}
  if(String.IsNullOrEmpty(source))text=SimpleSummary();
  summaryBusy=false;SaveSummary(day,text);
  excitedUntil=DateTime.UtcNow.AddSeconds(4);
  if(tray!=null)tray.ShowBalloonTip(8000,"Tito · Resumo do dia",text.Length>230?text.Substring(0,230)+"…":text,Forms.ToolTipIcon.Info);
  Switch("agenda");Expand(true);
 }
 // Busca local: páginas de nota, agenda e itens fixados. Nada sai do PC.
 static List<KeyValuePair<string,Action>> LocalSearch(string term){
  var hits=new List<KeyValuePair<string,Action>>();Func<string,bool> has=x=>x!=null&&x.IndexOf(term,StringComparison.CurrentCultureIgnoreCase)>=0;Func<string,string> cut=x=>{x=x.Replace("\r"," ").Replace("\n"," ");int i=Math.Max(0,x.IndexOf(term,StringComparison.CurrentCultureIgnoreCase)-20);x=x.Substring(i);return (i>0?"…":"")+(x.Length>60?x.Substring(0,60)+"…":x);};
  FlushNotes();for(int n=1;n<=NotePages();n++){int pg=n;string text="";try{text=File.ReadAllText(NotesPath(n));}catch{}if(has(text))hits.Add(new KeyValuePair<string,Action>("Nota "+n+" · "+cut(text),()=>GoNotePage(pg)));}
  foreach(var p in AgendaEntries().Where(x=>has(x[2])))hits.Add(new KeyValuePair<string,Action>("Agenda · "+p[0].Substring(8,2)+"/"+p[0].Substring(5,2)+" "+p[1]+" "+p[2],()=>Switch("agenda")));
  foreach(var pin in Pins().Where(has)){string x=pin;hits.Add(new KeyValuePair<string,Action>("Fixado · "+cut(x),()=>{lastClip=x;Clipboard.SetText(x);CmdDone();}));}
  if(hits.Count==0)hits.Add(new KeyValuePair<string,Action>("Nada encontrado para “"+term+"” em notas, agenda e fixados",()=>{}));
  return hits.Take(8).ToList();
 }
 static void CommandBox(){
  if(cmdWin!=null){cmdWin.Close();return;}
  var box=new TextBox{Background=Brushes.Transparent,Foreground=B("#FFF8F6"),CaretBrush=Brushes.White,BorderThickness=new Thickness(0),FontSize=18,Padding=new Thickness(12,10,12,10),ToolTip="foco 25 · pausa 5 · nota texto · lembrar amanhã 9h dentista · lembrar em 10 min texto · buscar texto · hoje · volume 40 · não incomodar · abrir notepad · ir para outlook · notas"};
  var rows=Stack();var hint=T("hoje  ·  foco 25  ·  nota …  ·  lembrar amanhã 9h …  ·  buscar …  ·  rotinas  ·  abrir …  ·  ir para …",11,"#8E8892");hint.TextWrapping=TextWrapping.Wrap;hint.Margin=new Thickness(14,0,14,10);
  var items=new List<KeyValuePair<string,Action>>();int sel=0;
  Action refresh=()=>{items=CommandMatches(box.Text);if(sel>=items.Count)sel=0;rows.Children.Clear();
   for(int i=0;i<items.Count;i++)rows.Children.Add(new Border{CornerRadius=new CornerRadius(8),Margin=new Thickness(6,1,6,1),Padding=new Thickness(8,6,8,6),Background=i==sel?B("#33FFFFFF"):Brushes.Transparent,Child=T(items[i].Key,13)});
   hint.Visibility=box.Text.Trim().Length==0?Visibility.Visible:Visibility.Collapsed;};
  var stack=Stack();stack.Children.Add(box);stack.Children.Add(rows);stack.Children.Add(hint);
  cmdWin=new Window{WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowInTaskbar=false,ResizeMode=ResizeMode.NoResize,Width=560,SizeToContent=SizeToContent.Height,Opacity=0,FontFamily=w.FontFamily,
   Content=new Border{CornerRadius=new CornerRadius(18),Background=B("#F2201E24"),BorderBrush=B("#33FFFFFF"),BorderThickness=new Thickness(1),Margin=new Thickness(8),Child=stack}};
  var win=cmdWin;
  box.TextChanged+=(s,e)=>{sel=0;refresh();};
  box.PreviewKeyDown+=(s,e)=>{var k=e.Key;
   if(k==System.Windows.Input.Key.Escape){win.Close();e.Handled=true;}
   else if(k==System.Windows.Input.Key.Down&&items.Count>0){sel=(sel+1)%items.Count;refresh();e.Handled=true;}
   else if(k==System.Windows.Input.Key.Up&&items.Count>0){sel=(sel+items.Count-1)%items.Count;refresh();e.Handled=true;}
   else if(k==System.Windows.Input.Key.Enter){e.Handled=true;if(items.Count==0)return;var run=items[sel].Value;win.Close();w.Dispatcher.BeginInvoke(new Action(()=>{try{run();}catch(Exception ex){MessageBox.Show(ex.Message,"Tito");}}));}};
  win.Deactivated+=(s,e)=>win.Close();win.Closed+=(s,e)=>{if(cmdWin==win)cmdWin=null;};
  refresh();win.Show();
  var area=Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;double scale=VisualTreeHelper.GetDpi(win).DpiScaleX;int width=(int)(win.ActualWidth*scale);
  SetWindowPos(new WindowInteropHelper(win).Handle,IntPtr.Zero,area.X+(area.Width-width)/2,area.Y+area.Height/5,0,0,0x15); // SWP_NOSIZE|NOZORDER|NOACTIVATE, em pixels da tela do mouse
  win.Opacity=1;win.Activate();box.Focus();
 }
}
}
