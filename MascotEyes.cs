using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace Tito {
// Tito: two eyes that change shape, color and movement with what the app is doing.
// Continuous animations only run for moods that need them (stepped, a few redraws per second).
public sealed class MascotEyes : FrameworkElement {
 public static readonly DependencyProperty BlinkProperty=DependencyProperty.Register("Blink",typeof(double),typeof(MascotEyes),new FrameworkPropertyMetadata(1.0,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Blink {get{return (double)GetValue(BlinkProperty);}set{SetValue(BlinkProperty,value);}}
 public static readonly DependencyProperty BlendProperty=DependencyProperty.Register("Blend",typeof(double),typeof(MascotEyes),new FrameworkPropertyMetadata(1.0,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Blend {get{return (double)GetValue(BlendProperty);}set{SetValue(BlendProperty,value);}}
 public static readonly DependencyProperty LiftProperty=DependencyProperty.Register("Lift",typeof(double),typeof(MascotEyes),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Lift {get{return (double)GetValue(LiftProperty);}set{SetValue(LiftProperty,value);}}
 public static readonly DependencyProperty PhaseProperty=DependencyProperty.Register("Phase",typeof(double),typeof(MascotEyes),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Phase {get{return (double)GetValue(PhaseProperty);}set{SetValue(PhaseProperty,value);}}
 public double GazeX,GazeY;
 public static readonly DependencyProperty MediaBlendProperty=DependencyProperty.Register("MediaBlend",typeof(double),typeof(MascotEyes),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
 public double MediaBlend {get{return (double)GetValue(MediaBlendProperty);}set{SetValue(MediaBlendProperty,value);}}
 public static readonly DependencyProperty WaveStepProperty=DependencyProperty.Register("WaveStep",typeof(double),typeof(MascotEyes),new FrameworkPropertyMetadata(1.0,FrameworkPropertyMetadataOptions.AffectsRender));
 public double WaveStep {get{return (double)GetValue(WaveStepProperty);}set{SetValue(WaveStepProperty,value);}}
 bool mediaMode;readonly double[] peaks=new double[7],waveFrom=new double[7],waveTo=new double[7];double envelope,agc;public Color WaveColor=Color.FromRgb(83,190,255);
 readonly RotateTransform tilt=new RotateTransform();
 public MascotEyes(){RenderTransform=tilt;RenderTransformOrigin=new Point(.5,.5);}
 public bool MediaMode {get{return mediaMode;}set{if(mediaMode==value)return;mediaMode=value;if(SystemParameters.ClientAreaAnimation)BeginAnimation(MediaBlendProperty,new DoubleAnimation(value?1:0,TimeSpan.FromMilliseconds(280)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut}});else{BeginAnimation(MediaBlendProperty,null);MediaBlend=value?1:0;}}}
 public void AudioPeak(double value){if(Double.IsNaN(value)||Double.IsInfinity(value))value=0;value=Math.Max(0,Math.Min(1,value));agc=Math.Max(value,agc*.997);value=Math.Min(1,value/Math.Max(.18,agc));double blend=WaveStep;for(int i=0;i<7;i++)waveFrom[i]=Mix(waveFrom[i],waveTo[i],blend);envelope+=(value-envelope)*(value>envelope?.5:.16);for(int i=0;i<6;i++)peaks[i]=peaks[i+1];peaks[6]=envelope;for(int i=0;i<7;i++)waveTo[i]=peaks[i]*.6+peaks[Math.Max(0,i-1)]*.2+peaks[Math.Min(6,i+1)]*.2;BeginAnimation(WaveStepProperty,null);WaveStep=0;if(mediaMode&&SystemParameters.ClientAreaAnimation)BeginAnimation(WaveStepProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(95)));else WaveStep=1;InvalidateVisual();}
 struct Face {public double Left,Right,Width,SmileL,SmileR,R,G,B,Heart,Spiral,Tilt,LookX,LookY,Glint;}
 static Face Pose(string mood){
  var f=new Face{Left=11,Right=10.5,Width=6,R=255,G=255,B=255};
  switch(mood){
   case "sleepy":f.Left=3;f.Right=3;f.R=110;f.G=150;f.B=255;break;
   case "focus":f.Left=7;f.Right=7;f.R=188;f.G=131;f.B=255;break;
   case "curious":f.Left=13;f.Right=9;f.R=64;f.G=255;f.B=224;break;
   case "surprised":f.Left=14;f.Right=14;f.Width=7;f.R=255;f.G=138;f.B=76;break;
   case "happy":f.Left=4;f.Right=4;f.SmileL=1;f.SmileR=1;f.R=255;f.G=214;f.B=64;break;
   case "excited":f.Left=15;f.Right=15;f.Width=7;f.Glint=1;f.R=255;f.G=196;f.B=0;break;
   case "love":f.Heart=1;f.R=255;f.G=92;f.B=138;break;
   case "wink":f.Right=4;f.SmileR=1;f.R=255;f.G=236;f.B=170;break;
   case "thinking":f.Left=9;f.Right=9;f.LookX=2.5;f.LookY=-3;f.R=90;f.G=220;f.B=255;break;
   case "worry":f.Left=11;f.Right=11;f.Tilt=16;f.R=255;f.G=176;f.B=64;break;
   case "dizzy":f.Spiral=1;f.R=190;f.G=120;f.B=255;break;
  }
  Nullable<Color> o=mood=="idle"?IdleTint:mood=="focus"?FocusTint:mood=="thinking"?ThinkTint:null;if(o.HasValue){f.R=o.Value.R;f.G=o.Value.G;f.B=o.Value.B;}
  return f;
 }
 // Theme colors for the idle, focus and thinking eyes (null = the built-in colors).
 public static Nullable<Color> IdleTint,FocusTint,ThinkTint;
 public void Retint(){from=to=Pose(mood);InvalidateVisual();}
 Face from=Pose("idle"),to=Pose("idle");string mood="idle";
 static double Mix(double a,double b,double t){return a+(b-a)*t;}
 Face Current(){double t=Blend;return new Face{Left=Mix(from.Left,to.Left,t),Right=Mix(from.Right,to.Right,t),Width=Mix(from.Width,to.Width,t),SmileL=Mix(from.SmileL,to.SmileL,t),SmileR=Mix(from.SmileR,to.SmileR,t),R=Mix(from.R,to.R,t),G=Mix(from.G,to.G,t),B=Mix(from.B,to.B,t),Heart=Mix(from.Heart,to.Heart,t),Spiral=Mix(from.Spiral,to.Spiral,t),Tilt=Mix(from.Tilt,to.Tilt,t),LookX=Mix(from.LookX,to.LookX,t),LookY=Mix(from.LookY,to.LookY,t),Glint=Mix(from.Glint,to.Glint,t)};}
 public string Mood {get{return mood;}set{if(mood==value)return;from=Current();to=Pose(value);mood=value;BeginAnimation(BlendProperty,null);Blend=0;if(SystemParameters.ClientAreaAnimation)BeginAnimation(BlendProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(230)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});else Blend=1;
   BeginAnimation(PhaseProperty,null);Phase=0;
   if(SystemParameters.ClientAreaAnimation){
    if(value=="thinking"||value=="dizzy"||value=="excited"){var steps=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(1200),RepeatBehavior=RepeatBehavior.Forever};for(int i=0;i<8;i++)steps.KeyFrames.Add(new DiscreteDoubleKeyFrame(i,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i*150))));BeginAnimation(PhaseProperty,steps);}
    if(value=="happy"||value=="love"||value=="surprised")Hop(1);else if(value=="excited")Hop(2);else if(value=="curious"||value=="dizzy")Wiggle();
   }}}
 public void Hop(int times){
  if(!SystemParameters.ClientAreaAnimation)return;
  var a=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(360*times),FillBehavior=FillBehavior.Stop};
  for(int i=0;i<times;i++){a.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i*360))));a.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i*360+150))));a.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i*360+340))));}
  BeginAnimation(LiftProperty,a);
 }
 public void Wiggle(){
  if(!SystemParameters.ClientAreaAnimation)return;
  var a=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(480),FillBehavior=FillBehavior.Stop};double[] angles={0,-9,8,-6,4,0};
  for(int i=0;i<angles.Length;i++)a.KeyFrames.Add(new LinearDoubleKeyFrame(angles[i],KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i*96))));
  tilt.BeginAnimation(RotateTransform.AngleProperty,a);
 }
 public void BlinkOnce(bool twice=false){
  if(!SystemParameters.ClientAreaAnimation)return;
  var animation=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(twice?400:170),FillBehavior=FillBehavior.Stop};
  animation.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.Zero)));
  animation.KeyFrames.Add(new LinearDoubleKeyFrame(.12,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(75))));
  animation.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170))));
  if(twice){animation.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(230))));animation.KeyFrames.Add(new LinearDoubleKeyFrame(.12,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));animation.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));}
  BeginAnimation(BlinkProperty,animation);
 }
 protected override void OnRender(DrawingContext dc){
  base.OnRender(dc);dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,ActualWidth,ActualHeight));
  Face f=Current();double lift=Lift;double x=ActualWidth/2+GazeX+f.LookX,y=ActualHeight/2+GazeY+f.LookY-5*lift;
  var brush=new SolidColorBrush(Color.FromRgb((byte)f.R,(byte)f.G,(byte)f.B));
  // Música: os dois olhos pulsam com o áudio e puxam a cor da mídia; nada além dos olhos é desenhado.
  double morph=MediaBlend,pulse=0;
  if(morph>0){double level=0;for(int i=0;i<7;i++)level+=Mix(waveFrom[i],waveTo[i],WaveStep);level/=7;pulse=morph*(SystemParameters.ClientAreaAnimation?Math.Sqrt(Math.Max(0,level))*.6:0);
   var wc=WaveColor;brush=new SolidColorBrush(Color.FromRgb((byte)Mix(f.R,wc.R,morph),(byte)Mix(f.G,wc.G,morph),(byte)Mix(f.B,wc.B,morph)));}
  if(mood=="thinking")x+=Blend*2.5*Math.Sin(Phase*Math.PI/4); // pensando: olhar vai de um lado para o outro
  Eye(dc,brush,x-7,y,f.Left*(1+pulse),f,f.SmileL,-1,lift);Eye(dc,brush,x+7,y,f.Right*(1+pulse),f,f.SmileR,1,lift);
 }
 void Eye(DrawingContext dc,Brush brush,double x,double y,double height,Face f,double smile,int side,double lift){
  double plain=(1-smile)*(1-f.Heart)*(1-f.Spiral);
  if(plain>.001){
   double h=Math.Max(1,height*Blink*(1+.18*lift)),w=f.Width*(1+(1-Blink)*.35);dc.PushOpacity(plain);
   if(f.Tilt>.01)dc.PushTransform(new RotateTransform(f.Tilt*side,x,y));
   dc.DrawRoundedRectangle(brush,null,new Rect(x-w/2,y-h/2,w,h),w/2,w/2);
   if(f.Glint>.01){dc.PushOpacity(f.Glint*(((int)Phase)%2==0?1:.35));dc.DrawEllipse(Brushes.White,null,new Point(x-w*.15,y-h*.22),1.6,1.6);dc.Pop();} // brilho nos olhos
   if(f.Tilt>.01)dc.Pop();dc.Pop();
  }
  double arc=smile*(1-f.Heart)*(1-f.Spiral);
  if(arc>.001){dc.PushOpacity(arc);var pen=new Pen(brush,2.6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};var curve=new StreamGeometry();using(var c=curve.Open()){c.BeginFigure(new Point(x-3,y+1),false,false);c.QuadraticBezierTo(new Point(x,y-4),new Point(x+3,y+1),true,false);}dc.DrawGeometry(null,pen,curve);dc.Pop();}
  if(f.Heart>.001){dc.PushOpacity(f.Heart);dc.PushTransform(new TranslateTransform(x,y));dc.PushTransform(new ScaleTransform(1.35+.2*lift,1.35+.2*lift));dc.DrawGeometry(brush,null,Geometry.Parse("M0,4 C-6,-1 -4,-6 0,-3 C4,-6 6,-1 0,4 Z"));dc.Pop();dc.Pop();dc.Pop();}
  if(f.Spiral>.001){
   dc.PushOpacity(f.Spiral);var pen=new Pen(brush,1.4){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};var spiral=new StreamGeometry();double spin=Phase*Math.PI/4;
   using(var c=spiral.Open()){for(int i=0;i<=30;i++){double t=i/30.0,a=spin+t*Math.PI*4,r=.4+t*4.6;var p=new Point(x+Math.Cos(a)*r,y+Math.Sin(a)*r);if(i==0)c.BeginFigure(p,false,false);else c.LineTo(p,true,true);}}
   dc.DrawGeometry(null,pen,spiral);dc.Pop();
  }
 }
}
}
