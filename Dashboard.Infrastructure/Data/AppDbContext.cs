// Dashboard.Infrastructure/Data/AppDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Identity;

namespace Dashboard.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();

    // --- ماژول انبارداری (WMS) ---
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
    public DbSet<PurchaseReceiptItem> PurchaseReceiptItems => Set<PurchaseReceiptItem>();

    public DbSet<InternalIssue> InternalIssues => Set<InternalIssue>();
    public DbSet<InternalIssueItem> InternalIssueItems => Set<InternalIssueItem>();

    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();

    public DbSet<ScrapRecord> ScrapRecords => Set<ScrapRecord>();
    public DbSet<ScrapRecordItem> ScrapRecordItems => Set<ScrapRecordItem>();

    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();

    public DbSet<StockCount> StockCounts => Set<StockCount>();
    public DbSet<StockCountItem> StockCountItems => Set<StockCountItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<CustomerReceipt> CustomerReceipts => Set<CustomerReceipt>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<InstallmentPlan> InstallmentPlans => Set<InstallmentPlan>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<ProductVariantAttribute> ProductVariantAttributes => Set<ProductVariantAttribute>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<City> Cities => Set<City>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // required — sets up Identity's tables

        // ---------- Product ----------
        builder.Entity<Product>(e =>
        {
            e.Property(p => p.Price).HasColumnType("decimal(18,2)");
            e.Property(p => p.CostPrice).HasColumnType("decimal(18,2)");
            e.Property(p => p.Weight).HasColumnType("decimal(18,3)");
            e.Property(p => p.Length).HasColumnType("decimal(18,3)");
            e.Property(p => p.Width).HasColumnType("decimal(18,3)");
            e.Property(p => p.Height).HasColumnType("decimal(18,3)");
            e.Property(p => p.Sku).HasMaxLength(50).IsRequired();
            e.Property(p => p.Barcode).HasMaxLength(50);
            e.HasIndex(p => p.Sku).IsUnique();
            e.HasIndex(p => p.Barcode).IsUnique().HasFilter("[Barcode] IS NOT NULL");
            e.Property(p => p.ImageUrl).HasMaxLength(500);
            e.Property(p => p.Slug).HasMaxLength(150);
            e.HasIndex(p => p.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL");
            e.Property(p => p.HtmlDescription);
            e.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        });

        // ---------- Cart (سبد خرید فروشگاه) ----------
        builder.Entity<Cart>(e =>
        {
            e.Property(c => c.CookieId).HasMaxLength(64).IsRequired();
            e.HasIndex(c => c.CookieId).IsUnique();
            e.HasIndex(c => c.ExpiresAt);
        });

        builder.Entity<CartItem>(e =>
        {
            e.Property(i => i.Quantity).HasColumnType("decimal(18,3)");
            e.HasOne(i => i.Cart).WithMany(c => c.Items).HasForeignKey(i => i.CartId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(i => new { i.CartId, i.ProductId }).IsUnique();
        });

        builder.Entity<DiscountCode>(e =>
        {
            e.Property(d => d.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(d => d.Code).IsUnique();
            e.Property(d => d.Value).HasColumnType("decimal(18,2)");
            e.Property(d => d.MaxDiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(d => d.MinCartAmount).HasColumnType("decimal(18,2)");
        });

        // کد تخفیف اعمال‌شده روی سبد — حذف کد نباید سبد را حذف کند
        builder.Entity<Cart>(e =>
        {
            e.HasOne(c => c.DiscountCode).WithMany().HasForeignKey(c => c.DiscountCodeId).OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- Order (سفارش فروشگاه) ----------
        builder.Entity<Order>(e =>
        {
            e.Property(o => o.OrderNumber).HasMaxLength(40).IsRequired();
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.Property(o => o.CustomerName).HasMaxLength(150).IsRequired();
            e.Property(o => o.CustomerPhone).HasMaxLength(20).IsRequired();
            e.Property(o => o.Province).HasMaxLength(80);
            e.Property(o => o.City).HasMaxLength(80);
            e.Property(o => o.AddressLine).HasMaxLength(500);
            e.Property(o => o.PostalCode).HasMaxLength(20);
            e.Property(o => o.TrackingCode).HasMaxLength(50);
            e.Property(o => o.AdminNote).HasMaxLength(500);
            e.Property(o => o.DiscountCodeText).HasMaxLength(50);
            e.Property(o => o.Subtotal).HasColumnType("decimal(18,2)");
            e.Property(o => o.DiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(o => o.ShippingCost).HasColumnType("decimal(18,2)");
            e.HasIndex(o => new { o.UserId, o.Status });
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.ProductName).HasMaxLength(150).IsRequired();
            e.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            e.Property(i => i.Quantity).HasColumnType("decimal(18,3)");
            e.HasOne(i => i.Order).WithMany(o => o.Items).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OutboxMessage>(e =>
        {
            e.Property(o => o.Recipient).HasMaxLength(200).IsRequired();
            e.Property(o => o.Body).HasMaxLength(1000).IsRequired();
            e.Property(o => o.LastError).HasMaxLength(500);
            e.Property(o => o.Subject).HasMaxLength(200);
            e.HasIndex(o => new { o.Status, o.Attempts });
        });

        builder.Entity<Notification>(e =>
        {
            e.Property(n => n.UserId).HasMaxLength(450).IsRequired();
            e.Property(n => n.Title).HasMaxLength(200).IsRequired();
            e.Property(n => n.Body).HasMaxLength(1000);
            e.Property(n => n.LinkUrl).HasMaxLength(300);
            e.HasIndex(n => new { n.UserId, n.IsRead });
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(p => p.Authority).HasMaxLength(100).IsRequired();
            e.HasIndex(p => p.Authority).IsUnique().HasFilter("[Authority] <> ''");
            e.Property(p => p.RefId).HasMaxLength(100);
            e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            e.HasOne(p => p.Order).WithMany(o => o.Payments).HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Warehouse / Supplier ----------
        builder.Entity<Warehouse>(e =>
        {
            e.Property(w => w.Name).HasMaxLength(150).IsRequired();
            e.HasIndex(w => w.Name).IsUnique();
        });

        builder.Entity<Supplier>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(150).IsRequired();
        });

        // ---------- StockLevel ----------
        builder.Entity<StockLevel>(e =>
        {
            e.Property(s => s.QuantityOnHand).HasColumnType("decimal(18,3)");
            e.HasIndex(s => new { s.ProductId, s.WarehouseId }).IsUnique();
            e.Property(s => s.RowVersion).IsRowVersion(); // توکن همزمانی خوش‌بینانه برای جلوگیری از race condition در موجودی
            // آخرین لایه‌ی دفاع در برابر موجودی منفی — حتی اگر کد برنامه دچار باگ شود، دیتابیس رد می‌کند
            e.ToTable(t => t.HasCheckConstraint("CK_StockLevels_NonNegative", "[QuantityOnHand] >= 0"));

            e.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Warehouse).WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- StockTransaction (ledger) ----------
        builder.Entity<StockTransaction>(e =>
        {
            e.Property(s => s.QuantityChange).HasColumnType("decimal(18,3)");
            e.Property(s => s.UnitCost).HasColumnType("decimal(18,2)");
            e.Property(s => s.Type).HasConversion<string>().HasMaxLength(30);

            e.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Warehouse).WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(s => new { s.ProductId, s.WarehouseId });
            e.HasIndex(s => s.OccurredAt);
        });

        // ---------- PurchaseReceipt ----------
        builder.Entity<PurchaseReceipt>(e =>
        {
            e.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Warehouse).WithMany().HasForeignKey(p => p.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Items).WithOne(i => i.PurchaseReceipt!).HasForeignKey(i => i.PurchaseReceiptId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PurchaseReceiptItem>(e =>
        {
            e.Property(i => i.Quantity).HasColumnType("decimal(18,3)");
            e.Property(i => i.UnitCost).HasColumnType("decimal(18,2)");
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- InternalIssue ----------
        builder.Entity<InternalIssue>(e =>
        {
            e.HasOne(p => p.Warehouse).WithMany().HasForeignKey(p => p.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Items).WithOne(i => i.InternalIssue!).HasForeignKey(i => i.InternalIssueId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<InternalIssueItem>(e =>
        {
            e.Property(i => i.Quantity).HasColumnType("decimal(18,3)");
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- SalesReturn ----------
        builder.Entity<SalesReturn>(e =>
        {
            e.HasOne(p => p.Warehouse).WithMany().HasForeignKey(p => p.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Items).WithOne(i => i.SalesReturn!).HasForeignKey(i => i.SalesReturnId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SalesReturnItem>(e =>
        {
            e.Property(i => i.Quantity).HasColumnType("decimal(18,3)");
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- ScrapRecord ----------
        builder.Entity<ScrapRecord>(e =>
        {
            e.HasOne(p => p.Warehouse).WithMany().HasForeignKey(p => p.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(p => p.Items).WithOne(i => i.ScrapRecord!).HasForeignKey(i => i.ScrapRecordId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ScrapRecordItem>(e =>
        {
            e.Property(i => i.Quantity).HasColumnType("decimal(18,3)");
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- StockTransfer ----------
        builder.Entity<StockTransfer>(e =>
        {
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(t => t.SourceWarehouse).WithMany().HasForeignKey(t => t.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.DestinationWarehouse).WithMany().HasForeignKey(t => t.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(t => t.Items).WithOne(i => i.StockTransfer!).HasForeignKey(i => i.StockTransferId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<StockTransferItem>(e =>
        {
            e.Property(i => i.Quantity).HasColumnType("decimal(18,3)");
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- StockCount ----------
        builder.Entity<StockCount>(e =>
        {
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

            e.HasOne(c => c.Warehouse).WithMany().HasForeignKey(c => c.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(c => c.Items).WithOne(i => i.StockCount!).HasForeignKey(i => i.StockCountId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<StockCountItem>(e =>
        {
            e.Property(i => i.SystemQuantity).HasColumnType("decimal(18,3)");
            e.Property(i => i.CountedQuantity).HasColumnType("decimal(18,3)");
            e.Ignore(i => i.Discrepancy); // محاسبه‌شده در حافظه، ستون دیتابیس نمی‌خواهد
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        // ---------- Customer ----------
        builder.Entity<Customer>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(150).IsRequired();
            e.Property(c => c.Phone).HasMaxLength(20);
            // جست‌وجوی مشتری با شماره تلفن در ثبت سفارش فروشگاه انجام می‌شود؛
            // بدون این ایندکس، درخواست‌های همزمان می‌توانند مشتری تکراری بسازند.
            e.HasIndex(c => c.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
        });

        // ---------- SalesInvoice ----------
        builder.Entity<SalesInvoice>(e =>
        {
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.DiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(s => s.ShippingAmount).HasColumnType("decimal(18,2)");
            e.Property(s => s.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(s => s.InvoiceNumber).HasMaxLength(50).IsRequired();
            e.HasIndex(s => s.InvoiceNumber).IsUnique();

            e.HasOne(s => s.Customer).WithMany().HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Warehouse).WithMany().HasForeignKey(s => s.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(s => s.Items).WithOne(i => i.SalesInvoice!).HasForeignKey(i => i.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SalesInvoiceItem>(e =>
        {
            e.Property(i => i.Quantity).HasColumnType("decimal(18,3)");
            e.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            e.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
        // ---------- Account ----------
        builder.Entity<Account>(e =>
        {
            e.Property(a => a.Code).HasMaxLength(20).IsRequired();
            e.Property(a => a.Name).HasMaxLength(150).IsRequired();
            e.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(a => a.Code).IsUnique();
            e.HasOne(a => a.ParentAccount).WithMany().HasForeignKey(a => a.ParentAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- JournalEntry ----------
        builder.Entity<JournalEntry>(e =>
        {
            e.Property(j => j.EntryNumber).HasMaxLength(50).IsRequired();
            e.HasIndex(j => j.EntryNumber).IsUnique();
            e.HasMany(j => j.Lines).WithOne(l => l.JournalEntry!).HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<JournalEntryLine>(e =>
        {
            e.Property(l => l.DebitAmount).HasColumnType("decimal(18,2)");
            e.Property(l => l.CreditAmount).HasColumnType("decimal(18,2)");
            e.HasOne(l => l.Account).WithMany().HasForeignKey(l => l.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => new { l.SubsidiaryType, l.SubsidiaryId }); // برای سرعت گزارش گردش حساب
        });
        // ---------- FinancialAccount ----------
        builder.Entity<FinancialAccount>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(150).IsRequired();
            e.Property(f => f.Type).HasConversion<string>().HasMaxLength(20);
            e.HasOne(f => f.Account).WithMany().HasForeignKey(f => f.AccountId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- CustomerReceipt ----------
        builder.Entity<CustomerReceipt>(e =>
        {
            e.Property(r => r.Amount).HasColumnType("decimal(18,2)");
            e.Property(r => r.Method).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.ReceiptNumber).HasMaxLength(50).IsRequired();
            e.HasIndex(r => r.ReceiptNumber).IsUnique();
            e.HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.FinancialAccount).WithMany().HasForeignKey(r => r.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Installment).WithMany().HasForeignKey(r => r.InstallmentId).OnDelete(DeleteBehavior.Restrict);

        });

        // ---------- SupplierPayment ----------
        builder.Entity<SupplierPayment>(e =>
        {
            e.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            e.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
            e.Property(p => p.PaymentNumber).HasMaxLength(50).IsRequired();
            e.HasIndex(p => p.PaymentNumber).IsUnique();
            e.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.FinancialAccount).WithMany().HasForeignKey(p => p.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        // ---------- InstallmentPlan ----------
        builder.Entity<InstallmentPlan>(e =>
        {
            e.HasOne(p => p.SalesInvoice).WithMany().HasForeignKey(p => p.SalesInvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.SalesInvoiceId).IsUnique(); // هر فاکتور فقط یک طرح اقساط
            e.HasMany(p => p.Installments).WithOne(i => i.InstallmentPlan!).HasForeignKey(i => i.InstallmentPlanId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Installment>(e =>
        {
            e.Property(i => i.Amount).HasColumnType("decimal(18,2)");
            e.Property(i => i.PaidAmount).HasColumnType("decimal(18,2)");
            e.Ignore(i => i.IsFullyPaid);
            e.Ignore(i => i.IsOverdue);
        });
        // ---------- Category ----------
        builder.Entity<Category>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(150).IsRequired();
            e.Property(c => c.Slug).HasMaxLength(150).IsRequired();
            e.HasIndex(c => c.Slug).IsUnique();
            e.HasOne(c => c.ParentCategory).WithMany().HasForeignKey(c => c.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- ProductGroup ----------
        builder.Entity<ProductGroup>(e =>
        {
            e.Property(g => g.Name).HasMaxLength(200).IsRequired();
            e.Property(g => g.Slug).HasMaxLength(200).IsRequired();
            e.HasIndex(g => g.Slug).IsUnique();
            e.HasOne(g => g.Category).WithMany().HasForeignKey(g => g.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(g => g.Variants).WithOne(p => p.ProductGroup).HasForeignKey(p => p.ProductGroupId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(g => g.Images).WithOne(i => i.ProductGroup).HasForeignKey(i => i.ProductGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- ProductAttribute / Value ----------
        builder.Entity<ProductAttribute>(e =>
        {
            e.Property(a => a.Name).HasMaxLength(100).IsRequired();
            e.HasMany(a => a.Values).WithOne(v => v.ProductAttribute).HasForeignKey(v => v.ProductAttributeId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ProductAttributeValue>(e =>
        {
            e.Property(v => v.Value).HasMaxLength(100).IsRequired();
        });

        // ---------- ProductVariantAttribute ----------
        builder.Entity<ProductVariantAttribute>(e =>
        {
            e.HasOne(pva => pva.Product).WithMany().HasForeignKey(pva => pva.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pva => pva.ProductAttributeValue).WithMany().HasForeignKey(pva => pva.ProductAttributeValueId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(pva => new { pva.ProductId, pva.ProductAttributeValueId }).IsUnique();
        });

        // ---------- ProductImage ----------
        builder.Entity<ProductImage>(e =>
        {
            e.Property(i => i.Url).HasMaxLength(500).IsRequired();
        });

        // ---------- Unit (واحد شمارش) ----------
        builder.Entity<Unit>(e =>
        {
            e.Property(u => u.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(u => u.Name).IsUnique();
        });


        builder.Entity<Domain.Identity.ApplicationUser>(e =>
        {
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            //e.Property(u => u.Province).HasMaxLength(100);
            //e.Property(u => u.City).HasMaxLength(100);
            e.Property(u => u.Address).HasMaxLength(300);
            e.HasOne(u => u.Province).WithMany().HasForeignKey(u => u.ProvinceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(u => u.City).WithMany().HasForeignKey(u => u.CityId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Province>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
        });
        builder.Entity<City>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.HasOne(c => c.Province).WithMany(p => p.Cities).HasForeignKey(c => c.ProvinceId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => c.ProvinceId);
        });
  
    }
}
