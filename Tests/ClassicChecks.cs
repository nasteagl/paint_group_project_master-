using Paint1.CrossPlatform;
using SkiaSharp;

static class ClassicChecks
{
    public static void Run(Action<bool,string> check)
    {
        Shape Box(float x,float y,SKColor color)=>new(){Kind="Rectangle",Points=[new(x,y),new(x+20,y+20)],Filled=true,Fill=color,Stroke=color};
        using(var doc=new PaintDocument(100,80))
        {
            doc.Checkpoint();doc.Commit(Box(10,10,SKColors.Red),false);doc.Undo();check(doc.Raster.GetPixel(15,15)==SKColors.White,"Undo removes a raster drawing");doc.Redo();check(doc.Raster.GetPixel(15,15)==SKColors.Red,"Redo restores a raster drawing");
            doc.Undo();doc.Checkpoint();doc.Commit(Box(30,30,SKColors.Blue),false);check(!doc.CanRedo,"A new edit clears the redo branch");
            using var region=SelectionRegion.Create(SelectionKind.Rectangle,[new(28,28),new(52,52)]);doc.Select(region,false);doc.Selection!.Move(10,0);
            using var copy=doc.CopySelection(false);check(copy!=null&&copy.Width==24&&copy.GetPixel(10,10)==SKColors.Blue,"Copy includes moved selection pixels");
            doc.Checkpoint();doc.Paste(copy!);check(doc.Selection?.Pixels!=null,"Paste creates a movable raster selection");doc.Selection!.Move(60,0);doc.FinishSelection();check(doc.Raster.GetPixel(70,10)==SKColors.Blue,"Pasted pixels are placed at the new position");
            using var crop=SelectionRegion.Create(SelectionKind.Rectangle,[new(60,0),new(84,24)]);doc.Select(crop,false);doc.Checkpoint();doc.Crop(false);check(doc.Width==24&&doc.Height==24&&doc.Raster.GetPixel(10,10)==SKColors.Blue,"Crop uses the selected bounds and contents");doc.Undo();check(doc.Width==100&&doc.Height==80,"Undo restores a cropped canvas");
        }
        using(var doc=new PaintDocument(100,60))
        {
            doc.Commit(Box(10,10,SKColors.Red),false);doc.Commit(Box(10,10,SKColors.Blue),true);doc.Checkpoint();doc.RotateCanvas(90);
            check(doc.Width==60&&doc.Height==100&&doc.Raster.GetPixel(40,20)==SKColors.Red,"Rotate 90° swaps dimensions and rotates raster content");
            using(var path=doc.Vectors[0].Path())check(Math.Abs(path.Bounds.MidX-40)<.1&&Math.Abs(path.Bounds.MidY-20)<.1,"Rotate 90° also rotates vector geometry");
            doc.Undo();doc.Checkpoint();doc.FlipCanvas(true);check(doc.Raster.GetPixel(80,20)==SKColors.Red,"Flip horizontal mirrors raster content");doc.Undo();doc.FlipCanvas(false);check(doc.Raster.GetPixel(20,40)==SKColors.Red,"Flip vertical mirrors raster content");
        }
        using(var doc=new PaintDocument(100,80))
        {
            doc.Commit(Box(10,10,SKColors.Green),false);using var region=SelectionRegion.Create(SelectionKind.Rectangle,[new(0,0),new(50,50)]);doc.Select(region,false);doc.Selection!.Flip(true);
            using var image=doc.Composite(false);check(image.GetPixel(30,20)==SKColors.Green&&image.GetPixel(15,20)==SKColors.White,"Selection flip leaves the rest of the canvas unchanged");
        }
        using(var doc=new PaintDocument(10,10)){using var large=new SKBitmap(30,20);doc.Paste(large);check(doc.Width==30&&doc.Height==20,"Large clipboard images expand the canvas");}
        foreach(var kind in ShapeGeometry.Gallery)
        {
            var shape=new Shape{Kind=kind,Points=kind=="Triangle"?[new(30,0),new(60,50),new(0,50)]:[new(2,2),new(60,50)]};using var path=shape.Path();
            check(!path.IsEmpty&&path.Bounds.Width>0,$"Shape gallery renders {kind}");
        }
        var text=new Shape{Kind="Text",Text="Paint",Points=[new(2,2)],FontSize=24,Stroke=SKColors.Black};using var textPath=text.Path();check(!textPath.IsEmpty&&textPath.Bounds.Width>20,"Text is rendered as scalable geometry");
        using var area=text.HitArea();check(!area.IsEmpty,"Text remains selectable");
    }
}
