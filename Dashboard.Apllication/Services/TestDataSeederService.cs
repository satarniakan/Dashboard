using Microsoft.Extensions.Logging;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Interfaces;
using Dashboard.Application.DTOs;

namespace Dashboard.Application.Services;

public interface ITestDataSeederService
{
    /// <summary>
    /// آیا دیتابیس همین الان خالیه؟ (یعنی هنوز هیچ کالایی ثبت نشده)
    /// از این برای نشون‌دادن/قایم‌کردن دکمه‌ی «تولید دیتای تستی» توی صفحه استفاده می‌کنیم.
    /// </summary>
    Task<bool> IsDatabaseEmptyAsync();

    /// <summary>
    /// در تمام جدول‌های اصلی، ۱۰ رکورد نمونه می‌سازد — از طریق همون سرویس‌های واقعی برنامه
    /// (نه insert مستقیم به دیتابیس)، تا موجودی انبار و سند‌های حسابداری هم درست و هماهنگ بمانند.
    /// اگر دیتابیس از قبل خالی نباشد، برای جلوگیری از دوباره‌کاری/دیتای تکراری، کاری نمی‌کند.
    /// </summary>
    Task SeedAsync(string? userId);
}

public class TestDataSeederService : ITestDataSeederService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockService _stockService;
    private readonly ISalesService _salesService;
    private readonly ITreasuryService _treasuryService;
    private readonly IInstallmentService _installmentService;
    private readonly ILogger<TestDataSeederService> _logger;

    public TestDataSeederService(
        IUnitOfWork unitOfWork,
        IStockService stockService,
        ISalesService salesService,
        ITreasuryService treasuryService,
        IInstallmentService installmentService,
        ILogger<TestDataSeederService> logger)
    {
        _unitOfWork = unitOfWork;
        _stockService = stockService;
        _salesService = salesService;
        _treasuryService = treasuryService;
        _installmentService = installmentService;
        _logger = logger;
    }

    public async Task<bool> IsDatabaseEmptyAsync()
    {
        var products = await _unitOfWork.Products.GetAllAsync();
        return !products.Any();
    }

    public async Task SeedAsync(string? userId)
    {
        if (!await IsDatabaseEmptyAsync())
        {
            _logger.LogWarning("Seed skipped: database already has products.");
            return;
        }

        // همه‌ی این عملیات داخل یک تراکنش واحدند: یا همه‌شون با موفقیت ثبت می‌شن،
        // یا اگه وسط کار به هر مشکلی خوردیم، هیچ‌کدوم ذخیره نمی‌شه — تا دیگه هیچ‌وقت
        // یه‌سری رکورد نصفه‌کاره (مثلاً چندتا انبار بدون بقیه‌ی داده‌ها) توی دیتابیس نمونه.
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await SeedInternalAsync(userId);
            await _unitOfWork.CommitTransactionAsync();
            _logger.LogInformation("Demo data seed completed successfully.");
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task SeedInternalAsync(string? userId)
    {
        // ---------- ۱. انبارها ----------
        string[] warehouseNames = { "انبار مرکزی", "انبار تهران", "انبار مشهد", "انبار اصفهان", "انبار شیراز",
            "انبار تبریز", "انبار اهواز", "انبار کرج", "انبار قم", "انبار یزد" };

        var warehouses = new List<WarehouseDto>();
        for (var i = 0; i < 10; i++)
        {
            var w = await _stockService.CreateWarehouseAsync(new CreateWarehouseDto
            {
                Name = warehouseNames[i],
                Code = $"WH-{i + 1:00}",
                Address = $"آدرس نمونه‌ی {warehouseNames[i]}"
            });
            warehouses.Add(w);
        }

        // ---------- ۲. تأمین‌کنندگان ----------
        string[] supplierNames = { "بازرگانی پارس", "شرکت آریا تجارت", "گروه صنعتی البرز", "پخش ایران‌کالا",
            "بازرگانی خلیج فارس", "شرکت تدارکات نوین", "گروه بازرگانی سبز", "پخش کوثر", "بازرگانی مهر", "شرکت راهبرد تجارت" };

        var suppliers = new List<SupplierDto>();
        for (var i = 0; i < 10; i++)
        {
            var s = await _stockService.CreateSupplierAsync(new CreateSupplierDto
            {
                Name = supplierNames[i],
                ContactPerson = $"آقای/خانم رابط {i + 1}",
                Phone = $"021-8800{i:00}00",
                Address = $"آدرس {supplierNames[i]}"
            });
            suppliers.Add(s);
        }

        // ---------- ۳. مشتریان ----------
        string[] customerNames = { "احمدی", "محمدی", "رضایی", "میرزایی", "حسینی",
            "کریمی", "جعفری", "قاسمی", "نوری", "صادقی" };

        var customers = new List<CustomerDto>();
        for (var i = 0; i < 10; i++)
        {
            var c = await _salesService.CreateCustomerAsync(new CreateCustomerDto
            {
                Name = customerNames[i],
                Phone = $"0912345{i:0000}",
                Address = $"آدرس مشتری {customerNames[i]}"
            });
            customers.Add(c);
        }

        // ---------- ۴. کالاها ----------
        // این‌ها مستقیم از روی Entity ساخته می‌شوند (نه از طریق IProductService)
        // چون سازنده‌ی کامل Product امکان تعیین Sku/بهای تمام‌شده/نقطه سفارش را یک‌جا می‌دهد.
        string[] productNames = { "لپ‌تاپ", "موبایل", "هدفون", "کیبورد", "ماوس",
            "مانیتور", "پاوربانک", "اسپیکر", "تبلت", "ساعت هوشمند" };

        var products = new List<Product>();
        for (var i = 0; i < 10; i++)
        {
            var costPrice = 500_000m + i * 100_000m;
            var salePrice = costPrice * 1.3m;
            var product = new Product(
                sku: $"SKU-{i + 1:000}",
                name: productNames[i],
                price: salePrice,
                costPrice: costPrice,
                unit: "عدد",
                barcode: $"869000000{i:0000}",
                reorderPoint: 20);
            await _unitOfWork.Products.AddAsync(product);
            products.Add(product);
        }
        // این Save لازم است تا Id واقعی کالاها ساخته شود؛ چون در ادامه برای ثبت رسید خرید به آن Id نیاز داریم
        await _unitOfWork.CompleteAsync();

        // ---------- ۵. رسیدهای خرید ----------
        // رسید اول: همه‌ی ۱۰ کالا را با موجودی زیاد (۱۰۰۰ عدد) وارد انبار مرکزی می‌کند
        // تا بقیه‌ی عملیات‌ها (فروش، حواله مصرف، ضایعات، انتقال) همیشه موجودی کافی داشته باشند.
        var mainReceiptItems = products.Select(p => new PurchaseReceiptItemInput
        {
            ProductId = p.Id,
            Quantity = 1000,
            UnitCost = p.CostPrice
        }).ToList();

        await _stockService.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
        {
            SupplierId = suppliers[0].Id,
            WarehouseId = warehouses[0].Id,
            ReceiptDate = DateTime.UtcNow.AddDays(-20),
            Notes = "رسید نمونه — موجودی اولیه انبار مرکزی",
            Items = mainReceiptItems
        }, userId);

        // ۹ رسید دیگر، هر کدام ۲ قلم، پخش‌شده روی بقیه‌ی انبارها — فقط برای تنوع و پرشدن جدول
        for (var i = 1; i < 10; i++)
        {
            var p1 = products[i % 10];
            var p2 = products[(i + 1) % 10];
            await _stockService.RegisterPurchaseReceiptAsync(new CreatePurchaseReceiptDto
            {
                SupplierId = suppliers[i].Id,
                WarehouseId = warehouses[i].Id,
                ReceiptDate = DateTime.UtcNow.AddDays(-15 + i),
                Notes = "رسید نمونه",
                Items = new List<PurchaseReceiptItemInput>
                {
                    new() { ProductId = p1.Id, Quantity = 300, UnitCost = p1.CostPrice },
                    new() { ProductId = p2.Id, Quantity = 300, UnitCost = p2.CostPrice }
                }
            }, userId);
        }

        // ---------- ۶. حواله‌های مصرف داخلی ----------
        for (var i = 0; i < 10; i++)
        {
            var product = products[i % 10];
            await _stockService.RegisterInternalIssueAsync(new CreateInternalIssueDto
            {
                WarehouseId = warehouses[0].Id, // انبار مرکزی، چون موجودی زیاد دارد
                IssueDate = DateTime.UtcNow.AddDays(-10 + i),
                Purpose = "مصرف داخلی نمونه",
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 5 } }
            }, userId);
        }

        // ---------- ۷. ضایعات ----------
        for (var i = 0; i < 10; i++)
        {
            var product = products[(i + 3) % 10];
            await _stockService.RegisterScrapAsync(new CreateScrapRecordDto
            {
                WarehouseId = warehouses[0].Id,
                RecordDate = DateTime.UtcNow.AddDays(-9 + i),
                Reason = "آسیب‌دیدگی نمونه",
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 2 } }
            }, userId);
        }

        // ---------- ۸. برگشت از فروش ----------
        for (var i = 0; i < 10; i++)
        {
            var product = products[(i + 5) % 10];
            await _stockService.RegisterSalesReturnAsync(new CreateSalesReturnDto
            {
                WarehouseId = warehouses[i].Id,
                ReturnDate = DateTime.UtcNow.AddDays(-8 + i),
                CustomerReference = customers[i].Name,
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 1 } }
            }, userId);
        }

        // ---------- ۹. انتقال بین انبار ----------
        for (var i = 1; i < 10; i++)
        {
            var product = products[0];
            await _stockService.RegisterStockTransferAsync(new CreateStockTransferDto
            {
                SourceWarehouseId = warehouses[0].Id,
                DestinationWarehouseId = warehouses[i].Id,
                TransferDate = DateTime.UtcNow.AddDays(-7 + i),
                Items = new List<StockItemInput> { new() { ProductId = product.Id, Quantity = 10 } }
            }, userId);
        }
        // یک انتقال اضافه در جهت عکس تا به ۱۰ برسیم
        await _stockService.RegisterStockTransferAsync(new CreateStockTransferDto
        {
            SourceWarehouseId = warehouses[1].Id,
            DestinationWarehouseId = warehouses[0].Id,
            TransferDate = DateTime.UtcNow.AddDays(-1),
            Items = new List<StockItemInput> { new() { ProductId = products[1].Id, Quantity = 5 } }
        }, userId);

        // ---------- ۱۰. انبارگردانی (یکی برای هر انبار) ----------
        for (var i = 0; i < 10; i++)
        {
            var stockCount = await _stockService.OpenStockCountAsync(warehouses[i].Id, userId);

            // برای واقعی‌تر شدن، توی نصف موارد یه مغایرت کوچیک هم عمداً ثبت می‌کنیم
            var counted = new Dictionary<int, decimal>();
            foreach (var item in stockCount.Items)
            {
                counted[item.ProductId] = i % 2 == 0
                    ? item.SystemQuantity
                    : Math.Max(0, item.SystemQuantity - 1); // یک واحد کسری فرضی
            }

            await _stockService.CloseStockCountAsync(stockCount.Id, counted, userId);
        }

        // ---------- ۱۱. فاکتورهای فروش ----------
        var invoiceIds = new List<int>();
        var invoiceTotals = new List<decimal>(); // مبلغ واقعی هر فاکتور رو نگه می‌داریم، برای اقساط لازمش داریم
        for (var i = 0; i < 10; i++)
        {
            var product = products[(i + 2) % 10];
            var lineTotal = product.Price * 2; // Quantity = 2 پایین‌تر
            var invoiceId = await _salesService.CreateDraftInvoiceAsync(new CreateSalesInvoiceDto
            {
                CustomerId = i % 4 == 0 ? null : customers[i].Id, // هر ۴ فاکتور یکی «مشتری حضوری» بدون مشتری مشخص
                WarehouseId = warehouses[0].Id,
                InvoiceDate = DateTime.UtcNow.AddDays(-6 + i),
                Items = new List<SalesInvoiceItemInput>
                {
                    new() { ProductId = product.Id, Quantity = 2, UnitPrice = product.Price }
                }
            }, userId);

            await _salesService.ConfirmInvoiceAsync(invoiceId, userId);
            invoiceIds.Add(invoiceId);
            invoiceTotals.Add(lineTotal); // بدون تخفیف، پس مبلغ فاکتور همین جمع اقلامه
        }

        // برای ۳ تا از فاکتورها، یک طرح اقساط سه‌قسطی هم می‌سازیم (یکی از اقساط را عمداً عقب‌افتاده می‌گذاریم)
        for (var i = 0; i < 3; i++)
        {
            var invoiceTotal = invoiceTotals[i];

            // نکته‌ی مهم: نمی‌شود هر سه قسط را جداگانه گرد کرد، چون جمعشان ممکن است
            // ۱-۲ تومان با مبلغ فاکتور فرق کند (تقسیم بر ۳ همیشه عدد صحیح نمی‌شود) و
            // اعتبارسنجی «جمع اقساط باید دقیقاً برابر فاکتور باشد» رد می‌شود.
            // برای همین، قسط سوم را گرد نمی‌کنیم — هرچی از تقسیم دو قسط اول باقی بماند،
            // دقیقاً همان را می‌گذاریم تا جمع همیشه درست از آب دربیاید.
            var firstAmount = Math.Round(invoiceTotal / 3, 0);
            var secondAmount = firstAmount;
            var thirdAmount = invoiceTotal - firstAmount - secondAmount;

            await _installmentService.CreateInstallmentPlanAsync(new CreateInstallmentPlanDto
            {
                SalesInvoiceId = invoiceIds[i],
                Installments = new List<InstallmentInput>
                {
                    new(DateTime.UtcNow.AddDays(-10), firstAmount), // این یکی عقب‌افتاده است
                    new(DateTime.UtcNow.AddDays(20), secondAmount),
                    new(DateTime.UtcNow.AddDays(50), thirdAmount)
                }
            }, userId);
        }

        // ---------- ۱۲. صندوق/بانک ----------
        var financialAccountNames = new (string Name, string Type, string? BankName)[]
        {
            ("صندوق نقدی مرکزی", "Cash", null),
            ("بانک ملی - شعبه مرکزی", "Bank", "بانک ملی"),
            ("بانک ملت - شعبه ولیعصر", "Bank", "بانک ملت"),
            ("بانک صادرات - شعبه آزادی", "Bank", "بانک صادرات"),
            ("بانک پارسیان", "Bank", "بانک پارسیان"),
            ("بانک تجارت", "Bank", "بانک تجارت"),
            ("بانک سامان", "Bank", "بانک سامان"),
            ("بانک پاسارگاد", "Bank", "بانک پاسارگاد"),
            ("صندوق فروشگاه شعبه ۲", "Cash", null),
            ("بانک سپه", "Bank", "بانک سپه"),
        };

        var financialAccountIds = new List<int>();
        foreach (var (name, type, bankName) in financialAccountNames)
        {
            var id = await _treasuryService.CreateFinancialAccountAsync(new CreateFinancialAccountDto
            {
                Name = name,
                Type = type,
                BankName = bankName,
                AccountNumber = bankName is null ? null : $"0123456789{financialAccountIds.Count}",
                Iban = bankName is null ? null : $"IR06200000000000{financialAccountIds.Count:00}0000"
            });
            financialAccountIds.Add(id);
        }

        // ---------- ۱۳. دریافت از مشتری ----------
        for (var i = 0; i < 10; i++)
        {
            var method = i == 3 || i == 7 ? "Cheque" : (i % 2 == 0 ? "Cash" : "BankTransfer");
            await _treasuryService.RegisterCustomerReceiptAsync(new CreateCustomerReceiptDto
            {
                CustomerId = customers[i].Id,
                FinancialAccountId = financialAccountIds[i % financialAccountIds.Count],
                Amount = 1_000_000m + i * 50_000m,
                Method = method,
                ReceiptDate = DateTime.UtcNow.AddDays(-5 + i),
                ChequeNumber = method == "Cheque" ? $"CHQ-{i:0000}" : null,
                ChequeDueDate = method == "Cheque" ? DateTime.UtcNow.AddDays(30) : null,
                Notes = "دریافت نمونه"
            }, userId);
        }

        // ---------- ۱۴. پرداخت به تأمین‌کننده ----------
        for (var i = 0; i < 10; i++)
        {
            var method = i == 2 ? "Cheque" : (i % 2 == 0 ? "BankTransfer" : "Cash");
            await _treasuryService.RegisterSupplierPaymentAsync(new CreateSupplierPaymentDto
            {
                SupplierId = suppliers[i].Id,
                FinancialAccountId = financialAccountIds[(i + 1) % financialAccountIds.Count],
                Amount = 800_000m + i * 40_000m,
                Method = method,
                PaymentDate = DateTime.UtcNow.AddDays(-4 + i),
                ChequeNumber = method == "Cheque" ? $"CHQ-OUT-{i:0000}" : null,
                ChequeDueDate = method == "Cheque" ? DateTime.UtcNow.AddDays(25) : null,
                Notes = "پرداخت نمونه"
            }, userId);
        }
    }
}
