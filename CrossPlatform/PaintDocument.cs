using SkiaSharp;

namespace Paint1.CrossPlatform;

public sealed class Shape
{
    public string Kind = "Line";
    public List<SKPoint> Points = [];
    public SKColor Stroke = SKColors.Black, Fill = SKColors.White;
    public float Thickness = 3;
    public bool Filled;
    public bool Outlined=true;
    public string Text="";
    public float FontSize=24;
    public bool Closed=>Kind is not ("Line" or "Brush" or "Pencil" or "Eraser" or "Curve");
    public SKMatrix Transform = SKMatrix.Identity;
    public Shape Copy() => new() { Kind = Kind, Points = [.. Points], Stroke = Stroke,
        Fill = Fill, Thickness = Thickness, Filled = Filled, Transform = Transform, Outlined=Outlined, Text=Text, FontSize=FontSize };
    public SKPath Path(bool world = true)
    {
        var path = new SKPath();
        if (Points.Count == 0) return path;
        var a = Points[0]; var b = Points[^1];
        var rect = new SKRect(Math.Min(a.X,b.X), Math.Min(a.Y,b.Y), Math.Max(a.X,b.X), Math.Max(a.Y,b.Y));
        if(Kind=="Text") {using var font=new SKFont(SKTypeface.Default,FontSize);using var textPath=font.GetTextPath(Text,new SKPoint(a.X,a.Y+FontSize));path.AddPath(textPath);}
        else if(ShapeGeometry.IsBuiltIn(Kind)) {using var shape=ShapeGeometry.Create(Kind,rect);path.AddPath(shape);}
        else if (Kind == "Rectangle") path.AddRect(rect);
        else if (Kind == "Ellipse") path.AddOval(rect);
        else
        {
            path.MoveTo(a); foreach (var p in Points.Skip(1)) path.LineTo(p);
            if (Kind is "Triangle" or "Polygon") path.Close();
            if (Kind == "Brush" && Points.All(p => p == a)) path.AddCircle(a.X,a.Y,Thickness/2);
        }
        if(world) path.Transform(Transform);
        return path;
    }
    public void Draw(SKCanvas canvas)
    {
        if(Points.Count==0) return;
        canvas.Save(); var matrix=Transform; canvas.Concat(in matrix);
        using var path=Path(false);
        using var paint=new SKPaint { IsAntialias=true, StrokeWidth=Thickness, StrokeCap=SKStrokeCap.Round, StrokeJoin=SKStrokeJoin.Round };
        if(Filled && Closed)
        {paint.Style=SKPaintStyle.Fill;paint.Color=Fill;canvas.DrawPath(path,paint);}
        paint.Style=Kind=="Text" || Kind=="Brush" && Points.All(p=>p==Points[0]) ? SKPaintStyle.Fill : SKPaintStyle.Stroke;
        paint.Color=Stroke;if(Outlined||Kind=="Text")canvas.DrawPath(path,paint);canvas.Restore();
    }
    public SKPath HitArea(float tolerance=0)
    {
        using var path=Path(false);
        if(Kind=="Text"){var textArea=new SKPath(path);textArea.Transform(Transform);return textArea;}
        using var paint=new SKPaint {Style=SKPaintStyle.Stroke,StrokeWidth=Math.Max(Thickness,tolerance),StrokeCap=SKStrokeCap.Round,StrokeJoin=SKStrokeJoin.Round};
        var area=new SKPath();paint.GetFillPath(path,area);
        if(Filled && Closed)
            area.Op(path,SKPathOp.Union,area);
        area.Transform(Transform);return area;
    }
}

public enum SelectionKind { Rectangle, Ellipse, Lasso }

public static class SelectionRegion
{
    public static SKPath Create(SelectionKind kind, IReadOnlyList<SKPoint> points)
    {
        var path=new SKPath(); if(points.Count<2)return path;
        var a=points[0];var b=points[^1];
        var rect=new SKRect(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y));
        if(kind==SelectionKind.Rectangle)path.AddRect(rect);
        else if(kind==SelectionKind.Ellipse)path.AddOval(rect);
        else if(points.Count>=3){path.MoveTo(a);foreach(var p in points.Skip(1))path.LineTo(p);path.Close();}
        return path;
    }
}

/// <summary>A transform always starts from the captured original, avoiding cumulative resampling.</summary>
public sealed class ActiveSelection : IDisposable
{
    public SKPath Region { get; }
    public SKRect OriginalBounds { get; }
    public List<(Shape Shape, SKMatrix Original)> Objects { get; } = [];
    public SKBitmap? Pixels { get; internal set; }
    public SKPoint PixelOrigin { get; internal set; }
    public float Width { get; private set; }
    public float Height { get; private set; }
    public float Angle { get; private set; }
    public SKPoint Offset { get; private set; }
    bool mirrorX,mirrorY;
    public ActiveSelection(SKPath path, SKRect bounds)
    {
        Region=new SKPath(path); OriginalBounds=bounds;
        Width=Math.Max(1,bounds.Width);Height=Math.Max(1,bounds.Height);
    }
    public SKMatrix Matrix
    {
        get
        {
            var cx=OriginalBounds.MidX;var cy=OriginalBounds.MidY;
            var scale=SKMatrix.CreateScale((mirrorX?-1:1)*Width/Math.Max(1,OriginalBounds.Width),(mirrorY?-1:1)*Height/Math.Max(1,OriginalBounds.Height));
            return SKMatrix.Concat(SKMatrix.CreateTranslation(cx+Offset.X,cy+Offset.Y),
                SKMatrix.Concat(SKMatrix.CreateRotationDegrees(Angle),SKMatrix.Concat(scale,SKMatrix.CreateTranslation(-cx,-cy))));
        }
    }
    public void SetTransform(float width,float height,float angle)
    {
        if(!float.IsFinite(width)||!float.IsFinite(height)||!float.IsFinite(angle)||width<1||height<1||width>16384||height>16384)
            throw new ArgumentOutOfRangeException(nameof(width),"Dimensiunile selecției trebuie să fie între 1 și 16384 px.");
        Width=width;Height=height;Angle=angle;
        foreach(var item in Objects)item.Shape.Transform=SKMatrix.Concat(Matrix,item.Original);
    }
    public void Flip(bool horizontal){if(horizontal)mirrorX=!mirrorX;else mirrorY=!mirrorY;SetTransform(Width,Height,Angle);}
    public void Move(float dx,float dy)
    {Offset=new(Offset.X+dx,Offset.Y+dy);SetTransform(Width,Height,Angle);}
    public SKPath Outline() {var path=new SKPath(Region);path.Transform(Matrix);return path;}
    public bool Contains(SKPoint point) {using var outline=Outline();return outline.Contains(point.X,point.Y);}
    public void DrawPixels(SKCanvas canvas)
    {
        if(Pixels==null)return;
        canvas.Save();var matrix=Matrix;canvas.Concat(in matrix);
        using var paint=new SKPaint {IsAntialias=true};canvas.DrawBitmap(Pixels,PixelOrigin,paint);canvas.Restore();
    }
    public void Dispose(){Region.Dispose();Pixels?.Dispose();}
}

public sealed partial class PaintDocument : IDisposable
{
    public int Width => Raster.Width;
    public int Height => Raster.Height;
    public SKBitmap Raster { get; private set; }
    public List<Shape> Vectors { get; } = [];
    public ActiveSelection? Selection { get; private set; }
    readonly Stack<DocumentSnapshot> undo = new();
    readonly Stack<DocumentSnapshot> redo = new();
    public bool CanRedo=>redo.Count>0;
    public bool CanUndo=>undo.Count>0;
    public PaintDocument(int width=1000,int height=650)
    {ValidateSize(width,height);Raster=new(width,height);using var canvas=new SKCanvas(Raster);canvas.Clear(SKColors.White);}
    public static void ValidateSize(int width,int height)
    {
        if(width<1||height<1||width>8192||height>8192||(long)width*height>24_000_000)
            throw new ArgumentOutOfRangeException(nameof(width),"Pânza: 1–8192 px pe latură, maximum 24 milioane de pixeli.");
    }
    public void Checkpoint()
    {
        foreach(var snapshot in redo)snapshot.Dispose();redo.Clear();
        // Snapshots render any floating pixels without committing or resampling the active selection.
        using var raster=Composite(false);
        undo.Push(new DocumentSnapshot(raster.Copy(),Vectors.Select(s=>s.Copy()).ToList()));
        var budget=96L*1024*1024;long used=0;var keep=0;
        foreach(var item in undo){used+=(long)item.Bitmap.Width*item.Bitmap.Height*4;if(keep>0&&(used>budget||keep>=12))break;keep++;}
        if(keep<undo.Count){var retained=undo.Take(keep).Reverse().ToArray();foreach(var old in undo.Skip(keep))old.Dispose();undo.Clear();foreach(var item in retained)undo.Push(item);}
    }
    public bool Undo()
    {
        if(!undo.TryPop(out var snapshot))return false;
        redo.Push(Snapshot());
        Selection?.Dispose();Selection=null;Raster.Dispose();Raster=snapshot.Bitmap;
        Vectors.Clear();Vectors.AddRange(snapshot.Shapes);return true;
    }
    public void FinishSelection()
    {
        if(Selection==null)return;
        using(var canvas=new SKCanvas(Raster))Selection.DrawPixels(canvas);
        Selection.Dispose();Selection=null;
    }
    public void DeleteSelection()
    {
        if(Selection==null)return;
        foreach(var item in Selection.Objects)Vectors.Remove(item.Shape);
        Selection.Dispose();Selection=null;
    }
    public bool Select(SKPath region,bool vector)
    {
        FinishSelection();
        if(region.IsEmpty || region.Bounds.Width<1 || region.Bounds.Height<1)return false;
        if(vector)
        {
            var objects=new List<Shape>();
            foreach(var shape in Vectors)
            {
                using var area=shape.HitArea();using var intersection=new SKPath();
                if(area.Op(region,SKPathOp.Intersect,intersection) && !intersection.IsEmpty)objects.Add(shape);
            }
            if(objects.Count==0)return false;
            var bounds=BoundsOf(objects);using var outline=new SKPath();outline.AddRect(bounds);
            Selection=new ActiveSelection(outline,bounds);
            foreach(var shape in objects)Selection.Objects.Add((shape,shape.Transform));
        }
        else
        {
            var bounds=region.Bounds;
            var left=Math.Clamp((int)Math.Floor(bounds.Left),0,Width);var top=Math.Clamp((int)Math.Floor(bounds.Top),0,Height);
            var right=Math.Clamp((int)Math.Ceiling(bounds.Right),0,Width);var bottom=Math.Clamp((int)Math.Ceiling(bounds.Bottom),0,Height);
            if(right<=left||bottom<=top)return false;
            var pixels=new SKBitmap(right-left,bottom-top);
            using(var canvas=new SKCanvas(pixels))
            {canvas.Clear(SKColors.Transparent);canvas.Translate(-left,-top);canvas.ClipPath(region,SKClipOperation.Intersect,false);canvas.DrawBitmap(Raster,0,0);}
            using(var canvas=new SKCanvas(Raster))
            {canvas.Save();canvas.ClipPath(region,SKClipOperation.Intersect,false);canvas.Clear(SKColors.White);canvas.Restore();}
            Selection=new ActiveSelection(region,new SKRect(left,top,right,bottom)){Pixels=pixels,PixelOrigin=new(left,top)};
        }
        return true;
    }
    public bool SelectObject(SKPoint point)
    {
        FinishSelection();var shape=Vectors.AsEnumerable().Reverse().FirstOrDefault(s=>{using var area=s.HitArea(12);return area.Contains(point.X,point.Y);});
        if(shape==null)return false;
        SelectShape(shape);return true;
    }
    public void SelectShape(Shape shape)
    {
        FinishSelection();var bounds=BoundsOf([shape]);using var region=new SKPath();region.AddRect(bounds);
        Selection=new ActiveSelection(region,bounds);Selection.Objects.Add((shape,shape.Transform));
    }
    public static SKRect BoundsOf(IEnumerable<Shape> shapes)
    {
        var bounds=SKRect.Empty;var first=true;
        foreach(var shape in shapes){using var path=shape.Path();var b=path.TightBounds;if(first){bounds=b;first=false;}else bounds=SKRect.Union(bounds,b);}
        // Horizontal/vertical lines still need a usable selection rectangle.
        if(bounds.Width<1){bounds.Left-=.5f;bounds.Right+=.5f;}if(bounds.Height<1){bounds.Top-=.5f;bounds.Bottom+=.5f;}
        return bounds;
    }
    public void Resize(int width,int height,bool scaleContent)
    {
        ValidateSize(width,height);FinishSelection();var next=new SKBitmap(width,height);
        using(var canvas=new SKCanvas(next))
        {canvas.Clear(SKColors.White);if(scaleContent)canvas.DrawBitmap(Raster,new SKRect(0,0,width,height));else canvas.DrawBitmap(Raster,0,0);}
        if(scaleContent){var scale=SKMatrix.CreateScale((float)width/Width,(float)height/Height);foreach(var shape in Vectors)shape.Transform=SKMatrix.Concat(scale,shape.Transform);}
        Raster.Dispose();Raster=next;
    }
    public void Clear(bool vector)
    {FinishSelection();if(vector)Vectors.Clear();else{using var canvas=new SKCanvas(Raster);canvas.Clear(SKColors.White);}}
    public void Commit(Shape shape,bool vector)
    {FinishSelection();if(vector)Vectors.Add(shape);else{using var canvas=new SKCanvas(Raster);shape.Draw(canvas);}}
    public SKBitmap Composite(bool vector,Shape? preview=null)
    {
        var bitmap=new SKBitmap(Width,Height);using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.White);
        if(vector){foreach(var shape in Vectors)shape.Draw(canvas);}else{canvas.DrawBitmap(Raster,0,0);Selection?.DrawPixels(canvas);}
        preview?.Draw(canvas);return bitmap;
    }
    public void Load(Stream stream)
    {
        using var bitmap=SKBitmap.Decode(stream)??throw new InvalidDataException("Format de imagine neacceptat.");
        ValidateSize(bitmap.Width,bitmap.Height);Resize(bitmap.Width,bitmap.Height,false);
        using var canvas=new SKCanvas(Raster);canvas.Clear(SKColors.White);canvas.DrawBitmap(bitmap,0,0);
    }
    public void Save(Stream stream,bool vector,bool jpeg)
    {using var bitmap=Composite(vector);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(jpeg?SKEncodedImageFormat.Jpeg:SKEncodedImageFormat.Png,95);data.SaveTo(stream);}
    public void Flood(int x,int y,SKColor color)
    {
        FinishSelection();if(x<0||y<0||x>=Width||y>=Height)return;
        var old=Raster.GetPixel(x,y);if(old==color)return;
        var queue=new Queue<(int,int)>();queue.Enqueue((x,y));Raster.SetPixel(x,y,color);
        while(queue.TryDequeue(out var p)){Visit(p.Item1-1,p.Item2);Visit(p.Item1+1,p.Item2);Visit(p.Item1,p.Item2-1);Visit(p.Item1,p.Item2+1);}
        void Visit(int px,int py){if(px>=0&&py>=0&&px<Width&&py<Height&&Raster.GetPixel(px,py)==old){Raster.SetPixel(px,py,color);queue.Enqueue((px,py));}}
    }
    public void Dispose(){Selection?.Dispose();Raster.Dispose();foreach(var snapshot in undo)snapshot.Dispose();foreach(var snapshot in redo)snapshot.Dispose();}
    sealed record DocumentSnapshot(SKBitmap Bitmap,List<Shape> Shapes):IDisposable{public void Dispose()=>Bitmap.Dispose();}
}
