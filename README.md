# GeoMetrics — ArcGIS Pro Add-in

[**📖 Full User Guide / دليل المستخدم الشامل**](file:///d:/Learning/Realtime%20Dim%20Area/USER_GUIDE.md) | [**English**](#english-documentation) | [**العربية**](#التوثيق-باللغة-العربية)

---

# English Documentation

## Real-Time Geometry Measurements for ArcGIS Pro

**GeoMetrics** is a high-performance, real-time CAD-style geometry measurement Add-in for **ArcGIS Pro 3.3.x / 3.4.x** built with C# and the **ArcGIS Pro SDK for .NET (.NET 8)**.

It operates passively on top of ArcGIS Pro's native **Edit → Modify** workflow (e.g. *Edit Vertices*, *Reshape*, *Move*). As you drag vertices or select features, segment lengths, polygon areas, perimeters, and vertex angles are rendered dynamically on the map viewport at 60 FPS without modifying the underlying geodatabase or interfering with native tools.

---

## 🌟 Key Features

- **Passive & Non-Intrusive**: Works seamlessly over native ArcGIS Pro editing tools without replacing or locking editing workflows.
- **Default State OFF**: Initializes in a deactivated state (`OFF`). No monitoring or overlays run until the user explicitly toggles it on.
- **Central State Synchronization**: Single source of truth (`DimensionSettings.IsEnabled`). Toggling the Ribbon button instantly synchronizes the Settings Dock Pane and vice versa.
- **Real-Time Vertex Dragging (60 FPS)**: Ultra-fast lock-free frame coalescing recalculates and renders dimensions smoothly while dragging vertices.
- **Dynamic Maplex-Style Positioning**: Labels automatically adjust during zoom and pan. When zoomed into a large parcel, labels remain anchored to the visible portion of the segment and interior area.
- **Comprehensive Measurements**:
  - **Segment Lengths**: Accurate planar or geodesic linear dimensions.
  - **Polygon Area**: Formatted with proper squared units (`m²`, `ft²`, `km²`, `mi²`).
  - **Perimeter**: Formatted with linear units (`m`, `ft`, `km`, `mi`). Default: *Unchecked*.
  - **Vertex Angles**: Measures angles between consecutive segments at vertices. Angles close to 180° (straight lines within ±1.0°) are automatically filtered out. Default: *Unchecked*.
  - **Bearings / Azimuth**: Segment direction angles.
  - **Quality Control (QC)**: Area difference and tolerance checking against pre-edit geometry.
- **Service Layer Support & Safety**:
  - Automatically identifies remote service-backed layers (FeatureServer, MapServer, Hosted Feature Layers, ArcGIS Online / Enterprise Portal services, WFS, WMS).
  - Distinguishes local data sources (File Geodatabase `.gdb`, Mobile Geodatabase `.geodatabase`, Shapefiles, direct Enterprise SDE).
  - **Apply to Service Layers** toggle (Default: *OFF*). If a service layer is selected while disabled, an informative notification is displayed without error.
- **Default Configuration**:
  - Default Display Unit: **Meter (`m` / `m²`)**.
  - Default Style: **Numbers_Only** (displays clean `NUMBER + UNIT` such as `35.42 m` and `1250.52 m²` without redundant word prefixes like `Length:` or `Area:`).

---

## 📐 Project Architecture

```
GeoMetrics/
├── Config.daml                          # ArcGIS Pro Add-in manifest (Ribbon, Buttons, DockPane)
├── Module1.cs                           # Module entry point & singleton lifecycle manager
├── GeoMetrics.csproj                    # .NET 8 Windows project file
│
├── Core/
│   ├── DimensionEngine.cs               # Central engine, frame coalescing, service layer checks
│   └── EditingMonitor.cs                # SDK event monitor (SketchModified, Camera, Selection)
│
├── Measurement/
│   ├── GeometryMeasurementService.cs    # Planar/geodesic calculations & vertex angle algorithms
│   ├── PolygonMeasurementResult.cs      # Measurement snapshot models
│   └── SegmentMeasurement.cs            # Individual segment metrics (length, bearing)
│
├── Models/
│   ├── DimensionSettings.cs             # Reactive configuration model (INotifyPropertyChanged)
│   ├── DimensionItem.cs                 # Viewport-aware graphic model & priority hierarchy
│   ├── DimensionResult.cs               # Measurement computation data
│   └── CachedGeometryMeasurements.cs    # Map-coordinate measurement cache
│
├── Rendering/
│   ├── GeoMetricsOverlayManager.cs      # Viewport layout calculation & MapView overlay lifecycle
│   ├── DimensionLabelManager.cs         # Geometry math, outward normals, angles, and AABB checks
│   └── DimensionRenderer.cs             # CIM graphics builder (symbols, text, ticks, halos)
│
├── UI/
│   ├── DimensionToggleButton.cs         # Ribbon ON/OFF toggle button
│   ├── ShowSettingsButton.cs            # Ribbon button to activate Settings Dock Pane
│   ├── DimensionSettingsPaneViewModel.cs# Dock Pane ViewModel & reactive layer collection
│   ├── DimensionSettingsPaneView.xaml   # Settings UI WPF layout
│   └── EnumEqualsConverter.cs           # WPF RadioButton enum binding converter
│
└── Utilities/
    ├── LayerHelper.cs                   # Service layer vs local geodatabase detection
    └── UnitConverter.cs                 # Conversion math and string formatters
```

---

## ⚙️ Settings Reference

| Option | Type | Default | Description |
|---|---|---|---|
| **GeoMetrics** | Toggle | `OFF` | Master ON/OFF switch. Synchronized across Ribbon and Dock Pane. |
| **Target Layer** | Dropdown | `Auto-detect` | Pins the tool to a specific layer or auto-detects from selection. |
| **Apply to Service Layers** | CheckBox | `OFF` | Allows dimensions on remote Feature Services / Map Services. |
| **Segment Lengths** | CheckBox | `ON` | Displays dimensions along each segment. |
| **Polygon Area** | CheckBox | `ON` | Displays total area in interior label position (`m²`, `ft²`, etc.). |
| **Perimeter** | CheckBox | `OFF` | Displays total boundary length (`m`, `ft`, etc.). |
| **Vertex Angles** | CheckBox | `OFF` | Displays corner angles between segments (filters out angles $\approx 180^\circ$). |
| **Vertex Coordinates** | CheckBox | `OFF` | Displays X and Y coordinates at each vertex in real-time (with Decimals selector 0-8). |
| **Area Difference** | CheckBox | `OFF` | Displays polygon area before editing, during editing, and net change ($\Delta$). |
| **Measurement Method** | Radio | `Automatic` | Automatic (Geodesic for geographic CRS, Planar for projected CRS), Planar, Geodesic. |
| **Display Units** | Dropdown | `Meters` | Meters, Feet, US Survey Feet, Kilometers, Miles, Layer Native CRS. |
| **Dimension Style** | Dropdown | `Numbers_Only` | `Numbers_Only`, `CAD_Standard`, `Minimal`, `High_Contrast`. |
| **Decimal Places** | Slider | `2` | Number of decimal places (0 to 4). |
| **Label Offset** | Slider | `14 px` | Perpendicular offset distance from segments. |

---

## 🛠️ Build & Installation

### Requirements
- **Windows 10 / 11 (x64)**
- **ArcGIS Pro 3.3.x or 3.4.x**
- **Visual Studio 2022** with .NET 8 Desktop Development & ArcGIS Pro SDK for .NET

### Building the Add-in
Run MSBuild from the Developer Command Prompt or Visual Studio:

```powershell
# Build Debug
msbuild GeoMetrics.csproj /p:Configuration=Debug

# Build Release
msbuild GeoMetrics.csproj /p:Configuration=Release
```

The output `.esriAddinX` package is generated in:
```
Addin_Package\GeoMetrics.esriAddinX
```

### Installation
1. Locate the pre-built Add-in in the `Addin_Package/` folder:
   - Double-click [`Addin_Package/GeoMetrics.esriAddinX`](file:///d:/Learning/Realtime%20Dim%20Area/Addin_Package/GeoMetrics.esriAddinX).
2. Click **Install Add-In** in the Esri Add-in Installation Utility.
3. Open ArcGIS Pro. The **GeoMetrics** tab appears on the Ribbon.

---
---

# التوثيق باللغة العربية

**GeoMetrics** هي إضافة (Add-in) احترافية وتفاعلية لبرنامج **ArcGIS Pro 3.3.x / 3.4.x** تم بناؤها بلغة C# باستخدام **ArcGIS Pro SDK for .NET (.NET 8)**.

تعمل الأداة بنظام المراقبة السلبية (Passive Real-Time Monitor) أثناء استخدام أدوات التعديل القياسية في ArcGIS Pro مثل (**Edit → Modify → Edit Vertices**). بمجرد سحب أي نقطة (Vertex) أو تحديد معلم، تظهر أبعاد الأضلاع، المساحة، المحيط، وزوايا الأركان مباشرة فوق الخريطة بشكل لحظي (60 FPS) وبدون الحاجة لحفظ التعديل أو تشغيل أي أوامر إضافية.

---

## 🌟 أبرز المميزات

1. **العمل التلقائي والحي (Real-Time 60 FPS)**:
   - تحديث مستمر ولحظي لأبعاد الأضلاع والمساحة أثناء سحب الـ Vertex بالماوس.
   - استخدام تقنية تجميع الإطارات (Frame Coalescing) لمنع أي بطء أو تجميد في واجهة البرنامج.

2. **الحالة الافتراضية معطلة (Default OFF)**:
   - الأداة تبدأ دائماً بحالة `OFF`. لا تبدأ بمراقبة الأحداث أو رسم الأبعاد إلا بعد قيام المستخدم بتفعيلها يدوياً.

3. **مزامنة مركزية موحدة (Central State Sync)**:
   - حالة تفعيل واحدة (`IsEnabled`) مشتركة بين زر الشريط العلوي (Ribbon) ولوحة الإعدادات (Dock Pane).

4. **التموضع الديناميكي الذكي (Maplex-Style Viewport Layout)**:
   - إعادة تموضع النصوص تلقائياً مع عمليات التكبير والتصغير (Zoom) والتحريك (Pan).
   - عند التقريب على جزء من مضلع كبير، يتم تثبيت الأبعاد على منتصف الجزء الظاهر من الضلع داخل الشاشة، ونقل نص المساحة لداخل الجزء المعروض.

5. **القياسات المتكاملة**:
   - **أطوال الأضلاع (Segment Lengths)**: قياس دقيق مسقط (Planar) أو جيوديسي (Geodesic).
   - **مساحة المضلع (Polygon Area)**: مع رمز الوحدة المربعة الصحيحة دائماً (`m²`, `ft²`, `km²`).
   - **المحيط (Perimeter)**: مع رمز الوحدة الطولية (`m`, `ft`). القيمة الافتراضية: *غير مفعّل*.
   - **زوايا الأركان (Vertex Angles)**: حساب الزوايا بين الأضلاع المتتالية، مع استبعاد الزوايا المستقيمة القريبة من 180° (في نطاق ±1.0°). القيمة الافتراضية: *غير مفعّل*.
   - **الانحرافات (Bearings / Azimuth)**: زوايا اتجاه كل ضلع.
   - **فحص الجودة (QC)**: حساب فرق المساحة ونسبة التسامح مقارنة بالمساحة الأصلية قبل التعديل.

6. **دعم طبقات الخدمات السحابية (Service Layers)**:
   - التمييز الدقيق بين الطبقات المحلية (File Geodatabase `.gdb`, Mobile Geodatabase, Shapefiles, Enterprise SDE direct) وبين طبقات الخدمات عن بعد (FeatureServer, MapServer, Hosted Layers, AGOL/Portal, WFS, WMS).
   - خيار "تطبيق على طبقات الخدمات" (`Apply to Service Layers`) معطل افتراضياً (`OFF`).
   - إشعار توضيحي غير مزعج للمستخدم عند اختيار طبقة خدمة بدون إظهار أخطاء.

7. **الإعدادات الافتراضية القياسية**:
   - الوحدة الافتراضية: **المتر (`Meter`)**.
   - النمط الافتراضي: **Numbers_Only** (يعرض الرقم + الوحدة مثل `35.42 m` و `1250.52 m²` بدون كلمات وصفية مثل `Length:` أو `Area:`).

---

## 🏗️ البنية الهيكلية للمشروع

```
GeoMetrics/
├── Config.daml                          # تعريف الـ Ribbon والـ DockPane والأزرار
├── Module1.cs                           # نقطة دخول الإضافة وإدارة دورة حياة الـ Engine
├── GeoMetrics.csproj                    # ملف المشروع (.NET 8 Windows x64)
│
├── Core/
│   ├── DimensionEngine.cs               # المحرك الرئيسي وإدارة المعالجة وفحص طبقات الخدمات
│   └── EditingMonitor.cs                # الاشتراك في أحداث التعديل والخرائط (Events)
│
├── Measurement/
│   ├── GeometryMeasurementService.cs    # حساب القياسات الجيوديسية والمسقطة وزوايا الأركان
│   ├── PolygonMeasurementResult.cs      # كائنات حفظ نتائج قياس المضلعات والخطوط
│   └── SegmentMeasurement.cs            # قياسات الأضلاع المنفردة (الطول والانحراف)
│
├── Models/
│   ├── DimensionSettings.cs             # كائن الإعدادات المتفاعل (INotifyPropertyChanged)
│   ├── DimensionItem.cs                 # نموذج العنصر الرسومي وترتيب أولويات العرض
│   ├── DimensionResult.cs               # بيانات الحساب اللحظي
│   └── CachedGeometryMeasurements.cs    # تخزين القياسات مؤقتاً بإحداثيات الخريطة
│
├── Rendering/
│   ├── GeoMetricsOverlayManager.cs      # حساب مواضع العرض في نافذة الخريطة وإدارة الرسومات
│   ├── DimensionLabelManager.cs         # الحسابات الرياضية، المتجهات العمودية، والتقاطعات
│   └── DimensionRenderer.cs             # بناء رموز CIM والخطوط والنصوص وعلامات CAD
│
├── UI/
│   ├── DimensionToggleButton.cs         # زر التفعيل/التعطيل في الشريط العلوي (Ribbon)
│   ├── ShowSettingsButton.cs            # زر فتح لوحة الإعدادات
│   ├── DimensionSettingsPaneViewModel.cs# ViewModel للوحة الإعدادات وقائمة الطبقات
│   ├── DimensionSettingsPaneView.xaml   # واجهة WPF للوحة الإعدادات
│   └── EnumEqualsConverter.cs           # محول ربط الـ RadioButtons مع الـ Enums
│
└── Utilities/
    ├── LayerHelper.cs                   # فحص وتمييز طبقات الخدمات عن الطبقات المحلية
    └── UnitConverter.cs                 # التحويلات الرياضية وتنسيق الأرقام والوحدات
```

---

## 📋 جدول الإعدادات والخيارات

| الخيار | النوع | القيمة الافتراضية | الوصف |
|---|---|---|---|
| **GeoMetrics** | تبديل | `OFF` | المفتاح الرئيسي لتشغيل/إيقاف الأداة ومزامنته مع الشريط العلوي. |
| **Target Layer** | قائمة | `تلقائي` | تحديد طبقة معينة أو الاكتشاف التلقائي من التحديد الحالي. |
| **Apply to Service Layers** | اختيار | `OFF` | السماح للأداة بالعمل على طبقات الـ Feature Service و Map Service. |
| **Segment Lengths** | اختيار | `ON` | إظهار أطوال الأضلاع. |
| **Polygon Area** | اختيار | `ON` | إظهار مساحة المضلع (`m²`, `ft²`, إلخ). |
| **Perimeter** | اختيار | `OFF` | إظهار المحيط الكلي للمضلع (`m`, `ft`, إلخ). |
| **Vertex Angles** | اختيار | `OFF` | إظهار الزوايا بين الأضلاع (مع استبعاد الزوايا $\approx 180^\circ$). |
| **Vertex Coordinates** | اختيار | `OFF` | إظهار إحداثيات النقاط والأركان (X, Y) بشكل لحظي مع تحديد الخانات العشرية. |
| **Area Difference** | اختيار | `OFF` | إظهار ومقارنة مساحة المضلع قبل التعديل وأثناء التعديل وفارق التغير ($\Delta$). |
| **Measurement Method** | خيارات | `Automatic` | تلقائي (جيوديسي للجغرافي، مسقط للمسقط)، مسقط، أو جيوديسي. |
| **Display Units** | قائمة | `Meters` | أمتار، أقدام، أقدام مساحية، كيلومترات، أميال، أو وحدات الطبقة. |
| **Dimension Style** | قائمة | `Numbers_Only` | نمط الأرقام فقط، نمط CAD القياسي، نمط مبسط، أو عالي التباين. |
| **Decimal Places** | شريط | `2` | عدد الخانات العشرية المعروضة (0 إلى 4). |
| **Label Offset** | شريط | `14 px` | مسافة إزاحة النص عن الضلع بالبكسل. |

---

## 🔨 البناء والتثبيت (Build & Install)

### المتطلبات
- نظام **Windows 10 / 11 (x64)**.
- برنامج **ArcGIS Pro 3.3.x أو 3.4.x**.
- بيئة **Visual Studio 2022** مع .NET 8 وحزمة ArcGIS Pro SDK for .NET.

### خطوات البناء عبر السطر البرمجي (MSBuild)
من موجه أوامر المطور (Developer Command Prompt):

```powershell
# بناء نسخة Release
msbuild GeoMetrics.csproj /p:Configuration=Release
```

يتم توليد ملف الإضافة الجاهز في المسار:
```
Addin_Package\GeoMetrics.esriAddinX
```

### التثبيت والتشغيل
1. افتح مجلد `Addin_Package/` وانقر نقراً مزدوجاً على ملف [`GeoMetrics.esriAddinX`](file:///d:/Learning/Realtime%20Dim%20Area/Addin_Package/GeoMetrics.esriAddinX).
2. اضغط على **Install Add-In** في نافذة التثبيت التلقائية لـ Esri.
3. افتح ArcGIS Pro ستجد تبويب **GeoMetrics** جاهزاً في الشريط العلوي.
