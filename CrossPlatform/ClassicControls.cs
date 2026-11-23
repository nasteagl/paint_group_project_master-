using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SkiaSharp;

namespace Paint1.CrossPlatform;

public sealed class PaintButton : Button
{
    bool selected,hover;
    public bool Selected {get=>selected;set{selected=value;Update();}}
    public PaintButton()
    {
        Padding=new Thickness(3);BorderThickness=new Thickness(1);CornerRadius=new CornerRadius(0);FontSize=12;
        Template=new FuncControlTemplate<Button>((parent,scope)=>
        {
            var content=new ContentPresenter{Content=parent.Content,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
            var border=new Border{Child=content};border.Bind(Border.BackgroundProperty,new Binding("Background"){Source=parent});border.Bind(Border.BorderBrushProperty,new Binding("BorderBrush"){Source=parent});border.Bind(Border.BorderThicknessProperty,new Binding("BorderThickness"){Source=parent});border.Bind(Border.PaddingProperty,new Binding("Padding"){Source=parent});return border;
        });
        PropertyChanged+=(_,e)=>{if(e.Property==IsEnabledProperty)Opacity=IsEnabled?1:.45;};
        PointerEntered+=(_,_)=>{hover=true;Update();};PointerExited+=(_,_)=>{hover=false;Update();};Update();
    }
    void Update(){Background=SolidColorBrush.Parse(hover?"#E5F3FF":selected?"#CDE8FF":"#00FFFFFF");BorderBrush=SolidColorBrush.Parse(hover||selected?"#99C9EF":"#00FFFFFF");}
}

public static class PaintIcons
{
    static readonly Dictionary<string,Avalonia.Media.Imaging.Bitmap> cache=[];
    public static Image Icon(string name,double size=24)
    {
        if(!cache.TryGetValue(name,out var bitmap))
        {
            using var surface=new SKBitmap(40,40);using var c=new SKCanvas(surface);c.Clear(SKColors.Transparent);c.Scale(1.25f);
            using var p=new SKPaint{IsAntialias=true,Color=SKColors.Black,Style=SKPaintStyle.Stroke,StrokeWidth=1.5f,StrokeJoin=SKStrokeJoin.Round,StrokeCap=SKStrokeCap.Round};
            void Path(string data,string? color=null,bool solid=false){using var path=SKPath.ParseSvgPathData(data);p.Color=SKColor.Parse(color??"#3B5266");p.Style=solid?SKPaintStyle.Fill:SKPaintStyle.Stroke;c.DrawPath(path,p);}
            if(ShapeGeometry.Gallery.Contains(name))
            {var shape=new Shape{Kind=name,Points=name=="Triangle"?[new(16,5),new(28,26),new(4,26)]:[new(4,6),new(28,26)],Stroke=new SKColor(39,63,88),Thickness=1.5f};shape.Draw(c);}
            else switch(name)
            {
                case "Paste":Path("M7 5H23V28H7Z","#C6A65D",true);Path("M7 5H23V28H7Z","#94763C");Path("M11 3H19V8H11Z","#E9E9E9",true);Path("M13 12H28V30H13Z","#EAF4FF",true);Path("M13 12H28V30H13Z");Path("M16 17H25M16 21H25M16 25H23","#7491AD");break;
                case "Copy":Path("M5 4H19V23H5Z","#F5FAFF",true);Path("M5 4H19V23H5Z");Path("M12 11H27V29H12Z","#F5FAFF",true);Path("M12 11H27V29H12Z");Path("M15 16H24M15 20H24M15 24H22","#8DA3BA");break;
                case "Cut":Path("M9 17L24 4M17 18L7 4","#5D6C7B");Path("M12 23C12 29 3 29 3 23C3 17 12 17 12 23M21 23C21 29 30 29 30 23C30 17 21 17 21 23","#3279B5");break;
                case "Select":p.PathEffect=SKPathEffect.CreateDash([2,2],0);Path("M4 5H28V27H4Z","#3983B4");p.PathEffect?.Dispose();p.PathEffect=null;break;
                case "Crop":Path("M7 2V24H29M2 8H24V30","#507C41");break;
                case "Resize":Path("M4 9H20V28H4Z","#8CA5BB",true);Path("M12 3H28V20H12Z","#E4EFF9",true);Path("M12 3H28V20H12Z");Path("M16 15L25 6M19 6H25V12","#3D77B5");break;
                case "Rotate":Path("M9 8C22 -1 34 15 23 25","#4A9645");Path("M8 2L8 11L17 10","#4A9645");Path("M5 16H18V29H5Z","#8BAECF",true);break;
                case "Pencil":Path("M5 24L23 4L28 9L10 28L4 29Z","#DBAF3F",true);Path("M5 24L23 4L28 9L10 28L4 29Z","#82652B");Path("M22 5L26 9M6 23L10 27","#444444");break;
                case "Brush":Path("M12 17L21 3C23 0 28 3 26 6L17 21Z","#9C622F",true);Path("M10 17L18 22L14 29L4 30L7 23Z","#1977C3",true);Path("M10 17L18 22","#A6B1BC");break;
                case "Eraser":Path("M4 22L17 8L28 18L17 30H12Z","#F3A9AF",true);Path("M4 22L17 8L28 18L17 30H12Z","#AC6972");Path("M11 15L22 25","#FFF0F2");break;
                case "Fill":Path("M6 16L16 5L26 16L15 27Z","#DDE6ED",true);Path("M6 16L16 5L26 16L15 27Z");Path("M8 14H23","#357FC2");Path("M27 19C20 28 26 33 30 28C33 24 29 22 27 19Z","#207DB6",true);break;
                case "Text":using(var font=new SKFont(SKTypeface.Default,28)){p.Color=new SKColor(36,61,92);p.Style=SKPaintStyle.Fill;c.DrawText("A",5,28,font,p);}break;
                case "Picker":Path("M5 25L22 8L26 12L9 29H4Z","#BBDFEE",true);Path("M5 25L22 8L26 12L9 29H4Z");Path("M18 6L27 15M22 3L29 10","#485566");break;
                case "Zoom":case "Zoom in":case "Zoom out":p.Color=new SKColor(73,110,144);c.DrawCircle(13,12,8,p);Path("M19 19L28 29","#46505C");if(name!="Zoom out")Path("M13 8V16","#4A7BA1");Path("M9 12H17","#4A7BA1");break;
                case "Undo":Path("M6 10C28 -3 35 28 12 27","#4286C6");Path("M6 3L5 13L15 12","#4286C6");break;
                case "Redo":Path("M26 10C4 -3 -3 28 20 27","#4286C6");Path("M26 3L27 13L17 12","#4286C6");break;
                case "Save":Path("M5 3H24L28 7V29H5Z","#5E91BC",true);Path("M10 3H22V13H10Z","#E8F1F7",true);Path("M10 20H23V29H10Z","#E8F1F7",true);Path("M18 4V11","#3E5B76");break;
                case "Edit colors":for(int y=0;y<3;y++)for(int x=0;x<3;x++){p.Style=SKPaintStyle.Fill;p.Color=SKColor.FromHsv((x+y*3)*40,80,95);c.DrawRect(3+x*9,3+y*9,8,8,p);}break;
                case "Size":for(int i=0;i<4;i++){p.Style=SKPaintStyle.Stroke;p.Color=SKColors.Black;p.StrokeWidth=i+1;c.DrawLine(4,5+i*7,28,5+i*7,p);}break;
                default:Path("M5 5H27V27H5Z","#6A829B");break;
            }
            using var encoded=SKImage.FromBitmap(surface);using var data=encoded.Encode(SKEncodedImageFormat.Png,100);using var stream=data.AsStream();bitmap=new Avalonia.Media.Imaging.Bitmap(stream);cache[name]=bitmap;
        }
        return new Image{Source=bitmap,Width=size,Height=size};
    }
}
