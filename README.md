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
  -e Zarinpal__MerchantId=*** \
  -v dashboard-dp-keys:/app/DataProtection-Keys \
  dashboard-web
```

نکات:

- همه‌ی تنظیمات حساس (ConnectionString، `Zarinpal:MerchantId`، `Sms:Kavenegar:ApiKey`، `Email:Smtp:Password`) را با متغیر محیطی تزریق کنید؛ در فایل‌های تنظیمات مقدار خالی است.
- ولوم `DataProtection-Keys` را حتماً mount کنید، وگرنه با هر ری‌استارت کانتینر همه‌ی کوکی‌های لاگین باطل می‌شوند.
- `Store__WarehouseId` باید انباری باشد که سفارش‌های آنلاین از آن تأمین/کسر می‌شوند؛ ویترین فروشگاه هم موجودی همان انبار را نشان می‌دهد.
- مهاجرت‌های EF Core هنگام شروع اپ به‌صورت خودکار اعمال می‌شوند.

## انتشار بدون Docker

```bash
dotnet publish Dashboard.Web -c Release -o ./publish
```
