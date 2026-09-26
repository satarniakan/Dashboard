// Dashboard.Domain/Interfaces/ICartRepository.cs
using Dashboard.Domain.Entities;

namespace Dashboard.Domain.Interfaces;

public interface ICartRepository
{
    /// <summary>سبد با اقلام و کالاهایش؛ یافت نشدن یا منقضی‌شدن = null</summary>
    Task<Cart?> GetByCookieIdAsync(string cookieId);

    /// <summary>سبد موجود را می‌دهد وگرنه می‌سازد؛ ضمناً سبدهای منقضی‌شده‌ی قدیمی را پاک می‌کند</summary>
    Task<Cart> GetOrCreateAsync(string cookieId);

    Task UpdateAsync(Cart cart);

    /// <summary>حذف سبدِ منقضی از دیروز به قبل — فراخوانی دوره‌ای/فرصت‌طلبانه</summary>
    Task DeleteExpiredAsync();
}
