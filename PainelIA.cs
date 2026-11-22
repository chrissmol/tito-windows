using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Tito {
public partial class App {
 // Painel IA: reads the local Claude Code and Codex logs directly (no server, no Python, no extra install).
 // Only counts are kept (events per day, tokens per model, plan limits); no conversation text is read into the result.
 static Dictionary<string,object> painel;static int painelState;/*0 reading, 1 ok, 2 failed*/static bool painelBusy,painelHigh;static DateTime painelNext=DateTime.UtcNow.AddSeconds(2);
 static readonly Regex RxTs=new Regex("\"timestamp\":\"([^\"]+)\"",RegexOptions.Compiled),RxModel=new Regex("\"model\":\"([^\"]+)\"",RegexOptions.Compiled);
 static readonly Regex RxIn=new Regex("\"input_tokens\":(\\d+)",RegexOptions.Compiled),RxOut=new Regex("\"output_tokens\":(\\d+)",RegexOptions.Compiled),RxCreate=new Regex("\"cache_creation_input_tokens\":(\\d+)",RegexOptions.Compiled),RxRead=new Regex("\"cache_read_input_tokens\":(\\d+)",RegexOptions.Compiled);
 static readonly Regex RxLast=new Regex("\"last_token_usage\":\\{[^}]*?\"total_tokens\":(\\d+)",RegexOptions.Compiled),RxPrimaryReset=new Regex("\"primary\":\\{[^}]*?\"resets_at\":(\\d+)",RegexOptions.Compiled),RxPrimary=new Regex("\"primary\":\\{[^}]*?\"used_percent\":([\\d.]+)",RegexOptions.Compiled),RxSecondary=new Regex("\"secondary\":\\{[^}]*?\"used_percent\":([\\d.]+)",RegexOptions.Compiled);
 static long Tok(Regex rx,string line){var m=rx.Match(line);return m.Success?long.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture):0;}
 static bool LocalDay(string line,out DateTime day,out DateTime utc){
  day=DateTime.MinValue;utc=DateTime.MinValue;var m=RxTs.Match(line);DateTime parsed;
  if(!m.Success||!DateTime.TryParse(m.Groups[1].Value,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out parsed))return false;
  utc=parsed;day=parsed.ToLocalTime().Date;return true;
 }
 // Windows paths of 260+ characters (long project names) need the \?\ prefix on .NET Framework.
 static string LongPath(string path){return path.StartsWith(@"\\?\")?path:@"\\?\"+path;}
 static bool Old(string file,DateTime since){try{return File.GetLastWriteTime(LongPath(file)).Date<since;}catch{return true;}}
 static string[] SafeFiles(string dir,string pattern,SearchOption option){try{return Directory.GetFiles(LongPath(dir),pattern,option).Select(f=>f.StartsWith(@"\\?\")?f.Substring(4):f).ToArray();}catch{return new string[0];}}
 static IEnumerable<string> Lines(string file){
  FileStream stream;try{stream=new FileStream(LongPath(file),FileMode.Open,FileAccess.Read,FileShare.ReadWrite);}catch{yield break;}
  using(stream)using(var reader=new StreamReader(stream,Encoding.UTF8)){string line;while((line=reader.ReadLine())!=null)yield return line;}
 }
 static void Count(Dictionary<string,object> activity,DateTime day,string ia){var key=day.ToString("yyyy-MM-dd");object existing;Dictionary<string,object> d;if(activity.TryGetValue(key,out existing))d=(Dictionary<string,object>)existing;else activity[key]=d=new Dictionary<string,object>();d[ia]=(d.ContainsKey(ia)?Convert.ToInt32(d[ia]):0)+1;}
 // shortcut: OpenCode and Copilot logs are not read; add them as another source here.
 static Dictionary<string,object> PainelScan(string home,string appData){
  DateTime today=DateTime.Now.Date,since=today.AddDays(-13*7),tokenSince=today.AddDays(-30);
  var activity=new Dictionary<string,object>();var tokens=new Dictionary<string,long>();
  DateTime nowUtc=DateTime.UtcNow,lastClaude=DateTime.MinValue,lastCodex=DateTime.MinValue;long h5=0,week=0,todayTokens=0;int msgs5=0;
  string claudeRoot=Path.Combine(home,".claude","projects");
  if(Directory.Exists(claudeRoot))foreach(string dir in Directory.GetDirectories(claudeRoot))foreach(string file in SafeFiles(dir,"*.jsonl",SearchOption.TopDirectoryOnly)){
   if(Old(file,since))continue;
   foreach(string line in Lines(file)){
    if(line.IndexOf("\"type\":\"assistant\"",StringComparison.Ordinal)<0||line.IndexOf("\"usage\"",StringComparison.Ordinal)<0)continue;
    DateTime day,utc;if(!LocalDay(line,out day,out utc)||day<since)continue;
    Count(activity,day,"claude");if(day>lastClaude)lastClaude=day;if(day<tokenSince)continue;
    var model=RxModel.Match(line);string key="claude|"+(model.Success?model.Groups[1].Value:"?");long fresh=Tok(RxIn,line)+Tok(RxOut,line)+Tok(RxCreate,line),n=fresh+Tok(RxRead,line);
    tokens[key]=(tokens.ContainsKey(key)?tokens[key]:0)+n;
    if(utc>=nowUtc.AddHours(-5)){h5+=fresh;msgs5++;}if(utc>=nowUtc.AddDays(-7))week+=fresh;if(day==today)todayTokens+=fresh;
   }
  }
  string codexRoot=Path.Combine(home,".codex","sessions");DateTime limitAt=DateTime.MinValue;double primary=-1,secondary=-1,primaryReset=0;
  if(Directory.Exists(codexRoot))foreach(string file in SafeFiles(codexRoot,"rollout-*.jsonl",SearchOption.AllDirectories)){
   if(Old(file,since))continue;string current="?";
   foreach(string line in Lines(file)){
    if(line.IndexOf("\"turn_context\"",StringComparison.Ordinal)>=0){var model=RxModel.Match(line);if(model.Success)current=model.Groups[1].Value;continue;}
    if(line.IndexOf("\"token_count\"",StringComparison.Ordinal)<0)continue;
    DateTime day,utc;if(!LocalDay(line,out day,out utc))continue;
    var limit=RxPrimary.Match(line);if(limit.Success&&day>=limitAt){limitAt=day;primary=double.Parse(limit.Groups[1].Value,CultureInfo.InvariantCulture);primaryReset=Tok(RxPrimaryReset,line);var second=RxSecondary.Match(line);secondary=second.Success?double.Parse(second.Groups[1].Value,CultureInfo.InvariantCulture):-1;}
    if(day<since)continue;Count(activity,day,"codex");if(day>lastCodex)lastCodex=day;if(day<tokenSince)continue;
    string key="codex|"+current;tokens[key]=(tokens.ContainsKey(key)?tokens[key]:0)+Tok(RxLast,line);
   }
  }
  var codexLimits=new List<object>();if(primary>=0)codexLimits.Add(new Dictionary<string,object>{{"nome","Sessão"},{"pct",primary},{"reinicia",primaryReset}});if(secondary>=0)codexLimits.Add(new Dictionary<string,object>{{"nome","Semana"},{"pct",secondary}});
  var claudeLimits=new List<object>();
  try{string plan=Path.Combine(appData,"Claude","plan-usage-history.json");if(File.Exists(plan)){var samples=(IList)json.Deserialize<Dictionary<string,object>>(File.ReadAllText(plan))["samples"];var u=((Dictionary<string,object>)samples[samples.Count-1])["u"] as Dictionary<string,object>;
   if(u!=null&&u.ContainsKey("fh"))claudeLimits.Add(new Dictionary<string,object>{{"nome","5 horas"},{"pct",Num(u["fh"])}});if(u!=null&&u.ContainsKey("sd"))claudeLimits.Add(new Dictionary<string,object>{{"nome","Semana"},{"pct",Num(u["sd"])}});}}catch{}
  return new Dictionary<string,object>{
   {"atividade",activity},
   {"ativos",new Dictionary<string,object>{{"claude",lastClaude>=today.AddDays(-14)},{"codex",lastCodex>=today.AddDays(-14)}}},
   {"claude_uso",new Dictionary<string,object>{{"h5",h5},{"msgs5",msgs5},{"semana",week},{"hoje",todayTokens}}},
   {"tokens_por_modelo",tokens.OrderByDescending(x=>x.Value).Select(x=>(object)new Dictionary<string,object>{{"ia",x.Key.Split('|')[0]},{"modelo",x.Key.Split('|')[1]},{"tokens",x.Value}}).ToList()},
   {"limites",new Dictionary<string,object>{{"claude",claudeLimits},{"codex",codexLimits}}}};
 }
 static string PainelCache(){return Path.Combine(dataDir,"painel.json");}
 // One island scans; the others reuse its cache file (no repeated 100+ MB reads).
 static void PainelPoll(){
  if(painelBusy||contextHidden||DateTime.UtcNow<painelNext)return;painelBusy=true;painelNext=DateTime.UtcNow.AddSeconds(300);
  var thread=new System.Threading.Thread(()=>{
   Dictionary<string,object> data=null;bool ok=true;
   try{
    string cache=PainelCache();
    if(File.Exists(cache)&&DateTime.UtcNow-File.GetLastWriteTimeUtc(cache)<TimeSpan.FromSeconds(280))data=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(cache));
    else{
     using(var gate=new System.Threading.Mutex(false,"Local\\TitoPainelScan"))
     {
      if(gate.WaitOne(0)){try{data=PainelScan(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));File.WriteAllText(cache,json.Serialize(data));}finally{gate.ReleaseMutex();}}
      else{System.Threading.Thread.Sleep(8000);if(File.Exists(cache))data=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(cache));}
     }
    }
   }catch(Exception ex){ok=false;try{File.WriteAllText(Path.Combine(dataDir,"painel-error.txt"),ex.ToString());}catch{}}
   try{w.Dispatcher.BeginInvoke(new Action(()=>{painelBusy=false;if(ok&&data!=null)PainelSet(data);else painelState=2;if(painelState!=1)painelNext=DateTime.UtcNow.AddSeconds(30);if(On("geral")||On("uso"))Render();}));}catch{}
  }){IsBackground=true,Priority=System.Threading.ThreadPriority.BelowNormal};
  thread.Start();
 }
 static void PainelSet(Dictionary<string,object> data){painel=data;painelState=1;painelHigh=PainelLimits().Any(l=>Num(l["pct"])>=90);}
 // An IA counts as present only if it was used in the last 14 days; old leftover logs must not show up.
 static bool PainelActive(string ia){var a=painel!=null&&painel.ContainsKey("ativos")?painel["ativos"] as Dictionary<string,object>:null;return a==null||!a.ContainsKey(ia)||Convert.ToBoolean(a[ia]);}
 static double Num(object value){try{return Convert.ToDouble(value,CultureInfo.InvariantCulture);}catch{return 0;}}
 static List<Dictionary<string,object>> PainelList(object value){var list=new List<Dictionary<string,object>>();var items=value as IEnumerable;if(items!=null)foreach(object item in items){var d=item as Dictionary<string,object>;if(d!=null)list.Add(d);}return list;}
 static List<Dictionary<string,object>> PainelLimits(){
  var all=new List<Dictionary<string,object>>();if(painel==null||!painel.ContainsKey("limites"))return all;
  var limits=painel["limites"] as Dictionary<string,object>;if(limits==null)return all;
  foreach(string who in new[]{"claude","codex"})if(limits.ContainsKey(who)&&PainelActive(who))foreach(var l in PainelList(limits[who])){var row=new Dictionary<string,object>(l);row["ia"]=who;all.Add(row);}
  return all;
 }
 static string Millions(double n){return n>=1e6?(n/1e6).ToString("0.0",CultureInfo.InvariantCulture)+" M":n>=1e3?(n/1e3).ToString("0",CultureInfo.InvariantCulture)+" k":n.ToString("0");}
 static void PainelSelfCheck(){
  string home=Path.Combine(Path.GetTempPath(),"tito-painel-"+Guid.NewGuid().ToString("N"));
  try{
   string stamp=DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture);
   Directory.CreateDirectory(Path.Combine(home,".claude","projects","p"));Directory.CreateDirectory(Path.Combine(home,".codex","sessions","2026"));
   File.WriteAllText(Path.Combine(home,".claude","projects","p","a.jsonl"),"{\"type\":\"assistant\",\"timestamp\":\""+stamp+"\",\"message\":{\"model\":\"claude-x\",\"usage\":{\"input_tokens\":5,\"output_tokens\":7,\"cache_creation_input_tokens\":1,\"cache_read_input_tokens\":2}}}\n{\"type\":\"user\"}\nlixo\n");
   File.WriteAllText(Path.Combine(home,".codex","sessions","2026","rollout-1.jsonl"),"{\"type\":\"turn_context\",\"payload\":{\"model\":\"gpt-x\"}}\n{\"timestamp\":\""+stamp+"\",\"payload\":{\"type\":\"token_count\",\"info\":{\"last_token_usage\":{\"total_tokens\":9}},\"rate_limits\":{\"primary\":{\"used_percent\":12.5,\"resets_at\":1},\"secondary\":{\"used_percent\":40.0,\"resets_at\":2}}}}\n");
   string longDir=Path.Combine(home,".claude","projects",new string('p',200));Directory.CreateDirectory(@"\\?\"+longDir);
   File.WriteAllText(@"\\?\"+Path.Combine(longDir,"b.jsonl"),"{\"type\":\"assistant\",\"timestamp\":\""+stamp+"\",\"message\":{\"model\":\"claude-long\",\"usage\":{\"input_tokens\":1,\"output_tokens\":2}}}\n");
   var data=PainelScan(home,Path.Combine(home,"none"));var today=(Dictionary<string,object>)((Dictionary<string,object>)data["atividade"]).Values.First();
   var tokens=PainelList(data["tokens_por_modelo"]);var codex=PainelList(((Dictionary<string,object>)data["limites"])["codex"]);
   var uso=(Dictionary<string,object>)data["claude_uso"];var ativos=(Dictionary<string,object>)data["ativos"];
   if(Num(uso["h5"])!=16||Convert.ToInt32(uso["msgs5"])!=2||!Convert.ToBoolean(ativos["claude"])||!Convert.ToBoolean(ativos["codex"])||Convert.ToInt32(today["claude"])!=2||Convert.ToInt32(today["codex"])!=1||!tokens.Any(t=>(string)t["modelo"]=="claude-x"&&Num(t["tokens"])==15)||!tokens.Any(t=>(string)t["modelo"]=="gpt-x"&&Num(t["tokens"])==9)||!tokens.Any(t=>(string)t["modelo"]=="claude-long"&&Num(t["tokens"])==3)||codex.Count!=2||Num(codex[0]["pct"])!=12.5||Num(codex[1]["pct"])!=40)throw new Exception("Painel IA log reader");
  }finally{try{Directory.Delete(@"\\?\"+home,true);}catch{}}
 }
 static Border Tile(string title,UIElement content,int column){
  var stack=Stack();var label=T(title,9.5,Theme.Muted);label.Margin=new Thickness(0,0,0,6);stack.Children.Add(label);stack.Children.Add(content);
  var tile=new Border{Background=B(Theme.Card),BorderBrush=B("#14FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Padding=new Thickness(11,9,11,9),Margin=new Thickness(column==0?0:4,0,column==2?0:4,0),Child=stack};Grid.SetColumn(tile,column);return tile;
 }
 static UIElement LimitRow(string label,double pct,double resets=0){
  if(resets>0&&resets<=(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds){pct=0;label+=" · renovou";}
  pct=Math.Max(0,Math.Min(100,pct));string color=pct>=90?Theme.Bad:pct>=70?Theme.Warn:Theme.Ok;
  var head=new Grid();head.Children.Add(T(label,11.5));var value=T(Math.Round(pct)+"%",11.5,color);value.HorizontalAlignment=HorizontalAlignment.Right;head.Children.Add(value);
  var bar=new Grid{Height=6,Margin=new Thickness(0,3,0,7)};bar.Children.Add(new Border{Background=B("#26FFFFFF"),CornerRadius=new CornerRadius(3)});
  var fill=new Border{Background=B(color),CornerRadius=new CornerRadius(3),HorizontalAlignment=HorizontalAlignment.Left};bar.SizeChanged+=(s,e)=>fill.Width=bar.ActualWidth*pct/100;bar.Children.Add(fill);
  var row=Stack();row.Children.Add(head);row.Children.Add(bar);return row;
 }
 static UIElement UsageRow(string label,string value){
  var row=new Grid{Margin=new Thickness(0,0,0,5)};row.Children.Add(T(label,11.5));var v=T(value,11.5,Theme.Info);v.HorizontalAlignment=HorizontalAlignment.Right;row.Children.Add(v);return row;
 }
 static void PainelPage(){
  var root=new Grid();view.Children.Add(root);
  var act=painel!=null&&painel.ContainsKey("atividade")?painel["atividade"] as Dictionary<string,object>:null;
  bool empty=painel!=null&&((!PainelActive("claude")&&!PainelActive("codex"))||((act==null||act.Count==0)&&PainelLimits().Count==0));
  if(painelState!=1||painel==null||empty){
   var box=Stack();box.VerticalAlignment=VerticalAlignment.Center;box.HorizontalAlignment=HorizontalAlignment.Center;
   var title=T(painelState==0?"Painel IA · lendo seus logs…":painelState==2?"Não consegui ler os logs":"Sem uso de IA por aqui ainda",18);title.HorizontalAlignment=HorizontalAlignment.Center;box.Children.Add(title);
   var hint=T(painelState==0?"Só na primeira vez pode levar alguns segundos.":painelState==2?"Tente de novo em instantes.":"Use o Claude Code ou o Codex neste PC e o painel aparece sozinho.",11,Theme.Muted);hint.HorizontalAlignment=HorizontalAlignment.Center;hint.Margin=new Thickness(0,4,0,10);box.Children.Add(hint);
   if(painelState==2){var retry=PillButton("Tentar de novo",()=>{painelNext=DateTime.UtcNow;painelState=0;Switch("painel");});retry.HorizontalAlignment=HorizontalAlignment.Center;box.Children.Add(retry);}
   root.Children.Add(box);return;
  }
  for(int i=0;i<3;i++)root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(i==0?1.15:1,GridUnitType.Star)});
  var limits=Stack();var rows=PainelLimits();
  if(PainelActive("claude")){
   foreach(var l in rows.Where(r=>Convert.ToString(r["ia"])=="claude"))limits.Children.Add(LimitRow("Claude · "+Convert.ToString(l["nome"]),Num(l["pct"])));
   var use=painel.ContainsKey("claude_uso")?painel["claude_uso"] as Dictionary<string,object>:null;
   if(use!=null){limits.Children.Add(UsageRow("Claude · 5 h",Millions(Num(use["h5"]))+" · "+Num(use["msgs5"])+" msg"));limits.Children.Add(UsageRow("Claude · hoje",Millions(Num(use["hoje"]))));limits.Children.Add(UsageRow("Claude · 7 dias",Millions(Num(use["semana"]))));}
  }
  if(PainelActive("codex")){
   foreach(var l in rows.Where(r=>Convert.ToString(r["ia"])=="codex"))limits.Children.Add(LimitRow("Codex · "+Convert.ToString(l["nome"]),Num(l["pct"]),l.ContainsKey("reinicia")?Num(l["reinicia"]):0));
  }
  root.Children.Add(Tile("USO AGORA",limits,0));
  var used=Stack();var tokens=(painel.ContainsKey("tokens_por_modelo")?PainelList(painel["tokens_por_modelo"]):new List<Dictionary<string,object>>()).Where(t=>PainelActive(Convert.ToString(t["ia"]))).ToList();
  var total=T(Millions(tokens.Sum(x=>Num(x["tokens"]))),24);total.FontWeight=FontWeights.SemiBold;used.Children.Add(total);
  foreach(var x in tokens.Where(t=>Num(t["tokens"])>0).Take(4)){var line=new Grid{Margin=new Thickness(0,2,0,0)};var name=T(Convert.ToString(x["modelo"]).Replace("claude-",""),10.5,"#D7CFD0");name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(0,0,48,0);line.Children.Add(name);var n=T(Millions(Num(x["tokens"])),10.5,Theme.Muted);n.HorizontalAlignment=HorizontalAlignment.Right;line.Children.Add(n);used.Children.Add(line);}
  root.Children.Add(Tile("TOKENS · 30 DIAS",used,1));
  var weeks=new double[13];int claude=0,codex=0;
  if(act!=null)foreach(var day in act){DateTime d;if(!DateTime.TryParseExact(day.Key,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d))continue;int back=(int)(DateTime.Now.Date-d).TotalDays/7;var counts=day.Value as Dictionary<string,object>;if(counts==null||back<0||back>12)continue;foreach(var c in counts){if(!PainelActive(c.Key))continue;weeks[12-back]+=Num(c.Value);if(c.Key=="claude")claude+=(int)Num(c.Value);else codex+=(int)Num(c.Value);}}
  var bars=new Grid{Height=88};double max=Math.Max(1,weeks.Max());for(int i=0;i<13;i++)bars.ColumnDefinitions.Add(new ColumnDefinition());
  for(int i=0;i<13;i++){var bar=new Border{Background=B(i==12?Theme.Info:"#3A8FB5"),CornerRadius=new CornerRadius(2),VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(1.2,0,1.2,0),Height=Math.Max(2,80*weeks[i]/max)};Grid.SetColumn(bar,i);bars.Children.Add(bar);}
  var activity=Stack();activity.Children.Add(bars);var legend=T(String.Join(" · ",new[]{PainelActive("claude")?"Claude "+claude:null,PainelActive("codex")?"Codex "+codex:null}.Where(x=>x!=null)),10,Theme.Muted);legend.Margin=new Thickness(0,5,0,0);legend.TextTrimming=TextTrimming.CharacterEllipsis;activity.Children.Add(legend);
  root.Children.Add(Tile("ATIVIDADE · 13 SEMANAS",activity,2));
 }
}
}
