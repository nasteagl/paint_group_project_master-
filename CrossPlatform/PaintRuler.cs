using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Paint1.CrossPlatform;

public sealed class PaintRuler(bool vertical) : Control
{
    float zoom=1;
    public void SetZoom(float value){zoom=value;InvalidateVisual();}
    public override void Render(DrawingContext context)
    {
        base.Render(context);context.DrawRectangle(SolidColorBrush.Parse("#F1F4F8"),null,new Rect(Bounds.Size));
        var pen=new Pen(SolidColorBrush.Parse("#788A9E"),1);var length=vertical?Bounds.Height:Bounds.Width;
        var step=zoom<.5f?50:10;
        for(int n=0;n*zoom<length;n+=step)
        {
            var at=n*zoom;var major=n%100==0;var tick=major?9:5;
            context.DrawLine(pen,vertical?new Point(20-tick,at):new Point(at,20-tick),vertical?new Point(20,at):new Point(at,20));
            if(major)
            {
                var label=new FormattedText(n.ToString(CultureInfo.InvariantCulture),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,Typeface.Default,9,SolidColorBrush.Parse("#506579"));
                context.DrawText(label,vertical?new Point(0,at+1):new Point(at+2,0));
            }
        }
    }
}
