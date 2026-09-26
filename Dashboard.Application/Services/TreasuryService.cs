using Dashboard.Application.DTOs;
using Dashboard.Application.Validators;
using Dashboard.Application.Helpers;
using Dashboard.Domain.Accounting;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;

namespace Dashboard.Application.Services;

public interface ITreasuryService
{
    Task<int> CreateFinancialAccountAsync(CreateFinancialAccountDto dto);
    Task<IEnumerable<FinancialAccountDto>> GetFinancialAccountsAsync();

    Task<int> RegisterCustomerReceiptAsync(CreateCustomerReceiptDto dto, string? userId);
    Task<IEnumerable<CustomerReceiptDto>> GetCustomerReceiptsAsync();

    Task<int> RegisterSupplierPaymentAsync(CreateSupplierPaymentDto dto, string? userId);
    Task<IEnumerable<SupplierPaymentDto>> GetSupplierPaymentsAsync();
}

public class TreasuryService : ITreasuryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJournalService _journalService;
    private readonly INotificationService _notifications;

    public TreasuryService(IUnitOfWork unitOfWork, IJournalService journalService, INotificationService notifications)
    {
        _unitOfWork = unitOfWork;
        _journalService = journalService;
    }

    // هر صندوق/بانک جدید، خودش هم یک سرفصل حساب معادل در دفتر کل می‌سازد؛ چون اگر موجودی
    // چند صندوق را زیر یک سرفصل مشترک بگذاریم، تراز آزمایشی دیگر نمی‌تواند موجودی هرکدام
    // را جدا نشان بدهد.
    public async Task<int> CreateFinancialAccountAsync(CreateFinancialAccountDto dto)
    {
        var type = Enum.Parse<FinancialAccountType>(dto.Type);

        // کد سرفصل را به‌صورت پیاپی می‌سازیم (مثلاً 1101-01, 1101-02, ...) تا برای حسابدار
        // قابل‌خواندن و دنبال‌کردن باشد. توجه: این شمارش بر پایه‌ی تعداد فعلی است، پس در
        // حالت درخواست‌های همزمان (که برای این عملیات مدیریتی بسیار نامحتمل است) ممکن است
        // نیاز به قفل‌گذاری بیشتری داشته باشد.
        var prefix = type == FinancialAccountType.Cash ? "1101" : "1102";
        var existingAccounts = await _unitOfWork.Accounts.GetAllAsync();
        var sameTypeCount = existingAccounts.Count(a => a.Code.StartsWith(prefix + "-"));
        var accountCode = $"{prefix}-{(sameTypeCount + 1):D2}";

        var ledgerAccount = new Account
        {
            Code = accountCode,
            Name = dto.Name,
            Type = Domain.Enums.AccountType.Asset,
            IsSystemAccount = true
        };
        await _unitOfWork.Accounts.AddAsync(ledgerAccount);

        var financialAccount = new FinancialAccount
        {
            Name = dto.Name,
            Type = type,
            BankName = dto.BankName,
            AccountNumber = dto.AccountNumber,
            Iban = dto.Iban,
            Account = ledgerAccount
        };
        await _unitOfWork.FinancialAccounts.AddAsync(financialAccount);
        await _unitOfWork.CompleteAsync();

        return financialAccount.Id;
    }

    public async Task<IEnumerable<FinancialAccountDto>> GetFinancialAccountsAsync()
    {
        var accounts = await _unitOfWork.FinancialAccounts.GetAllAsync();
        return accounts.Select(a => new FinancialAccountDto(a.Id, a.Name, a.Type.ToString()));
    }

    // ---------------- دریافت از مشتری ----------------
    public async Task<int> RegisterCustomerReceiptAsync(CreateCustomerReceiptDto dto, string? userId)
    {
        CommonValidations.ValidateAmountPositive(dto.Amount);

        var financialAccount = await _unitOfWork.FinancialAccounts.GetByIdAsync(dto.FinancialAccountId)
            ?? throw new NotFoundException("صندوق/بانک", dto.FinancialAccountId);

        var receipt = new CustomerReceipt
        {
            CustomerId = dto.CustomerId,
            FinancialAccountId = dto.FinancialAccountId,
            ReceiptNumber = $"TEMP-{Guid.NewGuid():N}",
            Amount = dto.Amount,
            Method = Enum.Parse<PaymentMethod>(dto.Method),
            ReceiptDate = dto.ReceiptDate,
            ChequeNumber = dto.ChequeNumber,
            ChequeDueDate = dto.ChequeDueDate,
            Notes = dto.Notes,
            InstallmentId = dto.InstallmentId,
            CreatedByUserId = userId
        };

        await _unitOfWork.CustomerReceipts.AddAsync(receipt);

        if (dto.InstallmentId.HasValue)
        {
            var installment = await _unitOfWork.InstallmentPlans.GetInstallmentByIdAsync(dto.InstallmentId.Value)
                ?? throw new NotFoundException("قسط", dto.InstallmentId!.Value);

            installment.PaidAmount += dto.Amount;
        }

        await _unitOfWork.CompleteAsync(); // اینجا receipt.Id واقعی ساخته می‌شود

        receipt.ReceiptNumber = DocumentNumberGenerator.Generate(receipt.ReceiptDate, receipt.CustomerId, receipt.Id);
        await _unitOfWork.CustomerReceipts.UpdateAsync(receipt);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("CustomerReceiptRegistered", userId, $"دریافت {receipt.ReceiptNumber} به مبلغ {dto.Amount} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.AccountingUser,
            "دریافت از مشتری ثبت شد", $"رسید {receipt.ReceiptNumber} به مبلغ {dto.Amount:0} تومان",
            NotificationType.System, "/accounting/customer-receipts");

        // دریافت پول: بدهکار صندوق/بانک، بستانکار حساب‌های دریافتنی (طلب از مشتری کم می‌شود)
        await _journalService.PostEntryAsync(
            description: $"دریافت وجه طبق رسید {receipt.ReceiptNumber}",
            lines: new List<JournalLineInput>
            {
            new(financialAccount.Account!.Code, dto.Amount, 0, "افزایش موجودی صندوق/بانک"),
            new(SystemAccountCodes.AccountsReceivable, 0, dto.Amount, "کاهش طلب از مشتری", "Customer", dto.CustomerId)
            },
            referenceType: nameof(CustomerReceipt),
            referenceId: receipt.Id,
            userId: userId);

        return receipt.Id;
    }
    public async Task<IEnumerable<CustomerReceiptDto>> GetCustomerReceiptsAsync()
    {
        var receipts = await _unitOfWork.CustomerReceipts.GetAllAsync();
        return receipts.Select(r => new CustomerReceiptDto(
            r.Id, r.ReceiptNumber, r.ReceiptDate, r.Customer?.Name, r.FinancialAccount?.Name ?? "-", r.Amount, r.Method.ToString()));
    }

    // ---------------- پرداخت به تأمین‌کننده ----------------

    public async Task<int> RegisterSupplierPaymentAsync(CreateSupplierPaymentDto dto, string? userId)
    {
        CommonValidations.ValidateAmountPositive(dto.Amount);

        var financialAccount = await _unitOfWork.FinancialAccounts.GetByIdAsync(dto.FinancialAccountId)
            ?? throw new NotFoundException("صندوق/بانک", dto.FinancialAccountId);

        var payment = new SupplierPayment
        {
            SupplierId = dto.SupplierId,
            FinancialAccountId = dto.FinancialAccountId,
            PaymentNumber = $"TEMP-{Guid.NewGuid():N}", // شماره موقت، فقط برای عبور از محدودیت Unique
            Amount = dto.Amount,
            Method = Enum.Parse<PaymentMethod>(dto.Method),
            PaymentDate = dto.PaymentDate,
            ChequeNumber = dto.ChequeNumber,
            ChequeDueDate = dto.ChequeDueDate,
            Notes = dto.Notes,
            CreatedByUserId = userId
        };

        await _unitOfWork.SupplierPayments.AddAsync(payment);
        await _unitOfWork.CompleteAsync(); // اینجا payment.Id واقعی ساخته می‌شود

        payment.PaymentNumber = DocumentNumberGenerator.Generate(payment.PaymentDate, payment.SupplierId, payment.Id);
        await _unitOfWork.SupplierPayments.UpdateAsync(payment);
        await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SupplierPaymentRegistered", userId, $"پرداخت {payment.PaymentNumber} به مبلغ {dto.Amount} ثبت شد."));
        await _unitOfWork.CompleteAsync();

        await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.AccountingUser,
            "پرداخت به تأمین‌کننده ثبت شد", $"سند {payment.PaymentNumber} به مبلغ {dto.Amount:0} تومان",
            NotificationType.System, "/accounting/supplier-payments");

        // پرداخت پول: بدهکار حساب‌های پرداختنی (بدهی کم می‌شود)، بستانکار صندوق/بانک
        await _journalService.PostEntryAsync(
            description: $"پرداخت وجه طبق سند {payment.PaymentNumber}",
            lines: new List<JournalLineInput>
            {
                new(SystemAccountCodes.AccountsPayable, dto.Amount, 0, "کاهش بدهی به تأمین‌کننده", "Supplier", dto.SupplierId),
                new(financialAccount.Account!.Code, 0, dto.Amount, "کاهش موجودی صندوق/بانک")
            },
            referenceType: nameof(SupplierPayment),
            referenceId: payment.Id,
            userId: userId);

        return payment.Id;
    }

    public async Task<IEnumerable<SupplierPaymentDto>> GetSupplierPaymentsAsync()
    {
        var payments = await _unitOfWork.SupplierPayments.GetAllAsync();
        return payments.Select(p => new SupplierPaymentDto(
            p.Id, p.PaymentNumber, p.PaymentDate, p.Supplier?.Name ?? "-", p.FinancialAccount?.Name ?? "-", p.Amount, p.Method.ToString()));
    }
}
