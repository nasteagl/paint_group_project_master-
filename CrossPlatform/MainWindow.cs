using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SkiaSharp;

namespace Paint1.CrossPlatform;

public sealed partial class MainWindow : Window
{
    readonly PaintDocument document=new();
    readonly Image image=new(){Focusable=true,Stretch=Stretch.Fill,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
    readonly ComboBox mode=new(){ItemsSource=new[]{"Raster (pixeli)","Vector (figuri)"},SelectedIndex=0,Width=150};
    readonly ComboBox tool=new(){ItemsSource=ToolItems,SelectedIndex=0,Width=190};
    readonly NumericUpDown thickness=Number(1,60,3,80), sides=Number(3,30,5,75);
    readonly CheckBox fill=new(){Content="Figuri umplute",VerticalAlignment=VerticalAlignment.Center};
    readonly ColorDashboard colors=new();
    readonly NumericUpDown canvasWidth=Number(1,8192,1000),canvasHeight=Number(1,8192,650);
    readonly CheckBox scaleContent=new(){Content="Scalează și conținutul"};
    readonly NumericUpDown selectionWidth=Number(1,16384,100),selectionHeight=Number(1,16384,100),selectionAngle=Number(-3600,3600,0);
    readonly CheckBox lockRatio=new(){Content="Păstrează proporțiile",IsChecked=true};
    readonly CheckBox fit=new(){Content="Adaptează la fereastră",IsChecked=false,VerticalAlignment=VerticalAlignment.Center};
    readonly NumericUpDown zoomPercent=Number(10,400,100,90);
    readonly TextBlock status=new(){Text="Trage pentru a desena. Shift: proporții fixe. Selectează o regiune pentru transformări.",Margin=new Thickness(10),TextWrapping=TextWrapping.Wrap};
    readonly TextBlock selectionInfo=new(){Text="No selection",TextWrapping=TextWrapping.Wrap,FontSize=12};
    readonly TextBlock sizeInfo=new(){Margin=new Thickness(8,0),VerticalAlignment=VerticalAlignment.Center};
    ScrollViewer viewport=null!;
    ScrollViewer dashboard=null!;
    readonly RowDefinition dashboardRow=new(new GridLength(124)){MinHeight=76};
    readonly Grid workspace=new();
    readonly GridSplitter dashboardSplitter=new()
    {
        Height=12,HorizontalAlignment=HorizontalAlignment.Stretch,
        ResizeDirection=GridResizeDirection.Rows,ResizeBehavior=GridResizeBehavior.PreviousAndNext,
        Background=SolidColorBrush.Parse("#CBD5E1"),Cursor=new Cursor(StandardCursorType.SizeNorthSouth),
        Focusable=true,KeyboardIncrement=8
    };
    readonly Button undoButton=new(){Content="Anulează",Margin=new Thickness(3)};
    Shape? preview,editShape;
    readonly List<SKPoint> regionPoints=[];
    SKPoint start,last;
    string? gesture;
    IPointer? capturedPointer;
    int handle;
    float zoom=1,pressWidth,pressHeight,pressAngle,pressDirection;
    bool syncing,transformSync,secondaryDrawing;
    Avalonia.Media.Imaging.Bitmap? displayed;
    bool Vector=>mode.SelectedIndex==1;
    string Tool=>(tool.SelectedItem as ToolItem)?.Key??"Brush";
    ActiveSelection? Selection=>document.Selection;
    static readonly ToolItem[] ToolItems=[new("Brush","Brush"),new("Pencil","Pencil"),new("Eraser","Eraser"),new("Picker","Color picker"),new("Text","Text"),new("Zoom","Magnifier"),new("Line","Line"),new("Curve","Curve"),new("Rounded rectangle","Rounded rectangle"),new("Right triangle","Right triangle"),new("Diamond","Diamond"),new("Pentagon","Pentagon"),new("Hexagon","Hexagon"),new("Arrow right","Arrow right"),new("Arrow left","Arrow left"),new("Arrow up","Arrow up"),new("Arrow down","Arrow down"),new("Star 4","Star 4"),new("Star 5","Star 5"),new("Star 6","Star 6"),new("Heart","Heart"),new("Lightning","Lightning"),new("Callout","Callout"),new("Cloud","Cloud"),new("Rectangle","Dreptunghi"),new("Ellipse","Elipsă"),new("Triangle","Triunghi"),new("Polygon","Poligon regulat"),new("Free polygon","Poligon liber"),new("Fill","Umplere raster"),new("Select rectangle","Selecție dreptunghi"),new("Select ellipse","Selecție elipsă"),new("Select lasso","Selecție liberă (lasou)"),new("Move","Mută selecția"),new("Rotate","Rotește selecția"),new("Scale","Redimensionează selecția"),new("Edit","Editează puncte vector")];
    sealed record ToolItem(string Key,string Title){public override string ToString()=>Title;}
    static NumericUpDown Number(decimal min,decimal max,decimal value,double width=110)=>new(){Minimum=min,Maximum=max,Value=value,Width=width,Increment=1,ShowButtonSpinner=false};

    public MainWindow()
    {
        Title="Untitled - Paint";Width=1320;Height=900;MinWidth=880;MinHeight=600;
        FontFamily=new FontFamily("Segoe UI, Arial");FontSize=12;RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Light;
        BuildClassicLayout();
        viewport.SizeChanged+=(_,_)=>UpdateZoom();fit.IsCheckedChanged+=(_,_)=>UpdateZoom();
        zoomPercent.ValueChanged+=(_,_)=>{if(!syncing){fit.IsChecked=false;UpdateZoom();}};
        selectionWidth.ValueChanged+=(_,_)=>DimensionChanged(true);selectionHeight.ValueChanged+=(_,_)=>DimensionChanged(false);
        mode.SelectionChanged+=(_,_)=>{document.FinishSelection();ResetGesture();SyncSelection();Refresh();};
        tool.SelectionChanged+=(_,_)=>{ResetGesture();UpdateToolButtons();Refresh();};
        image.PointerPressed+=Pressed;image.PointerMoved+=Moved;image.PointerReleased+=Released;
        image.PointerCaptureLost+=(_,_)=>{if(gesture!=null){preview=null;regionPoints.Clear();gesture=null;Refresh();}};
        KeyDown+=OnKeyDown;
        Closed+=(_,_)=>{displayed?.Dispose();clipboardBitmap?.Dispose();document.Dispose();};UpdateCanvasControls();SyncSelection();Refresh();
    }
    void ResetDashboardHeight()
    {
        dashboardRow.Height=new GridLength(Math.Clamp(124,dashboardRow.MinHeight,dashboardRow.MaxHeight));
    }
    static void Label(Panel panel,string text)=>panel.Children.Add(new TextBlock{Text=text,Margin=new Thickness(8,8,4,0)});
    static void Add(Panel panel,string text,Action action)
    {var b=new Button{Content=text,Margin=new Thickness(2),HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=(_,_)=>action();panel.Children.Add(b);}
    static void VerticalField(Panel panel,string label,Control control)
    {var field=new StackPanel{Spacing=4};field.Children.Add(new TextBlock{Text=label,FontSize=11});field.Children.Add(control);panel.Children.Add(field);}
    void Change(Action action,string message)
    {try{document.Checkpoint();action();ResetGesture();UpdateCanvasControls();SyncSelection();Refresh();status.Text=message;}catch(Exception ex){status.Text=ex.Message;}}
    void ResetGesture(){preview=null;regionPoints.Clear();gesture=null;editShape=null;var pointer=capturedPointer;capturedPointer=null;pointer?.Capture(null);}
    void UpdateCanvasControls(){canvasWidth.Value=document.Width;canvasHeight.Value=document.Height;sizeInfo.Text=$"{document.Width} × {document.Height} px";UpdateZoom();}
    void UpdateZoom()
    {
        zoom=fit.IsChecked==true?(float)Math.Clamp(Math.Min(Math.Max(50,viewport.Bounds.Width-40)/document.Width,Math.Max(50,viewport.Bounds.Height-40)/document.Height),.01,4):(float)(zoomPercent.Value??100)/100;
        syncing=true;if(fit.IsChecked==true)zoomPercent.Value=Math.Round((decimal)zoom*100);syncing=false;
        image.Width=document.Width*zoom;image.Height=document.Height*zoom;horizontalRuler.Width=image.Width+2;verticalRuler.Height=image.Height+2;horizontalRuler.SetZoom(zoom);verticalRuler.SetZoom(zoom);if(displayed!=null)Refresh();
    }
    SKPoint PositionOnCanvas(PointerEventArgs e)
    {var p=e.GetPosition(image);return new(Math.Clamp((float)p.X/zoom,0,document.Width-1),Math.Clamp((float)p.Y/zoom,0,document.Height-1));}
    Shape NewShape(SKPoint p)=>new(){Kind=Tool is "Pencil" or "Eraser"?"Brush":Tool=="Free polygon"?"Polygon":Tool,Points=[p,p],Stroke=Tool=="Eraser"?colors.Fill:secondaryDrawing?colors.Fill:colors.Stroke,Fill=secondaryDrawing?colors.Stroke:colors.Fill,Thickness=Tool=="Pencil"?1:(float)(thickness.Value??3),Filled=fill.IsChecked==true,Outlined=Tool is "Brush" or "Pencil" or "Eraser"||outlined};
    static float Distance(SKPoint a,SKPoint b)=>MathF.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));
    static float Direction(SKPoint center,SKPoint p)=>MathF.Atan2(p.Y-center.Y,p.X-center.X)*180/MathF.PI;
    SelectionKind RegionKind=>Tool=="Select ellipse"?SelectionKind.Ellipse:Tool=="Select lasso"?SelectionKind.Lasso:SelectionKind.Rectangle;
    (SKPoint Resize,SKPoint Rotate,SKPoint Center) SelectionHandles()
    {
        var s=Selection!;var b=s.OriginalBounds;
        var center=s.Matrix.MapPoint(new(b.MidX,b.MidY));var top=s.Matrix.MapPoint(new(b.MidX,b.Top));
        var length=Math.Max(.01f,Distance(center,top));var rotate=new SKPoint(top.X+(top.X-center.X)/length*28/zoom,top.Y+(top.Y-center.Y)/length*28/zoom);
        return(s.Matrix.MapPoint(new(b.Right,b.Bottom)),rotate,center);
    }
    void Pressed(object? sender,PointerPressedEventArgs e)
    {
        var buttons=e.GetCurrentPoint(image).Properties;
        if(!buttons.IsLeftButtonPressed&&!buttons.IsRightButtonPressed)return;
        secondaryDrawing=buttons.IsRightButtonPressed;
        try
        {
            image.Focus();var p=PositionOnCanvas(e);start=last=p;
            if(Tool=="Picker"){using var sample=document.Composite(Vector);if(secondaryDrawing)colors.SetFill(sample.GetPixel((int)p.X,(int)p.Y));else colors.SetStroke(sample.GetPixel((int)p.X,(int)p.Y));SetTool("Brush");return;}
            if(Tool=="Zoom"){fit.IsChecked=false;zoomPercent.Value=Math.Clamp((zoomPercent.Value??100)*2,10,400);return;}
            if(Tool=="Text"){ShowTextEditor(p);return;}
            if(Selection!=null)
            {
                var handles=SelectionHandles();
                if(Distance(p,handles.Resize)<12/zoom){BeginTransform("Scale",p);capturedPointer=e.Pointer;e.Pointer.Capture(image);return;}
                if(Distance(p,handles.Rotate)<12/zoom){BeginTransform("Rotate",p);capturedPointer=e.Pointer;e.Pointer.Capture(image);return;}
            }
            if(Tool.StartsWith("Select "))
            {
                if(Selection?.Contains(p)==true){BeginTransform("Move",p);}
                else{document.FinishSelection();SyncSelection();regionPoints.Clear();regionPoints.Add(p);regionPoints.Add(p);gesture="Region";}
            }
            else if(Tool is "Move" or "Rotate" or "Scale" or "Edit")
            {
                if((Selection==null || !Selection.Contains(p)) && Vector)document.SelectObject(p);
                if(Selection==null){status.Text="Select a region or vector object first.";Refresh();return;}
                if(Tool=="Edit")
                {
                    if(!Vector||Selection.Objects.Count!=1){status.Text="Select one vector object to edit its points.";return;}
                    document.Checkpoint();editShape=Selection.Objects[0].Shape;
                    handle=Enumerable.Range(0,editShape.Points.Count).MinBy(i=>Distance(editShape.Transform.MapPoint(editShape.Points[i]),p));gesture="Edit";
                }
                else BeginTransform(Tool,p);
                SyncSelection();
            }
            else if(Tool=="Fill")
            {if(Vector){status.Text="Fill works in Raster mode. Use the Fill menu for vector shapes.";return;}document.Checkpoint();document.Flood((int)p.X,(int)p.Y,secondaryDrawing?colors.Fill:colors.Stroke);SyncSelection();Refresh();return;}
            else if(Tool=="Free polygon")
            {
                document.FinishSelection();SyncSelection();
                if(preview==null)preview=NewShape(p);else{preview.Points[^1]=p;preview.Points.Add(p);}
                if(e.ClickCount>=2 && preview.Points.Count>=4){preview.Points.RemoveAt(preview.Points.Count-1);document.Checkpoint();document.Commit(preview,Vector);preview=null;}
                Refresh();return;
            }
            else{document.FinishSelection();SyncSelection();preview=NewShape(p);gesture="Draw";}
            capturedPointer=e.Pointer;e.Pointer.Capture(image);Refresh();
        }
        catch(Exception ex){status.Text=ex.Message;}
    }
    void BeginTransform(string type,SKPoint p)
    {document.Checkpoint();gesture=type;pressWidth=Selection!.Width;pressHeight=Selection.Height;pressAngle=Selection.Angle;pressDirection=Direction(SelectionHandles().Center,p);}
    void Moved(object? sender,PointerEventArgs e)
    {
        var p=PositionOnCanvas(e);
        pointerInfo.Text=$"{(int)p.X}, {(int)p.Y} px";
        if(Tool=="Free polygon"&&preview!=null){preview.Points[^1]=p;Refresh();return;}
        if(gesture==null)return;
        if(gesture=="Region")
        {if(RegionKind==SelectionKind.Lasso){if(Distance(regionPoints[^1],p)>1/zoom)regionPoints.Add(p);}else regionPoints[^1]=p;}
        else if(Selection!=null && gesture is "Move" or "Scale" or "Rotate")
        {
            if(gesture=="Move")Selection.Move(p.X-last.X,p.Y-last.Y);
            else if(gesture=="Rotate")
            {var angle=pressAngle+Direction(SelectionHandles().Center,p)-pressDirection;if(e.KeyModifiers.HasFlag(KeyModifiers.Shift))angle=MathF.Round(angle/15)*15;Selection.SetTransform(Selection.Width,Selection.Height,angle);}
            else
            {
                // Convert pointer deltas into the selection's rotated coordinate system.
                var radians=-pressAngle*MathF.PI/180;var dx=p.X-start.X;var dy=p.Y-start.Y;
                var width=Math.Clamp(pressWidth+2*(dx*MathF.Cos(radians)-dy*MathF.Sin(radians)),1,16384);
                var height=Math.Clamp(pressHeight+2*(dx*MathF.Sin(radians)+dy*MathF.Cos(radians)),1,16384);
                if(lockRatio.IsChecked==true||e.KeyModifiers.HasFlag(KeyModifiers.Shift))height=Math.Clamp(width*pressHeight/pressWidth,1,16384);
                Selection.SetTransform(width,height,Selection.Angle);
            }
            last=p;SyncSelection();
        }
        else if(gesture=="Edit"&&editShape!=null && editShape.Transform.TryInvert(out var inverse))editShape.Points[handle]=inverse.MapPoint(p);
        else if(preview!=null)
        {
            if(Tool is "Brush" or "Pencil" or "Eraser")preview.Points.Add(p);
            else
            {
                if(e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    if(Tool=="Line"){var angle=MathF.Round(MathF.Atan2(p.Y-start.Y,p.X-start.X)/(MathF.PI/4))*(MathF.PI/4);var length=Distance(start,p);p=new(start.X+MathF.Cos(angle)*length,start.Y+MathF.Sin(angle)*length);}
                    else{var size=Math.Max(Math.Abs(p.X-start.X),Math.Abs(p.Y-start.Y));p=new(start.X+Math.Sign(p.X-start.X)*size,start.Y+Math.Sign(p.Y-start.Y)*size);}
                }
                if(Tool=="Triangle")preview.Points=[new((start.X+p.X)/2,start.Y),new(p.X,p.Y),new(start.X,p.Y)];
                else if(Tool=="Polygon")
                {int count=(int)(sides.Value??5);var radius=Distance(start,p);preview.Points=[];for(int i=0;i<count;i++){var angle=-MathF.PI/2+i*2*MathF.PI/count;preview.Points.Add(new(start.X+radius*MathF.Cos(angle),start.Y+radius*MathF.Sin(angle)));}}
                else preview.Points=[start,p];
            }
        }
        Refresh();
    }
    void Released(object? sender,PointerReleasedEventArgs e)
    {
        if(gesture==null)return;
        try
        {
            Moved(sender,e);
            if(gesture=="Region")
            {
                if(Distance(start,PositionOnCanvas(e))<3/zoom && RegionKind!=SelectionKind.Lasso && Vector)document.SelectObject(start);
                else{using var region=SelectionRegion.Create(RegionKind,regionPoints);document.Select(region,Vector);}
                status.Text=Selection==null?"No objects found in this selection.":"Selection ready. Drag to move; use Resize to transform.";
            }
            else if(gesture=="Draw" && preview!=null){document.Checkpoint();document.Commit(preview,Vector);}
            else if(gesture=="Edit"&&editShape!=null)
            {document.SelectShape(editShape);}
            ResetGesture();e.Pointer.Capture(null);SyncSelection();Refresh();
        }
        catch(Exception ex){ResetGesture();e.Pointer.Capture(null);status.Text=ex.Message;Refresh();}
    }
    void SyncSelection()
    {
        transformSync=true;
        selectionWidth.IsEnabled=selectionHeight.IsEnabled=selectionAngle.IsEnabled=Selection!=null;
        if(Selection!=null){selectionWidth.Value=(decimal)Selection.Width;selectionHeight.Value=(decimal)Selection.Height;selectionAngle.Value=(decimal)Selection.Angle;}
        selectionInfo.Text=Selection==null?"No selection":Selection.Pixels!=null?"Raster pixel selection":$"{Selection.Objects.Count} vector object(s)";
        transformSync=false;undoButton.IsEnabled=document.CanUndo;UpdateCommandState();
    }
    void DimensionChanged(bool width)
    {
        if(transformSync||Selection==null||lockRatio.IsChecked!=true)return;
        transformSync=true;
        if(width)selectionHeight.Value=Math.Clamp((selectionWidth.Value??1)*(decimal)(Selection.Height/Selection.Width),1,16384);
        else selectionWidth.Value=Math.Clamp((selectionHeight.Value??1)*(decimal)(Selection.Width/Selection.Height),1,16384);
        transformSync=false;
    }
    void ApplyTransform()
    {
        if(Selection==null){status.Text="Select a shape or region first.";return;}
        try{document.Checkpoint();Selection.SetTransform((float)(selectionWidth.Value??1),(float)(selectionHeight.Value??1),(float)(selectionAngle.Value??0));SyncSelection();Refresh();status.Text="Selection resized and rotated.";}catch(Exception ex){status.Text=ex.Message;}
    }
    void ApplyColors()
    {
        if(Selection==null||!Vector){status.Text="Select vector shapes to change their colors.";return;}
        document.Checkpoint();foreach(var item in Selection.Objects){item.Shape.Stroke=colors.Stroke;item.Shape.Fill=colors.Fill;item.Shape.Filled=fill.IsChecked==true;item.Shape.Thickness=(float)(thickness.Value??3);}SyncSelection();Refresh();
    }
    void DeleteSelection(){if(Selection!=null)Change(document.DeleteSelection,"Selection deleted. Undo restores it.");}
    void SelectAll(){document.FinishSelection();using var region=new SKPath();region.AddRect(new SKRect(0,0,document.Width,document.Height));document.Select(region,Vector);SyncSelection();Refresh();}
    void Undo(){if(document.Undo()){ResetGesture();UpdateCanvasControls();SyncSelection();Refresh();status.Text="Undo.";}}
    void ResizeCanvas()=>Change(()=>document.Resize((int)(canvasWidth.Value??1000),(int)(canvasHeight.Value??650),scaleContent.IsChecked==true),"Canvas resized.");
    void OnKeyDown(object? sender,KeyEventArgs e)
    {
        // Editing numbers and HEX values must keep ordinary text-editing shortcuts.
        if(e.Source is TextBox || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)return;
        if(e.Key==Key.Escape){if(WindowState==WindowState.FullScreen)WindowState=WindowState.Normal;document.FinishSelection();ResetGesture();SyncSelection();Refresh();e.Handled=true;}
        if(e.Key is Key.Delete or Key.Back){DeleteSelection();e.Handled=true;}
        if(e.KeyModifiers.HasFlag(KeyModifiers.Control)||e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {if(e.Key==Key.Z){if(e.KeyModifiers.HasFlag(KeyModifiers.Shift))Redo();else Undo();e.Handled=true;}if(e.Key==Key.Y){Redo();e.Handled=true;}if(e.Key==Key.C){Copy();e.Handled=true;}if(e.Key==Key.X){Cut();e.Handled=true;}if(e.Key==Key.V){_ = Paste();e.Handled=true;}if(e.Key==Key.S){_ = Save();e.Handled=true;}if(e.Key==Key.O){_ = Open();e.Handled=true;}if(e.Key==Key.A){SelectAll();e.Handled=true;}}
    }
    void Refresh()
    {
        using var bitmap=document.Composite(Vector,preview);
        using(var canvas=new SKCanvas(bitmap))
        {
            if(showGrid.IsChecked==true && zoom>=4){using var gridPaint=new SKPaint{Color=new SKColor(180,180,180,130),StrokeWidth=.2f};for(int x=0;x<document.Width;x++)canvas.DrawLine(x,0,x,document.Height,gridPaint);for(int y=0;y<document.Height;y++)canvas.DrawLine(0,y,document.Width,y,gridPaint);}
            using var stroke=new SKPaint{Color=new SKColor(37,99,235),Style=SKPaintStyle.Stroke,StrokeWidth=1.5f/zoom,IsAntialias=true};
            using var dash=SKPathEffect.CreateDash([6/zoom,4/zoom],0);stroke.PathEffect=dash;
            if(regionPoints.Count>1){using var region=SelectionRegion.Create(RegionKind,regionPoints);canvas.DrawPath(region,stroke);}
            if(Selection!=null)
            {
                using var outline=Selection.Outline();canvas.DrawPath(outline,stroke);stroke.PathEffect=null;
                var handles=SelectionHandles();var top=Selection.Matrix.MapPoint(new SKPoint(Selection.OriginalBounds.MidX,Selection.OriginalBounds.Top));canvas.DrawLine(top,handles.Rotate,stroke);
                using var paint=new SKPaint{Color=new SKColor(37,99,235),IsAntialias=true};canvas.DrawCircle(handles.Rotate,5/zoom,paint);
                canvas.DrawRect(new SKRect(handles.Resize.X-5/zoom,handles.Resize.Y-5/zoom,handles.Resize.X+5/zoom,handles.Resize.Y+5/zoom),paint);
                if(Tool=="Edit")foreach(var item in Selection.Objects)foreach(var point in item.Shape.Points)canvas.DrawCircle(item.Shape.Transform.MapPoint(point),4/zoom,paint);
            }
        }
        var format=bitmap.ColorType==SKColorType.Rgba8888?Avalonia.Platform.PixelFormat.Rgba8888:Avalonia.Platform.PixelFormat.Bgra8888;
        var next=new Avalonia.Media.Imaging.Bitmap(format,Avalonia.Platform.AlphaFormat.Premul,bitmap.GetPixels(),new PixelSize(bitmap.Width,bitmap.Height),new Vector(96,96),bitmap.RowBytes);image.Source=next;displayed?.Dispose();displayed=next;undoButton.IsEnabled=document.CanUndo;UpdateCommandState();
    }
    async Task Open()
    {
        try
        {
            var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Open image",AllowMultiple=false,FileTypeFilter=[FilePickerFileTypes.ImageAll]});
            if(files.Count==0)return;await using var stream=await files[0].OpenReadAsync();document.Checkpoint();document.Load(stream);mode.SelectedIndex=0;ResetGesture();UpdateCanvasControls();SyncSelection();Refresh();status.Text="Image opened at its original size.";Title=files[0].Name+" - Paint";
        }
        catch(Exception ex){status.Text="Open failed: "+ex.Message;}
    }
    async Task Save()
    {
        try
        {
            var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{Title="Save image",SuggestedFileName="desen.png",DefaultExtension="png",FileTypeChoices=[new FilePickerFileType("PNG image"){Patterns=["*.png"]},new FilePickerFileType("JPEG image"){Patterns=["*.jpg","*.jpeg"]}]});
            if(file==null)return;await using var stream=await file.OpenWriteAsync();if(stream.CanSeek)stream.SetLength(0);document.Save(stream,Vector,file.Name.EndsWith(".jpg",StringComparison.OrdinalIgnoreCase)||file.Name.EndsWith(".jpeg",StringComparison.OrdinalIgnoreCase));status.Text="Saved: "+file.Name;Title=file.Name+" - Paint";
        }
        catch(Exception ex){status.Text="Save failed: "+ex.Message;}
    }
}
