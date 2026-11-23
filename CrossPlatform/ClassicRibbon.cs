using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using SkiaSharp;

namespace Paint1.CrossPlatform;

public sealed partial class MainWindow
{
    readonly Dictionary<string,Button> commandButtons=[];
    readonly Dictionary<string,PaintButton> toolButtons=[];
    readonly TextBlock pointerInfo=new(){Width=135,VerticalAlignment=VerticalAlignment.Center};
    readonly CheckBox showGrid=new(){Content="Gridlines",FontSize=12};
    readonly CheckBox showRulers=new(){Content="Rulers",FontSize=12};
    readonly CheckBox showStatus=new(){Content="Status bar",IsChecked=true,FontSize=12};
    readonly PaintRuler horizontalRuler=new(false){Height=20,IsVisible=false},verticalRuler=new(true){Width=20,IsVisible=false};
    Control statusBar=null!;
    readonly Slider zoomSlider=new(){Minimum=10,Maximum=400,Value=100,Width=125,Height=24};
    readonly StackPanel ribbonHost=new();
    readonly PaintButton homeTab=new(){Content="Home",Width=60,Height=25,Selected=true},viewTab=new(){Content="View",Width=60,Height=25};
    Control homeRibbon=null!,viewRibbon=null!;
    SKBitmap? clipboardBitmap;
    bool outlined=true;
    Flyout? resizeFlyout;
    readonly Dictionary<string,Control> resizeCommands=[];

    PaintButton Command(string name,string icon,Action action,bool large=false,double width=double.NaN)
    {
        var content=new StackPanel{Orientation=large?Orientation.Vertical:Orientation.Horizontal,Spacing=large?3:5,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        if(icon.Length>0)content.Children.Add(PaintIcons.Icon(icon,large?30:16));
        content.Children.Add(new TextBlock{Text=name,FontSize=12,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
        var button=new PaintButton{Content=content,Height=large?74:24,Width=width,Tag=name};
        Avalonia.Automation.AutomationProperties.SetName(button,name);ToolTip.SetTip(button,name);button.Click+=(_,_)=>action();commandButtons[name]=button;return button;
    }
    PaintButton ToolButton(string key,string icon,double size=25)
    {
        var button=new PaintButton{Content=PaintIcons.Icon(icon,size-5),Width=size,Height=size,Tag=key};ToolTip.SetTip(button,key);Avalonia.Automation.AutomationProperties.SetName(button,key);
        button.Click+=(_,_)=>SetTool(key);toolButtons[key]=button;return button;
    }
    static StackPanel Row(params Control[] controls){var panel=new StackPanel{Orientation=Orientation.Horizontal,Spacing=3};foreach(var c in controls)panel.Children.Add(c);return panel;}
    static StackPanel Column(params Control[] controls){var panel=new StackPanel{Spacing=0};foreach(var c in controls)panel.Children.Add(c);return panel;}
    static Border Group(string caption,Control content)
    {
        var root=new DockPanel{Height=105};var label=new TextBlock{Text=caption,FontSize=11,Foreground=SolidColorBrush.Parse("#5B6571"),Height=17,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom};
        DockPanel.SetDock(label,Dock.Bottom);root.Children.Add(label);root.Children.Add(content);
        return new Border{Child=root,Padding=new Thickness(5,3),BorderThickness=new Thickness(0,0,1,0),BorderBrush=SolidColorBrush.Parse("#D3D9E0")};
    }
    void Menu(PaintButton anchor,params (string Label,Action Action)[] actions)
    {
        var stack=new StackPanel{Spacing=1,MinWidth=185};var flyout=new Flyout{Content=stack};
        foreach(var item in actions){var button=Command(item.Label,"",()=>{flyout.Hide();item.Action();});button.HorizontalAlignment=HorizontalAlignment.Stretch;stack.Children.Add(button);}
        anchor.Click+=(_,_)=>flyout.ShowAt(anchor);
    }
    void SetTool(string key)
    {
        var item=tool.Items.Cast<ToolItem>().FirstOrDefault(t=>t.Key==key);
        if(item!=null)tool.SelectedItem=item;UpdateToolButtons();
        image.Cursor=new Cursor(key=="Text"?StandardCursorType.Ibeam:key=="Picker"?StandardCursorType.Cross:StandardCursorType.Cross);
    }
    void UpdateToolButtons(){foreach(var item in toolButtons)item.Value.Selected=item.Key==Tool;}
    void UpdateCommandState()
    {
        if(commandButtons.TryGetValue("Redo",out var redo))redo.IsEnabled=document.CanRedo;
        foreach(var name in new[]{"Cut","Copy","Crop","Delete selection","Apply to selection","Apply colors"})if(commandButtons.TryGetValue(name,out var b))b.IsEnabled=Selection!=null;
        if(commandButtons.TryGetValue("Undo",out var undo))undo.IsEnabled=document.CanUndo;
    }
    void BuildClassicLayout()
    {
        var root=new DockPanel{Background=SolidColorBrush.Parse("#F5F6F7")};
        var top=new StackPanel();
        var quick=new DockPanel{Height=25,Background=SolidColorBrush.Parse("#ECF2F9")};
        var quickButtons=Row();quickButtons.Children.Add(PaintIcons.Icon("Brush",19));
        var save=Command("Save","Save",async()=>await Save());save.Content=PaintIcons.Icon("Save",17);save.Width=27;quickButtons.Children.Add(save);
        undoButton.Content=PaintIcons.Icon("Undo",17);undoButton.Width=27;undoButton.Height=22;undoButton.Margin=new Thickness(0);undoButton.Tag="Undo";undoButton.Click+=(_,_)=>Undo();quickButtons.Children.Add(undoButton);commandButtons["Undo"]=undoButton;
        var redo=Command("Redo","Redo",Redo);redo.Content=PaintIcons.Icon("Redo",17);redo.Width=27;quickButtons.Children.Add(redo);
        quick.Children.Add(quickButtons);top.Children.Add(quick);
        var tabs=Row();tabs.Height=26;tabs.Background=Brushes.White;
        var file=new PaintButton{Content="File",Width=57,Height=25,Background=SolidColorBrush.Parse("#1979C9"),Foreground=Brushes.White};
        Menu(file,("New",NewDocument),("Open",async()=>await Open()),("Save",async()=>await Save()),("Save as",async()=>await Save()),("Image properties",()=>ShowResize()),("Exit",Close));
        tabs.Children.Add(file);tabs.Children.Add(homeTab);tabs.Children.Add(viewTab);
        homeTab.Click+=(_,_)=>SwitchRibbon(false);viewTab.Click+=(_,_)=>SwitchRibbon(true);top.Children.Add(tabs);
        DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);
        homeRibbon=BuildHomeRibbon();viewRibbon=BuildViewRibbon();ribbonHost.Children.Add(homeRibbon);
        dashboard=new ScrollViewer{Content=ribbonHost,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=SolidColorBrush.Parse("#F5F6F7")};
        viewport=new ScrollViewer{Content=new Border{Child=image,Margin=new Thickness(8),Background=Brushes.White,BorderBrush=SolidColorBrush.Parse("#A4AFBE"),BorderThickness=new Thickness(1),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top},HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=SolidColorBrush.Parse("#CAD5E4")};
        var canvasGrid=new Grid{RowDefinitions=new RowDefinitions("Auto,Auto"),ColumnDefinitions=new ColumnDefinitions("Auto,Auto"),Margin=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
        var paper=(Control)viewport.Content!;viewport.Content=null;paper.Margin=new Thickness(0);Grid.SetRow(paper,1);Grid.SetColumn(paper,1);canvasGrid.Children.Add(paper);
        Grid.SetColumn(horizontalRuler,1);Grid.SetRow(verticalRuler,1);canvasGrid.Children.Add(horizontalRuler);canvasGrid.Children.Add(verticalRuler);viewport.Content=canvasGrid;
        showRulers.IsCheckedChanged+=(_,_)=>{horizontalRuler.IsVisible=verticalRuler.IsVisible=showRulers.IsChecked==true;};
        workspace.RowDefinitions.Add(dashboardRow);workspace.RowDefinitions.Add(new RowDefinition(new GridLength(8)));workspace.RowDefinitions.Add(new RowDefinition(GridLength.Star){MinHeight=180});
        dashboardSplitter.Height=8;dashboardSplitter.Background=SolidColorBrush.Parse("#D6E2EF");
        Grid.SetRow(dashboard,0);Grid.SetRow(dashboardSplitter,1);Grid.SetRow(viewport,2);workspace.Children.Add(dashboard);workspace.Children.Add(dashboardSplitter);workspace.Children.Add(viewport);
        var grip=new Border{Width=40,Height=2,Background=SolidColorBrush.Parse("#96ABC2"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};Grid.SetRow(grip,1);workspace.Children.Add(grip);
        ToolTip.SetTip(dashboardSplitter,"Drag to resize the ribbon. Double-click to reset. Arrow keys adjust the height.");Avalonia.Automation.AutomationProperties.SetName(dashboardSplitter,"Resize ribbon");
        dashboardSplitter.DoubleTapped+=(_,_)=>ResetDashboardHeight();workspace.SizeChanged+=(_,_)=>dashboardRow.MaxHeight=Math.Max(dashboardRow.MinHeight,workspace.Bounds.Height-188);
        statusBar=BuildStatusBar();showStatus.IsCheckedChanged+=(_,_)=>statusBar.IsVisible=showStatus.IsChecked==true;DockPanel.SetDock(statusBar,Dock.Bottom);root.Children.Add(statusBar);root.Children.Add(workspace);Content=root;UpdateToolButtons();
    }
    Control BuildHomeRibbon()
    {
        var ribbon=Row();ribbon.Spacing=0;
        var paste=Command("Paste","Paste",async()=>await Paste(),true,49);
        ribbon.Children.Add(Group("Clipboard",Row(paste,Column(Command("Cut","Cut",Cut),Command("Copy","Copy",Copy)))));
        var select=Command("Select\n▾","Select",()=>{},true,51);select.Click+=(_,_)=>SetTool("Select rectangle");
        Menu(select,("Rectangular selection",()=>SetTool("Select rectangle")),("Free-form selection",()=>SetTool("Select lasso")),("Elliptical selection",()=>SetTool("Select ellipse")),("Select all",SelectAll),("Deselect",Deselect),("Delete selection",DeleteSelection));
        var crop=Command("Crop","Crop",()=>Change(()=>document.Crop(Vector),"Cropped to selection."));
        var resize=Command("Resize","Resize",()=>ShowResize());
        var rotate=Command("Rotate ▾","Rotate",()=>{});
        Menu(rotate,("Rotate right 90°",()=>Rotate(90)),("Rotate left 90°",()=>Rotate(270)),("Rotate 180°",()=>Rotate(180)),("Flip horizontal",()=>Flip(true)),("Flip vertical",()=>Flip(false)));
        ribbon.Children.Add(Group("Image",Row(select,Column(crop,resize,rotate))));
        var tools=new WrapPanel{Width=80,Height=60,Margin=new Thickness(2,5)};
        foreach(var (key,icon) in new[]{("Pencil","Pencil"),("Fill","Fill"),("Text","Text"),("Eraser","Eraser"),("Picker","Picker"),("Zoom","Zoom")})tools.Children.Add(ToolButton(key,icon));
        ribbon.Children.Add(Group("Tools",tools));
        var brushes=Command("Brushes\n▾","Brush",()=>SetTool("Brush"),true,60);
        Menu(brushes,("Brush",()=>{thickness.Value=3;SetTool("Brush");}),("Thick brush",()=>{thickness.Value=12;SetTool("Brush");}),("Pencil",()=>SetTool("Pencil")));
        toolButtons["Brush"]=brushes;ribbon.Children.Add(Group("Brushes",brushes));
        var gallery=new WrapPanel{Width=184,Height=78,Background=Brushes.White,Margin=new Thickness(0,0,4,0)};
        foreach(var key in ShapeGeometry.Gallery)gallery.Children.Add(ToolButton(key,key,26));
        var outline=Command("Outline ▾","Pencil",()=>{});Menu(outline,("Solid outline",()=>outlined=true),("No outline",()=>outlined=false));
        var filled=Command("Fill ▾","Fill",()=>{});Menu(filled,("No fill",()=>fill.IsChecked=false),("Solid color",()=>fill.IsChecked=true));
        var options=Command("More ▾","",()=>{});Menu(options,("Free polygon",()=>SetTool("Free polygon")),("Regular polygon",()=>SetTool("Polygon")),("Move selection",()=>SetTool("Move")),("Edit vector points",()=>SetTool("Edit")),("Apply colors",ApplyColors));
        ribbon.Children.Add(Group("Shapes",Row(gallery,Column(outline,filled,options))));
        var size=Command("Size\n▾","Size",()=>{},true,51);Menu(size,("1 px",()=>thickness.Value=1),("3 px",()=>thickness.Value=3),("5 px",()=>thickness.Value=5),("8 px",()=>thickness.Value=8),("12 px",()=>thickness.Value=12));ribbon.Children.Add(Group("Size",size));
        ribbon.Children.Add(Group("Colors",colors));return ribbon;
    }
    Control BuildViewRibbon()
    {
        var ribbon=Row();ribbon.Spacing=0;
        ribbon.Children.Add(Group("Zoom",Row(Command("Zoom in","Zoom in",()=>SetZoom((zoomPercent.Value??100)*1.25m),true),Command("Zoom out","Zoom out",()=>SetZoom((zoomPercent.Value??100)/1.25m),true),Command("100%","Zoom",()=>SetZoom(100),true))));
        fit.Content="Fit to window";var reset=Command("Reset ribbon","",ResetDashboardHeight);var view=Column(showRulers,showGrid,showStatus);ribbon.Children.Add(Group("Show or hide",view));showGrid.IsCheckedChanged+=(_,_)=>Refresh();
        mode.Width=140;mode.FontSize=12;mode.ItemsSource=new[]{"Raster","Vector"};mode.SelectedIndex=0;
        var modePanel=Column(new TextBlock{Text="Drawing mode",FontSize=11},mode);ribbon.Children.Add(Group("Workspace",modePanel));ribbon.Children.Add(Group("Display",Column(fit,reset,Command("Full screen","",()=>WindowState=WindowState==WindowState.FullScreen?WindowState.Normal:WindowState.FullScreen))));return ribbon;
    }
    Control BuildStatusBar()
    {
        var bar=new DockPanel{Height=27,Background=SolidColorBrush.Parse("#F0F0F0"),Margin=new Thickness(4,0)};
        var zoomRow=Row();zoomRow.VerticalAlignment=VerticalAlignment.Center;zoomRow.Children.Add(new TextBlock{Text="Zoom",FontSize=11,VerticalAlignment=VerticalAlignment.Center});zoomPercent.Width=53;zoomPercent.Height=23;zoomPercent.MinHeight=0;zoomPercent.FontSize=11;zoomRow.Children.Add(zoomPercent);
        zoomRow.Children.Add(Command("−","",()=>SetZoom((zoomPercent.Value??100)-25)));zoomRow.Children.Add(zoomSlider);zoomRow.Children.Add(Command("+","",()=>SetZoom((zoomPercent.Value??100)+25)));DockPanel.SetDock(zoomRow,Dock.Right);bar.Children.Add(zoomRow);
        zoomSlider.ValueChanged+=(_,_)=>{if(!syncing)SetZoom((decimal)zoomSlider.Value);};
        zoomPercent.ValueChanged+=(_,_)=>{var previous=syncing;syncing=true;zoomSlider.Value=(double)(zoomPercent.Value??100);syncing=previous;};
        pointerInfo.Text="0, 0 px";DockPanel.SetDock(pointerInfo,Dock.Left);bar.Children.Add(pointerInfo);sizeInfo.Width=142;DockPanel.SetDock(sizeInfo,Dock.Left);bar.Children.Add(sizeInfo);
        status.Text="For Help, choose a tool or hover over a command.";status.FontSize=11;status.Margin=new Thickness(4,4);status.TextTrimming=TextTrimming.CharacterEllipsis;status.TextWrapping=TextWrapping.NoWrap;bar.Children.Add(status);return bar;
    }
    void SwitchRibbon(bool view){homeTab.Selected=!view;viewTab.Selected=view;ribbonHost.Children.Clear();ribbonHost.Children.Add(view?viewRibbon:homeRibbon);}
    void SetZoom(decimal value){fit.IsChecked=false;zoomPercent.Value=Math.Clamp(value,10,400);}
    void Deselect(){document.FinishSelection();ResetGesture();SyncSelection();Refresh();}
    void Redo(){if(document.Redo()){ResetGesture();UpdateCanvasControls();SyncSelection();Refresh();status.Text="Redo.";}}
    void NewDocument()=>Change(()=>{document.Clear(false);document.Clear(true);},"New image. Undo restores the previous drawing.");
    void Rotate(int degrees)
    {
        if(Selection!=null){document.Checkpoint();Selection.SetTransform(Selection.Width,Selection.Height,Selection.Angle+degrees);SyncSelection();Refresh();}
        else Change(()=>document.RotateCanvas(degrees),"Image rotated.");
    }
    void Flip(bool horizontal)
    {
        if(Selection!=null){document.Checkpoint();Selection.Flip(horizontal);SyncSelection();Refresh();}
        else Change(()=>document.FlipCanvas(horizontal),"Image flipped.");
    }
    void Copy()
    {
        var bitmap=document.CopySelection(Vector);if(bitmap==null){status.Text="Select an area first.";return;}
        clipboardBitmap?.Dispose();clipboardBitmap=bitmap;status.Text="Selection copied.";
        _ = PublishClipboard(bitmap);
    }
    async Task PublishClipboard(SKBitmap bitmap)
    {
        try{if(Clipboard==null)return;using var img=SKImage.FromBitmap(bitmap);using var data=img.Encode(SKEncodedImageFormat.Png,100);using var stream=data.AsStream();using var image=new Avalonia.Media.Imaging.Bitmap(stream);await Clipboard.SetBitmapAsync(image);}catch(Exception ex){status.Text="Copied inside Paint. System clipboard unavailable: "+ex.Message;}
    }
    void Cut(){if(Selection==null)return;Copy();DeleteSelection();}
    async Task Paste()
    {
        try
        {
            SKBitmap? source=null;
            if(Clipboard!=null){using var image=await Clipboard.TryGetBitmapAsync();if(image!=null){using var stream=new MemoryStream();image.Save(stream,Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);stream.Position=0;source=SKBitmap.Decode(stream);}}
            source??=clipboardBitmap?.Copy();if(source==null){status.Text="No image on the clipboard.";return;}
            mode.SelectedIndex=0;using(source){document.Checkpoint();document.Paste(source);}SetTool("Move");SyncSelection();Refresh();status.Text="Pasted. Drag to position the selection.";
        }
        catch(Exception ex){status.Text="Paste failed: "+ex.Message;}
    }
    void ShowResize()
    {
        if(resizeFlyout==null)
        {
            var panel=new StackPanel{Spacing=8,Width=310};panel.Children.Add(new TextBlock{Text="Resize and rotate",FontSize=16,FontWeight=FontWeight.SemiBold});
            var selectionFields=Row();VerticalField(selectionFields,"Horizontal (px)",selectionWidth);VerticalField(selectionFields,"Vertical (px)",selectionHeight);panel.Children.Add(selectionInfo);panel.Children.Add(selectionFields);
            panel.Children.Add(lockRatio);VerticalField(panel,"Angle (degrees)",selectionAngle);panel.Children.Add(Command("Apply to selection","",()=>{ApplyTransform();resizeFlyout?.Hide();}));panel.Children.Add(Command("Apply colors","",ApplyColors));
            panel.Children.Add(new Separator());panel.Children.Add(new TextBlock{Text="Canvas dimensions",FontWeight=FontWeight.Bold});var canvasFields=Row();VerticalField(canvasFields,"Width (px)",canvasWidth);VerticalField(canvasFields,"Height (px)",canvasHeight);panel.Children.Add(canvasFields);scaleContent.Content="Scale image content";panel.Children.Add(scaleContent);
            panel.Children.Add(Command("Resize canvas","",()=>{ResizeCanvas();resizeFlyout?.Hide();}));VerticalField(panel,"Regular polygon sides",sides);
            resizeFlyout=new Flyout{Content=panel};
        }
        SyncSelection();resizeFlyout.ShowAt(commandButtons["Resize"]);
    }
    void ShowTextEditor(SKPoint point)
    {
        var text=new TextBox{PlaceholderText="Type your text",AcceptsReturn=true,Height=90};var fontSize=Number(6,200,24,80);var panel=Column(new TextBlock{Text="Text",FontSize=16},text,Row(new TextBlock{Text="Font size",VerticalAlignment=VerticalAlignment.Center},fontSize));panel.Width=300;panel.Spacing=8;
        var flyout=new Flyout{Content=panel};var insert=Command("Insert text","",()=>{if(!string.IsNullOrWhiteSpace(text.Text)){document.Checkpoint();var y=point.Y;foreach(var line in text.Text.Split('\n')){document.Commit(new Shape{Kind="Text",Text=line,FontSize=(float)(fontSize.Value??24),Points=[new SKPoint(point.X,y)],Stroke=colors.Stroke},Vector);y+=(float)(fontSize.Value??24)*1.3f;}Refresh();}flyout.Hide();});panel.Children.Add(insert);flyout.ShowAt(image);text.Focus();
    }
}
