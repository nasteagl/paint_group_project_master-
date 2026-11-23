# Paint1 — classic Paint for Windows and macOS

A .NET 10 / Avalonia 12 paint application with a Windows 7/10 Paint-style interface. It runs from the same project on Windows and macOS; Mono and .NET Framework targeting packs are not required.

## Run

```sh
dotnet run --project Paint1.csproj
```

In Rider open Paint1.sln, reload the project, and use a `.NET Project` run configuration targeting Paint1.

## Classic interface

- Quick-access Save, Undo, and Redo.
- File, Home, and View tabs.
- Home ribbon groups: Clipboard, Image, Tools, Brushes, Shapes, Size, and Colors.
- Color 1 / Color 2 previews, classic palette, ten custom-color slots, and an Edit Colors popup with RGB sliders and HEX input. Right-click a palette swatch to set Color 2.
- Blue-gray workspace with a bordered canvas, pixel-coordinate readout, document dimensions, and bottom zoom controls.
- Drag the divider below the ribbon to resize it. Double-click to reset, or use View > Reset ribbon. A focused divider also supports Up/Down arrow keys. The ribbon scrolls when compact, preserving a minimum canvas area.
- View controls for zoom, fit to window, rulers, pixel grid (visible at 400% or above), status bar, full screen, and the existing Raster/Vector modes.

## Drawing and editing

- Pencil, brush, eraser (uses Color 2), fill bucket, color picker, text, and magnifier.
- Line, S-curve, ellipse, rectangle, rounded rectangle, triangle, right triangle, diamond, pentagon, hexagon, four arrows, four/five/six-point stars, heart, lightning, callout, and cloud. Free and regular polygons are under Shapes > More.
- Outline and Fill menus, stroke-size presets, and normal/thick brush choices.
- Text popup accepts multiple lines and a font size. Text becomes scalable geometry and remains editable as a vector object through transforms; it is not a live text box after insertion.
- Rectangular, elliptical, and free-form selections work in Raster and Vector modes. Vector selection captures complete intersecting objects; Raster selection captures the masked pixels.
- Drag inside a selection to move it; drag its lower-right handle to scale or its upper round handle to rotate. Shift constrains lines/shapes and snaps rotation to 15°.
- Resize popup provides selection width/height, aspect lock, relative angle, color application, canvas dimensions, optional image-content scaling, and regular-polygon sides. Reselecting an object establishes a new transform reference.
- Copy, Cut, and Paste support the system image clipboard, with an in-app fallback. Pasted content becomes a movable raster selection, expanding the canvas when needed. Copying vectors to the clipboard rasterizes them.
- Crop, rotate 90/180/270°, and horizontal/vertical flip. Rotate/flip act on a selection when present, otherwise on the whole image.
- Undo and Redo. A new edit clears the redo branch. History retains at most 12 undo steps and approximately 96 MB of undo bitmap snapshots; the latest snapshot is always retained.
- Ctrl/Cmd+Z: Undo; Ctrl/Cmd+Shift+Z or Ctrl/Cmd+Y: Redo; Ctrl/Cmd+A/C/X/V/O/S: select all, copy, cut, paste, open, save. Canvas shortcuts preserve text editing when a text field has focus.
- Opening PNG/JPEG images adopts the original image dimensions. Export saves the active mode as PNG/JPEG including floating selections but without selection handles, rulers, or display grids.
- Canvas limits: 8192 px per edge and 24 million pixels total. Shrinking without scaling crops raster edges; Undo restores them.

## Scope and remaining differences

This is a classic Paint-style recreation, not the Microsoft Paint binary or complete feature parity. The window frame remains native to the host OS. Printing/page setup, scanner acquisition, skew, every textured brush type, transparent-selection color-key behavior, and live text re-editing are not implemented. The curve tool creates an S-curve rather than Paint's interactive bend workflow. Vector mode and a resizable ribbon are extensions retained from this project.

PNG/JPEG exports flatten content; there is currently no editable project-file format. Existing Windows Forms sources are retained but excluded from the active build.

## Source and verification

- `CrossPlatform/MainWindow.cs`: input, drawing gestures, image import/export, and transforms.
- `ClassicRibbon.cs`, `ClassicControls.cs`, `ColorDashboard.cs`, `PaintRuler.cs`: classic UI and commands.
- `PaintDocument.cs`, `ClassicOperations.cs`, `ShapeGeometry.cs`: drawing, selection, clipboard images, history, crop, and geometry.
- `Tests/`: executable model checks and Avalonia Headless input/render checks.

```sh
dotnet run --project Tests/Paint1.Checks.csproj -p:UsedAvaloniaProducts=
```

Headless checks validate rendering and interaction without a desktop. Native macOS and Windows execution have not been verified during this change; desktop automation is blocked by system permissions.

Restricted environments may block Avalonia telemetry's log directory. The local validation build uses:

```sh
dotnet build Paint1.sln -p:UsedAvaloniaProducts=
```
