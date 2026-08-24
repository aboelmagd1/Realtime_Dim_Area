# أداة Dimension Overlay Tool — إضافة (Add-in) لبرنامج ArcGIS Pro

شرح كامل بالعربية لبنية المشروع، والـ APIs المستخدمة، وخطوات البناء والتشغيل.

---

## 1. الفكرة العامة

الأداة عبارة عن **Add-in حقيقي** لبرنامج ArcGIS Pro (مبني بلغة C# على ArcGIS Pro SDK for .NET)
تعمل مثل أدوات الـ CAD: أثناء تعديل معلم (Feature) من نوع Polygon أو Polyline، تظهر
أبعاد الأضلاع والمساحة والمحيط **فوق الخريطة مباشرة وبشكل حي (Real-Time)** أثناء تحريك
أي Vertex، دون الحاجة لحفظ التعديل أو تشغيل Calculate Geometry.

النطاق (Scope) محصور بدقة حسب طلبك:

```
الخريطة النشطة
   └── الطبقة المختارة من المستخدم (Dimension Layer)
            └── المعلم المختار فقط (Selected Feature)
                     └── الهندسة الحالية (Current Geometry)
```

الأداة **لا تلمس** باقي الطبقات أو باقي المعالم إطلاقًا، ولا تُعدّل الـ Feature Class
الأصلية إلا عند إنهاء التعديل بشكل طبيعي (Save عبر EditOperation)، بينما كل ما يُعرض
أثناء السحب هو رسومات مؤقتة (Overlay Graphics) تختفي بمجرد إلغاء تفعيل الأداة أو تغيير التحديد.

---

## 2. بنية المشروع (Project Structure)

```
DimensionOverlay/
├── Config.daml                          ← تعريف الـ Ribbon (تبويب + أزرار)
├── Module1.cs                           ← نقطة الدخول، يحمل إعدادات مشتركة
├── DimensionOverlay.csproj
│
├── Tools/
│   └── DimensionOverlayTool.cs          ← الأداة التفاعلية (MapTool) — قلب النظام
│
├── Geometry/
│   ├── GeometryMeasurementService.cs    ← القياس الحقيقي (Planar/Geodesic)
│   ├── PolygonDimensionCalculator.cs    ← حساب أبعاد المضلعات
│   └── PolylineDimensionCalculator.cs   ← حساب أبعاد الخطوط
│
├── Graphics/
│   └── DimensionGraphicManager.cs       ← رسم الأبعاد كـ Overlay مؤقت
│
├── Models/
│   ├── DimensionSettings.cs             ← كل إعدادات المستخدم (طبقة، وحدات، خيارات عرض)
│   └── DimensionResult.cs               ← نتيجة القياس لكل تحديث
│
├── UI/
│   ├── DimensionSettingsPaneView.xaml   ← واجهة اللوحة الجانبية (Dock Pane)
│   ├── DimensionSettingsPaneView.xaml.cs
│   ├── DimensionSettingsPaneViewModel.cs
│   ├── ShowSettingsButton.cs
│   └── EnumEqualsConverter.cs
│
└── Utilities/
    └── UnitConverter.cs                 ← تنسيق الأرقام والنصوص فقط (بدون تحويل وحدات فعلي)
```

فصل المسؤوليات: **الهندسة/الحساب** (Geometry/) منفصلة تمامًا عن **الرسم**
(Graphics/) وعن **الواجهة** (UI/) — تمامًا كما طلبت في متطلب رقم 15.

---

## 3. أهم ملفات الكود وشرح كل API مستخدم فيها

### أ) `Tools/DimensionOverlayTool.cs` — الأداة الرئيسية

هذا هو الملف الأهم. يرث من `ArcGIS.Desktop.Mapping.MapTool` ويستخدم:

- **`IsSketchTool = true` + `SketchType`**: يجعل من الأداة أداة تعديل (Sketch Tool)
  حقيقية، تمامًا مثل أدوات "Reshape" أو "Edit Vertices" المدمجة في ArcGIS Pro.
- **`SetCurrentSketchAsync(geometry)`**: عند تفعيل الأداة، نحمّل هندسة المعلم
  المختار مباشرة داخل الـ Sketch — هذه هي الطريقة الصحيحة في SDK لبدء تعديل معلم
  موجود مسبقًا بدلاً من رسم شكل جديد.
- **`OnSketchModifiedAsync()`**: هذا الـ override هو **مفتاح الحل بالكامل** لمتطلب
  "Real-Time Editing" — يتم استدعاؤه تلقائيًا من الـ SDK في كل مرة يحرك فيها
  المستخدم أو يضيف أو يحذف Vertex، أي قبل أي حفظ. من هنا نعيد حساب الأبعاد ونعيد رسمها.
- **`OnSketchCompleteAsync(geometry)`**: يُستدعى عند إنهاء التعديل، وهنا فقط
  نستخدم `EditOperation.Modify(...)` و`ExecuteAsync()` لحفظ الهندسة الجديدة داخل
  الـ Feature Class — بنفس آلية التحرير القياسية في ArcGIS Pro (تدعم Undo/Redo
  والـ Versioning بشكل طبيعي).
- **`MapSelectionChangedEvent`**: نشترك في هذا الحدث من `ArcGIS.Desktop.Mapping.Events`
  حتى إذا غيّر المستخدم التحديد إلى معلم آخر، نمسح الرسومات القديمة فورًا ونحمّل
  المعلم الجديد — هذا يحقق متطلب "لا تترك رسومات قديمة عالقة على الخريطة".
- **`QueuedTask.Run(...)`**: كل عمليات القراءة من الـ Geodatabase (`GetTable`,
  `Search`, `GetShape`) يجب أن تنفَّذ داخل `QueuedTask` لأنها تتطلب MCT
  (Multi-threaded Cursor Thread) وليس UI Thread — وهذا مطلب أساسي في أي Add-in.

### ب) `Geometry/GeometryMeasurementService.cs` — القياس الصحيح

يستخدم `ArcGIS.Core.Geometry.GeometryEngine`:

- `GeometryEngine.Instance.Length(polyline)` و`.Area(polygon)`: قياس **Planar** حقيقي
  باستخدام نظام الإحداثيات الخاص بالطبقة نفسها (وليس WGS84 أو Web Mercator كما
  حذّرت في متطلباتك).
- `GeometryEngine.Instance.GeodesicLength(...)` و`.GeodesicArea(...)`: قياس
  **Geodesic** دقيق يُستخدم تلقائيًا إذا كان نظام إحداثيات الطبقة جغرافيًا
  (`SpatialReference.IsGeographic == true`، مثل WGS84 / EPSG:4326).
- **قرار Planar/Geodesic التلقائي** مبني بالكامل على `SpatialReference` الخاص
  بهندسة المعلم نفسه — وليس على SpatialReference الخاص بالخريطة (Map)، تنفيذًا
  حرفيًا لمتطلب "Do NOT use the Map's spatial reference... use the layer's".
- `GeometryEngine.Instance.LabelPoint(polygon)`: نقطة داخلية مضمونة لوضع تسمية
  المساحة حتى في المضلعات غير المنتظمة (Concave)، مع Fallback إلى `Centroid`.

### ج) `Graphics/DimensionGraphicManager.cs` — الرسم المؤقت

يستخدم `MapView.AddOverlay(CIMGraphic)` من `ArcGIS.Desktop.Mapping` — وهي الآلية
الرسمية في SDK لعرض رسومات **مؤقتة فقط في نافذة العرض الحالية**، لا تُخزَّن أبدًا
في أي طبقة أو قاعدة بيانات. كل نداء لـ `AddOverlay` يُعيد `IDisposable`؛ استدعاء
`Dispose()` عليه هو ما يُزيل الرسم فعليًا — لذلك دالة `Clear()` تستدعي Dispose على
كل الرسومات المحفوظة، وتُستدعى تلقائيًا عند: تعطيل الأداة، تغيير التحديد، أو بداية
كل إعادة رسم.

يبني كل بُعد كخط CAD حقيقي: `CIMLineGraphic` لخط البعد + علامات نهاية (Ticks) عمودية
+ `CIMTextGraphic` للنص مع `Angle` محسوب من اتجاه الضلع (مع تصحيح الزاوية بحيث لا
يظهر النص مقلوبًا رأسًا على عقب أبدًا — متطلب رقم 12).

### د) `UI/DimensionSettingsPaneViewModel.cs` — لوحة الإعدادات

يرث من `ArcGIS.Desktop.Framework.Contracts.DockPane` — نظام الـ Dock Pane القياسي
في ArcGIS Pro (نفس فكرة لوحة Catalog أو Contents). القائمة المنسدلة للطبقات مبنية
من `Map.GetLayersAsFlattenedList().OfType<FeatureLayer>()` مع فلترة صارمة على
`ShapeType == Polygon || Polyline` فقط، حتى لا يظهر المستخدم طبقات غير قابلة
للقياس.

---

## 4. كيف تُغطّى المتطلبات المطلوبة (ملخص)

| المتطلب | كيف تحقق |
|---|---|
| العمل على طبقة واحدة فقط يختارها المستخدم | `Settings.TargetLayer` — مصدر وحيد للحقيقة، لا Fallback لأي طبقة أخرى |
| العمل على المعلم المختار فقط | `targetLayer.GetSelection()` فقط — لا Cursor على كل الـ Feature Class |
| تحديث حي أثناء السحب بدون حفظ | `OnSketchModifiedAsync()` |
| احترام نظام إحداثيات الطبقة | القياس دائمًا على `shape.SpatialReference`، ليس Map SR |
| Planar تلقائي / Geodesic تلقائي | `GeometryMeasurementService.ResolveUseGeodesic(...)` |
| عدم تعديل الـ Feature Class أثناء العرض | كل الرسم عبر `MapView.AddOverlay` فقط |
| إزالة الرسومات عند إلغاء التفعيل/تغيير التحديد | `OnToolDeactivateAsync` + `OnMapSelectionChanged` يستدعيان `Clear()` |
| Area/Perimeter/Segment/Angle/Bearing | `PolygonDimensionCalculator` / `PolylineDimensionCalculator` |
| Area Difference + Tolerance (QC) | `DimensionResult.AreaDifference` + `Settings.AreaTolerance` |
| عرض حسب مقياس الرسم (Scale-Dependent) | `DimensionGraphicManager.Draw(...)` بحدود `SmallScaleThreshold` / `MediumScaleThreshold` |
| عدم قلب النص رأسًا على عقب | `AngleForLabel(...)` |

---

## 5. إنتاج ملف .esriAddinX لإصدار ArcGIS Pro 3.3.2 تحديدًا

**تنويه مهم بالصراحة:** بيئة العمل التي أُنشئ بها هذا المشروع (بيئة Claude) هي Linux
ولا تحتوي على Visual Studio ولا على مكتبات ArcGIS Pro SDK نفسها (وهي مكتبات مُغلقة
المصدر تُثبَّت فقط مع ArcGIS Pro على Windows). لذلك **لا يمكنني تصدير ملف .esriAddinX
مُترجَم (Compiled) وجاهز من هنا** — لكن المشروع مُهيَّأ بالكامل الآن لإنتاجه تلقائيًا
بمجرد أن تبنيه (Build) على جهازك؛ لا توجد خطوة "تحويل" منفصلة، فملف .esriAddinX هو
ببساطة **ناتج البناء نفسه** الذي تنتجه أدوات SDK تلقائيًا.

ما عدّلته في المشروع خصيصًا لإصدار **3.3.2**:

- `TargetFramework` = `net8.0-windows` (هذا هو إطار العمل الصحيح لكل إصدارات ArcGIS
  Pro 3.3.x، بما فيها 3.3.2).
- `PackageReference` لحزمة `Esri.ArcGISPro.Extensions30` مُقيَّدة بالنطاق
  `[3.3.0, 3.4.0)` حتى لا يسحب NuGet نسخة من إصدار 3.4 أو أحدث بالخطأ (وهو سبب شائع
  لفشل تحميل الإضافة برسالة "targets an incompatible version").
- أضفت `<EnableEsriAddInFileGeneration>true</EnableEsriAddInFileGeneration>` و
  `PlatformTarget=x64` وهما الإعدادان اللذان يُفعّلان خطوة تعبئة .esriAddinX تلقائيًا
  بعد كل بناء ناجح (تأتي هذه الآلية من ملفات .targets المُثبَّتة مع SDK Extension).
- أضفت مجلد `Images/` بأيقونات بديلة بسيطة (Placeholder) حتى لا يفشل البناء بسبب
  مسارات صور مفقودة يشير إليها `Config.daml` — استبدلها بأيقوناتك الخاصة متى أردت.
- عدّلت `desktopVersion` في `Config.daml` إلى `3.3.48105` (رقم بناء إصدار 3.3 الرسمي
  حسب توثيق Esri). **ملاحظة**: هذا الرقم بوابة تحقق على مستوى الإصدار 3.3 ككل وليس
  خاصًا بالتحديث الفرعي 3.3.2 بالتحديد (Esri توثّق أن هذه القيمة لا تُميّز التحديثات
  الفرعية Patches) — لكن الأدق دائمًا هو أخذ القيمة كما تُولِّدها Visual Studio نفسها
  عند إنشاء مشروع Add-in جديد على جهازك (انظر الخطوة 2 أدناه).

### خطوات الحصول على .esriAddinX فعليًا

1. على جهاز **Windows** به **ArcGIS Pro 3.3.2** مثبّتًا، ثبّت **ArcGIS Pro SDK for
   .NET** (من داخل ArcGIS Pro: Project > Options > Add-In، أو Visual Studio
   Installer > Individual Components). يتطلب Visual Studio 2022.
2. أنشئ مشروعًا تجريبيًا فارغًا: **File > New > Project > ArcGIS Pro Add-in (C#)**
   — هذا سيُولِّد `Config.daml` جديدًا يحتوي على قيمة `desktopVersion` الصحيحة تمامًا
   المطابقة لنسخة 3.3.2 المثبّتة لديك. انسخ هذه القيمة والصقها بدلاً من السطر الحالي
   في `Config.daml` المرفق هنا.
3. انسخ جميع ملفات هذا المشروع (بما فيها `Images/`) إلى مجلد المشروع التجريبي،
   لتحل محل الملفات الافتراضية، مع إبقاء اسم المشروع/الـ Assembly مطابقًا
   (`DimensionOverlay`).
4. من Visual Studio: **Build > Build Solution** (أو F5 للتشغيل المباشر مع تصحيح
   الأخطاء داخل ArcGIS Pro). عند النجاح ستجد الملف تلقائيًا هنا:
   ```
   bin\Debug\net8.0-windows\DimensionOverlay.esriAddinX
   ```
   (أو `bin\Release\...` إذا بنيت بوضع Release).
5. لتثبيته على أي جهاز آخر: انسخ ملف `.esriAddinX` وشغّله بنقرة مزدوجة —
   سيفتح ArcGIS Pro Add-in Manager تلقائيًا ويطلب تأكيد التثبيت.
6. إن ظهرت رسالة "targets an incompatible version of ArcGIS Pro"، فهذا يعني أن
   `desktopVersion` في `Config.daml` أعلى من نسخة Pro المثبّتة — راجع الخطوة 2
   وتأكد من نسخ الرقم الصحيح.

---

## 6. متطلبات التشغيل والبناء (Build & Install)

1. **ثبّت** "ArcGIS Pro SDK for .NET" كإضافة Visual Studio (من داخل ArcGIS Pro:
   Project > Options، أو من Esri Developer صفحة الـ SDK). يتطلب Visual Studio 2022.
2. من داخل Visual Studio: **File > New > Project > ArcGIS Pro Add-in (C#)**، ثم
   استبدل الملفات المُنشأة تلقائيًا بملفات هذا المشروع (أو انسخها إلى نفس المسارات).
3. تأكد أن `<TargetFramework>` في ملف `.csproj` يطابق إصدار ArcGIS Pro لديك:
   - ArcGIS Pro 3.3 / 3.4 → `net8.0-windows`
   - ArcGIS Pro 3.0 – 3.2 → غالبًا `net6.0-windows` (تحقق من Help > About)
4. اضغط **F5** لتشغيل التصحيح (Debug) — سيفتح ArcGIS Pro تلقائيًا مع تحميل الإضافة.
5. من التبويب الجديد **"Dimension Overlay"** في الـ Ribbon:
   - اضغط **Settings** لفتح لوحة الإعدادات واختيار الطبقة.
   - حدد معلمًا واحدًا من تلك الطبقة على الخريطة.
   - فعّل أداة **Dimension Overlay** من التبويب، وابدأ بتحريك النقاط.

---

## 7. ملاحظات مهمة وحدود النسخة الحالية (MVP)

- الكود مكتوب باتباع الأنماط الرسمية لـ ArcGIS Pro SDK (QueuedTask، EditOperation،
  MapTool sketch lifecycle)، لكنه **لم يُختبر بالتصحيح الفعلي** لأن بيئة التطوير هنا
  لا تحتوي SDK ArcGIS Pro نفسه (وهو مثبت فقط داخل Windows مع ArcGIS Pro). يُنصح
  بمراجعة أي أخطاء ترجمة بسيطة (Compile Errors) عند أول بناء داخل Visual Studio،
  خصوصًا أسماء الأنواع الدقيقة في إصدار SDK لديك.
- خوارزمية **تجنّب تداخل التسميات (Label Collision)** حاليًا مبسّطة (إزاحة ثابتة +
  اختيار عدد محدود من الأضلاع عند التصغير)؛ نظام تصادم كامل (Force-directed أو
  Grid-based) مذكور في قسم "التوسعات المستقبلية" أدناه.
- دعم Multipatch وPoint غير مُنفَّذ بعد، لكن البنية (`GeometryType` checks في
  `DimensionOverlayTool` وMethods منفصلة في `Geometry/`) مُعدة لإضافتهما لاحقًا
  دون إعادة هيكلة.

---

## 8. التوسعات المستقبلية المقترحة (كما في طلبك الأصلي)

كل توسعة يمكن إضافتها دون تعديل جوهري بفضل الفصل الحالي بين الطبقات:

- **مسافة بين نقطتين/معلمين**: دالة جديدة في `GeometryMeasurementService` + أداة
  MapTool منفصلة بسيطة.
- **إحداثيات الرؤوس / Grid dimensions**: توسيع `SegmentDimension` لإضافة إحداثيات
  X/Y لكل Vertex وعرضها في `DimensionGraphicManager`.
- **تصدير الأبعاد كـ Feature Class**: دالة جديدة تأخذ `DimensionResult` الأخير
  وتكتبه عبر `InsertCursor` إلى طبقة Annotation أو Line جديدة.
- **حفظ الأنماط (Dimension Styles) وSupport للـ Dark/Light Theme**: نقل الألوان
  والرموز في `DimensionGraphicManager` إلى كائن `DimensionStyle` قابل للتهيئة من
  الإعدادات، بدلاً من القيم الثابتة الحالية.
- **قواعد QC خاصة بالمخططات (Parcel-specific)**: طبقة جديدة فوق `PolygonDimensionCalculator`
  تقرأ قواعد من جدول إعدادات وتُظهر تحذيرات إضافية بجانب Tolerance الحالي.
