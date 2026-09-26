// Dashboard.Application/Services/CartService.cs
using Dashboard.Application.DTOs;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface ICartService
{
    /// <summary>سبد خرید؛ سبد ناموجود = سبد خالی</summary>
    Task<CartDto> GetCartAsync(string cookieId);

    /// <summary>تعداد کل اقلام سبد — برای شمارنده‌ی هدر</summary>
    Task<int> GetItemCountAsync(string cookieId);

    /// <summary>افزودن کالا؛ مقدار درخواستی به موجودیِ قابل فروش محدود (clamp) می‌شود</summary>
    Task<CartOperationResult> AddToCartAsync(string cookieId, int productId, decimal quantity);

    Task<CartOperationResult> UpdateQuantityAsync(string cookieId, int itemId, decimal quantity);

    Task<CartOperationResult> RemoveItemAsync(string cookieId, int itemId);

    /// <summary>اعمال کد تخفیف روی سبد — قواعد کد همین‌جا اعتبارسنجی می‌شود؛ مصرف نهایی هنگام ثبت سفارش شمرده می‌شود</summary>
    Task<CartOperationResult> ApplyDiscountCodeAsync(string cookieId, string code);

    Task<CartOperationResult> RemoveDiscountCodeAsync(string cookieId);

    /// <summary>خالی‌کردن کامل سبد — پس از ثبت سفارش موفق فراخوانی می‌شود</summary>
    Task ClearCartAsync(string cookieId);
}

public class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly StoreOptions _store;

    public CartService(IUnitOfWork unitOfWork, Microsoft.Extensions.Options.IOptions<StoreOptions> storeOptions)
    {
        _unitOfWork = unitOfWork;
        _store = storeOptions.Value;
    }

    public async Task<CartDto> GetCartAsync(string cookieId)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        if (cart is null || cart.Items.Count == 0)
            return new CartDto(Array.Empty<CartLineDto>(), 0, 0m);

        return await BuildCartDtoAsync(cart);
    }

    public async Task<int> GetItemCountAsync(string cookieId)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        return cart is null ? 0 : (int)cart.Items.Sum(i => i.Quantity);
    }

    public async Task<CartOperationResult> AddToCartAsync(string cookieId, int productId, decimal quantity)
    {
        if (quantity <= 0)
            return new CartOperationResult(false, "تعداد باید بیشتر از صفر باشد.");

        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product is null || !product.IsPublished)
            return new CartOperationResult(false, "این کالا در فروشگاه موجود نیست.");

        // موجودی انبار فروشگاه — سفارش‌ها فقط از همین انبار کسر می‌شوند
        var stock = await _unitOfWork.StockLevels.GetWarehouseStockAsync(new[] { productId }, _store.WarehouseId);
        var available = stock.TryGetValue(productId, out var s) ? s : 0;
        if (available <= 0)
            return new CartOperationResult(false, $"«{product.Name}» فعلاً ناموجود است.");

        var cart = await _unitOfWork.Carts.GetOrCreateAsync(cookieId);

        var existing = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        var currentQty = existing?.Quantity ?? 0;

        // سقف: موجودی قابل فروش منفی مقدار قبلاً اضافه‌شده در سبد
        var allowed = Math.Min(quantity, Math.Max(0, available - currentQty));
        if (allowed <= 0)
            return new CartOperationResult(false, $"همه‌ی موجودی «{product.Name}» (حداکثر {available:0.##} {product.Unit}) قبلاً در سبد شماست.");

        if (allowed < quantity)
        {
            if (existing is not null) existing.Quantity += allowed;
            else cart.Items.Add(new CartItem { ProductId = productId, Quantity = allowed });
            await _unitOfWork.Carts.UpdateAsync(cart);
            await _unitOfWork.CompleteAsync();
            return new CartOperationResult(true, $"فقط {allowed:0.##} {product.Unit} از «{product.Name}» موجود بود؛ همین مقدار به سبد اضافه شد.");
        }

        if (existing is not null) existing.Quantity += quantity;
        else cart.Items.Add(new CartItem { ProductId = productId, Quantity = quantity });

        await _unitOfWork.Carts.UpdateAsync(cart);
        await _unitOfWork.CompleteAsync();
        return new CartOperationResult(true, $"«{product.Name}» به سبد خرید اضافه شد.");
    }

    public async Task<CartOperationResult> UpdateQuantityAsync(string cookieId, int itemId, decimal quantity)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        var item = cart?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return new CartOperationResult(false, "این قلم در سبد شما یافت نشد.");

        if (quantity <= 0)
        {
            cart!.Items.Remove(item);
            await _unitOfWork.Carts.UpdateAsync(cart);
            await _unitOfWork.CompleteAsync();
            return new CartOperationResult(true);
        }

        var stock = await _unitOfWork.StockLevels.GetWarehouseStockAsync(new[] { item.ProductId }, _store.WarehouseId);
        var available = stock.TryGetValue(item.ProductId, out var s) ? s : 0;

        // موجودی تمام شده — قلم به‌جای ماندن با تعداد صفر، از سبد حذف می‌شود
        if (available <= 0)
        {
            cart!.Items.Remove(item);
            await _unitOfWork.Carts.UpdateAsync(cart);
            await _unitOfWork.CompleteAsync();
            return new CartOperationResult(true, "موجودی این کالا تمام شد و از سبد حذف گردید.");
        }

        item.Quantity = Math.Min(quantity, available);
        await _unitOfWork.Carts.UpdateAsync(cart!);
        await _unitOfWork.CompleteAsync();

        return item.Quantity < quantity
            ? new CartOperationResult(true, $"موجودی این کالا فقط تا {available:0.##} {item.Product?.Unit} است.")
            : new CartOperationResult(true);
    }

    public async Task<CartOperationResult> RemoveItemAsync(string cookieId, int itemId)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        var item = cart?.Items.FirstOrDefault(i => i.Id == itemId);
        if (item is null)
            return new CartOperationResult(false, "این قلم در سبد شما یافت نشد.");

        cart!.Items.Remove(item);
        await _unitOfWork.Carts.UpdateAsync(cart);
        await _unitOfWork.CompleteAsync();
        return new CartOperationResult(true);
    }

    public async Task<CartOperationResult> ApplyDiscountCodeAsync(string cookieId, string code)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        if (cart is null || cart.Items.Count == 0)
            return new CartOperationResult(false, "برای استفاده از کد تخفیف، اول کالایی به سبد اضافه کنید.");

        var discountCode = await _unitOfWork.DiscountCodes.GetByCodeAsync(code);
        if (discountCode is null)
            return new CartOperationResult(false, "کد تخفیف یافت نشد.");

        var subtotal = await GetCartSubtotalAsync(cart);
        var validationError = ValidateDiscountCode(discountCode, subtotal);
        if (validationError is not null)
            return new CartOperationResult(false, validationError);

        cart.DiscountCodeId = discountCode.Id;
        await _unitOfWork.Carts.UpdateAsync(cart);
        await _unitOfWork.CompleteAsync();

        var amount = discountCode.CalculateDiscount(subtotal);
        return new CartOperationResult(true, $"کد تخفیف «{discountCode.Code}» اعمال شد — {amount:0} تومان تخفیف.");
    }

    public async Task<CartOperationResult> RemoveDiscountCodeAsync(string cookieId)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        if (cart is null || cart.DiscountCodeId is null)
            return new CartOperationResult(true);

        cart.DiscountCodeId = null;
        await _unitOfWork.Carts.UpdateAsync(cart);
        await _unitOfWork.CompleteAsync();
        return new CartOperationResult(true, "کد تخفیف حذف شد.");
    }

    // جمع سبد قبل از تخفیف
    private async Task<decimal> GetCartSubtotalAsync(Cart cart)
    {
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = (await _unitOfWork.Products.GetByIdsAsync(productIds))
            .ToDictionary(p => p.Id);
        return cart.Items.Sum(i => i.Quantity * (products.GetValueOrDefault(i.ProductId)?.Price ?? 0));
    }

    private static string? ValidateDiscountCode(DiscountCode code, decimal subtotal)
    {
        if (!code.IsValidNow(DateTime.UtcNow))
        {
            if (!code.IsActive) return "این کد تخفیف غیرفعال است.";
            return "این کد تخفیف خارج از بازه‌ی زمانی مجاز است یا سقف مصرفش پر شده است.";
        }

        if (code.MinCartAmount is not null && subtotal < code.MinCartAmount.Value)
            return $"این کد فقط برای سبد حداقل {code.MinCartAmount.Value:0} تومان قابل استفاده است.";

        return null;
    }

    public async Task ClearCartAsync(string cookieId)
    {
        var cart = await _unitOfWork.Carts.GetByCookieIdAsync(cookieId);
        if (cart is null || cart.Items.Count == 0) return;

        cart.Items.Clear();
        cart.DiscountCodeId = null;
        await _unitOfWork.Carts.UpdateAsync(cart);
        await _unitOfWork.CompleteAsync();
    }

    private async Task<CartDto> BuildCartDtoAsync(Cart cart)
    {
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var stock = await _unitOfWork.StockLevels.GetWarehouseStockAsync(productIds, _store.WarehouseId);
        var products = (await _unitOfWork.Products.GetByIdsAsync(productIds))
            .ToDictionary(p => p.Id);

        var lines = cart.Items.Select(i =>
        {
            var p = products.GetValueOrDefault(i.ProductId);
            var price = p?.Price ?? 0;
            var available = stock.TryGetValue(i.ProductId, out var s) ? s : 0;
            return new CartLineDto(
                i.Id, i.ProductId,
                p?.Name ?? "کالای حذف‌شده",
                p?.Slug ?? string.Empty,
                p?.Unit ?? "عدد",
                price, p?.ImageUrl,
                i.Quantity, i.Quantity * price, available);
        }).ToList();

        var totalAmount = lines.Sum(l => l.LineTotal);

        // کد تخفیف اعمال‌شده — اعتبارسنجی مجدد هنگام نمایش
        string? discountCodeText = null;
        decimal discountAmount = 0m;
        if (cart.DiscountCodeId is not null)
        {
            var code = await _unitOfWork.DiscountCodes.GetByIdAsync(cart.DiscountCodeId.Value);
            if (code is not null && code.IsValidNow(DateTime.UtcNow) && ValidateDiscountCode(code, totalAmount) is null)
            {
                discountCodeText = code.Code;
                discountAmount = code.CalculateDiscount(totalAmount);
            }
        }

        return new CartDto(
            lines,
            (int)lines.Sum(l => l.Quantity),
            totalAmount,
            discountCodeText,
            discountAmount,
            totalAmount - discountAmount);
    }
}
