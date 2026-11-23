using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Tito {
public partial class App {
 // Conectores: eventos do Google Agenda (link secreto .ics, só leitura), trocar de janela pela caixa de comando e olhos que reagem a avisos de e-mail/Teams.
 // Cada um liga/desliga em Ajustes; nada disso sai do PC além de baixar o .ics.
 static bool calOn=true,winOn=true,notifOn=true;
 static class Nw {
  public delegate bool EnumProc(IntPtr h,IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc,IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h,uint cmd);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,System.Text.StringBuilder s,int max);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")] public static extern int GetWindowLong(IntPtr h,int index);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int cmd);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a,uint b,bool attach);
  [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int value,int size);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern uint RegisterWindowMessage(string name);
  [DllImport("user32.dll")] public static extern bool RegisterShellHookWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool DeregisterShellHookWindow(IntPtr h);
 }

 // ---------- Google Agenda (iCal) ----------
 static List<string[]> calEvents=new List<string[]>();static string calUrl,calStatus="";static DateTime calLast=DateTime.MinValue,calParsed=DateTime.MinValue;static bool calBusy;static TextBlock calStatusLabel;
 static string CalUrlPath(){return Path.Combine(dataDir,"calendario.txt");}
 static string CalCachePath(){return Path.Combine(dataDir,"calendario-cache.ics");}
 static string CalUrl(){if(calUrl==null){calUrl="";try{if(File.Exists(CalUrlPath()))calUrl=File.ReadAllText(CalUrlPath()).Trim();}catch{}}return calUrl;}
 static string CalStatusText(){return calStatus!=""?calStatus:CalUrl()==""?"Cole o endereço secreto em formato iCal (Google Agenda › Configurações › Integrar agenda). Só leitura.":"Pronto.";}
 static void CalShow(string text){calStatus=text;if(calStatusLabel!=null)calStatusLabel.Text=text;}
 static void CalSetUrl(string url){
  url=(url??"").Trim();if(url.StartsWith("webcal://",StringComparison.OrdinalIgnoreCase))url="https://"+url.Substring(9);
  if(url.Length>0&&!url.StartsWith("https://",StringComparison.OrdinalIgnoreCase)){CalShow("O link precisa começar com https:// (endereço secreto em formato iCal).");return;}
  try{
   calUrl=url;
   if(url==""){if(File.Exists(CalUrlPath()))File.Delete(CalUrlPath());if(File.Exists(CalCachePath()))File.Delete(CalCachePath());calEvents=new List<string[]>();CalShow("Link removido.");}
   else{Directory.CreateDirectory(dataDir);SafeWrite(CalUrlPath(),url);CalShow("Link salvo.");CalSync(true);}
  }catch{CalShow("Não consegui salvar o link.");}
 }
 // Baixa o .ics a cada 30 min (ou quando pedido), guarda uma cópia local e usa a cópia se a rede falhar.
 static void CalSync(bool force){
  if(secondary||uiTest||calBusy)return;string url=CalUrl();if(url=="")return;
  if(!calOn){if(force)CalShow("Desligado: ligue “Eventos do Google Agenda”.");return;}
  if(!force&&DateTime.Now-calLast<TimeSpan.FromMinutes(30))return;
  calLast=DateTime.Now;calBusy=true;CalShow("Atualizando…");string cache=CalCachePath();
  System.Threading.Tasks.Task.Run(()=>{
   string text=null,err=null;
   try{using(var c=new System.Net.WebClient()){c.Encoding=System.Text.Encoding.UTF8;text=c.DownloadString(url);}if(text.IndexOf("BEGIN:VCALENDAR",StringComparison.OrdinalIgnoreCase)<0)err="O link não devolveu um calendário (.ics). Confira o endereço secreto.";}
   catch{err="Sem conexão ou link inválido.";}
   w.Dispatcher.BeginInvoke(new Action(()=>{
    calBusy=false;
    if(err==null){try{SafeWrite(cache,text);}catch{}calEvents=ParseIcs(text,DateTime.Now);calParsed=DateTime.Now;CalShow("Atualizado às "+DateTime.Now.ToString("HH:mm",CultureInfo.InvariantCulture)+" · "+calEvents.Count+" evento(s) nos próximos dias");}
    else CalShow(err+(calEvents.Count>0?" Usando a cópia salva.":""));
   }));
  });
 }
 // Todos os Titos releem a cópia local de tempos em tempos; só o principal baixa.
 static void CalTick(){
  if(uiTest)return;
  if(DateTime.Now-calParsed>TimeSpan.FromMinutes(10)){calParsed=DateTime.Now;try{calEvents=File.Exists(CalCachePath())?ParseIcs(File.ReadAllText(CalCachePath()),DateTime.Now):new List<string[]>();}catch{}}
  CalSync(false);
 }
 sealed class IcsEv {public string Uid="",Title="",Rule="";public DateTime Start;public bool AllDay,Cancelled;public List<DateTime> Ex=new List<DateTime>();public DateTime? RecId;}
 static readonly string[] IcsDays={"SU","MO","TU","WE","TH","FR","SA"};
 static readonly Dictionary<string,string> IcsZones=new Dictionary<string,string>{{"America/Sao_Paulo","E. South America Standard Time"},{"America/New_York","Eastern Standard Time"},{"America/Chicago","Central Standard Time"},{"America/Los_Angeles","Pacific Standard Time"},{"Europe/London","GMT Standard Time"},{"Europe/Lisbon","GMT Standard Time"},{"Europe/Paris","Romance Standard Time"},{"Europe/Berlin","W. Europe Standard Time"},{"UTC","UTC"}};
 static string IcsText(string v){return (v??"").Replace("\\n"," ").Replace("\\N"," ").Replace("\\,",",").Replace("\\;",";").Replace("\\\\","\\").Trim();}
 static DateTime IcsTime(string value,string tzid,out bool allDay){
  allDay=false;value=(value??"").Trim();
  if(value.Length==8){allDay=true;return DateTime.ParseExact(value,"yyyyMMdd",CultureInfo.InvariantCulture);}
  if(value.EndsWith("Z"))return DateTime.ParseExact(value,"yyyyMMdd'T'HHmmss'Z'",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal); // vira hora local
  DateTime d=DateTime.ParseExact(value,"yyyyMMdd'T'HHmmss",CultureInfo.InvariantCulture);
  if(!String.IsNullOrEmpty(tzid)){try{string id;if(!IcsZones.TryGetValue(tzid,out id))id=tzid;var tz=TimeZoneInfo.FindSystemTimeZoneById(id);d=TimeZoneInfo.ConvertTime(d,tz,TimeZoneInfo.Local);}catch{}} // shortcut: fuso desconhecido vira hora local; mapeei só os mais comuns
  return d;
 }
 static bool IcsNth(DateTime d,string spec){
  if(spec.Length<2||spec.Substring(spec.Length-2)!=IcsDays[(int)d.DayOfWeek])return false;
  int n=0;if(spec.Length>2&&!Int32.TryParse(spec.Substring(0,spec.Length-2),out n))return false;
  if(n==0)return true;return n>0?(d.Day-1)/7+1==n:(DateTime.DaysInMonth(d.Year,d.Month)-d.Day)/7+1==-n;
 }
 // DAILY, WEEKLY (BYDAY), MONTHLY (dia do mês ou "2TU") e YEARLY, com INTERVAL, COUNT e UNTIL. shortcut: começa a contar até 4000 dias depois do início da série; séries mais antigas que ~11 anos perdem as ocorrências recentes.
 static List<DateTime> IcsOccurrences(IcsEv e,DateTime to){
  var res=new List<DateTime>();var r=new Dictionary<string,string>();
  foreach(string kv in e.Rule.Split(';')){int i=kv.IndexOf('=');if(i>0)r[kv.Substring(0,i).ToUpperInvariant()]=kv.Substring(i+1);}
  string freq,tmp;if(!r.TryGetValue("FREQ",out freq))return res;freq=freq.ToUpperInvariant();
  int interval=1,count=0;if(r.TryGetValue("INTERVAL",out tmp))Int32.TryParse(tmp,out interval);if(interval<1)interval=1;if(r.TryGetValue("COUNT",out tmp))Int32.TryParse(tmp,out count);
  DateTime until=DateTime.MaxValue;if(r.TryGetValue("UNTIL",out tmp)){try{bool ad;until=IcsTime(tmp,"",out ad);if(ad)until=until.AddDays(1);}catch{}}
  var days=new List<string>();if(r.TryGetValue("BYDAY",out tmp))days.AddRange(tmp.ToUpperInvariant().Split(','));
  DateTime weekStart=e.Start.Date.AddDays(-(((int)e.Start.DayOfWeek+6)%7));int n=0;int span=(int)Math.Min((to-e.Start.Date).TotalDays,4000);
  for(int k=0;k<=span;k++){
   DateTime d=e.Start.Date.AddDays(k);bool hit=false;
   if(freq=="DAILY")hit=k%interval==0;
   else if(freq=="WEEKLY"){int wk=(int)(d-weekStart).TotalDays/7;hit=wk%interval==0&&(days.Count==0?d.DayOfWeek==e.Start.DayOfWeek:days.Contains(IcsDays[(int)d.DayOfWeek]));}
   else if(freq=="MONTHLY"){int mo=(d.Year-e.Start.Year)*12+d.Month-e.Start.Month;if(mo%interval==0)hit=days.Count==0?d.Day==e.Start.Day:days.Any(x=>IcsNth(d,x));}
   else if(freq=="YEARLY")hit=d.Month==e.Start.Month&&d.Day==e.Start.Day&&(d.Year-e.Start.Year)%interval==0;
   if(!hit)continue;
   DateTime at=d+e.Start.TimeOfDay;if(at<e.Start)continue;if(at>until)break;
   n++;if(count>0&&n>count)break;res.Add(at);
  }
  return res;
 }
 // Eventos de ontem a daqui a 15 dias como {data, hora, título, "cal"}. Dia inteiro vem com hora vazia (sem lembrete); shortcut: evento de vários dias aparece só no primeiro dia.
 static List<string[]> ParseIcs(string text,DateTime now){
  var evs=new List<IcsEv>();IcsEv cur=null;
  string flat=Regex.Replace((text??"").Replace("\r\n","\n").Replace("\r","\n"),"\n[ \t]","");
  foreach(string line in flat.Split('\n')){
   if(line=="BEGIN:VEVENT"){cur=new IcsEv();continue;}
   if(line=="END:VEVENT"){if(cur!=null&&cur.Start!=DateTime.MinValue)evs.Add(cur);cur=null;continue;}
   if(cur==null)continue;
   int colon=line.IndexOf(':');if(colon<1)continue;string head=line.Substring(0,colon),val=line.Substring(colon+1);
   var parts=head.Split(';');string name=parts[0].ToUpperInvariant(),tz="";
   for(int i=1;i<parts.Length;i++)if(parts[i].StartsWith("TZID=",StringComparison.OrdinalIgnoreCase))tz=parts[i].Substring(5).Trim('"');
   try{
    bool ad;
    if(name=="SUMMARY")cur.Title=IcsText(val);
    else if(name=="UID")cur.Uid=val.Trim();
    else if(name=="STATUS")cur.Cancelled=val.Trim().ToUpperInvariant()=="CANCELLED";
    else if(name=="RRULE")cur.Rule=val.Trim();
    else if(name=="DTSTART"){cur.Start=IcsTime(val,tz,out ad);cur.AllDay=ad;}
    else if(name=="RECURRENCE-ID")cur.RecId=IcsTime(val,tz,out ad);
    else if(name=="EXDATE")foreach(string v in val.Split(','))cur.Ex.Add(IcsTime(v,tz,out ad));
   }catch{}
  }
  var skip=new HashSet<string>();foreach(var e in evs)if(e.RecId.HasValue)skip.Add(e.Uid+"|"+e.RecId.Value.ToString("s",CultureInfo.InvariantCulture));
  DateTime lo=now.Date.AddDays(-1),hi=now.Date.AddDays(15);var list=new List<string[]>();
  foreach(var e in evs){
   if(e.Cancelled)continue;
   var occ=e.RecId.HasValue||e.Rule==""?new List<DateTime>{e.Start}:IcsOccurrences(e,hi);
   foreach(var at in occ){
    if(at<lo||at>=hi)continue;
    if(!e.RecId.HasValue&&skip.Contains(e.Uid+"|"+at.ToString("s",CultureInfo.InvariantCulture)))continue;
    DateTime when=at;if(e.Ex.Any(x=>x==when))continue;
    string title=e.Title.Replace("|"," ").Trim();if(title.Length==0)title="(sem título)";if(title.Length>80)title=title.Substring(0,80)+"…";
    list.Add(new[]{at.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),e.AllDay?"":at.ToString("HH:mm",CultureInfo.InvariantCulture),title,"cal"});
   }
  }
  return list.OrderBy(p=>p[0]+p[1]).Take(300).ToList();
 }

 // ---------- Janelas: "ir para outlook", "abrir chrome" ----------
 sealed class WinInfo {public IntPtr H;public string Title="",Proc="";}
 static List<WinInfo> winCache=new List<WinInfo>();static DateTime winCacheAt=DateTime.MinValue;
 static List<WinInfo> OpenWindows(){
  if(DateTime.UtcNow-winCacheAt<TimeSpan.FromMilliseconds(800))return winCache;
  var list=new List<WinInfo>();var self=Process.GetCurrentProcess();uint me=(uint)self.Id;string own=self.ProcessName;
  try{Nw.EnumWindows(delegate(IntPtr h,IntPtr l){
   try{
    if(!Nw.IsWindowVisible(h)||Nw.GetWindow(h,4)!=IntPtr.Zero)return true;
    int ex=Nw.GetWindowLong(h,-20);if((ex&0x80)!=0&&(ex&0x40000)==0)return true;
    int cloaked;if(Nw.DwmGetWindowAttribute(h,14,out cloaked,4)==0&&cloaked!=0)return true;
    var sb=new System.Text.StringBuilder(256);Nw.GetWindowText(h,sb,256);string title=sb.ToString();if(title.Length==0||title=="Program Manager")return true;
    uint pid;Nw.GetWindowThreadProcessId(h,out pid);if(pid==me)return true;
    string proc="";try{proc=Process.GetProcessById((int)pid).ProcessName;}catch{}
    if(proc==own)return true;
    list.Add(new WinInfo{H=h,Title=title,Proc=proc});
   }catch{}
   return true;},IntPtr.Zero);}catch{}
  winCache=list;winCacheAt=DateTime.UtcNow;return list;
 }
 // procOnly: "abrir chrome" casa só pelo nome do programa; "ir para" também pelo título da janela.
 static List<WinInfo> WinFilter(List<WinInfo> all,string term,bool procOnly){
  string low=(term??"").Trim().ToLowerInvariant();if(low.Length==0)return new List<WinInfo>();
  return all.Where(x=>{string p=x.Proc.ToLowerInvariant();return p.StartsWith(low)||(!procOnly&&(p.Contains(low)||x.Title.ToLowerInvariant().Contains(low)));}).ToList();
 }
 static string WinLabel(WinInfo x){string t=x.Title.Length>48?x.Title.Substring(0,48)+"…":x.Title;return x.Proc.Length>0&&t.IndexOf(x.Proc,StringComparison.OrdinalIgnoreCase)<0?x.Proc+" · "+t:t;}
 static void FocusWindow(IntPtr h){
  try{
   if(Nw.IsIconic(h))Nw.ShowWindow(h,9);
   uint pid;uint fgThread=Nw.GetWindowThreadProcessId(Nw.GetForegroundWindow(),out pid),me=Nw.GetCurrentThreadId();
   bool attached=fgThread!=0&&fgThread!=me&&Nw.AttachThreadInput(me,fgThread,true);
   Nw.SetForegroundWindow(h);if(attached)Nw.AttachThreadInput(me,fgThread,false);
   CmdDone();
  }catch(Exception ex){MessageBox.Show("Não consegui trocar de janela.\n"+ex.Message,"Tito");}
 }
 static List<KeyValuePair<string,Action>> WindowCommands(string term){
  var res=new List<KeyValuePair<string,Action>>();
  if(!winOn){res.Add(new KeyValuePair<string,Action>("Janelas desligadas · ligue em Ajustes",()=>Switch("settings")));return res;}
  var all=OpenWindows();var hits=term==""?all:WinFilter(all,term,false);
  foreach(var x in hits.Take(6)){var win=x;res.Add(new KeyValuePair<string,Action>("Ir para · "+WinLabel(win),()=>FocusWindow(win.H)));}
  if(res.Count==0)res.Add(new KeyValuePair<string,Action>(term==""?"Nenhuma janela aberta":"Nenhuma janela aberta com “"+term+"”",()=>{}));
  return res;
 }

 // ---------- Avisos: o app pisca na barra de tarefas, os olhos se assustam ----------
 // O Windows só entrega o conteúdo das notificações a apps com identidade de pacote (UWP/MSIX); este app não tem. Então o Tito vê o piscar do botão na barra, sem ler mensagem alguma.
 static readonly string[] flashApps={"outlook","olk","ms-teams","msteams","teams","slack","thunderbird","whatsapp","telegram","discord"};
 static DateTime flashLast=DateTime.MinValue;static string notifLast="";static uint shellMsg;
 static bool OnFlash(string proc,bool inForeground){
  if(!notifOn||secondary||contextHidden||doNotDisturb||inForeground)return false;
  string p=(proc??"").ToLowerInvariant();if(!flashApps.Any(a=>p.StartsWith(a)))return false;
  if(DateTime.UtcNow-flashLast<TimeSpan.FromSeconds(15))return false;
  flashLast=DateTime.UtcNow;surprisedUntil=DateTime.UtcNow.AddSeconds(2);notifLast=proc+" às "+DateTime.Now.ToString("HH:mm",CultureInfo.InvariantCulture);return true;
 }
 static void ShellHook(int code,IntPtr hwnd){
  if(code!=0x8006)return; // HSHELL_FLASH
  uint pid;Nw.GetWindowThreadProcessId(hwnd,out pid);string proc="";try{proc=Process.GetProcessById((int)pid).ProcessName;}catch{}
  OnFlash(proc,Nw.GetForegroundWindow()==hwnd);
 }
 static void ConnectSetup(IntPtr hwnd){shellMsg=Nw.RegisterWindowMessage("SHELLHOOK");Nw.RegisterShellHookWindow(hwnd);w.Closed+=(s,e)=>Nw.DeregisterShellHookWindow(hwnd);}

 // ---------- Ajustes ----------
 static UIElement ConnectCard(){
  var box=Stack();var title=T("Google Agenda, janelas e avisos",12,Theme.Heading);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(8,6,0,6);box.Children.Add(title);
  box.Children.Add(ToggleRow("Eventos do Google Agenda",calOn,v=>{calOn=v;SavePrefs();if(v)CalSync(true);}));
  var link=new PasswordBox{Password=CalUrl(),Background=B("#18FFFFFF"),Foreground=B(Theme.Text),CaretBrush=Brushes.White,BorderThickness=new Thickness(0),Padding=new Thickness(8,5,8,5),FontSize=12,Margin=new Thickness(4,2,4,6),ToolTip="Endereço secreto em formato iCal. Quem tiver o link vê sua agenda: não compartilhe."};
  box.Children.Add(link);box.Children.Add(PillButton("Salvar link",()=>CalSetUrl(link.Password)));
  calStatusLabel=T(CalStatusText(),10.5,Theme.Soft);calStatusLabel.TextWrapping=TextWrapping.Wrap;calStatusLabel.Margin=new Thickness(8,6,8,4);box.Children.Add(calStatusLabel);
  box.Children.Add(LinkRow("Atualizar agora",()=>CalSync(true)));
  box.Children.Add(ToggleRow("Trocar de janela pela caixa de comando",winOn,v=>{winOn=v;SavePrefs();}));
  box.Children.Add(ToggleRow("Olhos reagem a avisos (Outlook, Teams…)",notifOn,v=>{notifOn=v;SavePrefs();}));
  var note=T("Eventos: só leitura, baixados do seu link e guardados em Dados\\calendario-cache.ics; eventos de dia inteiro aparecem sem lembrete. Janelas: a lista fica só neste PC. Avisos: o Tito percebe quando o app pisca na barra de tarefas e não lê mensagens."+(notifLast!=""?" Último aviso: "+notifLast+".":""),10.5,Theme.Soft);note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(8,4,8,4);box.Children.Add(note);
  return CardBox(box);
 }

 // ---------- Laboratório ----------
 static string IcsFloat(DateTime d){return d.ToString("yyyyMMdd'T'HHmmss",CultureInfo.InvariantCulture);}
 static void ConnectChecks(DateTime now){
  var keepEvents=calEvents;bool keepCal=calOn,keepNotif=notifOn,keepWin=winOn,keepCtx=contextHidden;DateTime keepSurprise=surprisedUntil;
  try{
   calOn=true;DateTime today=now.Date,week=today.AddDays(-21).AddHours(9);string wd=IcsDays[(int)week.DayOfWeek];
   string ics="BEGIN:VCALENDAR\r\n"
    +"BEGIN:VEVENT\r\nUID:a1\r\nSUMMARY:Planejamento\r\n  trimestral\r\nDTSTART:"+IcsFloat(today.AddDays(1).AddHours(10))+"\r\nEND:VEVENT\r\n"
    +"BEGIN:VEVENT\r\nUID:a2\r\nSUMMARY:Reunião\\, UTC\r\nDTSTART:"+today.AddDays(1).AddHours(15).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'",CultureInfo.InvariantCulture)+"\r\nEND:VEVENT\r\n"
    +"BEGIN:VEVENT\r\nUID:a3\r\nSUMMARY:Semanal\r\nDTSTART:"+IcsFloat(week)+"\r\nRRULE:FREQ=WEEKLY;BYDAY="+wd+"\r\nEXDATE:"+IcsFloat(week.AddDays(28))+"\r\nEND:VEVENT\r\n"
    +"BEGIN:VEVENT\r\nUID:a4\r\nSUMMARY:Diário\r\nDTSTART:"+IcsFloat(today.AddDays(-1).AddHours(8))+"\r\nRRULE:FREQ=DAILY;COUNT=2\r\nEND:VEVENT\r\n"
    +"BEGIN:VEVENT\r\nUID:a5\r\nSUMMARY:Cancelado\r\nSTATUS:CANCELLED\r\nDTSTART:"+IcsFloat(today.AddDays(2).AddHours(9))+"\r\nEND:VEVENT\r\n"
    +"BEGIN:VEVENT\r\nUID:a6\r\nSUMMARY:Feriado\r\nDTSTART;VALUE=DATE:"+today.AddDays(3).ToString("yyyyMMdd",CultureInfo.InvariantCulture)+"\r\nEND:VEVENT\r\n"
    +"BEGIN:VEVENT\r\nUID:a7\r\nSUMMARY:Já passou\r\nDTSTART:"+IcsFloat(now.AddMinutes(-20))+"\r\nEND:VEVENT\r\n"
    +"END:VCALENDAR\r\n";
   calEvents=ParseIcs(ics,now);
   Need(calEvents.Any(p=>p[2]=="Planejamento trimestral"&&p[1]=="10:00"&&p[0]==today.AddDays(1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)),"evento com linha dobrada");
   Need(calEvents.Any(p=>p[2]=="Reunião, UTC"&&p[1]=="15:00"),"evento em UTC deve virar hora local");
   Need(calEvents.Count(p=>p[2]=="Semanal")==2,"repetição semanal com exceção (esperado: hoje e daqui a 14 dias)");
   Need(calEvents.Count(p=>p[2]=="Diário")==2,"repetição diária com COUNT");
   Need(!calEvents.Any(p=>p[2]=="Cancelado"),"cancelado fica de fora");Need(calEvents.Any(p=>p[2]=="Feriado"&&p[1]==""),"dia inteiro aparece sem hora");
   Need(calEvents.All(p=>p.Length==4&&p[3]=="cal"),"eventos do Google precisam vir marcados");
   File.WriteAllText(AgendaPath(),LabLine(today.AddHours(23).AddMinutes(59),"Jantar")+"\n");
   Need(AgendaEntries().Any(p=>p[2]=="Planejamento trimestral")&&AgendaEntries().Any(p=>p[2]=="Jantar"),"agenda deve juntar lembretes e eventos");
   var jantar=AgendaEntries().First(p=>p[2]=="Jantar");AgendaReplace(jantar,new[]{jantar[0],jantar[1],"Jantar 2"});
   Need(!File.ReadAllText(AgendaPath()).Contains("Planejamento")&&File.ReadAllText(AgendaPath()).Contains("Jantar 2"),"evento do Google não pode ir para a agenda local");
   int fired=Fired().Count;lastAgendaCheck=DateTime.MinValue;AgendaReminderTick();Need(Fired().Count==fired,"evento do Google que começou há 20 min não deve avisar");
   calOn=false;Need(!AgendaEntries().Any(p=>p.Length==4),"desligado, os eventos somem");calOn=true;
   Switch("agenda");Capture("agenda-google");Switch("settings");Capture("settings-conectores");
   {bool kp=pcOn,kc=cpuHigh,km=memHigh;int pc=cpuPct,pm=memPct;string h;pcOn=true;cpuHigh=true;cpuPct=97;memHigh=false;Need(PcAlert(out h)=="CPU 97%","alerta de CPU");cpuHigh=false;memHigh=true;memPct=91;Need(PcAlert(out h)=="RAM 91%","alerta de memória");pcOn=false;Need(PcAlert(out h)==null&&BatteryText(out h)==null,"desligado, sem alerta");pcOn=kp;cpuHigh=kc;memHigh=km;cpuPct=pc;memPct=pm;}
   // Janelas.
   var wl=new List<WinInfo>{new WinInfo{Title="Caixa de Entrada - Outlook",Proc="OUTLOOK"},new WinInfo{Title="Docs - Google Chrome",Proc="chrome"}};
   Need(WinFilter(wl,"outlook",false).Count==1&&WinFilter(wl,"chr",true).Count==1&&WinFilter(wl,"google",true).Count==0&&WinFilter(wl,"google",false).Count==1,"filtro de janelas");
   Need(CommandMatches("ir para zzzxx").Count==1&&CommandMatches("ir para zzzxx")[0].Key.StartsWith("Nenhuma janela"),"ir para sem janela");
   winOn=false;Need(CommandMatches("ir para chrome")[0].Key.StartsWith("Janelas desligadas"),"janelas desligadas");winOn=true;
   // Avisos.
   contextHidden=false;surprisedUntil=DateTime.MinValue;flashLast=DateTime.MinValue;notifOn=true;
   Need(!OnFlash("notepad",false),"app fora da lista não deve reagir");Need(OnFlash("OUTLOOK",false)&&surprisedUntil>DateTime.UtcNow&&notifLast.StartsWith("OUTLOOK"),"olhos devem reagir ao piscar do Outlook");
   Need(!OnFlash("OUTLOOK",false),"segundo piscar seguido deve ser ignorado");
   flashLast=DateTime.MinValue;Need(!OnFlash("teams",true),"app já em foco não avisa");notifOn=false;Need(!OnFlash("teams",false),"avisos desligados");
  }finally{calEvents=keepEvents;calOn=keepCal;notifOn=keepNotif;winOn=keepWin;contextHidden=keepCtx;surprisedUntil=keepSurprise;flashLast=DateTime.MinValue;notifLast="";}
 }
}
}
