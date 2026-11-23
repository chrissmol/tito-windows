using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Forms=System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media;
namespace Tito {
public partial class App {
 // Notes, clipboard history and a local agenda. Nothing here leaves the machine.
 static readonly List<string> clipItems=new List<string>();static string lastClip="";static TextBox notesBox;
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);
 static bool keepAwake,batteryLow,batteryKnown,wasCharging;static DateTime curiousUntil,typingUntil,dizzyUntil,excitedUntil,loveUntil,winkUntil,agentBusyUntil,nextQuirk=DateTime.UtcNow.AddSeconds(10),quirkUntil;static Point quirkGaze;static bool agendaSoon,cpuHigh,memHigh,pcOn=true;static int cpuLimit=92,memLimit=90,battLimit=30,cpuPct,memPct;static long cpuIdle,cpuTotal;static int cpuStreak;static DateTime cpuAt;static DateTime welcomeUntil=DateTime.UtcNow.AddSeconds(3),chatDoneUntil;static bool chatWasBusy;
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
 // CPU acima do limite por ~9 s ou memória acima do limite: Tito fica preocupado e a cápsula mostra o valor. Uma leitura barata a cada 3 s, no mesmo tick de sempre.
 static void CpuTick(){
  var now=DateTime.UtcNow;if(now-cpuAt<TimeSpan.FromSeconds(3))return;cpuAt=now;
  long i,k,u;if(!GetSystemTimes(out i,out k,out u))return;long total=k+u;
  if(cpuTotal>0&&total>cpuTotal){cpuPct=(int)Math.Round((1-(double)(i-cpuIdle)/(total-cpuTotal))*100);cpuStreak=cpuPct>=cpuLimit?cpuStreak+1:0;cpuHigh=pcOn&&cpuStreak>=3;}
  cpuIdle=i;cpuTotal=total;
  var ms=new MemStatus();if(GlobalMemoryStatusEx(ms)){memPct=(int)ms.dwMemoryLoad;memHigh=pcOn&&memPct>=memLimit;}
 }
 static string PcAlert(out string color){color=Theme.Warn;return !pcOn?null:cpuHigh?"CPU "+cpuPct+"%":memHigh?"RAM "+memPct+"%":null;}
 static readonly Queue<DateTime> taps=new Queue<DateTime>();
 // Tito reacts to taps: a giggle, and dizzy after five quick ones.
 static void MascotTap(){
  var now=DateTime.UtcNow;taps.Enqueue(now);while(taps.Count>0&&now-taps.Peek()>TimeSpan.FromSeconds(3))taps.Dequeue();
  if(taps.Count>=5){dizzyUntil=now.AddSeconds(3);taps.Clear();}else{celebrateUntil=now.AddMilliseconds(1400);if(headerMascot!=null)headerMascot.Hop(1);}
 }
 static void SetKeepAwake(bool on){keepAwake=on;SetThreadExecutionState(on?0x80000003u:0x80000000u);}
 // Resting-pill battery: only when charging or low; desktops without a battery return null.
 static string BatteryText(out string color){
  color=null;if(!pcOn){batteryLow=false;return null;}var ps=System.Windows.Forms.SystemInformation.PowerStatus;
  if((ps.BatteryChargeStatus&(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery|System.Windows.Forms.BatteryChargeStatus.Unknown))!=0)return null;
  int pct=(int)Math.Round(ps.BatteryLifePercent*100);bool charging=ps.PowerLineStatus==System.Windows.Forms.PowerLineStatus.Online;
  int bad=Math.Min(20,battLimit);batteryLow=!charging&&pct<=bad;if(batteryKnown&&charging&&!wasCharging)loveUntil=DateTime.UtcNow.AddSeconds(2.5);batteryKnown=true;wasCharging=charging;
  if(charging&&pct<100){color=Theme.Ok;return "⚡ "+pct+"%";}
  if(!charging&&pct<=battLimit){color=pct<=bad?Theme.Bad:"#FFB000";return pct+"%";}
  return null;
 }
 // Pomodoro: foco 25 min, pausa 5 (15 a cada 4 ciclos); cada fim vai para focus-log.txt.
 static int focusCycles;static bool onBreak;
 static void FocusEnded(){
  try{File.AppendAllText(Path.Combine(dataDir,"focus-log.txt"),DateTime.Now.ToString("yyyy-MM-dd HH:mm")+"|"+(onBreak?"pausa":"foco")+"\n");}catch{}
  if(!onBreak)focusCycles++;onBreak=!onBreak;remaining=onBreak?(focusCycles%4==0?900:300):1500;
 }
 // Páginas de nota: a 1 é notes.txt (compatível com antes), as outras notes-N.txt.
 static int notesPage=1;
 static string NotesPath(int n=0){if(n==0)n=notesPage;return Path.Combine(dataDir,n==1?"notes.txt":"notes-"+n+".txt");}
 static int NotePages(){int n=1;while(File.Exists(NotesPath(n+1)))n++;return n;}
 // A aba mostra a primeira linha da página (até 14 letras); vazia, o número.
 static string NoteTitle(int n){try{string first=File.Exists(NotesPath(n))?File.ReadAllText(NotesPath(n)).Split('\n').Select(l=>l.Trim().TrimStart('#',' ')).FirstOrDefault(l=>l.Length>0):null;if(!String.IsNullOrEmpty(first))return first.Length>14?first.Substring(0,14)+"…":first;}catch{}return n.ToString();}
 // Junta todas as páginas num .md em Dados e mostra o arquivo no Explorer.
 static void ExportNotes(){FlushNotes();var sb=new System.Text.StringBuilder();for(int n=1;n<=NotePages();n++){string text="";try{text=File.ReadAllText(NotesPath(n));}catch{}sb.Append("## "+NoteTitle(n)+"\n\n"+text.TrimEnd()+"\n\n");}string path=Path.Combine(dataDir,"notas-exportadas.md");try{SafeWrite(path,sb.ToString());System.Diagnostics.Process.Start("explorer.exe","/select,\""+path+"\"");}catch(Exception ex){error="Não consegui exportar as notas. "+ex.Message;ShowError();}}
 static void GoNotePage(int n){FlushNotes();notesPage=n;Switch("notes");}
 static string AgendaPath(){return Path.Combine(dataDir,"agenda.txt");}
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();static uint clipSeq;
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 static bool clipPaused;static readonly string[] secretApps={"1password","bitwarden","keepass","lastpass","dashlane","nordpass","enpass","roboform","protonpass","keeper"};
 static bool IsSecretApp(string name){name=(name??"").ToLowerInvariant();return secretApps.Any(a=>name.Contains(a));}
 // Quem copiou (dono da área de transferência) ou o app em foco é um gerenciador de senhas: não guarda.
 static bool FromPasswordManager(){
  try{IntPtr owner=GetClipboardOwner();if(owner!=IntPtr.Zero){uint pid;GetWindowThreadProcessId(owner,out pid);if(IsSecretApp(System.Diagnostics.Process.GetProcessById((int)pid).ProcessName))return true;}}catch{}
  return IsSecretApp(DesktopContext.ForegroundApp);
 }
 static void ClipboardTick(){
  uint seq=GetClipboardSequenceNumber();if(seq==clipSeq)return;clipSeq=seq;if(!clipOn||clipPaused)return;
  try{
   if(!Clipboard.ContainsText()||Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")||FromPasswordManager())return;
   string text=Clipboard.GetText();if(String.IsNullOrWhiteSpace(text)||text==lastClip||text.Length>2000)return;
   curiousUntil=DateTime.UtcNow.AddMilliseconds(1500);lastClip=text;clipItems.Remove(text);clipItems.Insert(0,text);if(clipItems.Count>15)clipItems.RemoveAt(15);if(page=="clipboard")Switch("clipboard");
  }catch{}
 }
 static StackPanel Panel(string title,out ScrollViewer scroll,Button action=null){
  var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());view.Children.Add(root);
  var header=new DockPanel{Margin=new Thickness(0,0,0,6)};if(action!=null){DockPanel.SetDock(action,Dock.Right);header.Children.Add(action);}var heading=T(title,22,PageHex(page));heading.FontWeight=FontWeights.SemiBold;header.Children.Add(heading);root.Children.Add(header);
  scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);root.Children.Add(scroll);var rows=Stack();scroll.Content=rows;return rows;
 }
 static string notesPending;static System.Windows.Threading.DispatcherTimer notesSave;static TextBlock notesState;
 // Grava a nota 600 ms depois da última tecla (e ao sair da aba/fechar), não a cada tecla.
 static void FlushNotes(){if(notesSave!=null)notesSave.Stop();if(notesPending==null)return;bool ok=true;try{SafeWrite(NotesPath(),notesPending);notesPending=null;}catch{ok=false;}if(notesState!=null){notesState.Text=ok?"Salvo":"Não foi possível salvar · o texto continua aqui";notesState.Foreground=B(ok?Theme.Muted:Theme.Bad);}}
 static void Notes(){
  FlushNotes();ScrollViewer sc;var rows=Panel("Notas",out sc);
  string saved="";try{if(File.Exists(NotesPath()))saved=File.ReadAllText(NotesPath());}catch{}
  var bar=Stack(Orientation.Horizontal);bar.Margin=new Thickness(0,0,0,6);int total=NotePages();
  for(int n=1;n<=total;n++){int pg=n;var tab=Btn(NoteTitle(n),()=>GoNotePage(pg),"Página "+n);tab.Padding=new Thickness(8,2,8,2);if(n==notesPage)tab.Background=B("#40FFFFFF");bar.Children.Add(tab);}
  var more=Btn("+",()=>{int next=NotePages()+1;try{File.WriteAllText(NotesPath(next),"");}catch{}GoNotePage(next);},"Nova página");more.Padding=new Thickness(8,2,8,2);bar.Children.Add(more);var export=Btn("⇩",ExportNotes,"Exportar todas as páginas em .md");export.Padding=new Thickness(8,2,8,2);bar.Children.Add(export);var wipe=Btn("🗑",()=>{string old=notesBox.Text;if(old=="")return;notesBox.Text="";FlushNotes();Undoable("Página apagada",()=>{notesPending=old;FlushNotes();});},"Apagar o texto desta página");wipe.Padding=new Thickness(8,2,8,2);bar.Children.Add(wipe);
  var find=new TextBox{Width=130,Padding=new Thickness(8,3,8,3),FontSize=Theme.Caption,Margin=new Thickness(8,0,0,0),Tag="Buscar nas notas",ToolTip="Buscar nas páginas (Enter)"};
  find.KeyDown+=(s2,e2)=>{if(e2.Key!=System.Windows.Input.Key.Enter||find.Text.Trim()=="")return;FlushNotes();int count=NotePages();for(int k=1;k<=count;k++){int n=(notesPage-1+k)%count+1;try{if(File.ReadAllText(NotesPath(n)).IndexOf(find.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0){GoNotePage(n);return;}}catch{}}find.Text="Nada encontrado";};bar.Children.Add(find);notesState=T("",11,Theme.Muted);notesState.Margin=new Thickness(8,0,0,0);bar.Children.Add(notesState);rows.Children.Add(bar);
  notesBox=new TextBox{Text=saved,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Tag="Escreva aqui. A primeira linha vira o título da página.",VerticalContentAlignment=VerticalAlignment.Top,Padding=new Thickness(10),FontSize=13,MinHeight=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
  notesBox.TextChanged+=(s,e)=>{HideUndo();typingUntil=DateTime.UtcNow.AddSeconds(3);notesPending=notesBox.Text;notesState.Text="Salvando…";notesState.Foreground=B(Theme.Muted);if(notesSave==null){notesSave=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(600)};notesSave.Tick+=(a,b)=>FlushNotes();w.Closed+=(a,b)=>FlushNotes();}notesSave.Stop();notesSave.Start();};rows.Children.Add(notesBox);
 }
 // Só os itens fixados vão para o disco (clip-pinned.json); o histórico continua só na memória.
 static List<string> clipPinned;static string clipFilter="";
 static string PinPath(){return Path.Combine(dataDir,"clip-pinned.json");}
 static List<string> Pins(){if(clipPinned==null){clipPinned=new List<string>();try{if(File.Exists(PinPath()))clipPinned=json.Deserialize<List<string>>(File.ReadAllText(PinPath()));}catch{}}return clipPinned;}
 static void SavePins(){try{SafeWrite(PinPath(),json.Serialize(Pins()));}catch{error="Não consegui salvar os fixados no disco. Eles continuam até fechar o Tito.";ShowError();}}
 static void ClipboardPage(){
  ScrollViewer sc;var rows=Panel("Área de transferência",out sc,Btn("Limpar",()=>{clipItems.Clear();Switch("clipboard");},"Limpa o histórico; os fixados ficam"));
  var search=new TextBox{Text=clipFilter,Tag="Buscar no histórico e nos fixados",ToolTip="Digite para buscar no histórico e nos fixados",Margin=new Thickness(0,0,0,6)};
  rows.Children.Add(search);
  Func<string,bool> match=t=>clipFilter==""||t.IndexOf(clipFilter,StringComparison.CurrentCultureIgnoreCase)>=0;
  rows.Children.Add(ToggleRow("Pausar o histórico",clipPaused,v=>clipPaused=v));
  var list=Stack();rows.Children.Add(list);
  Action fill=()=>{list.Children.Clear();
  var pins=Pins().Where(match).ToList();var recent=clipItems.Where(t=>match(t)&&!Pins().Contains(t)).ToList();
  if(pins.Count==0&&recent.Count==0){var empty=T(clipFilter==""?"Copie um texto e ele aparece aqui. O histórico fica só na memória; fixe (📌) o que quiser guardar.":"Nada encontrado.",13,"#BDBDC5");empty.TextWrapping=TextWrapping.Wrap;empty.FontSize=Theme.Body;empty.Margin=new Thickness(8,6,8,6);list.Children.Add(empty);}
  foreach(string item in pins.Concat(recent)){string text=item;bool pinned=Pins().Contains(text);string line=text.Replace("\r"," ").Replace("\n"," ");
   var row=new DockPanel{Margin=new Thickness(0,1,0,1)};
   var pin=Btn(pinned?"✕":"📌",()=>{if(pinned)Pins().Remove(text);else{Pins().Remove(text);Pins().Insert(0,text);if(Pins().Count>30)Pins().RemoveAt(30);}SavePins();Switch("clipboard");},pinned?"Desafixar":"Fixar (salva no disco)");DockPanel.SetDock(pin,Dock.Right);row.Children.Add(pin);
   var label=new TextBlock{Text=(pinned?"📌 ":"")+line,TextTrimming=TextTrimming.CharacterEllipsis};var b=Btn("",()=>{lastClip=text;Clipboard.SetText(text);celebrateUntil=DateTime.UtcNow.AddSeconds(2);},"Copiar de novo");b.Content=label;b.ToolTip=text.Length>400?text.Substring(0,400)+"…":text;b.HorizontalContentAlignment=HorizontalAlignment.Left;row.Children.Add(b);list.Children.Add(row);}
  };
  search.TextChanged+=(s,e)=>{clipFilter=search.Text.Trim();fill();};fill();
  var note=T("Senhas de gerenciadores (1Password, Bitwarden, KeePass…) e itens marcados como sensíveis não entram. Sair do Tito apaga o histórico; só os fixados ficam.",10.5,Theme.Soft);note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(8,8,8,4);rows.Children.Add(note);
 }
 static List<string[]> LocalEntries(){
  var list=new List<string[]>();try{if(File.Exists(AgendaPath()))foreach(string line in File.ReadAllLines(AgendaPath())){var p=line.Split(new[]{'|'},3);if(p.Length==3)list.Add(p);}}catch{}
  return list.OrderBy(p=>p[0]+p[1]).ToList();
 }
 // Lembretes locais + eventos do Google Agenda (4º campo "cal", só leitura: nunca voltam para agenda.txt).
 static List<string[]> AgendaEntries(){var list=LocalEntries();if(calOn)list.AddRange(calEvents);return list.OrderBy(p=>p[0]+p[1]).ToList();}
 static string agendaError="";static DateTime lastAgendaCheck;static HashSet<string> agendaFired;
 // Lembretes já avisados ficam no disco (agenda-avisados.txt): o que vencer com o Tito fechado, o PC suspenso, em jogo ou no não incomodar aparece depois (até 24 h).
 static string FiredPath(){return Path.Combine(dataDir,"agenda-avisados.txt");}
 static HashSet<string> Fired(){if(agendaFired==null){agendaFired=new HashSet<string>();try{if(File.Exists(FiredPath()))foreach(string l in File.ReadAllLines(FiredPath()))agendaFired.Add(l);}catch{}}return agendaFired;}
 static DateTime AgendaAt(string[] p){DateTime at;return DateTime.TryParseExact(p[0]+" "+p[1],"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out at)?at:DateTime.MinValue;}
 // Grava num .tmp e troca, para nunca deixar o arquivo pela metade.
 static void SafeWrite(string path,string text){string tmp=path+".tmp";File.WriteAllText(tmp,text);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
 // Troca (ou apaga, com null) só a primeira entrada igual; entradas repetidas continuam.
 static void AgendaReplace(string[] entry,string[] with){
  try{var list=LocalEntries();int i=list.FindIndex(x=>x[0]==entry[0]&&x[1]==entry[1]&&x[2]==entry[2]);if(i<0)return;if(with==null)list.RemoveAt(i);else list[i]=with;SafeWrite(AgendaPath(),String.Concat(list.Select(x=>String.Join("|",x)+"\n")));agendaError="";}
  catch{agendaError="Não consegui salvar a agenda. Nada foi alterado.";}
 }
 static void AgendaAdd(string[] entry){try{var list=LocalEntries();list.Add(entry);SafeWrite(AgendaPath(),String.Concat(list.Select(x=>String.Join("|",x)+"\n")));agendaError="";}catch{agendaError="Não consegui salvar a agenda. Nada foi alterado.";}}
 // "[hoje|amanhã|dd/MM|yyyy-MM-dd] 14:30|9h|9h30 título" -> date yyyy-MM-dd, time HH:mm.
 static bool ParseAgenda(string text,DateTime now,out string date,out string time,out string title){
  date=time=title="";
  var m=System.Text.RegularExpressions.Regex.Match((text??"").Trim(),@"^(?:(?<d>\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}|hoje|amanh[ãa])\s+)?(?<h>\d{1,2})(?::(?<m>\d{2})|h(?<m>\d{2})?)\s+(?<t>.+)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
  if(!m.Success)return false;
  int h=int.Parse(m.Groups["h"].Value),min=m.Groups["m"].Success?int.Parse(m.Groups["m"].Value):0;if(h>23||min>59)return false;
  DateTime d=now.Date;string ds=m.Groups["d"].Value.ToLowerInvariant();
  if(ds.StartsWith("amanh"))d=d.AddDays(1);
  else if(ds.Contains("-")){if(!DateTime.TryParseExact(ds,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d))return false;}
  else if(ds.Contains("/")){var p=ds.Split('/');int dd=int.Parse(p[0]),mo=int.Parse(p[1]);try{d=new DateTime(now.Year,mo,dd);if(d<now.Date)d=d.AddYears(1);}catch{return false;}}
  date=d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);time=h.ToString("00")+":"+min.ToString("00");title=m.Groups["t"].Value;return true;
 }
 // Focus time done today ("yyyy-MM-dd|seconds" in focus.txt). Only the main island counts, so several screens do not add the same minutes twice.
 static double focusToday;static string focusDay="";static DateTime focusLast=DateTime.MinValue;static int focusSavedMin;static TextBlock focusLabel;
 static string FocusPath(){return Path.Combine(dataDir,"focus.txt");}
 static void FocusLoad(){
  string today=DateTime.Now.ToString("yyyy-MM-dd");focusDay=today;focusToday=0;
  try{if(File.Exists(FocusPath())){var p=File.ReadAllText(FocusPath()).Trim().Split('|');double sec;if(p.Length==2&&p[0]==today&&Double.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out sec))focusToday=sec;}}catch{}
  focusSavedMin=(int)(focusToday/60);
 }
 static string FocusTodayText(){if(secondary||focusDay=="")FocusLoad();int m=(int)(focusToday/60);return "Hoje: "+(m>=60?(m/60)+" h "+(m%60)+" min":m+" min")+" de foco";}
 static void FocusTick(){
  if(secondary)return;
  if(focusDay!=DateTime.Now.ToString("yyyy-MM-dd"))FocusLoad();
  var now=DateTime.UtcNow;if(running&&!onBreak&&focusLast!=DateTime.MinValue)focusToday+=Math.Min(2,(now-focusLast).TotalSeconds);
  bool stopped=!running&&focusLast!=DateTime.MinValue;focusLast=running?now:DateTime.MinValue;
  int min=(int)(focusToday/60);
  if(min!=focusSavedMin||stopped){focusSavedMin=min;try{File.WriteAllText(FocusPath(),focusDay+"|"+focusToday.ToString("F0",CultureInfo.InvariantCulture));}catch{}if(focusLabel!=null&&page=="focus")focusLabel.Text=FocusTodayText();}
 }
 // Sem mídia, o cartão do início mostra o próximo lembrete (ou um vencido ainda não concluído) de hoje.
 static string[] NextAgenda(){var now=DateTime.Now;return AgendaEntries().FirstOrDefault(p=>{var at=AgendaAt(p);return at.Date==now.Date&&(at>now||now-at<=TimeSpan.FromHours(2));});}
 static void AgendaReminderTick(){
  DateTime now=DateTime.Now;if(now-lastAgendaCheck<TimeSpan.FromSeconds(1))return;lastAgendaCheck=now;bool soon=false;var due=new List<string[]>();var all=AgendaEntries();
  foreach(var p in all){DateTime at=AgendaAt(p);if(at==DateTime.MinValue)continue;
   if(at>now&&at-now<=TimeSpan.FromMinutes(10))soon=true;
   if(at<=now&&now-at<=TimeSpan.FromHours(24)&&(p.Length<4||now-at<=TimeSpan.FromMinutes(15))&&!Fired().Contains(String.Join("|",p)))due.Add(p);}
  agendaSoon=soon;
  if(due.Count==0||secondary||contextHidden||doNotDisturb)return; // continua pendente e aparece quando der
  foreach(var p in due)Fired().Add(String.Join("|",p));
  try{File.WriteAllLines(FiredPath(),all.Select(p=>String.Join("|",p)).Where(k=>Fired().Contains(k)).Distinct().ToArray());}catch{}
  excitedUntil=DateTime.UtcNow.AddSeconds(6);System.Media.SystemSounds.Asterisk.Play();
  if(tray!=null){if(due.Count==1)tray.ShowBalloonTip(5000,"Tito · "+due[0][1],due[0][2],Forms.ToolTipIcon.Info);else tray.ShowBalloonTip(5000,"Tito · "+due.Count+" lembretes",String.Join("\n",due.Select(p=>p[1]+" "+p[2])),Forms.ToolTipIcon.Info);}
  Switch("agenda");Expand(true);
 }
 static void Agenda(){
  ScrollViewer sc;var rows=Panel("Agenda",out sc);
  var add=new TextBox{Tag="Novo lembrete: 14:30 Reunião · amanhã 9h Dentista",ToolTip="14:30 Reunião  ·  amanhã 9h Dentista  ·  25/11 18h30 Jantar  ·  2026-11-20 09:00 Médico",Margin=new Thickness(0,0,0,6)};
  add.KeyDown+=(s,e)=>{if(e.Key!=System.Windows.Input.Key.Enter||String.IsNullOrWhiteSpace(add.Text))return;
   string date,time,title;if(!ParseAgenda(add.Text,DateTime.Now,out date,out time,out title)){agendaError="Não entendi. Tente: 14:30 Reunião · amanhã 9h Dentista · 25/11 18h30 Jantar";Switch("agenda");return;}
   agendaError="";try{File.AppendAllText(AgendaPath(),date+"|"+time+"|"+title.Replace("|"," ")+"\n");}catch{agendaError="Não consegui salvar a agenda.";}celebrateUntil=DateTime.UtcNow.AddSeconds(2);Switch("agenda");};
  rows.Children.Add(add);
  string sum=SummaryToday();if(sum!=null){var st=T(sum,12);st.TextWrapping=TextWrapping.Wrap;st.Margin=new Thickness(8,2,8,6);var sh=T("Resumo do dia",10.5,Theme.Muted);sh.Margin=new Thickness(8,6,8,0);rows.Children.Add(CardBox(new StackPanel{Children={sh,st}}));}
  if(agendaError!="")rows.Children.Add(T(agendaError,12,"#FF453A"));
  DateTime now=DateTime.Now;string today=now.ToString("yyyy-MM-dd");var upcoming=AgendaEntries().Where(p=>{var at=AgendaAt(p);if(p.Length==4&&p[1]=="")return String.CompareOrdinal(p[0],today)>=0;return at!=DateTime.MinValue&&now-at<=TimeSpan.FromHours(p.Length==4?1:24);}).Take(20).ToList();
  if(upcoming.Count==0)rows.Children.Add(T("Nada marcado. Crie um lembrete para não esquecer: digite “amanhã 9h Dentista” e Enter.",13,"#BDBDC5"));
  foreach(var p in upcoming){var row=Stack(Orientation.Horizontal);bool google=p.Length==4;bool overdue=!google&&AgendaAt(p)<=now;string when=p[1]==""?(p[0]==today?"Dia todo":DateTime.ParseExact(p[0],"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("dd/MM",CultureInfo.InvariantCulture)):p[0]==today?p[1]:DateTime.ParseExact(p[0],"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("dd/MM",CultureInfo.InvariantCulture)+" "+p[1];
   var t=T(when,12,overdue?Theme.Warn:p[0]==today?"#FFB36B":Theme.Muted);t.Width=84;row.Children.Add(t);var title=T(p[2],13);title.MaxWidth=330;title.TextTrimming=TextTrimming.CharacterEllipsis;row.Children.Add(title);
   var entry=p;
   if(google){var tag=T("Google",10.5,Theme.Soft);tag.Margin=new Thickness(8,2,0,0);row.Children.Add(tag);row.Margin=new Thickness(0,3,0,3);rows.Children.Add(row);continue;}
   if(overdue){var later=Btn("+5 min",()=>{var at=DateTime.Now.AddMinutes(5);AgendaReplace(entry,new[]{at.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),at.ToString("HH:mm",CultureInfo.InvariantCulture),entry[2]});Switch("agenda");},"Adiar 5 minutos");later.Padding=new Thickness(6,2,6,2);row.Children.Add(later);}
   var done=Btn(overdue?"✓":"✕",()=>{AgendaReplace(entry,null);Switch("agenda");if(agendaError=="")Undoable(overdue?"Lembrete concluído":"Lembrete removido",()=>AgendaAdd(entry));},overdue?"Concluído":"Remover");done.Padding=new Thickness(6,2,6,2);row.Children.Add(done);row.Margin=new Thickness(0,3,0,3);rows.Children.Add(row);}
 }
}
}
