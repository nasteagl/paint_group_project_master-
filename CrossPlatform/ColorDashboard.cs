using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaSharp;

namespace Paint1.CrossPlatform;

public sealed class ColorDashboard : StackPanel
{
    readonly PaintButton outlineTarget=new(),fillTarget=new();
    readonly Border outlinePreview=new(){Width=29,Height=29,BorderBrush=Brushes.White,BorderThickness=new Thickness(3)},fillPreview=new(){Width=29,Height=29,BorderBrush=Brushes.White,BorderThickness=new Thickness(3)};
    readonly Slider red=new(){Minimum=0,Maximum=255},green=new(){Minimum=0,Maximum=255},blue=new(){Minimum=0,Maximum=255};
    readonly TextBox hex=new(){PlaceholderText="#RRGGBB",Width=130};
    readonly TextBlock feedback=new(){FontSize=11};
    bool syncing,secondary;
    readonly List<Button> customColors=[];
    int customIndex;
    public SKColor Stroke{get;private set;}=SKColors.Black;
    public SKColor Fill{get;private set;}=SKColors.White;
    public event Action? ColorsChanged;
    public ColorDashboard()
    {
        Orientation=Orientation.Horizontal;Spacing=3;Height=79;
        PaintButton ColorButton(PaintButton button,Border preview,string label)
        {var stack=new StackPanel{Spacing=3};stack.Children.Add(preview);stack.Children.Add(new TextBlock{Text=label,FontSize=11,HorizontalAlignment=HorizontalAlignment.Center});button.Content=stack;button.Width=43;button.Height=73;return button;}
        Children.Add(ColorButton(outlineTarget,outlinePreview,"Color 1"));Children.Add(ColorButton(fillTarget,fillPreview,"Color 2"));
        outlineTarget.Click+=(_,_)=>{secondary=false;Sync();};fillTarget.Click+=(_,_)=>{secondary=true;Sync();};
        var palette=new WrapPanel{Width=226,Height=71,VerticalAlignment=VerticalAlignment.Top};
        foreach(var value in new[]{"#000000","#7F7F7F","#880015","#ED1C24","#FF7F27","#FFF200","#22B14C","#00A2E8","#3F48CC","#A349A4","#FFFFFF","#C3C3C3","#B97A57","#FFAEC9","#FFC90E","#EFE4B0","#B5E61D","#99D9EA","#7092BE","#C8BFE7","#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF","#FFFFFF"})
        {
            var swatch=new Button{Width=20,Height=20,MinHeight=0,MinWidth=0,Margin=new Thickness(1),Padding=new Thickness(2),Background=Brushes.White,BorderBrush=SolidColorBrush.Parse("#A0A0A0"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(0),Content=new Border{Background=SolidColorBrush.Parse(value),Width=14,Height=14}};
            swatch.Template=new FuncControlTemplate<Button>((p,_)=>new Border{Background=p.Background,BorderBrush=p.BorderBrush,BorderThickness=p.BorderThickness,Padding=p.Padding,Child=new Avalonia.Controls.Presenters.ContentPresenter{Content=p.Content}});
            swatch.Tag=SKColor.Parse(value);ToolTip.SetTip(swatch,value);swatch.Click+=(_,_)=>SetActive((SKColor)swatch.Tag!);swatch.PointerPressed+=(_,e)=>{if(e.GetCurrentPoint(swatch).Properties.IsRightButtonPressed)SetFill((SKColor)swatch.Tag!);};if(palette.Children.Count>=20)customColors.Add(swatch);palette.Children.Add(swatch);
        }
        Children.Add(palette);
        var editor=new StackPanel{Spacing=6,Width=300};editor.Children.Add(new TextBlock{Text="Edit Colors",FontWeight=FontWeight.Bold,FontSize=15});
        foreach(var item in new[]{("Red",red),("Green",green),("Blue",blue)}){editor.Children.Add(new TextBlock{Text=item.Item1});editor.Children.Add(item.Item2);}
        editor.Children.Add(hex);var apply=new Button{Content="Add to custom colors"};apply.Click+=(_,_)=>{if(SKColor.TryParse(hex.Text,out var color)){SetActive(color);var slot=customColors[customIndex++%customColors.Count];slot.Tag=color;((Border)slot.Content!).Background=Brush(color);ToolTip.SetTip(slot,color.ToString());feedback.Text="";}else feedback.Text="Enter a color as #RRGGBB.";};editor.Children.Add(apply);editor.Children.Add(feedback);
        var edit=new PaintButton{Width=53,Height=73};var editContent=new StackPanel{Spacing=4};editContent.Children.Add(PaintIcons.Icon("Edit colors",32));editContent.Children.Add(new TextBlock{Text="Edit\ncolors",TextAlignment=TextAlignment.Center,FontSize=11});edit.Content=editContent;
        var flyout=new Flyout{Content=editor};edit.Click+=(_,_)=>flyout.ShowAt(edit);Children.Add(edit);
        red.ValueChanged+=(_,_)=>SlidersChanged();green.ValueChanged+=(_,_)=>SlidersChanged();blue.ValueChanged+=(_,_)=>SlidersChanged();Sync();
    }
    public void SetStroke(SKColor color){Stroke=color;Sync();ColorsChanged?.Invoke();}
    public void SetFill(SKColor color){Fill=color;Sync();ColorsChanged?.Invoke();}
    void SetActive(SKColor color){if(secondary)Fill=color;else Stroke=color;Sync();ColorsChanged?.Invoke();}
    void SlidersChanged(){if(!syncing)SetActive(new SKColor((byte)Math.Round(red.Value),(byte)Math.Round(green.Value),(byte)Math.Round(blue.Value)));}
    void Sync(){syncing=true;var color=secondary?Fill:Stroke;red.Value=color.Red;green.Value=color.Green;blue.Value=color.Blue;hex.Text=$"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";outlinePreview.Background=Brush(Stroke);fillPreview.Background=Brush(Fill);outlineTarget.Selected=!secondary;fillTarget.Selected=secondary;syncing=false;}
    static SolidColorBrush Brush(SKColor color)=>new(Color.FromRgb(color.Red,color.Green,color.Blue));
}
