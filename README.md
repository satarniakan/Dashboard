# Dashboard

سامانه‌ی مدیریت فروش، انبار و حسابداری با ویترین فروشگاه اینترنتی — Blazor (.NET 10)، معماری Clean.

## اجرای محلی (Development)

```bash
docker start dashboard-sql        # SQL Server با ConnectionString در appsettings.Development.json
dotnet run --project Dashboard.Web
```

اپ روی `http://localhost:5293` بالا می‌آید؛ سلامت آن از `/health` قابل بررسی است.

## استقرار با Docker

```bash
docker build -t dashboard-web .
docker run -d --name dashboard-web -p 8080:8080 \
  -e ConnectionStrings__Default="Server=HOST;Database=DashboardDb;User Id=sa;Password=***;TrustServerCertificate=True" \
  -e Store__WarehouseId=1 \
  -e Store__OnlinePaymentFinancialAccountId=1 \
  -e Store__OrderPaymentWindowHours=6 \
  -e ForwardedHeaders__TrustAllProxies=true \
  -e Zarinpal__MerchantId=*** \
  -v dashboard-dp-keys:/app/DataProtection-Keys \
  dashboard-web
```

نکات:

- همه‌ی تنظیمات حساس (ConnectionString، `Zarinpal:MerchantId`، `Sms:Kavenegar:ApiKey`، `Email:Smtp:Password`) را با متغیر محیطی تزریق کنید؛ در فایل‌های تنظیمات مقدار خالی است.
- ولوم `DataProtection-Keys` را حتماً mount کنید، وگرنه با هر ری‌استارت کانتینر همه‌ی کوکی‌های لاگین باطل می‌شوند.
- `Store__WarehouseId` باید انباری باشد که سفارش‌های آنلاین از آن تأمین/کسر می‌شوند؛ ویترین فروشگاه هم **موجودی قابل‌فروش** همان انبار را نشان می‌دهد.
- `ForwardedHeaders__TrustAllProxies=true` را **پشت reverse proxy حتماً روشن کنید**. بدون آن، `RemoteIpAddress` همهٔ کاربران IP پروکسی است و محدودیت نرخ (مثل «۳ درخواست کد ورود در ۵ دقیقه») عملاً برای کل سایت اعمال می‌شود. اگر اپ بدون پروکسی در معرض اینترنت نیست، همان `false` امن‌تر است.
- `Store__OnlinePaymentFinancialAccountId` اختیاری است: اگر ست شود، بعد از پرداخت موفق آنلاین **رسید دریافت خودکار** ثبت می‌شود (بدهکار آن صندوق/بانک، بستانکار حساب‌های دریافتنی). خالی یعنی رسیدها مثل قبل دستی در حسابداری ثبت شوند. دقت کنید: پس از فعال‌کردن، برای تسویهٔ درگاه به حساب بانکی نباید همان مبلغ را دوباره دستی رسید بزنید.
- `Store__OrderPaymentWindowHours` مهلت پرداخت سفارش است (پیش‌فرض ۶ ساعت). پس از آن، سفارش پرداخت‌نشده خودکار لغو و موجودیِ رزروشده‌اش آزاد می‌شود؛ سفارشی که پرداختِ تازه‌شروع‌شده دارد (کاربر روی درگاه است) تا ۳۰ دقیقه مصون از انقضا می‌ماند.
- مهاجرت‌های EF Core هنگام شروع اپ به‌صورت خودکار اعمال می‌شوند.

## انتشار بدون Docker

```bash
dotnet publish Dashboard.Web -c Release -o ./publish
```

## تست‌ها

```bash
# تست‌های واحد (بدون نیاز به دیتابیس)
dotnet test Dashboard.Tests

# تست‌های یکپارچگی — دیتابیس موقت واقعی می‌سازد و در پایان حذف می‌کند.
# اگر SQL Server در دسترس نباشد، تست‌ها Skip می‌شوند (نه Fail).
TEST_MSSQL_CONNECTION="Server=localhost,1433;User Id=sa;Password=***;TrustServerCertificate=True" \
  dotnet test Dashboard.IntegrationTests
```

CI (`.github/workflows/build.yml`) هر push/PR هر دو مجموعه را با سرویس SQL Server اجرا می‌کند.
