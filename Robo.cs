using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Forms=System.Windows.Forms;
namespace Tito {
public partial class App {
 // Robo x Windows. The small local model never runs actions: intents are recognised in code from the user's own words,
 // only this fixed list touches the system (no arbitrary commands), and real PC data is injected into the prompt.
 static readonly Dictionary<string,string> RoboApps=new Dictionary<string,string>{{"calculadora","calc"},{"bloco de notas","notepad"},{"explorador","explorer"},{"arquivos","explorer"},{"paint","mspaint"},{"navegador","msedge"},{"configurações do windows","ms-settings:"}};
 static Match RM(string t,string p){return Regex.Match(t,p,RegexOptions.IgnoreCase);}
 // Returns the intent kind (null = just chat) and its argument. Pure, so --test can check it.
 static string RoboParse(string text,out string arg){
  arg="";string t=Regex.Replace((text??"").Trim(),@"^(?:(?:tito|robo|robô|por favor)[\s,]+)+","",RegexOptions.IgnoreCase).TrimEnd(' ','?','!','.');
  if(t.Length==0||t.Length>200)return null;Match m;
  if((m=RM(t,@"^(?:(?:inicia|iniciar|começa|começar|comece|liga|ligar)\s+(?:um\s+|o\s+)?)?foco(?:\s+de)?(?:\s+(\d{1,3}))?(?:\s*min\w*)?$")).Success){arg=m.Groups[1].Value;return "focus";}
  if(RM(t,@"^(?:para|parar|pausa|pausar|cancela|cancelar)\s+(?:o\s+)?foco$").Success)return "focusstop";
  if((m=RM(t,@"^(?:(?:anotar|anota|nova nota|cria(?:r)? (?:uma )?nota)\b|nota:)\s*:?\s*(.+)$")).Success){arg=m.Groups[1].Value.Trim();return "note";}
  if(RM(t,@"^(?:minha\s+)?agenda(?:\s+(?:de\s+)?hoje)?$|\b(?:o que|oque|quais?)\b.*\b(?:agenda|compromissos?|tenho hoje|tenho pra hoje)\b").Success)return "q:agenda";
  if((m=RM(t,@"^(?:me\s+lembra(?:r)?|lembra(?:r)?(?:-me)?|lembrete|agenda(?:r)?)\b\s*(?:de\s+|que\s+|para\s+|na agenda\s+)?(.+)$")).Success){arg=Regex.Replace(m.Groups[1].Value,@"\b(?:às|as)\s+(?=\d)","",RegexOptions.IgnoreCase).Trim();return "agenda";}
  if(RM(t,@"^(?:pausa|pausar|para|parar|toca|tocar|continua|continuar|retoma|retomar|play|pause|dá play|da play|dá pause)(?:\s+(?:a|o|de)?\s*(?:música|musica|mídia|midia|som))?$").Success)return "play";
  if(RM(t,@"^(?:próxima|proxima|pula|pular|avança|avançar|passa|passar)(?:\s+(?:a\s+|essa\s+)?(?:música|musica|faixa))?$").Success)return "next";
  if(RM(t,@"^(?:anterior|volta|voltar)(?:\s+(?:a\s+)?(?:música|musica|faixa))?$|^(?:música|musica|faixa) anterior$").Success)return "prev";
  if(RM(t,@"\bvolume\b").Success&&!RM(t,@"^qual\b").Success){
   if((m=RM(t,@"(\d{1,3})")).Success){arg=m.Groups[1].Value;return "vol";}
   if(RM(t,@"\b(?:aumenta|aumentar|sobe|subir|mais alto)\b").Success)return "volup";
   if(RM(t,@"\b(?:abaixa|abaixar|diminui|diminuir|baixa|baixar|mais baixo)\b").Success)return "voldown";
  }
  if((m=RM(t,@"^(?:abre|abrir|abra|open)\s+(?:o\s+|a\s+|um\s+)?(.+)$")).Success){arg=m.Groups[1].Value.Trim();return "open";}
  if(RM(t,@"\bbateria\b|\bcarga\b").Success)return "q:battery";
  if(RM(t,@"\bvolume\b").Success)return "q:volume";
  if(RM(t,@"(?:o que|oque|qual).*(?:tocando|música|musica)|\btocando\b").Success)return "q:media";
  if(RM(t,@"\b(?:qual|que)\b.*\b(?:app|aplicativo|programa|janela)\b|\bem foco\b").Success)return "q:app";
  if(RM(t,@"\b(?:quanto tempo|timer|cronômetro)\b|\bstatus do foco\b").Success)return "q:timer";
  if(RM(t,@"\b(?:notas|anotações|anotei)\b").Success)return "q:notes";
  if(RM(t,@"\b(?:copiei|área de transferência|clipboard)\b").Success)return "q:clip";
  return null;
 }
 static void AddNote(string note){string old=File.Exists(NotesPath(1))?File.ReadAllText(NotesPath(1)):"";File.AppendAllText(NotesPath(1),(old.Length>0&&!old.EndsWith("\n")?"\n":"")+note+"\n");}
 static void AddAgenda(string date,string time,string title){File.AppendAllText(AgendaPath(),date+"|"+time+"|"+title.Replace("|"," ")+"\n");}
 static string RoboBattery(){
  var ps=Forms.SystemInformation.PowerStatus;
  if((ps.BatteryChargeStatus&(Forms.BatteryChargeStatus.NoSystemBattery|Forms.BatteryChargeStatus.Unknown))!=0)return "este PC não tem bateria";
  return Math.Round(ps.BatteryLifePercent*100)+"%"+(ps.PowerLineStatus==Forms.PowerLineStatus.Online?", carregando":", na bateria");
 }
 static string RoboMedia(){return media.available&&!String.IsNullOrWhiteSpace(media.title)?(media.playing?"tocando":"pausado")+": "+media.title+(String.IsNullOrWhiteSpace(media.artist)?"":" — "+media.artist):"nenhuma mídia aberta";}
 static string RoboAgendaToday(){string today=DateTime.Now.ToString("yyyy-MM-dd");var l=AgendaEntries().Where(p=>p[0]==today).Select(p=>p[1]+" "+p[2]).ToList();return l.Count==0?"nada marcado":String.Join("; ",l);}
 static string RoboVolume(){return Math.Round(DesktopAudio.Volume(null)*100)+"%";}
 // Real PC data for the system prompt (notes and clipboard stay out: only answered when asked).
 static string RoboContext(){
  string vol="indisponível";try{vol=RoboVolume();}catch{}
  return "Dados reais do PC agora: bateria "+RoboBattery()+"; volume "+vol+"; app em foco "+(String.IsNullOrEmpty(DesktopContext.ForegroundApp)?"desconhecido":DesktopContext.ForegroundApp)+"; mídia "+RoboMedia()+"; agenda de hoje: "+RoboAgendaToday()+".";
 }
 // Runs a recognised intent and returns the reply, or null when the message should go to the model.
 static string RoboAction(string text){
  string arg;string kind=RoboParse(text,out arg);if(kind==null)return null;
  try{switch(kind){
   case "focus":{int min=arg.Length>0?Math.Max(1,Math.Min(MaxFocusMin,Int32.Parse(arg))):25;CmdFocus(min);return "Foco de "+min+" min iniciado.";}
   case "focusstop":CmdPauseFocus();return "Foco pausado.";
   case "note":AddNote(arg);CmdDone();return "Anotado.";
   case "agenda":{string d,h,title;if(!ParseAgenda(arg,DateTime.Now,out d,out h,out title))return "Não entendi o horário. Diga assim: me lembra amanhã 9h Dentista.";AddAgenda(d,h,title);CmdDone();return "Agendado: "+d.Substring(8,2)+"/"+d.Substring(5,2)+" às "+h+" — "+title+".";}
   case "play":if(!media.available)return "Nenhum player de mídia conectado ao Windows agora.";SendMedia("toggle");return "Comando de play/pause enviado a "+(String.IsNullOrEmpty(media.source)?"mídia":media.source)+".";
   case "next":SendMedia("next");return "Próxima faixa.";
   case "prev":SendMedia("previous");return "Faixa anterior.";
   case "vol":{int v=Math.Max(0,Math.Min(100,Int32.Parse(arg)));DesktopAudio.Volume(v/100f);return "Volume em "+v+"%.";}
   case "volup":DesktopAudio.Volume(DesktopAudio.Volume(null)+0.1f);return "Volume em "+RoboVolume()+".";
   case "voldown":DesktopAudio.Volume(DesktopAudio.Volume(null)-0.1f);return "Volume em "+RoboVolume()+".";
   case "open":{
    string low=arg.ToLowerInvariant();foreach(var p in cmdPages)if(p[1]==low){Switch(p[0]);return "Abrindo "+p[1]+".";}
    string exe;if(RoboApps.TryGetValue(low,out exe)){CmdOpen(exe);return "Abrindo "+low+".";}
    if(Regex.IsMatch(arg,@"^https?://\S+$",RegexOptions.IgnoreCase)||Regex.IsMatch(arg,@"^(?:www\.)?[a-z0-9-]+(?:\.[a-z0-9-]+)*\.(?:com|org|net|br|io|dev|app|gov|edu)(?:/\S*)?$",RegexOptions.IgnoreCase)){CmdOpen(arg.StartsWith("http",StringComparison.OrdinalIgnoreCase)?arg:"https://"+arg);return "Abrindo "+arg+".";}
    return "Só abro páginas do Tito, apps conhecidos ("+String.Join(", ",RoboApps.Keys.Take(RoboApps.Count-1))+") e links.";}
   case "q:battery":return "Bateria: "+RoboBattery()+".";
   case "q:volume":return "Volume em "+RoboVolume()+".";
   case "q:media":return "Mídia: "+RoboMedia()+".";
   case "q:app":return String.IsNullOrEmpty(DesktopContext.ForegroundApp)?"Não sei qual app está em foco.":"App em foco: "+DesktopContext.ForegroundApp+".";
   case "q:timer":return running?"Foco em andamento, faltam "+Format(Math.Ceiling(remaining))+".":"O timer está parado em "+Format(Math.Ceiling(remaining))+".";
   case "q:agenda":return "Hoje: "+RoboAgendaToday()+".";
   case "q:notes":{string n=File.Exists(NotesPath(1))?File.ReadAllText(NotesPath(1)):"";var lines=n.Split('\n').Select(x=>x.Trim()).Where(x=>x.Length>0).ToList();return lines.Count==0?"Você ainda não tem notas.":"Suas últimas notas:\n"+String.Join("\n",lines.Skip(Math.Max(0,lines.Count-5)));}
   case "q:clip":{string c=clipItems.FirstOrDefault();return c==null?"A área de transferência do Tito está vazia.":"Você copiou por último: "+(c.Length>300?c.Substring(0,300)+"…":c);}
  }}catch(Exception ex){return "Não consegui: "+ex.Message;}
  return null;
 }
}
}
