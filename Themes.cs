using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
namespace Tito {
public partial class App {
 // Selectable color themes. Each row: id, name, accents for music, focus, IA, agenda, notes, idle eye color.
 // They tint buttons, the drawer glow, the slow glass backdrop and the eyes' mood colors. The eyes stay eyes: only their color changes.
 static readonly string[][] themeData={
  new[]{"padrao","Padrão","#FF375F","#BF5AF2","#FF9F0A","#FF453A","#FFD60A","#FFFFFF"},
  new[]{"neon","Neon","#FF2E93","#A855FF","#00F0FF","#39FF88","#FFF000","#B8FFF2"},
  new[]{"sol","Pôr do sol","#FF5E62","#FF9966","#FFB347","#FF6FA3","#FFD36E","#FFE2C2"},
  new[]{"oceano","Oceano","#00B4D8","#4895EF","#48CAE4","#2EC4B6","#90E0EF","#D4F4FF"},
  new[]{"sakura","Sakura","#FF8FB1","#FFB3C7","#E5A3FF","#FF9AA2","#FFD6E0","#FFE3EC"},
  new[]{"retro","Retrô","#FF3EA5","#FFE600","#00E5FF","#7CFF3F","#FF7A00","#FFF3A8"},
  new[]{"capa","Seguir a capa"}};
 static string themeId;static Nullable<Color> coverColor;static GradientStop[] backdrop;
 static string ThemeFile(){return System.IO.Path.Combine(dataDir,"tema.txt");}
 static string Hex(Color c){return "#"+c.R.ToString("X2")+c.G.ToString("X2")+c.B.ToString("X2");}
 static Color Lighten(Color c,double t){return Color.FromRgb((byte)(c.R+(255-c.R)*t),(byte)(c.G+(255-c.G)*t),(byte)(c.B+(255-c.B)*t));}
 static Color Hue(Color c,double deg){
  double r=c.R/255.0,g=c.G/255.0,b=c.B/255.0,max=Math.Max(r,Math.Max(g,b)),d=max-Math.Min(r,Math.Min(g,b)),h=0;
  if(d>0){if(max==r)h=((g-b)/d+6)%6;else if(max==g)h=(b-r)/d+2;else h=(r-g)/d+4;}
  h=(h*60+deg+360)%360;double s=max<=0?0:d/max;Func<double,byte> f=n=>{double k=(n+h/60)%6;return (byte)Math.Round(255*(max-max*s*Math.Max(0,Math.Min(Math.Min(k,4-k),1))));};
  return Color.FromRgb(f(5),f(3),f(1));
 }
 // The current theme's row; "capa" derives its accents from the cover color and falls back to the default until a cover is known.
 static string[] ThemeRow(){
  if(themeId==null){themeId="padrao";try{if(File.Exists(ThemeFile()))themeId=File.ReadAllText(ThemeFile()).Trim();}catch{}}
  if(themeId=="capa"&&coverColor.HasValue){Color c=coverColor.Value;return new[]{"capa","",Hex(c),Hex(Hue(c,40)),Hex(Hue(c,-40)),Hex(Hue(c,80)),Hex(Hue(c,-80)),Hex(Lighten(c,.6))};}
  foreach(var r in themeData)if(r[0]==themeId&&r.Length>3)return r;
  return themeData[0];
 }
 static string ThemeAccent(string page){var r=ThemeRow();int i=Array.IndexOf(new[]{"music","focus","ia","agenda","notes"},page);return r[0]=="padrao"||i<0?null:r[2+i];}
 static DropShadowEffect CoverGlow(){return coverColor.HasValue?new DropShadowEffect{BlurRadius=42,ShadowDepth=0,Opacity=.8,Color=coverColor.Value}:new DropShadowEffect{BlurRadius=28,ShadowDepth=6,Direction=270,Opacity=.55,Color=Colors.Black};}
 static void CoverColor(Color c){coverColor=c;if(coverCard!=null)coverCard.Effect=CoverGlow();if(themeId=="capa")ApplyTheme(false);}
 static Border MakeBackdrop(){
  backdrop=new[]{new GradientStop(Colors.Transparent,0),new GradientStop(Colors.Transparent,.55),new GradientStop(Colors.Transparent,1)};
  var stops=new GradientStopCollection();foreach(var g in backdrop)stops.Add(g);
  var b=new Border{IsHitTestVisible=false,Background=new LinearGradientBrush(stops,35)};ApplyBackdrop();return b;
 }
 // Very low alpha so the text on the dark glass keeps its contrast; it drifts slowly, at a few frames per second, and only while the panel is open.
 static void ApplyBackdrop(){
  if(backdrop==null)return;var r=ThemeRow();bool run=opened&&SystemParameters.ClientAreaAnimation;
  for(int i=0;i<3;i++){
   Color c=HexColor(r[2+i]),next=HexColor(r[2+(i+1)%3]);c.A=next.A=0x2C;
   backdrop[i].BeginAnimation(GradientStop.ColorProperty,null);backdrop[i].Color=c;
   if(run){var a=new ColorAnimation(c,next,TimeSpan.FromSeconds(16)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever};Timeline.SetDesiredFrameRate(a,8);backdrop[i].BeginAnimation(GradientStop.ColorProperty,a);}
  }
 }
 static void ApplyTheme(bool render){
  var r=ThemeRow();bool on=r[0]!="padrao";
  MascotEyes.IdleTint=on?HexColor(r[7]):(Nullable<Color>)null;MascotEyes.FocusTint=on?Lighten(HexColor(r[3]),.35):(Nullable<Color>)null;MascotEyes.ThinkTint=on?Lighten(HexColor(r[4]),.35):(Nullable<Color>)null;
  foreach(var m in new[]{mascot,headerMascot})if(m!=null){m.WaveColor=on?Lighten(HexColor(r[2]),.3):Color.FromRgb(83,190,255);m.Retint();}
  ApplyBackdrop();SetGlow(page);if(render&&view!=null)Render();
 }
 // Self-check for the UI test: every theme applies, accents come from the theme and the cover theme follows the cover.
 static void CheckThemes(){
  foreach(var r in themeData){
   themeId=r[0];coverColor=Color.FromRgb(200,60,90);ApplyTheme(true);var row=ThemeRow();
   if(r[0]=="padrao"?ThemeAccent("music")!=null:ThemeAccent("music")!=row[2]||MascotEyes.IdleTint==null)throw new Exception("Theme "+r[0]);
   if(r[0]=="capa"&&ThemeAccent("music")!=Hex(coverColor.Value))throw new Exception("Cover theme");
  }
  themeId="padrao";coverColor=null;ApplyTheme(true);
 }
 static UIElement ThemeCard(){
  var box=Stack();var title=T("Tema e cores",12,Theme.Heading);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(8,6,0,6);box.Children.Add(title);
  var pick=new ComboBox{Height=28,Margin=new Thickness(4,0,4,6)};ThemeRow();
  foreach(var r in themeData){pick.Items.Add(new ComboBoxItem{Content=r[1],Tag=r[0]});if(r[0]==themeId)pick.SelectedIndex=pick.Items.Count-1;}
  pick.SelectionChanged+=(s,e)=>{var it=pick.SelectedItem as ComboBoxItem;if(it==null||(string)it.Tag==themeId)return;themeId=(string)it.Tag;try{File.WriteAllText(ThemeFile(),themeId);}catch{}ApplyTheme(true);};
  box.Children.Add(pick);return CardBox(box);
 }
}
}
