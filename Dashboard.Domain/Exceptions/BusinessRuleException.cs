namespace Dashboard.Domain.Exceptions;

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>
/// خطای فنی دیتابیس (تکراریِ یکتا، طول فیلد، و…) — نه قاعدهٔ کسب‌وکار.
/// جداسازی از <see cref="BusinessRuleException"/> عمدی است: فراخواننده نباید این را
/// «موجودی تمام شده» یا «درخواست نامعتبر» تفسیر کند و کاربر/سفارش را بر این اساس لغو کند.
/// </summary>
public class DataIntegrityException : Exception
{
    public DataIntegrityException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
