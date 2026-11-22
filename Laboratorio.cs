using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Windows;
namespace Tito {
public partial class App {
 // Laboratório: confere a lógica (lembretes, agenda, notas, foco) e tira fotos de cada tela e tema.
 // Roda numa pasta temporária: nunca toca nas notas e lembretes reais.
 static void Need(bool ok,string what){if(!ok)throw new Exception("Laboratório: "+what);}
 static string LabLine(DateTime d,string title){return d.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+"|"+d.ToString("HH:mm",CultureInfo.InvariantCulture)+"|"+title;}
 static void LabChecks(){
  string real=dataDir,tmp=Path.Combine(Path.GetTempPath(),"tito-lab-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(tmp);dataDir=tmp;agendaFired=null;notesPending=null;notesPage=1;bool dnd=doNotDisturb;
  try{
   DateTime now=DateTime.Now;
   // Lembretes: vencido em "não incomodar" fica pendente, depois aparece uma vez só; o de 30 h atrás já expirou.
   File.WriteAllText(AgendaPath(),LabLine(now.AddHours(-3),"Perdido")+"\n"+LabLine(now.AddHours(-30),"Antigo")+"\n"+LabLine(now.AddMinutes(5),"Já vai")+"\n"+LabLine(now.AddHours(3),"Futuro")+"\n");
   doNotDisturb=true;lastAgendaCheck=DateTime.MinValue;AgendaReminderTick();
   Need(Fired().Count==0,"lembrete vencido não pode sumir em não incomodar");Need(agendaSoon,"lembrete nos próximos 10 min não foi detectado");
   doNotDisturb=false;lastAgendaCheck=DateTime.MinValue;AgendaReminderTick();
   Need(Fired().Count==1&&Fired().Contains(LabLine(now.AddHours(-3),"Perdido")),"lembrete perdido deveria avisar uma vez, e só ele");
   Need(File.ReadAllText(FiredPath()).Contains("Perdido"),"aviso não foi gravado no disco");
   lastAgendaCheck=DateTime.MinValue;AgendaReminderTick();Need(Fired().Count==1,"lembrete avisado de novo");
   // Adiar, concluir, e repetidos: só um sai.
   var missed=AgendaEntries().First(p=>p[2]=="Perdido");var later=now.AddMinutes(5);
   AgendaReplace(missed,new[]{later.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),later.ToString("HH:mm",CultureInfo.InvariantCulture),"Perdido"});
   Need(AgendaEntries().Any(p=>p[2]=="Perdido"&&AgendaAt(p)>now),"adiar 5 min não gravou");
   File.AppendAllText(AgendaPath(),LabLine(now.AddHours(4),"Igual")+"\n"+LabLine(now.AddHours(4),"Igual")+"\n");
   AgendaReplace(AgendaEntries().First(p=>p[2]=="Igual"),null);Need(AgendaEntries().Count(p=>p[2]=="Igual")==1,"concluir deveria tirar só um repetido");
   Need(agendaError==""&&!File.Exists(AgendaPath()+".tmp"),"gravação da agenda deixou lixo ou erro");
   // Frases de lembrete.
   string d,t,x;var n0=new DateTime(2026,11,19,10,0,0);
   Need(ParseAgenda("hoje 9h30 X",n0,out d,out t,out x)&&d=="2026-11-19"&&t=="09:30"&&x=="X","hoje 9h30");
   Need(ParseAgenda("25/11 18h30 Jantar",n0,out d,out t,out x)&&d=="2026-11-25"&&t=="18:30"&&x=="Jantar","25/11 18h30");
   Need(ParseAgenda("10/11 8h X",n0,out d,out t,out x)&&d=="2027-11-10","data que já passou vai para o ano seguinte");
   Need(ParseAgenda("2026-12-01 09:00 Médico",n0,out d,out t,out x)&&d=="2026-12-01"&&x=="Médico","data completa");
   Need(!ParseAgenda("9h",n0,out d,out t,out x)&&!ParseAgenda("14:60 X",n0,out d,out t,out x)&&!ParseAgenda("31/02 9h X",n0,out d,out t,out x),"frases inválidas devem ser recusadas");
   // Notas: gravam inteiras, com título e várias páginas.
   notesPending="# Compras\nleite e pão";FlushNotes();
   Need(File.ReadAllText(NotesPath(1))=="# Compras\nleite e pão"&&!File.Exists(NotesPath(1)+".tmp"),"nota não gravou inteira");
   Need(NoteTitle(1)=="Compras","título da nota");File.WriteAllText(NotesPath(2),"outra");Need(NotePages()==2,"páginas de nota");
   // Foco: texto do dia e leitura do arquivo (outro dia conta zero).
   focusDay=DateTime.Now.ToString("yyyy-MM-dd");focusToday=3900;Need(FocusTodayText()=="Hoje: 1 h 5 min de foco","texto do foco");
   File.WriteAllText(FocusPath(),focusDay+"|1800");FocusLoad();Need(FocusTodayText()=="Hoje: 30 min de foco","foco do dia não foi lido");
   File.WriteAllText(FocusPath(),"2000-01-01|1800");FocusLoad();Need(FocusTodayText()=="Hoje: 0 min de foco","foco de outro dia deve zerar");
   // Soltar arquivo: lê, fica pronto, e-mail e confirmação; o texto do arquivo vai para a pergunta.
   string fx=Path.Combine(tmp,"brief-cliente.txt");File.WriteAllText(fx,"Resumo do cliente: entregar a proposta até sexta.");
   ReceiveDrop(new DataObject(DataFormats.FileDrop,new[]{fx}));flowTimer.Stop();
   Need(flowState==1&&flowItem!=null&&flowItem.Name=="brief-cliente.txt","soltar arquivo deve iniciar a leitura");Need(flowText.Contains("proposta"),"texto do arquivo deveria ir para a pergunta");
   Switch("shelf");flowPct=44;FlowPaint();Need(flowBar!=null&&Math.Abs(flowBar.Width-132)<1,"barra de progresso");Capture("flow-lendo");
   flowState=2;Switch("shelf");Capture("flow-pronto");
   flowState=3;flowTo="ana@estudio.com";flowBody="Segue o resumo combinado.";Switch("shelf");Capture("flow-email");
   flowState=4;flowResult="Rascunho aberto no Outlook com o arquivo anexado. É só conferir e enviar.";Switch("shelf");Capture("flow-enviado");
   flowState=0;flowItem=null;shelf.Clear();dropHover=true;Switch("shelf");Capture("flow-soltar");dropHover=false;Switch("shelf");Capture("flow-vazio");
   // Desfazer: lembrete, página de nota e prateleira voltam; sem desfazer, o aviso some e confirma.
   File.WriteAllText(AgendaPath(),LabLine(now.AddHours(2),"Dentista")+"\n");var dent=AgendaEntries()[0];AgendaReplace(dent,null);Switch("agenda");Undoable("Lembrete removido",()=>AgendaAdd(dent));
   Need(AgendaEntries().Count==0&&undoBar!=null,"lembrete removido deveria mostrar o aviso");Capture("desfazer");UndoNow();
   Need(AgendaEntries().Count==1&&AgendaEntries()[0][2]=="Dentista"&&undoBar==null,"desfazer lembrete");
   string oldNote=File.ReadAllText(NotesPath(1));File.WriteAllText(NotesPath(1),"");Undoable("Página apagada",()=>{notesPending=oldNote;FlushNotes();});UndoNow();Need(File.ReadAllText(NotesPath(1))==oldNote,"desfazer nota");
   shelf.Clear();var sd=new DataObject();sd.SetText("Para voltar");ReceiveDrop(sd);var si=shelf[0];shelf.Remove(si);Undoable("Item removido",()=>shelf.Insert(0,si));UndoNow();Need(shelf.Count==1&&shelf[0]==si,"desfazer prateleira");shelf.Clear();
   bool committed=false;Undoable("x",()=>{},()=>committed=true);HideUndo();Need(committed&&undoBar==null,"aviso sem desfazer deveria confirmar");
   // Primeiro uso: cada passo desenha, e terminar grava o arquivo (não abre de novo).
   for(int i=0;i<tourSteps.Length;i++){tourStep=i;Switch("tour");Capture("guia-"+(i+1));}
   Need(!File.Exists(TourPath()),"guia não deveria marcar como visto antes do fim");TourEnd();Need(File.Exists(TourPath())&&page=="home","terminar o guia");
   // Fotos: agenda (com vencido), notas, área de transferência e cada tema.
   foreach(string p in new[]{"agenda","notes","clipboard"}){Switch(p);Capture(p);}
   foreach(var r in themeData){themeId=r[0];ApplyTheme(true);Switch("home");Capture("theme-"+r[0]);}
   themeId="padrao";ApplyTheme(true);Switch("home");
   foreach(string f in Directory.GetFiles(tmp,"preview-*.png"))File.Copy(f,Path.Combine(real,Path.GetFileName(f)),true);
  }finally{
   dataDir=real;agendaFired=null;notesPending=null;notesPage=1;doNotDisturb=dnd;focusDay="";
   try{Directory.Delete(tmp,true);}catch{}
  }
 }
}
}
