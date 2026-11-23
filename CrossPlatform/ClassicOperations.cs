using SkiaSharp;

namespace Paint1.CrossPlatform;

public sealed partial class PaintDocument
{
    DocumentSnapshot Snapshot(){using var raster=Composite(false);return new(raster.Copy(),Vectors.Select(s=>s.Copy()).ToList());}
    public bool Redo()
    {
        if(!redo.TryPop(out var snapshot))return false;undo.Push(Snapshot());
        Selection?.Dispose();Selection=null;Raster.Dispose();Raster=snapshot.Bitmap;Vectors.Clear();Vectors.AddRange(snapshot.Shapes);return true;
    }
    public SKBitmap? CopySelection(bool vector)
    {
        if(Selection==null)return null;
        using var outline=Selection.Outline();var bounds=outline.Bounds;
        var w=Math.Clamp((int)Math.Ceiling(bounds.Width),1,8192);var h=Math.Clamp((int)Math.Ceiling(bounds.Height),1,8192);ValidateSize(w,h);
        var bitmap=new SKBitmap(w,h);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.Transparent);canvas.Translate(-bounds.Left,-bounds.Top);
        if(vector){foreach(var item in Selection.Objects)item.Shape.Draw(canvas);}else Selection.DrawPixels(canvas);
        return bitmap;
    }
    public void Paste(SKBitmap bitmap)
    {
        FinishSelection();ValidateSize(bitmap.Width,bitmap.Height);
        if(bitmap.Width>Width||bitmap.Height>Height)Resize(Math.Max(bitmap.Width,Width),Math.Max(bitmap.Height,Height),false);
        using var region=new SKPath();region.AddRect(new SKRect(0,0,bitmap.Width,bitmap.Height));
        Selection=new ActiveSelection(region,region.Bounds){Pixels=bitmap.Copy(),PixelOrigin=new SKPoint(0,0)};
    }
    public void Crop(bool vector)
    {
        if(Selection==null)return;
        using var outline=Selection.Outline();var b=outline.Bounds;
        var left=Math.Clamp((int)Math.Floor(b.Left),0,Width-1);var top=Math.Clamp((int)Math.Floor(b.Top),0,Height-1);
        var right=Math.Clamp((int)Math.Ceiling(b.Right),left+1,Width);var bottom=Math.Clamp((int)Math.Ceiling(b.Bottom),top+1,Height);
        FinishSelection();var next=new SKBitmap(right-left,bottom-top);using(var canvas=new SKCanvas(next)){canvas.Clear(SKColors.White);canvas.DrawBitmap(Raster,-left,-top);}
        Raster.Dispose();Raster=next;var shift=SKMatrix.CreateTranslation(-left,-top);foreach(var shape in Vectors)shape.Transform=SKMatrix.Concat(shift,shape.Transform);
    }
    public void RotateCanvas(int degrees)
    {
        FinishSelection();degrees=((degrees%360)+360)%360;if(degrees==0)return;
        if(degrees is not(90 or 180 or 270))throw new ArgumentOutOfRangeException(nameof(degrees));
        var width=degrees==180?Width:Height;var height=degrees==180?Height:Width;
        var matrix=SKMatrix.Concat(SKMatrix.CreateTranslation(width/2f,height/2f),SKMatrix.Concat(SKMatrix.CreateRotationDegrees(degrees),SKMatrix.CreateTranslation(-Width/2f,-Height/2f)));
        var next=new SKBitmap(width,height);using(var canvas=new SKCanvas(next)){canvas.Clear(SKColors.White);canvas.Concat(in matrix);canvas.DrawBitmap(Raster,0,0);}
        Raster.Dispose();Raster=next;foreach(var shape in Vectors)shape.Transform=SKMatrix.Concat(matrix,shape.Transform);
    }
    public void FlipCanvas(bool horizontal)
    {
        FinishSelection();var matrix=SKMatrix.CreateScale(horizontal?-1:1,horizontal?1:-1,Width/2f,Height/2f);var next=new SKBitmap(Width,Height);
        using(var canvas=new SKCanvas(next)){canvas.Clear(SKColors.White);canvas.Concat(in matrix);canvas.DrawBitmap(Raster,0,0);}Raster.Dispose();Raster=next;foreach(var shape in Vectors)shape.Transform=SKMatrix.Concat(matrix,shape.Transform);
    }
}
