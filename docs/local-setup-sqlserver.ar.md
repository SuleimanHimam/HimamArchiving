<div dir="rtl">

# دليل المطوّر: تشغيل المشروع محليًا مع SQL Server

هذا الدليل موجَّه لمطوّر يستلم المشروع لأول مرة ويريد تشغيله على جهازه مع **SQL Server**.
بنهاية الخطوات سيكون لديك:

- الخادم (ASP.NET Core API) على `http://localhost:5105`
- الواجهة (React) على `http://localhost:5173`، **بالعربية** (من اليمين لليسار) افتراضيًا
- قاعدة بيانات `Archiving` على SQL Server، أنشأ الخادم جداولها وبياناتها الأولية بنفسه

> النظام يدعم محرّكين: **SQL Server** و **MySQL**. هذا الدليل لـ SQL Server. تنصيب النسخة النهائية
> على خادم الإنتاج له دليل منفصل: [`deploy/README.ar.md`](../deploy/README.ar.md).

---

## المحتويات

1. [المتطلبات](#١-المتطلبات)
2. [جلب الكود](#٢-جلب-الكود)
3. [إنشاء قاعدة البيانات](#٣-إنشاء-قاعدة-البيانات)
4. [إعداد الخادم](#٤-إعداد-الخادم-backend)
5. [تشغيل الخادم](#٥-تشغيل-الخادم)
6. [تشغيل الواجهة](#٦-تشغيل-الواجهة-frontend)
7. [الدخول واستخدام النظام بالعربية](#٧-الدخول-واستخدام-النظام-بالعربية)
8. [الاختبارات والتحقق](#٨-الاختبارات-والتحقق)
9. [تعديل نموذج البيانات (Migrations)](#٩-تعديل-نموذج-البيانات-migrations)
10. [هيكل المشروع](#١٠-هيكل-المشروع)
11. [مشكلات شائعة وحلولها](#١١-مشكلات-شائعة-وحلولها)

---

## ١. المتطلبات

| البرنامج | الإصدار | للتحقق |
|---|---|---|
| نظام التشغيل | **Windows** 10/11 أو Windows Server (إلزامي، انظر الملاحظة) | |
| Git | أي إصدار حديث | `git --version` |
| .NET SDK | **10.0** | `dotnet --list-sdks` |
| Node.js | **22 LTS** أو أحدث (الحد الأدنى 20.19) | `node -v` |
| SQL Server | 2016 أو أحدث، **أي إصدار بما فيه Express**. ننصح بـ 2022 Express أو Developer | `Get-Service MSSQL*` |
| SSMS أو `sqlcmd` | اختياري، لإنشاء قاعدة البيانات | `sqlcmd -?` |
| محرّر | Visual Studio (إصدار يدعم .NET 10) أو VS Code أو Rider | |

**لماذا Windows؟** الملفات المرفوعة تُخزَّن مشفّرة، ومفتاح التشفير يُحفظ في ملف محمي بخدمة
DPAPI الخاصة بويندوز. على نظام آخر يرفض الخادم الإقلاع ما لم يُضبط مزوّد مفاتيح خارجي
(`Kms:Provider = ExternalKms`).

**لا يحتاج النظام** خاصية Full-Text Search في SQL Server، ولا تثبيت Tesseract (ملفات OCR العربية
والإنجليزية مرفقة مع المشروع).

---

## ٢. جلب الكود

```powershell
git clone https://github.com/SuleimanHimam/HimamArchiving.git
cd HimamArchiving
```

---

## ٣. إنشاء قاعدة البيانات

أنشئ قاعدة فارغة باسم `Archiving`. **لا تُنشئ أي جداول**: الخادم يُنشئها بنفسه عند أول تشغيل.

**بسطر واحد عبر `sqlcmd`:**

```powershell
sqlcmd -S .\SQLEXPRESS -E -C -Q "IF DB_ID(N'Archiving') IS NULL CREATE DATABASE [Archiving];"
```

**أو من SSMS:** اتصل بالخادم، ثم انقر بالزر الأيمن على **Databases** واختر **New Database…**، واكتب
الاسم `Archiving`، ثم **OK**.

**اسم الخادم (`-S`) حسب طريقة تثبيت SQL Server:**

| التثبيت | اسم الخادم |
|---|---|
| SQL Server Express (الافتراضي) | `.\SQLEXPRESS` |
| Developer أو Standard بنسخة افتراضية (default instance) | `.` أو `localhost` |
| نسخة مسمّاة (named instance) | `.\اسم_النسخة` |

إن لم تعرف الاسم فشغّل `Get-Service MSSQL*`: الخدمة `MSSQLSERVER` تعني النسخة الافتراضية، والخدمة
`MSSQL$SQLEXPRESS` تعني النسخة المسمّاة `SQLEXPRESS`.

**لماذا ننشئها مسبقًا؟** عند الإقلاع ينتظر الخادم أن تصبح القاعدة متاحة (حتى ٣ دقائق) قبل أن ينشئ
الجداول. وجود القاعدة مسبقًا يجنّبك هذا الانتظار.

> الحقول النصية من نوع `nvarchar`، فالعربية تُحفظ سليمة مع أي Collation. إن أردت ترتيبًا أبجديًا
> عربيًا فأنشئ القاعدة بـ `COLLATE Arabic_CI_AS`.

---

## ٤. إعداد الخادم (Backend)

### أ) أنشئ ملف الإعدادات المحلي

```powershell
cd backend\src\Archiving.Api
copy appsettings.Development.SqlServer.json.example appsettings.Development.json
```

الملف `appsettings.Development.json` **مستثنى من Git** (موجود في `.gitignore`)، لأنه يحمل أسرار
جهازك. لا ترفعه أبدًا.

### ب) عدّل سلسلة الاتصال

افتح `appsettings.Development.json` وعدّل `ConnectionStrings:Default` حسب جهازك:

```jsonc
// SQL Server Express بحساب ويندوز (الافتراضي في القالب)
"Default": "Server=.\\SQLEXPRESS;Database=Archiving;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true;"

// نسخة افتراضية (Developer / Standard)
"Default": "Server=localhost;Database=Archiving;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true;"

// حساب SQL (اسم مستخدم وكلمة مرور)
"Default": "Server=localhost,1433;Database=Archiving;User ID=archiving;Password=كلمة_المرور;TrustServerCertificate=true;MultipleActiveResultSets=true;"
```

- **في JSON تُكتب الشَّرطة المائلة مرتين:** `.\\SQLEXPRESS` وليس `.\SQLEXPRESS`.
- القالب يضبط `"Database": { "Provider": "SqlServer" }`. أبقِه كما هو، فهو ما يجعل الخادم يستخدم
  SQL Server بدل MySQL.

### ج) ضع مفتاح JWT

استبدل `YOUR_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS` بقيمة عشوائية. لتوليدها في PowerShell:

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); [Convert]::ToBase64String($b)
```

### د) إعدادات اختيارية (يمكن تركها كما هي)

| الإعداد | الافتراضي | ملاحظة |
|---|---|---|
| `Storage:RootPath` | مجلد `storage` بجانب ملفات التشغيل (`bin\Debug\net10.0\storage`) | مكان حفظ الملفات المرفوعة (مشفّرة) |
| `Kms:KeyringPath` | `C:\ArchivingData\keys\keyring.dat` | يُنشأ تلقائيًا. **لا تحذفه**: بدونه لا يمكن فكّ تشفير الملفات المرفوعة |
| `License` | بلا مفتاح عام | الترخيص **معطَّل** في بيئة التطوير، فلا شيء محجوب |
| `Cors:AllowedOrigins` | `http://localhost:5173` | في وضع التطوير يُسمح بأي منفذ للواجهة على كل حال |

---

## ٥. تشغيل الخادم

```powershell
cd backend
dotnet run --project src/Archiving.Api --launch-profile http
```

**في أول تشغيل** يقوم الخادم تلقائيًا بما يلي:

1. ينتظر قاعدة البيانات حتى تصبح متاحة.
2. يطبّق كل الـ Migrations، فينشئ الجداول.
3. يُدخل البيانات الأولية: الصلاحيات، والأدوار، وحساب المدير، والهيكل التنظيمي، وسير العمل الافتراضي.
4. يُنشئ الحسابات التجريبية (في بيئة التطوير فقط).

للتأكد أنه يعمل:

| الرابط | المتوقع |
|---|---|
| `http://localhost:5105/health` | `{"status":"healthy"}` |
| `http://localhost:5105/scalar/v1` | توثيق تفاعلي لكل واجهات الـ API (في التطوير فقط) |

> الملف الشخصي `http` يستمع على `0.0.0.0:5105`، أي أنه متاح أيضًا من أجهزة الشبكة المحلية.

---

## ٦. تشغيل الواجهة (Frontend)

في نافذة طرفية **ثانية**:

```powershell
cd frontend
npm install
npm run dev
```

افتح `http://localhost:5173`.

الواجهة تتصل افتراضيًا بـ `http://localhost:5105/api`. إن كان الخادم على عنوان آخر:

```powershell
copy .env.example .env.local
# ثم عدّل فيه:  VITE_API_URL=http://عنوان-الخادم:5105/api
```

---

## ٧. الدخول واستخدام النظام بالعربية

- **العربية هي اللغة الافتراضية** والواجهة من اليمين لليسار. زر تبديل اللغة (عربي/English) وزر الوضع
  الفاتح/الداكن موجودان في صفحة الدخول. المتصفّح يحفظ اختيار اللغة.
- **حساب المدير:**

  | اسم المستخدم | كلمة المرور |
  |---|---|
  | `admin` أو `admin@archiving.local` | `Admin@12345` |

- **حسابات تجريبية** تظهر أزرارًا في صفحة الدخول، والنقر على أي منها يملأ البيانات. **تظهر في بيئة
  التطوير فقط**:

  | الدور | البريد | كلمة المرور |
  |---|---|---|
  | مدير | `demo.manager@archiving.local` | `DemoManager@2026` |
  | موظف أرشيف | `demo.archivist@archiving.local` | `DemoArchivist@2026` |
  | موظف | `demo.employee@archiving.local` | `DemoEmployee@2026` |

> ⚠️ هذه كلمات مرور معروفة ومنشورة في الكود. غيّر كلمة مرور المدير فورًا في أي بيئة غير جهازك
> المحلي.

---

## ٨. الاختبارات والتحقق

```powershell
# اختبارات الخادم (158 اختبارًا، لا تحتاج قاعدة بيانات)
dotnet test backend/Archiving.slnx

# الواجهة: الفحص والبناء
cd frontend
npm run lint
npm run build
```

---

## ٩. تعديل نموذج البيانات (Migrations)

لكل محرّك قاعدة بيانات سجلّ Migrations خاص به:

| المحرّك | مكان الـ Migrations |
|---|---|
| MySQL | `backend/src/Archiving.Infrastructure/Persistence/Migrations` |
| SQL Server | `backend/src/Archiving.Migrations.SqlServer/Migrations` |

**أي تعديل على الكيانات (Entities) أو على `AppDbContext` يحتاج Migration للمحرّكين معًا، وبالاسم
نفسه.** وإلا تعطّل النظام على المحرّك الذي نُسي.

```powershell
cd backend
dotnet tool restore            # يثبّت dotnet-ef بالإصدار المحدد في dotnet-tools.json

# MySQL
$env:ARCHIVING_DB_PROVIDER = 'MySql'
dotnet ef migrations add اسم_التعديل --project src/Archiving.Infrastructure --startup-project src/Archiving.Api --output-dir Persistence/Migrations

# SQL Server
$env:ARCHIVING_DB_PROVIDER = 'SqlServer'
dotnet ef migrations add اسم_التعديل --project src/Archiving.Migrations.SqlServer --startup-project src/Archiving.Api --output-dir Migrations
```

اكتب اسم التعديل بالإنجليزية، مثل `AddDocumentTags`.

للتأكد أن المحرّكين متطابقان مع النموذج:

```powershell
$env:ARCHIVING_DB_PROVIDER = 'SqlServer'
dotnet ef migrations has-pending-model-changes --project src/Archiving.Migrations.SqlServer --startup-project src/Archiving.Api
$env:ARCHIVING_DB_PROVIDER = 'MySql'
dotnet ef migrations has-pending-model-changes --project src/Archiving.Infrastructure --startup-project src/Archiving.Api
# المطلوب في الحالتين: No changes have been made to the model since the last migration.
```

**لا حاجة لتشغيل `dotnet ef database update`:** الخادم يطبّق الـ Migrations الجديدة بنفسه عند الإقلاع.

---

## ١٠. هيكل المشروع

```
backend/
  Archiving.slnx                       ← ملف الحل (Solution)
  src/
    Archiving.Domain/                  ← الكيانات والـ Enums
    Archiving.Application/             ← DTOs وواجهات الخدمات
    Archiving.Infrastructure/          ← EF Core، الخدمات، الأمان، التخزين، Migrations الخاصة بـ MySQL
    Archiving.Migrations.SqlServer/    ← Migrations الخاصة بـ SQL Server
    Archiving.Api/                     ← الـ Controllers ونقطة التشغيل (Program.cs)
  tests/Archiving.Tests/               ← اختبارات xUnit
  tools/Archiving.Seeder/              ← بيانات تجريبية كثيرة (⚠️ تعمل مع MySQL فقط حاليًا)
frontend/                              ← React 19 + TypeScript + Vite + Tailwind
  src/pages        src/components      src/lib        src/i18n (ar.json · en.json)
deploy/                                ← سكربتات وحزمة التنصيب على Windows Server
docs/                                  ← التوثيق
tools/                                 ← scan-agent (المسح الضوئي)، licensegen و licensemanager (التراخيص)، archiving-mcp
brand/                                 ← الشعار والأيقونات
```

**ملاحظات للمطوّر:**

- كل نص يظهر للمستخدم يجب أن يكون مفتاح ترجمة في `frontend/src/i18n/ar.json` **و** `en.json`.
  الأمر `npm run scan:i18n` يعدّ النصوص العربية المكتوبة مباشرة في الكود.
- الواجهة مصمَّمة من اليمين لليسار أولًا. استخدم خصائص CSS المنطقية (`margin-inline`،
  `inset-inline-start`) بدل `left`/`right`.
- مفاتيح التراخيص الخاصة (`license-private.pem`) لا تُرفع إلى Git أبدًا. التفاصيل في
  [`docs/licensing.md`](licensing.md).

---

## ١١. مشكلات شائعة وحلولها

| العَرَض | السبب والحل |
|---|---|
| السجل يكرّر `Waiting for the database (attempt N)` | القاعدة غير موجودة أو اسم الخادم خاطئ. أنشئها (الخطوة ٣) وتحقق من `Server=` |
| `A network-related or instance-specific error…` | خدمة SQL Server متوقفة أو اسم النسخة خاطئ. شغّل `Get-Service MSSQL*` وتأكد أنها Running |
| `Login failed for user '…'` | حساب ويندوز لا يملك صلاحية على القاعدة. من SSMS امنحه Login وعضوية `db_owner` على `Archiving`، أو استخدم حساب SQL |
| `The certificate chain was issued by an authority that is not trusted` | أضف `TrustServerCertificate=true;` إلى سلسلة الاتصال |
| `Unknown database provider '…'` | قيمة `Database:Provider` يجب أن تكون `SqlServer` أو `MySql` |
| `PlatformNotSupportedException … DPAPI` | الخادم يعمل على نظام غير Windows (انظر المتطلبات) |
| صفحة الدخول تقول «تعذّر الاتصال بالخادم» | الخادم غير مشغَّل على 5105، أو `VITE_API_URL` في `.env.local` خاطئ |
| `Address already in use` على 5105 | نسخة أخرى من الخادم تعمل. أوقفها، أو غيّر `applicationUrl` في `Properties/launchSettings.json` |
| أخطاء عند `npm install` أو `npm run dev` | إصدار Node قديم. ثبّت Node 22 LTS ثم احذف `node_modules` وأعد `npm install` |
| بعد حذف القاعدة: الملفات القديمة لا تُفتح | ملفات `storage` تخص القاعدة القديمة. عند إعادة البدء من الصفر احذف مجلد `storage` أيضًا |

---

## مراجع

- [`README.ar.md`](../README.ar.md): نظرة عامة على المشروع ووحداته
- [`docs/features.ar.md`](features.ar.md): شرح وظيفي مفصّل لكل وحدة
- [`deploy/README.ar.md`](../deploy/README.ar.md): التنصيب على خادم الإنتاج مع SQL Server
- [`docs/licensing.md`](licensing.md): نظام التراخيص
- [`docs/data-model.md`](data-model.md): نموذج البيانات

</div>
