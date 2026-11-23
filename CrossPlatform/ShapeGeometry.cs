using SkiaSharp;

namespace Paint1.CrossPlatform;

public static class ShapeGeometry
{
    public static readonly string[] Gallery=["Line","Curve","Ellipse","Rectangle","Rounded rectangle","Triangle","Right triangle","Diamond","Pentagon","Hexagon","Arrow right","Arrow left","Arrow up","Arrow down","Star 4","Star 5","Star 6","Heart","Lightning","Callout","Cloud"];
    public static bool IsBuiltIn(string kind)=>Gallery.Contains(kind)&&kind is not ("Line" or "Ellipse" or "Rectangle" or "Triangle");
    public static SKPath Create(string kind,SKRect rect)
    {
        var path=new SKPath();
        switch(kind)
        {
            case "Rounded rectangle":path.AddRoundRect(rect,Math.Min(rect.Width,rect.Height)*.18f,Math.Min(rect.Width,rect.Height)*.18f);return path;
            case "Curve":path.MoveTo(rect.Left,rect.Bottom);path.CubicTo(rect.MidX,rect.Bottom,rect.MidX,rect.Top,rect.Right,rect.Top);return path;
            case "Diamond":Polygon(path,[new(.5f,0),new(1,.5f),new(.5f,1),new(0,.5f)]);break;
            case "Right triangle":Polygon(path,[new(0,0),new(0,1),new(1,1)]);break;
            case "Pentagon":Regular(path,5);break;
            case "Hexagon":Regular(path,6);break;
            case "Star 4":Star(path,4);break;
            case "Star 5":Star(path,5);break;
            case "Star 6":Star(path,6);break;
            case "Arrow right":Polygon(path,[new(0,.3f),new(.6f,.3f),new(.6f,0),new(1,.5f),new(.6f,1),new(.6f,.7f),new(0,.7f)]);break;
            case "Arrow left":using(var other=Create("Arrow right",new(0,0,1,1))){other.Transform(SKMatrix.CreateScale(-1,1,.5f,.5f));path.AddPath(other);}break;
            case "Arrow up":using(var other=Create("Arrow right",new(0,0,1,1))){other.Transform(SKMatrix.CreateRotationDegrees(-90,.5f,.5f));path.AddPath(other);}break;
            case "Arrow down":using(var other=Create("Arrow right",new(0,0,1,1))){other.Transform(SKMatrix.CreateRotationDegrees(90,.5f,.5f));path.AddPath(other);}break;
            case "Heart":path.MoveTo(.5f,.95f);path.CubicTo(-.35f,.4f,.05f,-.2f,.5f,.2f);path.CubicTo(.95f,-.2f,1.35f,.4f,.5f,.95f);path.Close();break;
            case "Lightning":Polygon(path,[new(.55f,0),new(.1f,.55f),new(.45f,.55f),new(.3f,1),new(.95f,.35f),new(.6f,.35f),new(.8f,0)]);break;
            case "Callout":path.AddRoundRect(new SKRect(0,0,1,.75f),.12f,.12f);path.MoveTo(.25f,.7f);path.LineTo(.16f,1);path.LineTo(.55f,.7f);path.Close();break;
            case "Cloud":path.MoveTo(.12f,.7f);path.CubicTo(-.1f,.6f,0,.28f,.23f,.3f);path.CubicTo(.18f,.02f,.58f,-.05f,.65f,.18f);path.CubicTo(.94f,.05f,1.1f,.38f,.9f,.5f);path.CubicTo(1.1f,.9f,.6f,1,.5f,.8f);path.CubicTo(.3f,1,0,.9f,.12f,.7f);path.Close();break;
        }
        path.Transform(SKMatrix.Concat(SKMatrix.CreateTranslation(rect.Left,rect.Top),SKMatrix.CreateScale(rect.Width,rect.Height)));return path;
    }
    static void Polygon(SKPath path,SKPoint[] points){path.MoveTo(points[0]);foreach(var p in points.Skip(1))path.LineTo(p);path.Close();}
    static void Regular(SKPath path,int count)=>Polygon(path,Enumerable.Range(0,count).Select(i=>{var a=-MathF.PI/2+i*2*MathF.PI/count;return new SKPoint(.5f+.5f*MathF.Cos(a),.5f+.5f*MathF.Sin(a));}).ToArray());
    static void Star(SKPath path,int points)=>Polygon(path,Enumerable.Range(0,points*2).Select(i=>{var a=-MathF.PI/2+i*MathF.PI/points;var r=i%2==0?.5f:.22f;return new SKPoint(.5f+r*MathF.Cos(a),.5f+r*MathF.Sin(a));}).ToArray());
}
