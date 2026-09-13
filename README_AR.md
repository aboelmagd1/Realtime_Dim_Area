# أداة GeoMetrics — إضافة (Add-in) لبرنامج ArcGIS Pro

[**📖 اضغط هنا لقراءة دليل المستخدم الشامل خطوة بخطوة (User Guide)**](file:///d:/Learning/Realtime%20Dim%20Area/USER_GUIDE.md)

دليل توثيقي شامل لبنية المشروع، ومحرك القياس والرسم اللحظي، وخيارات التخصيص، وخطوات البناء والتثبيت.

---

## 1. الفكرة العامة

**GeoMetrics** هي إضافة احترافية لبرنامج **ArcGIS Pro (3.3.x / 3.4.x)** مبنية بلغة C# على إطار العمل **ArcGIS Pro SDK for .NET (.NET 8)**.

تعمل الأداة بنظام المراقبة السلبية غير التداخلية (Passive Real-Time Monitor) فوق أدوات التعديل القياسية في ArcGIS Pro مثل (**Edit → Modify → Edit Vertices**). 

أثناء قيام المستخدم بسحب أي نقطة (Vertex Dragging) أو تحديد معلم، تُحسب أبعاد الأضلاع والمساحة والمحيط وزوايا الأركان وتُرسم **فوق الخريطة مباشرة بشكل لحظي وبسرعة 60 FPS** بدون الحاجة لحفظ التعديل، وبدون التأثير على أداء البرنامج أو قاعدة البيانات.

---

## 2. أبرز المميزات والخصائص

1. **العمل التلقائي والحي (Real-Time 60 FPS)**:
   - تحديث سلس ومستمر لجميع الأبعاد والمساحات أثناء سحب الرؤوس بالماوس.
   - استخدام معمارية تجميع الإطارات (Frame Coalescing) لمنع تراكم الطلبات وضمان سلاسة 60 إطاراً في الثانية.

2. **الحالة الافتراضية معطلة (Default State = OFF)**:
   - تبدأ الإضافة دائماً بحالة `OFF`. لا تبدأ بمراقبة الأحداث أو رسم أي رسومات حتى ينقر المستخدم على زر التفعيل.
   - تظل بيئة التعديل القياسية في ArcGIS Pro غير متأثرة تماماً.

3. **حالة موحدة ومتزامنة (Central State Synchronization)**:
   - زر الشريط العلوي (Ribbon Toggle Button) ولوحة الإعدادات (Dock Pane) مرتبطان بحالة مركزية واحدة (`DimensionSettings.IsEnabled`).

4. **التموضع الديناميكي الذكي (Maplex-Style Layout)**:
   - يعاد حساب مواضع النصوص مع عمليات التكبير والتصغير (Zoom) والتحريك (Pan).
   - عند التقريب على جزء من مضلع كبير، يتموضع النص على منتصف    - خيار "تطبيق على طبقات الخدمات" (`Apply to Service Layers`) معطل افتراضياً (`OFF`).
    - إشعار توضيحي غير مزعج للمستخدم عند اختيار طبقة خدمة بدون إظهار أخطاء.

7. **التوافق التلقائي مع السمات (Dark & Light Themes)**:
   - دعم متكامل مع سمات ArcGIS Pro؛ تتحول ألوان نصوص لوحة الإعدادات وعناصر التحكم تلقائياً إلى **الأسود** في النمط الفاتح (Light Theme) لضمان سهولة القراءة، وتبقى **باللون الأبيض** في النمط الداكن (Dark Theme).

8. **قياسات متعددة للمعالم (Multi-Feature Measurements)**:
   - إمكانية قياس وترقيم عدة مضلعات أو خطوط محددة في وقت واحد مع تحديد حد أقصى (10 إلى 200)، وخيار وضع الأبعاد داخل حدود المضلعات لتجنب تداخل النصوص بين المعالم المتجاورة.

9. **شاشة المعلومات اللحظية والتحذيرات (HUD & Warnings)**:
   - شاشة معلومات (HUD) تعرض عدد النقاط والأضلاع الظاهرة على الشاشة لحظياً، بالإضافة إلى تنبيه للأبعاد المخفية بسبب مستوى التكبير.

10. **الإعدادات الافتراضية القياسية**:
    - الوحدة الافتراضية: **المتر (`Meter`)**.
    - النمط الافتراضي: **Numbers_Only** (يعرض الرقم + الوحدة مثل `35.42 m` و `1250.52 m²` بدون كلمات وصفية مثل `Length:` أو `Area:`).

---

## 3. بنية المشروع ومكونات الكود

```
GeoMetrics/
├── Config.daml                          # تعريف عناصر الواجهة (Ribbon Tab, Group, Buttons, DockPane)
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
│   ├── DimensionSettingsPaneView.xaml.cs# كود إدارة السمات وتحديث ألوان النصوص تلقائياً
│   └── EnumEqualsConverter.cs           # محول ربط الـ RadioButtons مع الـ Enums
│
└── Utilities/
    ├── LayerHelper.cs                   # فحص وتمييز طبقات الخدمات عن الطبقات المحلية
    └── UnitConverter.cs                 # التحويلات الرياضية وتنسيق الأرقام والوحدات
```

---

## 4. شرح الـ APIs الرئيسية المستخدمة

### أ) `Core/EditingMonitor.cs` — مراقبة الأحداث
- `SketchModifiedEvent`: الحدث الأساسي الذي يُطلق عند تحريك أي Vertex في أدوات التعديل.
- `MapViewCameraChangedEvent`: يُطلق عند عمل Zoom أو Pan لإعادة ضبط مواضع التسميات لحظياً.
- `MapSelectionChangedEvent`: يُطلق عند تغيير المعلم المختار لمسح الرسومات القديمة وعرض الأبعاد للمعلم الجديد.

### ب) `Measurement/GeometryMeasurementService.cs` — حسابات الهندسة والزوايا
- `GeometryEngine.Instance.Length` و `GeometryEngine.Instance.Area`: قياس مسقط Planar باستخدام نظام الإحداثيات الخاص بالمعلم.
- `GeometryEngine.Instance.GeodesicLength` و `GeometryEngine.Instance.GeodesicArea`: قياس جيوديسي تلقائي للطبقات الجغرافية (`WGS84`).
- حساب الزوايا بين الأضلاع المتتالية عبر حاصل الضرب القياسي (Dot Product) مع استبعاد الزوايا المستقيمة $\approx 180^\circ$.

### ج) `Rendering/GeoMetricsOverlayManager.cs` — إدارة الرسم المؤقت
- يستخدم `MapView.AddOverlay(CIMGraphic)` لعرض الرسومات في الذاكرة الرسومية المؤقتة للخريطة، وتفريغها عبر `IDisposable` لمنع تسريب الذاكرة.

---

## 5. جدول الإعدادات والخيارات

| الخيار | النوع | القيمة الافتراضية | الوصف |
|---|---|---|---|
| **GeoMetrics** | تبديل | `OFF` | المفتاح الرئيسي لتشغيل/إيقاف الأداة. |
| **Target Layer** | قائمة | `تلقائي` | حصر الأداة في طبقة معينة أو الاكتشاف التلقائي من التحديد. |
| **Multi-Feature Measurements** | اختيار | `OFF` | قياس وترقيم جميع المعالم المحددة في وقت واحد. |
| **Max Features Limit** | قائمة | `50` | الحد الأقصى للمعالم المقاسة معاً (`10`, `25`, `50`, `100`, `200`). |
| **Place Dimensions Inside** | اختيار | `OFF` | رسم أبعاد المضلعات للداخل لتفادي تداخل النصوص بين المعالم المتجاورة. |
| **Apply to Service Layers** | اختيار | `OFF` | السماح بالعمل على طبقات الـ Feature Service و Map Service. |
| **Segment Lengths** | اختيار | `ON` | إظهار أطوال الأضلاع. |
| **Polygon Area** | اختيار | `ON` | إظهار مساحة المضلع (`m²`, `ft²`, إلخ). |
| **Perimeter** | اختيار | `OFF` | إظهار المحيط الكلي للمضلع (`m`, `ft`, إلخ). |
| **Vertex Angles** | اختيار | `OFF` | إظهار الزوايا بين الأضلاع (مع استبعاد الزوايا $\approx 180^\circ$). |
| **Vertex Coordinates** | اختيار | `OFF` | إظهار إحداثيات النقاط والأركان (X, Y) بشكل لحظي مع تحديد الخانات العشرية (0-8). |
| **Area Difference** | اختيار | `OFF` | إظهار ومقارنة مساحة المضلع قبل التعديل وأثناء التعديل وفارق التغير ($\Delta$). |
| **Visible Count HUD** | اختيار | `OFF` | عداد حي لعدد النقاط والأضلاع الظاهرة على الشاشة في أعلى يسار نافذة الخريطة. |
| **Hidden Warning** | اختيار | `OFF` | تحذير عند إخفاء أبعاد بسبب مستوى التقريب في أعلى يمين نافذة الخريطة. |
| **Measurement Method** | خيارات | `Automatic` | تلقائي، مسقط (Planar)، أو جيوديسي (Geodesic). |
| **Display Units** | قائمة | `Meters` | أمتار، أقدام، أقدام مساحية، كيلومترات، أميال، أو وحدات الطبقة. |
| **Dimension Style** | قائمة | `Numbers_Only` | نمط الأرقام فقط، نمط CAD القياسي، نمط مبسط، أو عالي التباين. |
| **Text Color** | قائمة | `Black` | لون نصوص الأبعاد على الخريطة (أسود، أزرق، أحمر، أخضر، برتقالي، أبيض، إلخ). |
| **Decimal Places** | شريط | `2` | عدد الخانات العشرية المعروضة (0 إلى 4). |
| **Label Offset** | شريط | `14 px` | مسافة إزاحة النص عن الضلع بالبكسل. |
| **UI Theme Text Color** | تلقائي | `Adaptive` | تكيّف لون نصوص الواجهة تلقائياً: **أسود** في النمط الفاتح و**أبيض** في النمط الداكن. |
---

## 6. البناء والتثبيت (Build & Install)

### المتطلبات:
- نظام **Windows 10 / 11 (x64)**.
- برنامج **ArcGIS Pro 3.3.x أو 3.4.x**.
- بيئة **Visual Studio 2022** مع .NET 8 وحزمة ArcGIS Pro SDK for .NET.

### أمر البناء:
```powershell
msbuild GeoMetrics.csproj /p:Configuration=Release
```

ملف التثبيت الناتج:
```
Addin_Package\GeoMetrics.esriAddinX
```

### التثبيت:
1. افتح مجلد `Addin_Package/` وانقر نقراً مزدوجاً على ملف [`GeoMetrics.esriAddinX`](file:///d:/Learning/Realtime%20Dim%20Area/Addin_Package/GeoMetrics.esriAddinX).
2. اضغط **Install Add-In** في نافذة التثبيت التلقائية.
3. افتح ArcGIS Pro ستجد تبويب **GeoMetrics** جاهزاً في الشريط العلوي.
