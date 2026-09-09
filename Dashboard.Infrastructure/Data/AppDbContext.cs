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
        });

        // ---------- SalesInvoice ----------
        builder.Entity<SalesInvoice>(e =>
        {
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(s => s.DiscountAmount).HasColumnType("decimal(18,2)");
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
    }
}
