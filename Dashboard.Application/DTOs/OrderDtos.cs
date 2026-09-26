// Dashboard.Application/DTOs/OrderDtos.cs
using System.ComponentModel.DataAnnotations;
using Dashboard.Domain.Enums;

namespace Dashboard.Application.DTOs;

public class CheckoutDto
{
    [Required(ErrorMessage = "نام گیرنده الزامی است.")]
    [StringLength(150)]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "شماره موبایل الزامی است.")]
    [RegularExpression("^09\\d{9}$", ErrorMessage = "شماره موبایل معتبر نیست (مثال: 09123456789).")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "استان الزامی است.")]
    [StringLength(80)]
    public string Province { get; set; } = string.Empty;

    [Required(ErrorMessage = "شهر الزامی است.")]
    [StringLength(80)]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "نشانی کامل الزامی است.")]
    [StringLength(500)]
    public string AddressLine { get; set; } = string.Empty;

    [RegularExpression("^\\d{5,10}$", ErrorMessage = "کد پستی باید ۵ تا ۱۰ رقم باشد.")]
    public string? PostalCode { get; set; }

    [EnumDataType(typeof(ShippingMethod), ErrorMessage = "روش ارسال معتبر نیست.")]
    public ShippingMethod ShippingMethod { get; set; } = ShippingMethod.Post;
}

public record OrderItemDto(int ProductId, string ProductName, decimal UnitPrice, decimal Quantity, decimal LineTotal, string? ImageUrl);

public record OrderDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    string StatusText,
    string CustomerName,
    string CustomerPhone,
    string Province,
    string City,
    string AddressLine,
    string? PostalCode,
    string ShippingMethodText,
    decimal Subtotal,
    decimal DiscountAmount,
    string? DiscountCodeText,
    decimal ShippingCost,
    decimal Total,
    string? TrackingCode,
    string? AdminNote,
    DateTime CreatedAt,
    int? SalesInvoiceId,
    IReadOnlyList<OrderItemDto> Items);

public record OrderSummaryDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    string StatusText,
    string CustomerName,
    decimal Total,
    DateTime CreatedAt);
