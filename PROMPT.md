# System Specification & Master Prompt: GeoMetrics for ArcGIS Pro

> **Purpose**: This file contains the complete, self-contained master prompt and engineering specification for building or maintaining the **GeoMetrics** ArcGIS Pro Add-in. It encompasses all architectural rules, SDK API contracts, geometry mathematics, threading guarantees, UI specifications, and default behaviors.

---

## 1. System Overview & Core Objectives

Create a professional, passive real-time CAD-style geometry measurement Add-in for **ArcGIS Pro (3.3.x / 3.4.x)** built with **C#** on top of the **ArcGIS Pro SDK for .NET (.NET 8)**.

The tool operates as a **passive real-time overlay** over ArcGIS Pro's native **Edit → Modify** workflow (e.g. *Edit Vertices*, *Reshape*, *Move*). As the user drags vertices or selects features, segment lengths, polygon areas, perimeters, and corner vertex angles are computed and rendered dynamically on the active map viewport at **60 FPS**, without altering feature class attributes, creating permanent graphic layers, or interfering with native ArcGIS Pro editing transactions.

---

## 2. Technical Stack & Environment

- **Target Host**: ArcGIS Pro 3.3.0+ / 3.4.x (64-bit).
- **Target Framework**: `net8.0-windows` (.NET 8.0).
- **Primary SDK Reference**: `Esri.ArcGISPro.Extensions30` NuGet package.
- **UI Framework**: WPF / XAML integrated into the ArcGIS Pro Framework (`DockPane`, `Button`, `Module`).
- **Packaging Format**: Esri ArcGIS Pro Add-in package (`.esriAddinX` archive containing `Config.daml` and `Images/` at root, and binaries in `Install/`).

---

## 3. Strict Architectural Principles

1. **Passive Event-Driven Operation**:
   - Do **NOT** implement an intrusive custom `MapTool` that replaces ArcGIS Pro's native editing sketch tool.
   - Listen passively to SDK mapping and editing events (`SketchModifiedEvent`, `MapViewCameraChangedEvent`, `MapSelectionChangedEvent`, `ActiveMapViewChangedEvent`).
2. **Lock-Free 60 FPS Frame Coalescing**:
   - Real-time vertex movement generates high-frequency events.
   - Employ lock-free `Interlocked.CompareExchange` coalescing state to ensure that only the latest sketch frame is processed on `QueuedTask`, preventing UI lag or event queue flooding.
3. **Viewport-Aware Dynamic Layout (Maplex Style)**:
   - Separate **geometry measurement** (map coordinates) from **viewport layout** (screen coordinates).
   - Recompute label positioning dynamically during Zoom and Pan without recalculating raw geometry.
   - For large polygons when zoomed in, anchor segment labels to the midpoint of the *visible section* of the segment, and anchor the area label inside the *visible portion* of the polygon.
4. **Clean Graphics Lifecycle**:
   - All overlay elements must use `MapView.AddOverlay(CIMGraphic)`.
   - Store all returned `IDisposable` handles in an isolated collection.
   - Cleanly dispose of all handles on deactivation, selection change, layer switch, or prior to redraws to guarantee zero memory leaks.
5. **Thread Safety & Exception Isolation**:
   - Symbol creation (`SymbolFactory`, `ColorFactory`), geometry construction (`GeometryBuilderEx`, `PolylineBuilderEx`), and `MapView.AddOverlay` **MUST** execute inside `QueuedTask.Run` (MCT thread).
   - UI updates (WPF bindings, DockPane layer collections) **MUST** execute on the UI Dispatcher thread.
   - Wrap all event handlers in `try/catch` blocks so no unhandled exceptions ever propagate to ArcGIS Pro.

---

## 4. Detailed Functional Requirements

### A. Default Tool State & Central Synchronization
- **Default State**: The Add-in **MUST** be **OFF** by default on startup.
- **Initialization**: When ArcGIS Pro loads, the engine must not start active dimension rendering until explicitly enabled.
- **Single State of Truth**: The entire Add-in relies on a single central state property: `DimensionSettings.IsEnabled`.
- **Bidirectional Sync**:
  - Clicking the Ribbon toggle button (`DimensionToggleButton`) toggles `Settings.IsEnabled` and updates button text (`GeoMetrics (ON)` / `GeoMetrics (OFF)`).
  - Toggling the CheckBox inside the Settings Dock Pane updates `Settings.IsEnabled` and immediately synchronizes the Ribbon button.

### B. Service Layer Support & Exclusion Logic
- **Definition of Service Layer**:
  - Backed by remote HTTP/HTTPS web services: `FeatureServer`, `MapServer`, Hosted Feature Layers, ArcGIS Online / Enterprise Portal feature services, `WFS`, `WMS`, `OGC`.
  - Local layers (File Geodatabase `.gdb`, Mobile Geodatabase `.geodatabase`, Shapefiles, direct Enterprise SDE direct connect) are **NOT** service layers.
- **Configuration Option**: `Apply to Service Layers` (`bool`, Default: **OFF**).
- **Processing Logic**:
  1. If `IsEnabled == false`: Do nothing.
  2. If `IsEnabled == true` and layer is local: Process normally.
  3. If `IsEnabled == true` and layer is a service layer and `ApplyToServiceLayers == true`: Process normally.
  4. If `IsEnabled == true` and layer is a service layer and `ApplyToServiceLayers == false`:
     - Do NOT process live dimensions.
     - Clear existing overlays.
     - Display a non-intrusive notification:
       > *"Service layers are excluded. Enable 'Apply to Service Layers' in Settings to use GeoMetrics with this layer."*

### C. Units & Formatting Rules
- **Default Display Unit**: **Meter (`Meters`)**.
- **Supported Units**: `Meters` (`m` / `m²`), `Feet` (`ft` / `ft²`), `US_Survey_Feet` (`ft` / `ft²`), `Kilometers` (`km` / `km²`), `Miles` (`mi` / `mi²`), `LayerNative` (CRS units).
- **Area Formatting**: **MUST** always append squared notation (`m²`, `ft²`, `km²`, `mi²`). Never display `m` for area.
- **Perimeter Formatting**: **MUST** use linear notation (`m`, `ft`, `km`, `mi`). Default state: **Unchecked / OFF**.
- **Numbers_Only Style (Default Style)**:
  - Renders clean **`NUMBER + UNIT`** (e.g. `35.42 m`, `1250.52 m²`, `145.63 m`, `90.0°`).
  - Must **NOT** contain redundant field name prefixes (e.g. Do NOT output `Length: 35.42 m` or `Area: 1250.52 m²`).

### D. Vertex Angles Calculation
- **Calculation**: Computes corner angle between consecutive incoming and outgoing segments at each polygon/polyline vertex using the dot product:
  $$\cos\theta = \frac{\vec{v}_1 \cdot \vec{v}_2}{\|\vec{v}_1\| \|\vec{v}_2\|}, \quad \theta = \arccos(\cos\theta) \times \frac{180^\circ}{\pi}$$
- **Straight Line / 180° Exclusion**: If the angle is close to straight/collinear ($|\theta - 180^\circ| < 1.0^\circ$ or $\theta < 0.5^\circ$), it is **omitted** to eliminate visual clutter.
- **Default State**: **Unchecked / OFF** (`ShowVertexAngles = false`).
- **Placement**: Offset dynamically along the interior angle bisector vector away from the vertex node.

### E. Coordinate Systems & Geodesic Calculation
- **Resolution**:
  - `Automatic`: If `SpatialReference.IsGeographic == true` (e.g. WGS84) → Geodesic measurement (`GeometryEngine.GeodesicLength`, `GeometryEngine.GeodesicArea`). If Projected CRS → Planar measurement (`GeometryEngine.Length`, `GeometryEngine.Area`).
  - `Planar`: Always uses linear CRS planar units.
  - `Geodesic`: Always uses geodesic curvature math.
- **Rule**: Always use the **geometry's own SpatialReference**, never the Map's spatial reference.

### F. Styling & CAD Rendering
- **Styles**:
  - `Numbers_Only` (Default): Clean floating text at offset midpoints.
  - `CAD_Standard`: Dimension line parallel to segment + perpendicular extension lines + $45^\circ$ diagonal slash ticks + dimension text.
  - `Minimal`: Dimension line and ticks without extension lines.
  - `High_Contrast`: High contrast cyan lines with black/white halos.
- **Colors**: Black, Blue, Red, Green, Orange, White, Yellow, Cyan. Text symbols must include a matching high-contrast halo (2.2 pt) so text is readable over any basemap.

### G. Multi-Feature Measurements & Decluttering
- **Multi-Feature Support**: Allows measuring all selected features simultaneously when `MultiFeatureEnabled == true`.
- **Feature Limit Guard**: Configurable limit (`MaxFeaturesCount`, default `50`, range `10`–`200`) to prevent performance drops with large selection sets.
- **Inside Placement (`PlaceDimensionsInside`)**: Inverts segment offset normal vectors to keep label annotations strictly inside polygon boundaries, preventing text collisions between adjacent lots/parcels.
- **Viewport Decluttering & HUD**:
  - Segment and angle labels are dynamically culled if edge length in screen pixels falls below readability thresholds.
  - Optional `ShowVisibleCountHud`: Displays live on-screen overlay of visible vertices and segments count.
  - Optional `ShowHiddenWarning`: Displays warning when items are suppressed due to map zoom level.

### H. Dynamic UI Theme Adaptation (Light & Dark Themes)
- **Contrast Requirement**:
  - **Light Theme**: All text in the Settings DockPane and dialogs must render in crisp **Black** (`#000000`) for complete readability against light backgrounds.
  - **Dark Theme**: Text renders in native **White** (`#FFFFFF` / `#DCDCDC`) with native dark contrast.
- **Architectural Implementation**:
  - `DimensionSettingsPaneView.xaml`: Defines implicit styles for `GroupBox`, `CheckBox`, `RadioButton`, and `TextBlock` bound to `Esri_TextPrimaryBrush` / `Esri_TextSecondaryBrush`.
  - `DimensionSettingsPaneView.xaml.cs`: Queries `FrameworkApplication.ApplicationTheme`. If `Default` (Light Theme), swaps dynamic brush resources to pure black solid color brushes. Hooked into `Loaded` and `IsVisibleChanged` lifecycle events to maintain high contrast dynamically.

### I. Visual Branding & Icon Architecture
- **Main Tool & Add-In Brand Logo**:
  - Official high-resolution icon assets: `GeoMetricsLogo_16.png`, `GeoMetricsLogo_32.png`, `GeoMetricsLogo_64.png`.
  - **Add-In Manager (`AddInInfo`)**: Points directly to `<Image>Images\GeoMetricsLogo_32.png</Image>` to ensure immediate professional representation in the ArcGIS Pro Add-In Manager.
- **Dynamic Ribbon Button Behavior**:
  - `GeoMetrics_ToggleDimensions` declares `smallImage="Images\GeoMetricsLogo_16.png"` and `largeImage="Images\GeoMetricsLogo_32.png"` in `Config.daml`.
  - In `DimensionToggleButton.cs`: Default/OFF appearance renders the official **GeoMetrics Logo** (`GeoMetricsLogo_32.png`), and active state renders the vibrant emerald **Toggle ON** icon (`GeoMetricsToggle_ON_32.png`).
- **Settings Icon**:
  - `ShowSettingsButton` declares and dynamically loads `GeoMetricsSettings_32.png` / `16.png`.
- **Packaging Guarantees**:
  - MSBuild target `PackageAddInCustom` in `GeoMetrics.csproj` copies all `Images\**\*.*` to the root `Images/` folder of the staging directory as well as to `Install/Images/`. This guarantees ArcGIS Pro's DAML engine can resolve icon paths from the package root prior to assembly activation.

---

## 5. File Layout & Code Blueprint

```
GeoMetrics/
├── Config.daml                          # Module registration, Ribbon Tab, Group, Buttons, DockPane
├── Module1.cs                           # Add-in Module entry point, Settings singleton, lifecycle
├── GeoMetrics.csproj                    # SDK references & custom .esriAddinX packager target
│
├── Core/
│   ├── DimensionEngine.cs               # Central engine, frame coalescing, service checks, overlay updates
│   └── EditingMonitor.cs                # Subscribes to SketchModified, Camera, Selection events
│
├── Measurement/
│   ├── GeometryMeasurementService.cs    # Planar/geodesic math, segment builder, vertex angle calculation
│   ├── PolygonMeasurementResult.cs      # Measurement result snapshots (Polygon, Polyline, VertexAngle)
│   └── SegmentMeasurement.cs            # Individual segment data (start, end, length, bearing)
│
├── Models/
│   ├── DimensionSettings.cs             # INotifyPropertyChanged configuration model
│   ├── DimensionItem.cs                 # Viewport graphic item model (role, value, anchors, priority)
│   ├── DimensionResult.cs               # Generic result data container
│   └── CachedGeometryMeasurements.cs    # Map-coordinate cache for viewport transforms
│
├── Rendering/
│   ├── GeoMetricsOverlayManager.cs      # Viewport-aware layout logic & MapView graphic handle lifecycle
│   ├── DimensionLabelManager.cs         # Geometry math (normals, bisectors, angles, AABB checks)
│   └── DimensionRenderer.cs             # Direct CIM graphic renderer (lines, text, ticks, halos)
│
├── UI/
│   ├── DimensionToggleButton.cs         # Ribbon toggle button (GeoMetrics ON/OFF)
│   ├── ShowSettingsButton.cs            # Ribbon button to open Dimension Settings DockPane
│   ├── DimensionSettingsPaneViewModel.cs# DockPane ViewModel (reactive layer dropdown & settings bindings)
│   ├── DimensionSettingsPaneView.xaml   # WPF Settings Pane UI layout
│   ├── DimensionSettingsPaneView.xaml.cs# Theme color management & dynamic text contrast logic
│   └── EnumEqualsConverter.cs           # IValueConverter for enum radio button bindings
│
└── Utilities/
    ├── LayerHelper.cs                   # Robust detection of service-backed vs local FeatureLayers
    └── UnitConverter.cs                 # Metric/Imperial conversions and string formatters
```

---

## 6. Exact Default Configuration Matrix

```csharp
IsEnabled            = false;                             // Master Tool State: OFF
ApplyToServiceLayers = false;                             // Service Layers: OFF
DisplayUnit          = DisplayUnitOption.Meters;          // Units: Meter (m / m²)
DimensionStyle       = DimensionStyleOption.Numbers_Only; // Style: Numbers_Only
ShowSegmentLength    = true;                              // Segment Lengths: ON
ShowArea             = true;                              // Polygon Area: ON
ShowPerimeter        = false;                             // Perimeter: OFF
ShowVertexAngles     = false;                             // Vertex Angles: OFF
ShowVertexCoordinates= false;                             // Vertex Coordinates: OFF
ShowBearings         = false;                             // Bearings: OFF
ShowAreaDifference   = false;                             // QC Area Difference: OFF
ShowToleranceStatus  = false;                             // QC Tolerance: OFF
MultiFeatureEnabled  = false;                             // Multi-Feature: OFF
MaxFeaturesCount     = 50;                                // Max Features: 50
PlaceDimensionsInside= false;                             // Place Inside: OFF
ShowVisibleCountHud  = false;                             // Visible HUD: OFF
ShowHiddenWarning    = false;                             // Hidden Warning: OFF
Method               = MeasurementMethod.Automatic;       // Method: Automatic
Precision            = 2;                                 // Decimal Places: 2
OffsetPixels         = 14.0;                              // Label Offset: 14 px
TextColor            = TextColorOption.Black;             // Text Color: Black
```

---

## 7. Diagnostics & Logging Specification

Diagnostic messages must be logged to `System.Diagnostics.Trace` using exact formatted tags:
```
[DIM] Overlay Enabled = true/false
[DIM] ApplyToServiceLayers = true/false
[DIM] Target Layer = <LayerName>
[DIM] IsServiceLayer = true/false
[DIM] Service Layer Processing = Enabled/Disabled
[DIM] Display Unit = Meters
[DIM] Style = Numbers_Only
[DIM] Formatted Segment = 35.42 m
[DIM] Formatted Area = 1250.52 m²
[DIM] Formatted Perimeter = 145.63 m
```

---

## 8. Build & Packaging Instructions

1. **Compile with MSBuild**:
   ```powershell
   msbuild GeoMetrics.csproj /p:Configuration=Release /v:m
   ```
2. **Esri-Compliant Package Structure**:
   - `GeoMetrics.esriAddinX` is a standard ZIP package structured as:
     - `Config.daml` (at archive root).
     - `Install/GeoMetrics.dll` + dependencies + images.
3. **Deployment**: Double-click `.esriAddinX` on any machine with ArcGIS Pro 3.3.x+ installed.
