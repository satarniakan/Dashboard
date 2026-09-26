// Dashboard.Infrastructure/Repositories/CartRepository.cs
using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Infrastructure.Data;

namespace Dashboard.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly AppDbContext _context;

    public CartRepository(AppDbContext context) => _context = context;

    public async Task<Cart?> GetByCookieIdAsync(string cookieId)
    {
        var cart = await _context.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CookieId == cookieId);

        if (cart is not null && cart.ExpiresAt < DateTime.UtcNow)
            return null; // منقضی — عملاً سبد تازه شروع می‌شود

        return cart;
    }

    public async Task<Cart> GetOrCreateAsync(string cookieId)
    {
        // پاک‌سازی فرصت‌طلبانه‌ی سبدهای منقضی (کوئری سبک؛ در نبود سبد منقضی هزینه‌ای ندارد)
        var expired = await _context.Carts.Where(c => c.ExpiresAt < DateTime.UtcNow).ToListAsync();
        if (expired.Count > 0)
            _context.Carts.RemoveRange(expired);

        var cart = await _context.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.CookieId == cookieId);
        if (cart is null)
        {
            cart = new Cart { CookieId = cookieId, ExpiresAt = DateTime.UtcNow.AddDays(1) };
            await _context.Carts.AddAsync(cart);
        }
        else
        {
            // هر دسترسی، انقضای سبد را تمدید می‌کند
            cart.ExpiresAt = DateTime.UtcNow.AddDays(1);
        }

        return cart;
    }

    public async Task UpdateAsync(Cart cart)
    {
        // سبد همیشه tracked است (همان Context درخواست)؛ Update() روی انتیتی تازه‌ساخته (Added)
        // با کلید موقت کرش می‌کند. SaveChanges در CompleteAsync همه‌چیز را ذخیره می‌کند.
        if (_context.Entry(cart).State == EntityState.Detached)
            _context.Carts.Update(cart);
        await Task.CompletedTask;
    }

    public async Task DeleteExpiredAsync()
    {
        var expired = await _context.Carts.Where(c => c.ExpiresAt < DateTime.UtcNow).ToListAsync();
        _context.Carts.RemoveRange(expired);
        await Task.CompletedTask;
    }
}
