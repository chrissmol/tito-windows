using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Reflection;
namespace Tito {
public partial class App {
 // Files dragged out of browsers, mail and zip archives are "virtual files" (FileGroupDescriptorW + FileContents).
 // They are saved as real files so the shelf hands back the same file, not a link.
 const long MaxVirtualFile=500L*1024*1024;
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
 [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr handle);
 [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr handle);
 [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr handle);
 [DllImport("ole32.dll")] static extern void ReleaseStgMedium(ref STGMEDIUM medium);
 static string[] DescriptorNames(byte[] raw){
  var names=new List<string>();if(raw==null||raw.Length<4)return new string[0];int count=BitConverter.ToInt32(raw,0);
  for(int i=0;i<count;i++){int start=4+i*592;if(start+592>raw.Length)break;names.Add(Encoding.Unicode.GetString(raw,start+72,520).Split('\0')[0]);}
  return names.ToArray();
 }
 static string SafeName(string name){foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');return String.IsNullOrWhiteSpace(name)?"arquivo":name;}
 static bool HasVirtualFiles(System.Windows.IDataObject data){return data.GetDataPresent("FileGroupDescriptorW");}
 static byte[] VirtualFileBytes(System.Runtime.InteropServices.ComTypes.IDataObject com,int index){
  var format=new FORMATETC{cfFormat=(short)RegisterClipboardFormat("FileContents"),ptd=IntPtr.Zero,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=index,tymed=TYMED.TYMED_ISTREAM|TYMED.TYMED_HGLOBAL};
  STGMEDIUM medium;com.GetData(ref format,out medium);
  try{
   if(medium.tymed==TYMED.TYMED_ISTREAM){
    var stream=(IStream)Marshal.GetObjectForIUnknown(medium.unionmember);var result=new MemoryStream();var buffer=new byte[65536];IntPtr read=Marshal.AllocHGlobal(4);
    try{stream.Seek(0,0,IntPtr.Zero);while(true){stream.Read(buffer,buffer.Length,read);int n=Marshal.ReadInt32(read);if(n<=0)break;result.Write(buffer,0,n);if(result.Length>MaxVirtualFile)throw new Exception("Arquivo grande demais para a prateleira.");}}
    finally{Marshal.FreeHGlobal(read);}
    return result.ToArray();
   }
   if(medium.tymed==TYMED.TYMED_HGLOBAL){
    IntPtr pointer=GlobalLock(medium.unionmember);
    try{long size=(long)GlobalSize(medium.unionmember).ToUInt64();if(size>MaxVirtualFile)throw new Exception("Arquivo grande demais para a prateleira.");var bytes=new byte[size];Marshal.Copy(pointer,bytes,0,(int)size);return bytes;}finally{GlobalUnlock(medium.unionmember);}
   }
  }finally{ReleaseStgMedium(ref medium);}
  return null;
 }
 static bool ReceiveVirtualFiles(System.Windows.IDataObject data,ShelfItem item){
  var raw=data.GetData("FileGroupDescriptorW") as MemoryStream;var com=data as System.Runtime.InteropServices.ComTypes.IDataObject;if(raw==null||com==null)return false;
  string[] names=DescriptorNames(raw.ToArray());if(names.Length==0)return false;
  string folder=Path.Combine(dataDir,"shelf",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);var paths=new List<string>();
  try{
   for(int i=0;i<names.Length;i++){byte[] bytes=VirtualFileBytes(com,i);if(bytes==null)continue;string path=Path.Combine(folder,SafeName(names[i]));if(File.Exists(path))path=Path.Combine(folder,i+"-"+SafeName(names[i]));File.WriteAllBytes(path,bytes);paths.Add(path);}
  }catch(Exception ex){error=ex.Message;ShowError();}
  if(paths.Count==0){try{Directory.Delete(folder,true);}catch{}return false;}
  item.Files=paths.ToArray();item.Folder=folder;item.Name=names.Length==1?names[0]:paths.Count+" arquivos";return true;
 }

 // Soltar arquivo: o Tito "lê" (barra de progresso), avisa que está pronto e oferece perguntar à IA ou enviar por e-mail.
 // Nada sai do PC sem você: o e-mail só abre como rascunho.
 static int flowState;static ShelfItem flowItem;static double flowPct;static string flowText="",flowTo="",flowSubject="",flowBody="",flowResult="";static DateTime flowStarted;static System.Windows.Threading.DispatcherTimer flowTimer;static Border flowBar;static TextBlock flowLabel;
 static readonly string[] flowTextExts={".txt",".md",".csv",".json",".log",".xml",".html",".cs",".py",".js",".ts",".ini",".yml",".yaml",".srt"};
 static void FlowStart(ShelfItem item){
  flowItem=item;flowState=1;flowPct=0;flowStarted=DateTime.UtcNow;celebrateUntil=DateTime.MinValue;flowText="";flowTo="";flowBody="";flowSubject=item.Name;flowResult="";
  try{string p=item.Files[0];var info=new FileInfo(p);if(info.Exists&&info.Length<=2*1024*1024&&Array.IndexOf(flowTextExts,Path.GetExtension(p).ToLowerInvariant())>=0){string t=File.ReadAllText(p);flowText=t.Length>6000?t.Substring(0,6000):t;}else if(info.Exists&&info.Length<=20*1024*1024&&Path.GetExtension(p).ToLowerInvariant()==".pdf")flowText=PdfText(File.ReadAllBytes(p));}catch{}
  if(flowTimer==null){flowTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};flowTimer.Tick+=(s,e)=>FlowTick();}
  flowTimer.Start();if(page=="shelf")Switch("shelf");
 }

 // Lê o texto de PDF sem biblioteca: descomprime os fluxos Flate e junta os textos de Tj/TJ.
 // shortcut: só texto simples (fonte com codificação padrão); PDF escaneado ou com fonte embutida codificada volta vazio, aí o Tito avisa que não leu.
 static string PdfText(byte[] raw){
  var latin=Encoding.GetEncoding("iso-8859-1");string s=latin.GetString(raw);var sb=new StringBuilder();int pos=0;
  while(sb.Length<6000){
   int st=s.IndexOf("stream",pos,StringComparison.Ordinal);if(st<0)break;
   if(st>=3&&String.CompareOrdinal(s,st-3,"end",0,3)==0){pos=st+6;continue;}
   int a=st+6;if(a<s.Length&&s[a]=='\r')a++;if(a<s.Length&&s[a]=='\n')a++;
   int b=s.IndexOf("endstream",a,StringComparison.Ordinal);if(b<0)break;pos=b+9;
   int d=s.LastIndexOf("obj",st,StringComparison.Ordinal);string dict=d>=0?s.Substring(d,st-d):"";
   byte[] data=new byte[b-a];Buffer.BlockCopy(raw,a,data,0,b-a);
   if(dict.IndexOf("/FlateDecode",StringComparison.Ordinal)>=0){
    if(data.Length<3)continue;
    try{using(var ms=new MemoryStream(data,2,data.Length-2))using(var z=new System.IO.Compression.DeflateStream(ms,System.IO.Compression.CompressionMode.Decompress))using(var o=new MemoryStream()){z.CopyTo(o);data=o.ToArray();}}catch{continue;}
   }else if(dict.IndexOf("/Filter",StringComparison.Ordinal)>=0)continue;
   PdfStreamText(latin.GetString(data),sb);
  }
  string text=System.Text.RegularExpressions.Regex.Replace(sb.ToString(),@"\s+"," ").Trim();
  return text.Length>6000?text.Substring(0,6000):text;
 }
 static void PdfSpace(StringBuilder sb){if(sb.Length>0&&sb[sb.Length-1]!=' ')sb.Append(' ');}
 static void PdfStreamText(string c,StringBuilder sb){
  int from=0;
  while(true){
   int bt=c.IndexOf("BT",from,StringComparison.Ordinal);if(bt<0)return;int et=c.IndexOf("ET",bt+2,StringComparison.Ordinal);if(et<0)et=c.Length;from=et;
   string seg=c.Substring(bt+2,et-bt-2);int n=seg.Length,i=0;bool arr=false;
   while(i<n){
    char ch=seg[i];
    if(ch=='('){
     int depth=1;i++;
     while(i<n&&depth>0){
      ch=seg[i];
      if(ch=='\\'&&i+1<n){
       i++;char e=seg[i];
       if(e>='0'&&e<='7'){int v=0,k=0;while(k<3&&i<n&&seg[i]>='0'&&seg[i]<='7'){v=v*8+(seg[i]-'0');i++;k++;}i--;if(v>=32)sb.Append((char)(v&255));}
       else if(e=='n'||e=='r'||e=='t')PdfSpace(sb);
       else if(e=='('||e==')'||e=='\\')sb.Append(e);
      }
      else if(ch=='(')depth++;
      else if(ch==')'){depth--;}
      else if(ch>=32)sb.Append(ch);
      i++;
     }
     if(!arr)PdfSpace(sb);
     continue;
    }
    if(ch=='['){arr=true;i++;continue;}
    if(ch==']'){arr=false;PdfSpace(sb);i++;continue;}
    if(arr&&ch=='-'&&i+1<n&&Char.IsDigit(seg[i+1])){int j=i+1;while(j<n&&(Char.IsDigit(seg[j])||seg[j]=='.'))j++;double v;if(Double.TryParse(seg.Substring(i,j-i),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out v)&&v<-200)PdfSpace(sb);i=j;continue;}
    if(!arr&&ch=='T'&&i+1<n&&(seg[i+1]=='d'||seg[i+1]=='D'||seg[i+1]=='*'||seg[i+1]=='m')&&(i==0||Char.IsWhiteSpace(seg[i-1]))&&(i+2>=n||Char.IsWhiteSpace(seg[i+2]))){PdfSpace(sb);i+=2;continue;}
    i++;
   }
   PdfSpace(sb);
  }
 }
 // Auto-teste: PDF mínimo, com fluxo puro e com fluxo Flate.
 static void CheckPdf(){
  string body="BT /F1 12 Tf (Ola) Tj 0 -14 Td [(mun)-300(do)-250(!)] TJ (caf\\351 \\(ok\\)) Tj ET";
  var latin=Encoding.GetEncoding("iso-8859-1");
  byte[] plain=latin.GetBytes("%PDF-1.4\n1 0 obj\n<< /Length 10 >>\nstream\n"+body+"\nendstream\nendobj\n");
  byte[] packed;
  using(var o=new MemoryStream()){o.WriteByte(0x78);o.WriteByte(0x9C);using(var z=new System.IO.Compression.DeflateStream(o,System.IO.Compression.CompressionMode.Compress,true)){var bytes=latin.GetBytes(body);z.Write(bytes,0,bytes.Length);}
   var head=latin.GetBytes("%PDF-1.4\n1 0 obj\n<< /Filter /FlateDecode >>\nstream\n");var tail=latin.GetBytes("\nendstream\nendobj\n");var all=new MemoryStream();all.Write(head,0,head.Length);var po=o.ToArray();all.Write(po,0,po.Length);all.Write(tail,0,tail.Length);packed=all.ToArray();}
  const string want="Ola mun do ! café (ok)";
  if(PdfText(plain)!=want||PdfText(packed)!=want||PdfText(latin.GetBytes("%PDF-1.4 sem texto"))!="")throw new Exception("PDF text");
 }
 static void FlowPaint(){if(flowBar!=null)flowBar.Width=3*flowPct;if(flowLabel!=null)flowLabel.Text=Math.Round(flowPct)+" %";}
 static void FlowTick(){
  double t=Math.Min(1,(DateTime.UtcNow-flowStarted).TotalSeconds/1.6);flowPct=100*(1-Math.Pow(1-t,2));FlowPaint();
  if(t>=1){flowTimer.Stop();flowState=2;celebrateUntil=DateTime.UtcNow.AddSeconds(2);if(page=="shelf")Switch("shelf");}
 }
 static void FlowAsk(){
  string t=flowText.Length>0?"Responda com base neste arquivo ("+flowItem.Name+"):\n"+flowText+"\n\nMinha pergunta: ":"Tenho o arquivo "+flowItem.Name+". Não consigo ler o conteúdo dele aqui; me ajude com esta pergunta sobre ele: ";
  chatDraft=t;Switch("robo");if(chatInput!=null)chatInput.Text=t;
 }
 static void FlowSend(){
  string path=flowItem.Files[0];bool ok=false;
  try{
   Type app=Type.GetTypeFromProgID("Outlook.Application");
   if(app!=null){
    object outlook=Activator.CreateInstance(app);object mail=app.InvokeMember("CreateItem",BindingFlags.InvokeMethod,null,outlook,new object[]{0});Type mt=mail.GetType();
    mt.InvokeMember("To",BindingFlags.SetProperty,null,mail,new object[]{flowTo});mt.InvokeMember("Subject",BindingFlags.SetProperty,null,mail,new object[]{flowSubject});mt.InvokeMember("Body",BindingFlags.SetProperty,null,mail,new object[]{flowBody});
    object atts=mt.InvokeMember("Attachments",BindingFlags.GetProperty,null,mail,null);atts.GetType().InvokeMember("Add",BindingFlags.InvokeMethod,null,atts,new object[]{path});
    mt.InvokeMember("Display",BindingFlags.InvokeMethod,null,mail,null);ok=true;
   }
  }catch{}
  if(!ok){System.Diagnostics.Process.Start("mailto:"+Uri.EscapeDataString(flowTo)+"?subject="+Uri.EscapeDataString(flowSubject)+"&body="+Uri.EscapeDataString(flowBody));System.Diagnostics.Process.Start("explorer.exe","/select,\""+path+"\"");}
  flowResult=ok?"Rascunho aberto no Outlook com o arquivo anexado. É só conferir e enviar.":"Abri o seu e-mail. O arquivo está selecionado na pasta: arraste para a mensagem.";
  flowState=4;celebrateUntil=DateTime.UtcNow.AddSeconds(2.5);Switch("shelf");
 }
 static UIElement FlowCard(){
  if(flowItem==null||flowState==0||!shelf.Contains(flowItem))return null;
  var box=Stack();flowBar=null;flowLabel=null;
  var head=T(flowItem.Name,13);head.FontWeight=FontWeights.SemiBold;head.TextTrimming=TextTrimming.CharacterEllipsis;head.Margin=new Thickness(8,4,8,2);head.MaxWidth=480;head.HorizontalAlignment=HorizontalAlignment.Left;if(flowState!=3)box.Children.Add(head);else{var file=T("Anexo: "+flowItem.Name,Theme.Caption,Theme.Muted);file.TextTrimming=TextTrimming.CharacterEllipsis;file.MaxWidth=480;file.HorizontalAlignment=HorizontalAlignment.Left;file.Margin=new Thickness(8,4,8,8);box.Children.Add(file);}
  if(flowState==1){
   var sub=T("Lendo o arquivo…",11,Theme.Soft);sub.Margin=new Thickness(8,0,0,6);box.Children.Add(sub);
   var track=new Grid{Width=300,Height=4,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(8,0,0,4)};track.Children.Add(new Border{CornerRadius=new CornerRadius(2),Background=B("#26FFFFFF")});
   flowBar=new Border{CornerRadius=new CornerRadius(2),Background=B("#30D158"),HorizontalAlignment=HorizontalAlignment.Left,Width=3*flowPct};track.Children.Add(flowBar);box.Children.Add(track);
   flowLabel=T(Math.Round(flowPct)+" %",11,Theme.Soft);flowLabel.Margin=new Thickness(8,0,0,4);box.Children.Add(flowLabel);
  }else if(flowState==2){
   var sub=T("Pronto. O que você quer fazer?",11,Theme.Soft);sub.Margin=new Thickness(8,0,0,6);box.Children.Add(sub);
   var row=Stack(Orientation.Horizontal);row.Margin=new Thickness(2,0,0,4);row.Children.Add(Btn("Perguntar sobre ele",FlowAsk,"Leva o arquivo para o chat; só vai para a IA quando você apertar Enter"));row.Children.Add(Btn("Enviar por e-mail",()=>{flowState=3;Switch("shelf");}));row.Children.Add(Btn("Fechar",()=>{flowState=0;Switch("shelf");}));box.Children.Add(row);
  }else if(flowState==3){
   var two=new Grid{Margin=new Thickness(4,0,4,0)};two.ColumnDefinitions.Add(new ColumnDefinition());two.ColumnDefinitions.Add(new ColumnDefinition());
   var to=Field("Para","nome@empresa.com",flowTo,v=>flowTo=v,34,false);to.Margin=new Thickness(0,0,4,6);var subject=Field("Assunto","Sobre o arquivo",flowSubject,v=>flowSubject=v,34,false);subject.Margin=new Thickness(4,0,0,6);Grid.SetColumn(subject,1);two.Children.Add(to);two.Children.Add(subject);box.Children.Add(two);
   var last=new Grid{Margin=new Thickness(4,0,4,4)};last.ColumnDefinitions.Add(new ColumnDefinition());last.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var msg=Field("Mensagem","Escreva uma mensagem (opcional)",flowBody,v=>flowBody=v,34,false);last.Children.Add(msg);
   var row=Stack(Orientation.Horizontal);row.VerticalAlignment=VerticalAlignment.Bottom;row.Margin=new Thickness(8,0,0,0);row.Children.Add(Btn("Cancelar",()=>{flowState=2;Switch("shelf");}));row.Children.Add(PillButton("Enviar",()=>FlowSend()));Grid.SetColumn(row,1);last.Children.Add(row);box.Children.Add(last);
  }else{
   var done=T(flowResult,12,Theme.Ok);done.TextWrapping=TextWrapping.Wrap;done.Margin=new Thickness(8,0,8,6);box.Children.Add(done);
   var row=Stack(Orientation.Horizontal);row.Margin=new Thickness(2,0,0,4);row.Children.Add(Btn("Fechar",()=>{flowState=0;Switch("shelf");}));box.Children.Add(row);
  }
  return CardBox(box);
 }
 static UIElement DropZone(){
  var zone=new Grid{Height=74,Margin=new Thickness(0,2,0,0)};
  var edge=new System.Windows.Shapes.Rectangle{RadiusX=18,RadiusY=18,Stroke=B(dropHover?"#30D158":"#55FFFFFF"),StrokeThickness=dropHover?2:1.25,StrokeDashArray=new DoubleCollection{4,4},Fill=B(dropHover?"#1A30D158":"#0AFFFFFF")};zone.Children.Add(edge);
  var text=Stack();text.VerticalAlignment=VerticalAlignment.Center;text.HorizontalAlignment=HorizontalAlignment.Center;
  var a=T(dropHover?"Solte para o Tito guardar":"Solte um arquivo aqui",14);a.FontWeight=FontWeights.SemiBold;a.HorizontalAlignment=HorizontalAlignment.Center;text.Children.Add(a);
  var b=T("Ele lê, você pergunta à IA ou envia por e-mail · nada sai sem você pedir",11,Theme.Soft);b.HorizontalAlignment=HorizontalAlignment.Center;b.Margin=new Thickness(0,3,0,0);text.Children.Add(b);zone.Children.Add(text);
  if(dropHover&&SystemParameters.ClientAreaAnimation)edge.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(1,.45,TimeSpan.FromMilliseconds(600)){AutoReverse=true,RepeatBehavior=System.Windows.Media.Animation.RepeatBehavior.Forever});
  return zone;
 }
 static void Forget(ShelfItem item){shelf.Remove(item);if(item.Folder!=null)try{Directory.Delete(item.Folder,true);}catch{}}
}
}
