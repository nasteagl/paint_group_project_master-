using Paint1.CrossPlatform;
using SkiaSharp;

var passed=0;
void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
bool Near(float a,float b)=>Math.Abs(a-b)<.01;
Shape Box(float x,float y,float w,float h,SKColor color)=>new(){Kind="Rectangle",Points=[new(x,y),new(x+w,y+h)],Filled=true,Fill=color,Stroke=color,Thickness=1};
SKPath Region(float x,float y,float w,float h,SelectionKind kind=SelectionKind.Rectangle)=>SelectionRegion.Create(kind,[new(x,y),new(x+w,y+h)]);

// Canvas changes preserve content; image import uses its original dimensions.
using(var doc=new PaintDocument(120,80))
{
    doc.Commit(Box(10,10,20,20,SKColors.Red),false);doc.Checkpoint();doc.Resize(240,160,false);
    Check(doc.Width==240&&doc.Height==160,"Pânză cu dimensiuni dinamice");
    Check(doc.Raster.GetPixel(15,15)==SKColors.Red&&doc.Raster.GetPixel(200,140)==SKColors.White,"Extinderea păstrează desenul și adaugă fundal alb");
    doc.Checkpoint();doc.Resize(20,20,false);Check(doc.Undo()&&doc.Width==240&&doc.Raster.GetPixel(25,25)==SKColors.Red,"Undo restaurează pixelii decupați");
    Check(doc.Undo()&&doc.Width==120&&doc.Height==80,"Undo restaurează dimensiunile anterioare");
    doc.Resize(240,160,true);Check(doc.Raster.GetPixel(45,45)==SKColors.Red,"Scalarea pânzei scalează conținutul raster");
    using var png=new MemoryStream();doc.Save(png,false,false);png.Position=0;
    using var loaded=new PaintDocument(10,10);loaded.Load(png);Check(loaded.Width==240&&loaded.Height==160,"Importul păstrează dimensiunile originale");
    using var jpeg=new MemoryStream();doc.Save(jpeg,false,true);jpeg.Position=0;using var decoded=SKBitmap.Decode(jpeg);Check(decoded.Width==240,"Export JPEG valid");
}

// Raster extraction: only the mask is lifted; deselection restores unchanged content.
foreach(var kind in new[]{SelectionKind.Rectangle,SelectionKind.Ellipse,SelectionKind.Lasso})
{
    using var doc=new PaintDocument(140,100);doc.Commit(Box(0,0,140,100,SKColors.Red),false);
    using var path=kind==SelectionKind.Lasso?SelectionRegion.Create(kind,[new(10,10),new(50,10),new(10,50)]):Region(10,10,40,40,kind);
    Check(doc.Select(path,false),$"Selecție raster {kind}");
    Check(doc.Raster.GetPixel(20,20)==SKColors.White,$"Selecția {kind} extrage pixelii");
    Check(doc.Raster.GetPixel(70,70)==SKColors.Red,$"Selecția {kind} păstrează exteriorul");
    if(kind!=SelectionKind.Rectangle)Check(doc.Raster.GetPixel(48,48)==SKColors.Red,$"Masca {kind} nu extrage dreptunghiul complet");
    doc.Checkpoint();doc.Selection!.Move(60,0);
    using(var composite=doc.Composite(false))Check(composite.GetPixel(20,20)==SKColors.White&&composite.GetPixel(80,20)==SKColors.Red,$"Mutare raster {kind}");
    Check(doc.Undo()&&doc.Raster.GetPixel(20,20)==SKColors.Red,$"Undo mutare {kind} restaurează regiunea");
    doc.Select(path,false);doc.FinishSelection();Check(doc.Raster.GetPixel(20,20)==SKColors.Red,$"Deselectarea {kind} păstrează imaginea");
}

// Raster rotate/scale and export of floating selections.
using(var doc=new PaintDocument(200,160))
{
    doc.Commit(Box(20,20,20,40,SKColors.Blue),false);using var path=Region(19,19,22,42);doc.Select(path,false);
    var selection=doc.Selection!;selection.SetTransform(44,84,90);selection.Move(70,30);
    var originalPixels=selection.Pixels;
    for(int i=0;i<20;i++)selection.SetTransform(44,84,90);
    Check(ReferenceEquals(originalPixels,selection.Pixels),"Transformările repetate refolosesc pixelii originali");
    using var png=new MemoryStream();doc.Save(png,false,false);png.Position=0;using var bitmap=SKBitmap.Decode(png);
    Check(bitmap.GetPixel(100,70).Blue==255&&bitmap.GetPixel(100,70).Red==0,"Exportul include selecția raster rotită și scalată");
    doc.Checkpoint();doc.DeleteSelection();Check(doc.Raster.GetPixel(30,30)==SKColors.White,"Ștergerea selecției raster");
    doc.Undo();Check(doc.Raster.GetPixel(100,70).Blue==255&&doc.Raster.GetPixel(100,70).Red==0,"Undo restaurează selecția raster transformată");
}

// Object selection intersects actual geometry, not only bounding boxes.
using(var doc=new PaintDocument(300,200))
{
    var red=Box(20,20,40,20,SKColors.Red);var blue=Box(180,120,30,30,SKColors.Blue);doc.Commit(red,true);doc.Commit(blue,true);
    using var path=Region(15,15,60,40);Check(doc.Select(path,true)&&doc.Selection!.Objects.Count==1,"Selecție vectorială dreptunghiulară");
    doc.Checkpoint();var selection=doc.Selection!;selection.SetTransform(80,40,90);selection.Move(60,40);
    using(var geometry=red.Path()){Check(Near(geometry.Bounds.Width,40)&&Near(geometry.Bounds.Height,80),"Rotire vectorială la 90° și scalare");Check(Near(geometry.Bounds.MidX,100)&&Near(geometry.Bounds.MidY,70),"Mutarea păstrează centrul transformării");}
    selection.SetTransform(80,40,90);using(var geometry=red.Path())Check(Near(geometry.Bounds.Height,80),"Scalarea repetată nu se acumulează");
    using(var bitmap=doc.Composite(true))Check(bitmap.GetPixel(100,70)==SKColors.Red&&bitmap.GetPixel(190,130)==SKColors.Blue,"Transformarea afectează numai figura selectată");
    Check(doc.SelectObject(new(100,70)),"Hit-test pe figura rotită");
    doc.DeleteSelection();Check(doc.Vectors.Count==1,"Ștergerea figurii selectate");
    doc.Undo();Check(doc.Vectors.Count==2,"Undo restaurează geometria vectorială");
    doc.Resize(600,400,true);using(var geometry=doc.Vectors[0].Path())Check(Near(geometry.Bounds.Width,80),"Scalarea pânzei scalează vectorii");
}
using(var doc=new PaintDocument(120,100))
{
    var outline=Box(10,10,80,70,SKColors.Black);outline.Filled=false;doc.Commit(outline,true);
    using var inside=Region(35,35,10,10);Check(!doc.Select(inside,true),"Selecția din golul figurii nu selectează conturul");
    using var ellipse=Region(5,5,20,20,SelectionKind.Ellipse);Check(doc.Select(ellipse,true),"Selecția eliptică intersectează conturul");
    using var lasso=SelectionRegion.Create(SelectionKind.Lasso,[new(0,0),new(30,0),new(0,40)]);Check(doc.Select(lasso,true),"Selecția liberă intersectează conturul");
    doc.Clear(true);var line=new Shape{Kind="Line",Points=[new(10,50),new(100,50)],Stroke=SKColors.Black};doc.Commit(line,true);
    Check(doc.SelectObject(new(50,50)),"Selectarea unei linii orizontale");doc.Selection!.SetTransform(180,2,45);
    using var geometry=line.Path();Check(geometry.Bounds.Height>100,"Rotirea unei linii orizontale");
}
using(var doc=new PaintDocument(100,100))
{
    doc.Commit(Box(10,10,60,60,SKColors.Black),false);doc.Flood(30,30,SKColors.Green);
    Check(doc.Raster.GetPixel(30,30)==SKColors.Green&&doc.Raster.GetPixel(0,0)==SKColors.White,"Regresie: flood fill limitat de contur");
    for(int i=0;i<20;i++){doc.Checkpoint();doc.Commit(Box(i,i,2,2,SKColors.Red),true);}var count=0;while(doc.Undo())count++;
    Check(count==12,"Istoric limitat la 12 operații");
}
try{PaintDocument.ValidateSize(0,20);throw new Exception("Invalid size accepted");}catch(ArgumentOutOfRangeException){Check(true,"Respinge pânza cu dimensiune zero");}
try{PaintDocument.ValidateSize(8192,8192);throw new Exception("Oversized canvas accepted");}catch(ArgumentOutOfRangeException){Check(true,"Limitează memoria pânzelor foarte mari");}
ClassicChecks.Run(Check);
UiChecks.Run(Check,args.FirstOrDefault());
Console.WriteLine($"\n{passed} verificări au trecut.");
