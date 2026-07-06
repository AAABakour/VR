# VR Real GPU SPH - Phase 02 Upgrade

## الهدف
هذه المرحلة تضيف نواة GPU Fluid/SPH عملية فوق النسخة المستقرة، بدون كسر أنظمة المشروع الحالية. الهدف من Phase 02 هو تشغيل طبقة GPU كبيرة للطلاء، اختبار ميزانية 262k إلى 1M particle، وتجهيز مسار الرندر والتصادم قبل الانتقال إلى Neighbor Search / Density-Pressure SPH الكامل.

## الملفات المضافة
- `Assets/Scripts/SPH/RealGpuSphController.cs`
- `Assets/Resources/RealBucketSPH_Phase02.compute`
- `Assets/SPH/Shaders/GpuSphIndirectParticle_URP.shader`
- `Assets/Editor/RealGpuSphPhase02Tools.cs`

## ماذا يعمل الآن
- Auto-create runtime controller باسم `RealGpuSphController_Phase02`.
- تحميل Compute Shader من `Resources` تلقائياً.
- إنشاء GPU buffers للـ particles والـ visible render particles.
- تشغيل preview آمن بـ 262,144 particle.
- دعم وضع 1,000,000 particle عبر F11.
- رندر GPU باستخدام `Graphics.DrawMeshInstancedIndirect` بدل GameObject لكل particle.
- تصادم bucket-local cylinder يتبع دوران الدلو.
- بداية منطق السكب عند ميلان الدلو.
- surface-plane preview لإيقاف الطلاء عند اللوحة/السطح.
- Overlay runtime بسيط يعرض حالة النظام.
- ربط مع QA وStats UI.

## مفاتيح التشغيل
داخل Play Mode:
- `F9`: تشغيل/إيقاف Phase 02 GPU fluid preview.
- `F11`: تشغيل وضع 1,000,000 particle.
- `F12`: إعادة تهيئة الجسيمات.

## أدوات Unity Editor
- `Tools > VR Paint > SPH Phase 02 > Add Runtime Controller To Scene`
- `Tools > VR Paint > SPH Phase 02 > Run Phase 02 Validation`

## حدود هذه المرحلة بصراحة
هذه ليست نهاية SPH العالمية بعد. هذه مرحلة GPU fluid core/preview مستقرة. المرحلة التالية يجب أن تضيف:
1. GPU spatial hashing مع prefix sums أو sorted grid.
2. Density pass.
3. Pressure pass.
4. Viscosity pass.
5. Boundary SDF للدلو بدل cylinder فقط.
6. AsyncGPUReadback محدود جداً أو append buffer للـ surface impacts.
7. تحويل collisions إلى `SphCollisionSurfaceBridge` بدون قراءة مليون particle على CPU.

## لماذا هذا التدرج مهم
محاولة إضافة مليون particle مع SPH كامل ورندر وتصادم دفعة واحدة غالباً ستكسر المشروع أو تجعل التشخيص مستحيلاً. هذه المرحلة تثبت المسارات الأساسية:
- GPU allocation
- Indirect rendering
- bucket-following simulation
- runtime switching
- QA integration

بعد نجاح هذه المرحلة، الانتقال إلى SPH الكامل يكون آمن ومدروس.
