using Dashboard.Domain.Exceptions;

namespace Dashboard.Web.Components.Shared;

public static class ErrorMessageHelper
{
    // اگر خطا از نوع BusinessRuleException یا NotFoundException بود، پیامش امن است و مستقیم نشان می‌دهیم.
    // در غیر این صورت (خطای فنی/غیرمنتظره) یک پیام عمومی نشان می‌دهیم تا جزئیات فنی لو نرود.
    public static string ToUserMessage(Exception ex) => ex switch
    {
        BusinessRuleException businessEx => businessEx.Message,
        NotFoundException notFoundEx => notFoundEx.Message,
        // نکته: این پروژه فعلاً برای خطاهای کسب‌وکاری از InvalidOperationException هم استفاده می‌کند
        // (مثلاً در SalesService). چون پیام‌هایش دستی و فارسی نوشته شده، فعلاً امن در نظر می‌گیریم.
        // در آینده بهتر است این‌ها هم به BusinessRuleException تبدیل شوند.
        InvalidOperationException invalidOpEx => invalidOpEx.Message,
        _ => "خطای غیرمنتظره‌ای رخ داد. لطفاً دوباره تلاش کنید یا با پشتیبانی تماس بگیرید."
    };
}