# GeoMetrics — Complete User Guide | دليل المستخدم الشامل

---

## 📖 Table of Contents | فهرس المحتويات

1. [Introduction | مقدمة عن الأداة](#1-introduction--مقدمة-عن-الأداة)
2. [Key Features | أهم المميزات](#2-key-features--أهم-المميزات)
3. [System Requirements & Installation | متطلبات التشغيل والتثبيت](#3-system-requirements--installation--متطلبات-التشغيل-والتثبيت)
4. [User Interface Overview | جولة في واجهة المستخدم](#4-user-interface-overview--جولة-في-واجهة-المستخدم)
5. [Settings Reference | دليل وشرح الإعدادات](#5-settings-reference--دليل-وشرح-الإعدادات)
6. [Step-by-Step Workflows | خطوات وسيناريوهات العمل](#6-step-by-step-workflows--خطوات-وسيناريوهات-العمل)
7. [Coordinate Systems & Geodesy | أنظمة الإحداثيات والقياس الجيوديسي](#7-coordinate-systems--geodesy--أنظمة-الإحداثيات-والقياس-الجيوديسي)
8. [Troubleshooting & FAQ | الأسئلة الشائعة وحل المشكلات](#8-troubleshooting--faq--الأسئلة-الشائعة-وحل-المشكلات)

---

## 1. Introduction | مقدمة عن الأداة

**GeoMetrics** is a high-performance, real-time geometry measurement and CAD overlay Add-in for **ArcGIS Pro (3.3.x / 3.4.x)** built with .NET 8.

It operates as a **passive real-time monitor** during standard ArcGIS Pro workflows (such as **Edit → Modify → Edit Vertices** or **Feature Selection**). As you drag vertices or select features, GeoMetrics calculates and draws segment lengths, polygon area, perimeter, vertex corner angles, vertex coordinates (X, Y), and bearings directly onto the MapView at 60 FPS without creating feature locks, modifying feature classes, or requiring manual tool execution.

**GeoMetrics** هي إضافة احترافية لبرنامج **ArcGIS Pro** تعمل في الوقت الفعلي (Real-Time 60 FPS) لعرض أبعاد الأضلاع، مساحات المضلعات، المحيطات، زوايا الأركان، وإحداثيات النقاط (X, Y) مباشرة فوق الخريطة أثناء التعديل أو التحديد، وبشكل سلبي تماماً دون التأثير على سير العمل أو إغلاق الطبقات.

---

## 2. Key Features | أهم المميزات

- ⚡ **Real-Time 60 FPS Performance**: Frame-coalesced rendering ensures butter-smooth vertex dragging with zero UI lag.
- 🎯 **Default OFF State**: Add-in starts safely disabled by default and only operates when you turn it ON.
- 👥 **Multi-Feature Measurements**: Simultaneously measure and number multiple selected polygons/polylines with configurable safety limits (10 to 200 features).
- 🔲 **Place Dimensions Inside**: Invert offset directions to keep polygon segment labels strictly inside boundary edges, preventing collisions between adjacent parcels.
- 📍 **Vertex Coordinates (X, Y)**: Live coordinate display for every vertex with configurable decimal places (0 to 8, default 4).
- 📐 **Corner Angles & Bearings**: Automatic calculation of internal/corner angles and segment direction azimuths.
- 📊 **Live HUD & Warnings**: On-screen real-time counters for visible vertices/segments and decluttering warnings when zoomed out.
- 🔍 **Adaptive Viewport Layout (Maplex-Style)**: Labels dynamically reposition when you zoom in on parts of large polygons.
- 🌓 **Theme-Aware UI**: Full native adaptation for ArcGIS Pro Dark and Light themes with dynamic text color contrast (Black on Light Theme, White on Dark Theme).
- 🛡️ **Service Layer Protection**: Automatically excludes slow remote Feature/Map Services unless explicitly enabled.
- 📏 **Comprehensive Unit Support**: Meters, Feet, US Survey Feet, Kilometers, Miles, and Native CRS linear units.

---

## 3. System Requirements & Installation | متطلبات التشغيل والتثبيت

### System Requirements
- **Operating System**: Windows 10 / 11 (64-bit).
- **Host Application**: ArcGIS Pro 3.3.x, 3.4.x (or newer).
- **Runtime**: Microsoft .NET Desktop Runtime 8.0 (x64).

### 1-Click Installation (تثبيت بنقرة واحدة)
1. Close any running instances of **ArcGIS Pro**.
2. Navigate to the project folder:
   ```
   d:\Learning\Realtime Dim Area\Addin_Package\
   ```
3. Double-click [**`GeoMetrics.esriAddinX`**](file:///d:/Learning/Realtime%20Dim%20Area/Addin_Package/GeoMetrics.esriAddinX).
4. In the Esri Add-In Installation Wizard, click **Install Add-In**.
5. Launch **ArcGIS Pro**. The **GeoMetrics** tab will appear on your top ribbon.

---

## 4. User Interface Overview | جولة في واجهة المستخدم

```
┌────────────────────────────────────────────────────────────────────────┐
│                              RIBBON BAR                                │
│ [Tab: GeoMetrics]                                                      │
│  ├── [🔘 GeoMetrics (ON/OFF)] -> Master toggle switch with live icon   │
│  └── [⚙️ Settings]            -> Opens the GeoMetrics Settings Pane    │
└────────────────────────────────────────────────────────────────────────┘
```

### 1. Ribbon Tab: `GeoMetrics`
- **GeoMetrics (ON / OFF) Button**:
  - **Red / OFF**: GeoMetrics is inactive. No events are monitored, and no graphics are drawn.
  - **Green / ON**: GeoMetrics is actively monitoring edits and selections, updating overlays live.
- **Settings Button**:
  - Opens the dedicated **GeoMetrics Settings** dockpane on the right side of the screen.

### 2. GeoMetrics Settings DockPane

```
┌──────────────────────────────────────────────────────────┐
│ GeoMetrics Settings                                  ✕  │
├──────────────────────────────────────────────────────────┤
│ ┌─ Tool State (ON / OFF) ──────────────────────────────┐ │
│ │  ☑ Enable GeoMetrics (ON / OFF)                      │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌─ Selection & Multi-Feature ──────────────────────────┐ │
│ │  ☐ Multi-feature measurements   [Max Features: 50 ▼] │ │
│ │  ☐ Place dimensions inside boundary                  │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌─ Target Layer ───────────────────────────────────────┐ │
│ │  [ Auto-detect from selection                      ▼] │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌─ Service Layers ─────────────────────────────────────┐ │
│ │  ☐ Apply to Service Layers                           │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌─ Show / Hide ────────────────────────────────────────┐ │
│ │  ☑ Segment lengths                                   │ │
│ │  ☑ Polygon area                                      │ │
│ │  ☐ Perimeter                                         │ │
│ │  ☐ Vertex angles (between segments)                  │ │
│ │  ☐ Vertex coordinates (X, Y)   [Decimals: 4 ▼]      │ │
│ │  ☐ Area difference (before & after edit)             │ │
│ │  ☐ Visible count HUD (on map)                        │ │
│ │  ☐ Hidden dimensions warning                         │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌─ Measurement Method ─────────────────────────────────┐ │
│ │  🔘 Automatic   ○ Planar   ○ Geodesic                │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌─ Display Units ──────────────────────────────────────┐ │
│ │  [ Meters (m)                                      ▼] │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌─ Precision & Appearance ─────────────────────────────┐ │
│ │  Decimal places:  ──●── [ 2 ]                        │ │
│ │  Label offset:    ────● [ 14 px ]                    │ │
│ │  Style:           [ Numbers Only (Clean Text)      ▼] │ │
│ │  Text Color:      [ Black / White                  ▼] │ │
│ └──────────────────────────────────────────────────────┘ │
│ ┌─ Live Status (read-only) ────────────────────────────┐ │
│ │  CRS:    WGS 1984 UTM Zone 36N                       │ │
│ │  WKID:   32636                                       │ │
│ │  Method: Planar                                      │ │
│ └──────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────┘
```

> [!NOTE]
> **Theme Adaptation**: The Settings DockPane UI dynamically adjusts its text elements based on ArcGIS Pro's theme — rendering high-contrast **Black text in Light Theme** and clean **White text in Dark Theme**.

---

## 5. Settings Reference | دليل وشرح الإعدادات

| Setting | Type | Default | Description | الوصف بالعربية |
|---|---|---|---|---|
| **Enable GeoMetrics** | CheckBox | `OFF` | Master switch controlling real-time dimension monitoring. | المفتاح الرئيسي لتشغيل أو إيقاف الأداة. |
| **Multi-Feature Measurements** | CheckBox | `OFF` | Simultaneously measures and numbers all selected features. | قياس وترقيم جميع المعالم المحددة في وقت واحد. |
| **Max Features Limit** | DropDown | `50` | Maximum number of selected features to measure (`10`, `25`, `50`, `100`, `200`). | الحد الأقصى لعدد المعالم المقاسة معاً لمنع بطء الأداء. |
| **Place Dimensions Inside** | CheckBox | `OFF` | Inverts offset vectors to position segment labels inside polygon boundaries. | رسم أبعاد المضلعات للداخل لتفادي تداخل النصوص بين المعالم المتجاورة. |
| **Target Layer** | DropDown | `Auto-detect` | Pins the tool to a specific layer, or auto-detects from the active selection. | حصر القياسات في طبقة محددة أو الاكتشاف التلقائي. |
| **Apply to Service Layers** | CheckBox | `OFF` | Enables dimensions on remote ArcGIS Server / AGOL / Portal feature services. | السماح بالعمل على طبقات الويب والخدمات السحابية. |
| **Segment Lengths** | CheckBox | `ON` | Displays measured lengths along boundary edges. | إظهار أطوال الأضلاع. |
| **Polygon Area** | CheckBox | `ON` | Displays total polygon area in the interior label point. | إظهار مساحة المضلع مع الوحدة المربعة (`m²`, `ft²`). |
| **Perimeter** | CheckBox | `OFF` | Displays total perimeter length (`P: 145.63 m`). | إظهار المحيط الكلي للمضلع. |
| **Vertex Angles** | CheckBox | `OFF` | Displays measured angles at corners (filters out straight angles $\approx 180^\circ$). | إظهار الزوايا بين الأضلاع عند الأركان. |
| **Vertex Coordinates** | CheckBox | `OFF` | Displays live X and Y coordinates at each vertex point. | إظهار إحداثيات النقاط والأركان (X, Y) لحظياً. |
| **Decimals (Coordinates)** | DropDown | `4` | Number of decimal digits for X, Y coordinates (0 to 8). | عدد الخانات العشرية المعروضة لإحداثيات X و Y. |
| **Area Difference** | CheckBox | `OFF` | Displays polygon area before editing, during editing, and the net difference ($\Delta$). | إظهار ومقارنة مساحة المضلع قبل التعديل وأثناء التعديل وفارق التغير. |
| **Visible Count HUD** | CheckBox | `OFF` | Displays a live on-screen counter of visible vertices and segments (top-left). | عداد حي لعدد النقاط والأضلاع الظاهرة على الشاشة (أعلى اليسار). |
| **Hidden Warning** | CheckBox | `OFF` | Displays a warning overlay when dimensions are hidden due to zoom level (top-right). | تحذير عند إخفاء أبعاد بسبب مستوى التقريب (أعلى اليمين). |
| **Measurement Method** | Radio | `Automatic` | `Automatic` (Geodesic for GCS, Planar for Projected), `Planar`, or `Geodesic`. | طريقة الحساب (تلقائي، مسقط، أو جيوديسي). |
| **Display Units** | DropDown | `Meters` | `Meters`, `Feet`, `US Survey Feet`, `Kilometers`, `Miles`, `Layer Native`. | وحدة القياس والعرض لجميع الأبعاد. |
| **Decimal Places** | Slider | `2` | Number of decimal digits for lengths and areas (0 to 4). | عدد الخانات العشرية لأطوال الأضلاع والمساحة. |
| **Label Offset** | Slider | `14 px` | Distance in screen pixels to offset text from segment lines. | مسافة إزاحة النصوص عن الأضلاع بالبكسل. |
| **Dimension Style** | DropDown | `Numbers_Only` | `Numbers_Only`, `CAD_Standard`, `Minimal`, `High_Contrast`. | نمط الإخراج الرسومي (أرقام فقط، نمط CAD، إلخ). |
| **Text Color** | DropDown | `Black` | `Black`, `Blue`, `Red`, `Green`, `Orange`, `White`, `Yellow`, `Cyan`. | لون الخط المستخدم في كتابة الأبعاد على الخريطة. |
| **UI Theme Text Color** | Auto | `Adaptive` | Automatically switches UI text to **Black in Light Theme** and **White in Dark Theme**. | تكيّف لون نصوص الواجهة تلقائياً: **أسود** في النمط الفاتح و**أبيض** في الداكن. |

---

## 6. Step-by-Step Workflows | خطوات وسيناريوهات العمل

### 🔷 Workflow 1: Live Vertex Dragging (التعديل اللحظي للنقاط)
1. Open your Map in ArcGIS Pro with your polygon or polyline feature layer.
2. On the **GeoMetrics** ribbon tab, click **GeoMetrics** to turn it **ON** (button turns Green).
3. Go to the **Edit** tab on the ribbon $\rightarrow$ click **Modify** $\rightarrow$ select **Edit Vertices**.
4. Click on any polygon or polyline on the map.
5. **Drag any vertex**: All surrounding segment lengths, area, perimeter, and vertex angles update instantly at 60 FPS as your mouse moves.
6. When finished, press **Finish Sketch** (F2) or discard edits.

---

### 🔷 Workflow 2: Instant Feature Selection Dimensions (قياس المعالم المحددة)
1. Ensure GeoMetrics is **ON**.
2. Select any polygon or polyline using the standard **Select** tool on the map.
3. GeoMetrics immediately draws all measurements for the selected feature.
4. Switch selections or click empty space: Overlays instantly update or clear cleanly.

---

### 🔷 Workflow 3: Displaying Parcel Vertex Coordinates (X, Y) (عرض إحداثيات الأركان)
1. Open the **GeoMetrics Settings** pane from the ribbon.
2. In the **Show / Hide** section:
   - Check **Vertex coordinates (X, Y)**.
   - Set **Decimals** to your desired precision (e.g., `4` for survey accuracy, or `6` for degrees).
3. Select or edit any parcel/polygon:
   - Every vertex will display its exact `X:` and `Y:` coordinates outside the polygon boundary without overlapping angle labels or editing handles.

---

### 🔷 Workflow 4: Area Difference (Before & After Edit) (مقارنة المساحة قبل وبعد التعديل)
1. In the **GeoMetrics Settings** pane, enable **Area difference (before & after edit)**.
2. Select or edit any parcel/polygon (Modify $\rightarrow$ Edit Vertices).
3. As you drag vertices or reshape the polygon, the interior label displays:
   ```
   Before:  1,200.00 m²
   Current: 1,225.50 m²
   Δ:       +25.50 m² (+2.1%)
   ```
4. This gives real-time visibility into the exact area gained or lost during boundary adjustments.

### 🔷 Workflow 5: Multi-Feature Measurements & Inside Dimensions (قياس معالم متعددة)
1. Open the **GeoMetrics Settings** pane from the ribbon.
2. In the **Selection & Multi-Feature** section:
   - Check **Multi-feature measurements**.
   - (Optional) Adjust **Max features** (e.g. `50` or `100`).
   - (Optional) Check **Place dimensions inside boundary** to draw segment labels inside polygons, preventing overlaps between neighboring parcels.
3. Use the ArcGIS Pro **Select** tool to select multiple polygons or polylines (or draw a selection rectangle).
4. All selected features will be dimensioned simultaneously, with distinct feature index badges (`#1`, `#2`, etc.) and area labels.

---

## 7. Coordinate Systems & Geodesy | أنظمة الإحداثيات والقياس الجيوديسي

GeoMetrics accurately follows ArcGIS Pro's standard measurement engine:

1. **Geographic Coordinate Systems (GCS e.g. WGS84, EPSG:4326)**:
   - When set to `Automatic`, GeoMetrics performs true **Geodesic** ellipsoidal calculations on the WGS84 ellipsoid.
   - Segment lengths and areas are computed in true ground meters and converted to your chosen display unit.
   - Text placement uses latitude-aware geodesy ($\cos(\text{latitude})$ scaling) for precise angle and normal vector orientations.

2. **Projected Coordinate Systems (PCS e.g. UTM, State Plane, Egypt Red Belt)**:
   - When set to `Automatic`, GeoMetrics uses high-speed **Planar** Euclidean geometry using the layer's native CRS units.

---

## 8. Troubleshooting & FAQ | الأسئلة الشائعة وحل المشكلات

### Q1: The Settings Pane appears blank or does not open.
- **Solution**: Ensure you have installed the latest [`Addin_Package/GeoMetrics.esriAddinX`](file:///d:/Learning/Realtime%20Dim%20Area/Addin_Package/GeoMetrics.esriAddinX). Close ArcGIS Pro, double-click the `.esriAddinX` package to reinstall, and reopen ArcGIS Pro.

### Q2: Dimensions are not appearing when I select a feature.
- **Check 1**: Make sure the **GeoMetrics (ON / OFF)** master switch on the ribbon is **ON** (Green).
- **Check 2**: If the layer is a web service (FeatureServer / MapServer), check **Apply to Service Layers** in the Settings dockpane.
- **Check 3**: Ensure the layer is a Polygon or Polyline layer. Point layers are not dimensioned.

### Q3: How does text color behave in ArcGIS Pro Light and Dark themes?
- **Settings DockPane UI Text**: Automatically adapts to your active ArcGIS Pro theme. In **Light Theme**, all UI labels and control text render in crisp **Black** (`#000000`). In **Dark Theme**, all UI text renders in **White** (`#FFFFFF`) with native contrast. No manual action is needed.
- **Map Overlay Text**: Customizable via **GeoMetrics Settings** $\rightarrow$ **Precision & Appearance** $\rightarrow$ **Text Color** (e.g. choose `Black` or `Dark Blue` for light basemaps, or `White`, `Yellow`, or `Cyan` for dark satellite/imagery basemaps).

### Q4: Does GeoMetrics modify my feature class data or attribute tables?
- **No**. GeoMetrics is strictly an overlay visualization tool. It utilizes temporary `MapView.AddOverlay` CIM graphics stored in ephemeral graphics memory and never modifies your shapefiles, enterprise geodatabases, or attribute tables.

---

*GeoMetrics Add-in — Developed by Al-Qotr Co. & Aboelmagd for ArcGIS Pro 3.3+.*
