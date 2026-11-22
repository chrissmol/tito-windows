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
  try{string p=item.Files[0];var info=new FileInfo(p);if(info.Exists&&info.Length<=2*1024*1024&&Array.IndexOf(flowTextExts,Path.GetExtension(p).ToLowerInvariant())>=0){string t=File.ReadAllText(p);flowText=t.Length>6000?t.Substring(0,6000):t;}}catch{}
  if(flowTimer==null){flowTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};flowTimer.Tick+=(s,e)=>FlowTick();}
  flowTimer.Start();if(page=="shelf")Switch("shelf");
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
 static TextBox FlowField(string hint,string value,Action<string> set,double height,bool multi){
  var box=new TextBox{Text=value,Background=B("#14FFFFFF"),Foreground=B(Theme.Text),CaretBrush=Brushes.White,BorderThickness=new Thickness(0),Padding=new Thickness(10,5,10,5),FontSize=12,Height=height,Margin=new Thickness(4,0,4,5),VerticalContentAlignment=VerticalAlignment.Center,AcceptsReturn=multi,TextWrapping=multi?TextWrapping.Wrap:TextWrapping.NoWrap,ToolTip=hint};
  System.Windows.Automation.AutomationProperties.SetName(box,hint);box.TextChanged+=(s,e)=>set(box.Text);return box;
 }
 static UIElement FlowCard(){
  if(flowItem==null||flowState==0||!shelf.Contains(flowItem))return null;
  var box=Stack();flowBar=null;flowLabel=null;
  var head=T(flowItem.Name,13);head.FontWeight=FontWeights.SemiBold;head.TextTrimming=TextTrimming.CharacterEllipsis;head.Margin=new Thickness(8,4,8,2);head.MaxWidth=480;box.Children.Add(head);
  if(flowState==1){
   var sub=T("Lendo o arquivo…",11,Theme.Soft);sub.Margin=new Thickness(8,0,0,6);box.Children.Add(sub);
   var track=new Grid{Width=300,Height=4,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(8,0,0,4)};track.Children.Add(new Border{CornerRadius=new CornerRadius(2),Background=B("#26FFFFFF")});
   flowBar=new Border{CornerRadius=new CornerRadius(2),Background=B("#30D158"),HorizontalAlignment=HorizontalAlignment.Left,Width=3*flowPct};track.Children.Add(flowBar);box.Children.Add(track);
   flowLabel=T(Math.Round(flowPct)+" %",11,Theme.Soft);flowLabel.Margin=new Thickness(8,0,0,4);box.Children.Add(flowLabel);
  }else if(flowState==2){
   var sub=T("Pronto. O que você quer fazer?",11,Theme.Soft);sub.Margin=new Thickness(8,0,0,6);box.Children.Add(sub);
   var row=Stack(Orientation.Horizontal);row.Margin=new Thickness(2,0,0,4);row.Children.Add(Btn("Perguntar sobre ele",FlowAsk,"Leva o arquivo para o chat; só vai para a IA quando você apertar Enter"));row.Children.Add(Btn("Enviar por e-mail",()=>{flowState=3;Switch("shelf");}));row.Children.Add(Btn("Fechar",()=>{flowState=0;Switch("shelf");}));box.Children.Add(row);
  }else if(flowState==3){
   box.Children.Add(FlowField("Para",flowTo,v=>flowTo=v,28,false));box.Children.Add(FlowField("Assunto",flowSubject,v=>flowSubject=v,28,false));box.Children.Add(FlowField("Mensagem",flowBody,v=>flowBody=v,52,true));
   var row=Stack(Orientation.Horizontal);row.Margin=new Thickness(2,0,0,4);row.Children.Add(PillButton("Enviar",()=>FlowSend()));row.Children.Add(Btn("Cancelar",()=>{flowState=2;Switch("shelf");}));box.Children.Add(row);
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
