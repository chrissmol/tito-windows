using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace Tito {
// One place for the colors and the font the screens share; change them here.
static class Theme {
 public const string Text="#F5F3F8",Muted="#B8B3C2",Soft="#B8B3C2",Heading="#E3D9E8",Ok="#78DDB0",Info="#76C9F2",Warn="#F4C47A",Bad="#F28B91",Card="#14FFFFFF",Font="./Fonts/#Inter";
}
public partial class App {
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
</ResourceDictionary>");
  w.Resources.MergedDictionaries.Add(styles);
 }
}
}
