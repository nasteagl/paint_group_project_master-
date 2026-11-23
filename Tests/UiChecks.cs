using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Paint1.CrossPlatform;
using SkiaSharp;

static class UiChecks
{
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(obj)!;
    public static void Run(Action<bool,string> check,string? screenshot)
    {
        AppBuilder.Configure<PaintApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
        var window=new MainWindow();window.Show();Field<CheckBox>(window,"fit").IsChecked=true;Dispatcher.UIThread.RunJobs();
        var doc=Field<PaintDocument>(window,"document");var image=Field<Image>(window,"image");var mode=Field<ComboBox>(window,"mode");var tool=Field<ComboBox>(window,"tool");
        var zoomField=window.GetType().GetField("zoom",BindingFlags.Instance|BindingFlags.NonPublic)!;
        check(window.IsVisible&&image.Width>0,"Interfața Avalonia se construiește și se afișează headless");
        var viewport=Field<ScrollViewer>(window,"viewport");
        check(image.Width<=viewport.Bounds.Width-35&&image.Height<=viewport.Bounds.Height-35,"Pânza se adaptează spațiului disponibil");
        void SelectTool(string key){var items=tool.Items.Cast<object>().ToArray();tool.SelectedItem=items.First(i=>(string)i.GetType().GetProperty("Key")!.GetValue(i)! == key);Dispatcher.UIThread.RunJobs();}
        Point At(float x,float y){var z=(float)zoomField.GetValue(window)!;return image.TranslatePoint(new Point(x*z,y*z),window)!.Value;}
        void Drag(float x1,float y1,float x2,float y2)
        {window.MouseDown(At(x1,y1),MouseButton.Left);window.MouseMove(At(x2,y2),RawInputModifiers.LeftMouseButton);window.MouseUp(At(x2,y2),MouseButton.Left);Dispatcher.UIThread.RunJobs();}
        void Click(string content)
        {
            var aliases=new Dictionary<string,string>{{"Resetează panoul","Reset ribbon"},{"Aplică dimensiune și unghi","Apply to selection"},{"Aplică culorile pe figuri","Apply colors"},{"Șterge selecția","Delete selection"},{"Anulează","Undo"},{"Redimensionează pânza","Resize canvas"},{"Deselectează","Deselect"}};
            var name=aliases.GetValueOrDefault(content,content);var commands=Field<Dictionary<string,Button>>(window,"commandButtons");
            if(!commands.TryGetValue(name,out var button))throw new Exception("Missing command: "+name);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        }
        var dashboard=Field<ScrollViewer>(window,"dashboard");
        var dashboardOrigin=dashboard.TranslatePoint(default,window)!.Value;
        var viewportOrigin=viewport.TranslatePoint(default,window)!.Value;
        check(dashboardOrigin.Y+dashboard.Bounds.Height<=viewportOrigin.Y,"Dashboard-ul este deasupra pânzei");
        check(viewport.Bounds.Width>=window.ClientSize.Width-1,"Pânza folosește toată lățimea ferestrei, fără bară laterală");
        var labels=window.GetVisualDescendants().OfType<TextBlock>().Select(t=>t.Text).ToArray();
        check(new[]{"Clipboard","Image","Tools","Brushes","Shapes","Size","Colors"}.All(labels.Contains),"Ribbon-ul conține grupele clasice Windows Paint");
        var commands=Field<Dictionary<string,Button>>(window,"commandButtons");
        check(new[]{"Paste","Cut","Copy","Crop","Resize","Size\n▾"}.All(commands.ContainsKey),"Comenzile clasice sunt conectate la ribbon");
        var home=Field<PaintButton>(window,"homeTab");var view=Field<PaintButton>(window,"viewTab");
        view.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Zoom"),"View tab opens the zoom and display ribbon");
        var rulers=Field<CheckBox>(window,"showRulers");rulers.IsChecked=true;Dispatcher.UIThread.RunJobs();
        check(Field<PaintRuler>(window,"horizontalRuler").IsVisible&&Field<PaintRuler>(window,"verticalRuler").IsVisible,"View toggles canvas rulers");rulers.IsChecked=false;
        var statusToggle=Field<CheckBox>(window,"showStatus");statusToggle.IsChecked=false;Dispatcher.UIThread.RunJobs();check(!Field<Control>(window,"statusBar").IsVisible,"View can hide the status bar");statusToggle.IsChecked=true;
        home.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
        check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Clipboard"),"Home tab restores the classic drawing ribbon");
        var divider=Field<GridSplitter>(window,"dashboardSplitter");
        var initialDashboardHeight=dashboard.Bounds.Height;
        var initialCanvasHeight=viewport.Bounds.Height;
        var initialDocSize=(doc.Width,doc.Height);
        void DragDivider(double delta)
        {
            var point=divider.TranslatePoint(new Point(divider.Bounds.Width/2,divider.Bounds.Height/2),window)!.Value;
            window.MouseDown(point,MouseButton.Left);window.MouseMove(new Point(point.X,point.Y+delta),RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(point.X,point.Y+delta),MouseButton.Left);Dispatcher.UIThread.RunJobs();
        }
        DragDivider(70);
        check(dashboard.Bounds.Height>initialDashboardHeight+60&&viewport.Bounds.Height<initialCanvasHeight-60,"Tragerea separatorului redimensionează dashboard-ul și zona de desen");
        check((doc.Width,doc.Height)==initialDocSize,"Redimensionarea dashboard-ului păstrează dimensiunile documentului");
        DragDivider(-1000);
        check(dashboard.Bounds.Height>=76&&dashboard.Bounds.Height<initialDashboardHeight,"Panoul are o înălțime minimă utilizabilă");
        check(dashboard.Extent.Height>dashboard.Viewport.Height,"Controalele rămân accesibile prin scroll când panoul este mic");
        DragDivider(2000);
        check(viewport.Bounds.Height>=179,"Separatorul păstrează spațiu minim pentru pânză");
        Click("Resetează panoul");
        check(Math.Abs(dashboard.Bounds.Height-initialDashboardHeight)<1,"Resetarea restaurează înălțimea implicită a dashboard-ului");
        divider.Focus();window.KeyPress(Key.Down,RawInputModifiers.None,PhysicalKey.ArrowDown,"ArrowDown");window.KeyRelease(Key.Down,RawInputModifiers.None,PhysicalKey.ArrowDown,"ArrowDown");Dispatcher.UIThread.RunJobs();
        check(dashboard.Bounds.Height>initialDashboardHeight,"Separatorul poate fi redimensionat din tastatură");
        Click("Resetează panoul");
        mode.SelectedIndex=1;SelectTool("Rectangle");Field<CheckBox>(window,"fill").IsChecked=true;
        Drag(100,100,220,180);check(doc.Vectors.Count==1,"Gestul mouse-ului creează o figură vectorială");
        SelectTool("Select rectangle");Drag(80,80,240,200);check(doc.Selection?.Objects.Count==1,"Gestul creează o selecție dreptunghiulară");
        Click("Resize");
        var width=Field<NumericUpDown>(window,"selectionWidth");var height=Field<NumericUpDown>(window,"selectionHeight");var angle=Field<NumericUpDown>(window,"selectionAngle");
        width.Value=240;check(Math.Abs((double)(height.Value??0)-160)<.1,"Câmpurile dimensiunii păstrează proporțiile");
        angle.Value=90;Click("Aplică dimensiune și unghi");using(var path=doc.Vectors[0].Path())check(Math.Abs(path.Bounds.Width-160)<1&&Math.Abs(path.Bounds.Height-240)<1,"Butonul aplică dimensiunea și rotația figurii");
        SelectTool("Move");Drag(160,140,240,220);using(var path=doc.Vectors[0].Path())check(Math.Abs(path.Bounds.MidX-240)<1&&Math.Abs(path.Bounds.MidY-220)<1,"Gestul mută figura selectată");
        var colorDashboard=Field<ColorDashboard>(window,"colors");Field<Slider>(colorDashboard,"red").Value=255;Field<Slider>(colorDashboard,"green").Value=0;Field<Slider>(colorDashboard,"blue").Value=0;
        check(colorDashboard.Stroke==SKColors.Red,"Sliderele RGB actualizează culoarea activă");Click("Aplică culorile pe figuri");check(doc.Vectors[0].Stroke==SKColors.Red,"Panoul recolorează figura selectată");
        Click("Șterge selecția");check(doc.Vectors.Count==0,"Butonul șterge selecția");Click("Anulează");check(doc.Vectors.Count==1,"Butonul Anulează restaurează figura");
        Click("Resize");Field<NumericUpDown>(window,"canvasWidth").Value=1400;Field<NumericUpDown>(window,"canvasHeight").Value=900;Click("Redimensionează pânza");check(doc.Width==1400&&doc.Height==900,"Controalele modifică dimensiunile pânzei");
        window.Width=880;window.Height=1100;Dispatcher.UIThread.RunJobs();check(image.Width<=viewport.Bounds.Width-35&&image.Height<=viewport.Bounds.Height-35,"Afișarea se adaptează redimensionării ferestrei");
        var originalWidth=doc.Width;var originalHeight=doc.Height;Field<CheckBox>(window,"fit").IsChecked=false;Field<NumericUpDown>(window,"zoomPercent").Value=150;
        check(Math.Abs(image.Width-doc.Width*1.5)<1&&doc.Width==originalWidth&&doc.Height==originalHeight,"Zoom-ul schimbă afișarea fără a modifica documentul");
        Field<CheckBox>(window,"fit").IsChecked=true;window.Width=1320;window.Height=900;Dispatcher.UIThread.RunJobs();
        mode.SelectedIndex=0;SelectTool("Rectangle");Drag(60,60,180,140);SelectTool("Select ellipse");Drag(40,40,200,160);
        check(doc.Selection?.Pixels!=null,"Selecția eliptică prin mouse extrage pixelii raster");
        SelectTool("Move");Drag(120,100,240,180);check(doc.Selection!.Offset.X>100,"Mutarea prin mouse funcționează pe selecția raster");
        Click("Deselectează");check(doc.Selection==null,"Deselectarea finalizează selecția raster");
        SelectTool("Select lasso");window.MouseDown(At(200,130),MouseButton.Left);window.MouseMove(At(300,130),RawInputModifiers.LeftMouseButton);window.MouseMove(At(300,230),RawInputModifiers.LeftMouseButton);window.MouseMove(At(200,230),RawInputModifiers.LeftMouseButton);window.MouseUp(At(200,130),MouseButton.Left);
        check(doc.Selection?.Pixels!=null,"Gestul lasou creează selecția liberă");
        Click("Deselectează");mode.SelectedIndex=0;
        Field<ColorDashboard>(window,"colors").SetStroke(SKColors.Black);Field<ColorDashboard>(window,"colors").SetFill(SKColors.White);
        window.GetType().GetField("outlined",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,false);
        SelectTool("Pencil");Drag(450,100,500,100);check(doc.Raster.GetPixel(475,100)!=SKColors.White,"Pencil tool draws a thin raster stroke");
        Field<NumericUpDown>(window,"thickness").Value=12;SelectTool("Eraser");Drag(460,100,490,100);check(doc.Raster.GetPixel(475,100)==SKColors.White,"Eraser paints with Color 2");
        doc.Commit(new Shape{Kind="Rectangle",Points=[new(550,80),new(590,120)],Filled=true,Fill=SKColors.Green,Stroke=SKColors.Green},false);
        SelectTool("Picker");window.MouseDown(At(570,100),MouseButton.Left);window.MouseUp(At(570,100),MouseButton.Left);
        check(Field<ColorDashboard>(window,"colors").Stroke==SKColors.Green,"Color picker samples the canvas into Color 1");
        var grid=Field<CheckBox>(window,"showGrid");grid.IsChecked=true;Field<NumericUpDown>(window,"zoomPercent").Value=400;Dispatcher.UIThread.RunJobs();
        check(image.Width==doc.Width*4,"View supports pixel-grid zoom at 400 percent");grid.IsChecked=false;Field<CheckBox>(window,"fit").IsChecked=true;
        window.GetType().GetField("outlined",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(window,true);
        if(screenshot!=null)
        {
            Click("Deselectează");mode.SelectedIndex=0;documentPreview();
            void documentPreview(){doc.Clear(false);doc.Resize(1000,650,false);var previewColor=Field<ColorDashboard>(window,"colors");previewColor.SetStroke(SKColors.Black);previewColor.SetFill(SKColors.White);}
            window.Width=1320;window.Height=900;Field<CheckBox>(window,"fit").IsChecked=false;Field<NumericUpDown>(window,"zoomPercent").Value=100;SelectTool("Brush");
            // Trigger normal controls after preparing the preview dimensions.
            Click("Resize");Field<NumericUpDown>(window,"canvasWidth").Value=1000;Field<NumericUpDown>(window,"canvasHeight").Value=650;Click("Resize canvas");
            Dispatcher.UIThread.RunJobs();
            using var frame=window.CaptureRenderedFrame();frame?.Save(screenshot,Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            
        }
        window.Close();
    }
}
