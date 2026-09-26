# نقشه راه: سیستم Notification و ارسال پیام

زیرساخت موجود: `ISmsSender` + `FakeSmsSender` (dev)، `ToastService`، مدارهای InteractiveServer، نقاط تحقیق آماده (ثبت سفارش، تغییر وضعیت، تأیید فاکتور).

## فاز ۱ — هسته‌ی Notification ✅
- [x] انتیتی `Notification` (UserId/Title/Body/Type/LinkUrl/IsRead/ReadAt)
- [x] `INotificationRepository` + `INotificationService` + DTO ها
- [x] تریگرها: پرداخت موفق سفارش → اعلان مشتری؛ تغییر وضعیت ادمین → اعلان وضعیت جدید (با کد رهگیری)

## فاز ۲ — UI اعلان‌ها ✅
- [x] Endpoint های سبک JSON (`GET /notifications/recent`، `POST /notifications/{id}/read`، `POST /notifications/read-all`)
- [x] جزیره‌ی تعاملی `NotificationBell` در MainLayout (زنگ + شمارنده + دراپ‌داون) با **polling ۳۰ ثانیه‌ای از طریق fetch**
- [x] صفحه‌ی آرشیو `/notifications`
- تصمیم معماری مهم: bell و صفحات، داده را با **fetch** (scope جدا) می‌گیرند نه سرویس مدار — تا هم‌روندی DbContext (درس سبد خرید) تکرار نشود

## فاز ۳ — پیامک واقعی با Outbox ✅
- [x] جدول `SmsOutbox` + ریپو + `ISmsOutboxService.QueueAsync` (ثبت سریع و تضمینی)
- [x] `SmsOutboxProcessor` (BackgroundService هر ۳۰ ثانیه): ارسال با ISmsSender، retry تا سقف `Sms:MaxAttempts`، بعدش Failed دائمی
- [x] `KavenegarSmsSender` — انتخاب Provider از `Sms:Provider` (Fake پیش‌فرض dev)؛ ApiKey در User Secrets
- [x] OrderService → پیامک سفارش از مسیر صف (اختلال پیامک، ثبت سفارش را نمی‌شکند)
- نکته: OTP عمداً از صف رد نمی‌شود (کاربر منتظر است) و مستقیم می‌رود
- [ ] قالب‌های پیام بیشتر: ارسال+کد رهگیری (تغییر وضعیت ادمین فعلاً فقط Notification است؛ می‌تواند SMS هم شود)

## فاز ۴ — اضافه‌ها ✅
- [x] **اعلان سراسری ادمین** — `/admin/broadcast` (همه‌ی کاربران یا اعضای یک نقش؛ ارسال دسته‌ای در یک Save)
- [x] **ایمیل روی همان Outbox** — `OutboxMessage` عمومی شد (کانال Sms/Email + Subject)؛ `SmtpEmailSender` با SMTP داخلی .NET (بدون پکیج بیرونی؛ MailKit پیشنهاد ارتقای آینده)؛ `OutboxProcessor` دو-کاناله با retry و Failed دائمی
- [ ] تنظیمات اعلان هر کاربر (بی‌صدا کردن نوع‌ها)

## تنظیمات فعال‌سازی
```
Sms:Provider = Kavenegar | Fake          # پیامک
Sms:Kavenegar:ApiKey / Sender             # در User Secrets
Email:Smtp:Host / Port / Username / Password / FromAddress / FromName   # ایمیل
```
Host ایمیل خالی باشد → پیام در Outbox با خطای روشن Failed می‌شود (گم نمی‌شود).
