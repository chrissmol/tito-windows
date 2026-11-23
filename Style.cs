using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
namespace Tito {
// One place for the colors and the font the screens share; change them here.
static class Theme {
 public const string Text="#F5F3F8",Muted="#B8B3C2",Soft="#B8B3C2",Heading="#E3D9E8",Ok="#78DDB0",Info="#76C9F2",Warn="#F4C47A",Bad="#F28B91",Card="#14FFFFFF",Field="#18FFFFFF",Accent="#0A84FF",Font="./Fonts/#Inter";
 // Escala única de texto e espaços: toda tela nova usa estes valores em vez de números soltos.
 public const double Title=22,Body=12,Caption=11,Gap=8;
}
public partial class App {
 static ResourceDictionary sharedStyles;
 // Texto nítido em toda janela: formatação Display encaixa os glifos na grade de pixels e o arredondamento de layout evita linhas borradas.
 static void Crisp(Window win){TextOptions.SetTextFormattingMode(win,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(win,TextRenderingMode.Grayscale);TextOptions.SetTextHintingMode(win,TextHintingMode.Fixed);win.UseLayoutRounding=true;win.SnapsToDevicePixels=true;}
 // Legenda pequena acima de um campo.
 static TextBlock Caption(string s){var t=T(s,Theme.Caption,Theme.Muted);t.Margin=new Thickness(4,0,0,3);return t;}
 // Campo padrão: legenda em cima, texto de exemplo (Tag) quando vazio. Use sempre este em vez de new TextBox.
 static StackPanel Field(string label,string hint,string value,Action<string> set,double height,bool multi){
  var box=new TextBox{Text=value,Tag=hint,AcceptsReturn=multi,TextWrapping=multi?TextWrapping.Wrap:TextWrapping.NoWrap,VerticalContentAlignment=multi?VerticalAlignment.Top:VerticalAlignment.Center,VerticalScrollBarVisibility=multi?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden};
  if(height>0)box.Height=height;System.Windows.Automation.AutomationProperties.SetName(box,label);box.TextChanged+=(s,e)=>set(box.Text);
  var col=Stack();col.Children.Add(Caption(label));col.Children.Add(box);return col;
 }
 // Implicit styles so every screen gets thin scrollbars and dark rounded dropdowns instead of Windows defaults.
 static void ApplyStyles(){
  var styles=(ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
<Style TargetType='ScrollBar'><Setter Property='Width' Value='6'/><Setter Property='MinWidth' Value='0'/><Setter Property='HorizontalAlignment' Value='Right'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border CornerRadius='3' Background='#50FFFFFF' Margin='1,0'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='#FFF8F6'/><Setter Property='Padding' Value='10,5'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='b' CornerRadius='7' Padding='{TemplateBinding Padding}' Background='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='b' Property='Background' Value='#33FFFFFF'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ComboBox'><Setter Property='Foreground' Value='#FFF8F6'/><Setter Property='FontSize' Value='12'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid>
<ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen,Mode=TwoWay,RelativeSource={RelativeSource TemplatedParent}}'><ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='#26FFFFFF' CornerRadius='10'><Path Data='M0,0 L4,4 8,0' Stroke='#B0FFFFFF' StrokeThickness='1.5' StrokeStartLineCap='Round' StrokeEndLineCap='Round' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,0'/></Border></ControlTemplate></ToggleButton.Template></ToggleButton>
<ContentPresenter Content='{TemplateBinding SelectionBoxItem}' Margin='12,5,28,5' IsHitTestVisible='False' VerticalAlignment='Center' HorizontalAlignment='Left'/>
<Popup IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='Fade'><Border MinWidth='{TemplateBinding ActualWidth}' MaxHeight='170' Background='#F4121216' CornerRadius='10' Margin='0,4,12,12' Padding='4'><ScrollViewer><StackPanel IsItemsHost='True'/></ScrollViewer></Border></Popup>
</Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='Slider'><Setter Property='Height' Value='20'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Slider'><Grid Background='Transparent'><Border Height='4' CornerRadius='2' Background='#33FFFFFF' VerticalAlignment='Center'/><Track x:Name='PART_Track'><Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' IsTabStop='False' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='4' CornerRadius='2' Background='#FFFFFFFF' VerticalAlignment='Center'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton><Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' IsTabStop='False' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='20' Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton><Track.Thumb><Thumb Width='14' Height='14' Focusable='False'><Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse Fill='White'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='MenuItem'><Setter Property='Foreground' Value='#FFF8F6'/><Setter Property='Padding' Value='14,7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='MenuItem'><Border x:Name='b' CornerRadius='8' Padding='{TemplateBinding Padding}' Background='Transparent'><ContentPresenter ContentSource='Header' RecognizesAccessKey='True'/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='b' Property='Background' Value='#33FFFFFF'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='ContextMenu'><Setter Property='Foreground' Value='#FFF8F6'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ContextMenu'><Border Background='#F4141418' CornerRadius='12' Padding='4' Margin='4'><StackPanel IsItemsHost='True'/></Border></ControlTemplate></Setter.Value></Setter></Style>
<Style TargetType='TextBox'><Setter Property='Foreground' Value='#F5F3F8'/><Setter Property='Background' Value='#18FFFFFF'/><Setter Property='BorderBrush' Value='#14FFFFFF'/><Setter Property='BorderThickness' Value='1'/><Setter Property='CaretBrush' Value='White'/><Setter Property='SelectionBrush' Value='#990A84FF'/><Setter Property='Padding' Value='10,6'/><Setter Property='FontSize' Value='12'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border x:Name='b' CornerRadius='10' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><Grid><ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/><TextBlock x:Name='ph' Text='{TemplateBinding Tag}' Foreground='#80FFFFFF' IsHitTestVisible='False' Visibility='Collapsed' Margin='{TemplateBinding Padding}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' TextTrimming='CharacterEllipsis'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='Text' Value=''><Setter TargetName='ph' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#B0FFFFFF'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>");
  sharedStyles=styles;w.Resources.MergedDictionaries.Add(styles);Crisp(w);
 }
}
}
