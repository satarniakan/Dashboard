using Dashboard.Application.DTOs;
using Dashboard.Application.Validators;
using Dashboard.Application.Helpers;
using Dashboard.Domain.Accounting;
using Dashboard.Domain.Entities;
using Dashboard.Domain.Enums;
using Dashboard.Domain.Exceptions;
using Dashboard.Domain.Interfaces;

using Microsoft.Extensions.Logging;

namespace Dashboard.Application.Services;

public interface ITreasuryService
{
    Task<int> CreateFinancialAccountAsync(CreateFinancialAccountDto dto);
    Task<IEnumerable<FinancialAccountDto>> GetFinancialAccountsAsync();

    Task<int> RegisterCustomerReceiptAsync(CreateCustomerReceiptDto dto, string? userId);
    Task<IEnumerable<CustomerReceiptDto>> GetCustomerReceiptsAsync();

    Task<int> RegisterSupplierPaymentAsync(CreateSupplierPaymentDto dto, string? userId);
    Task<IEnumerable<SupplierPaymentDto>> GetSupplierPaymentsAsync();

    /// <summary>انتقال وجه بین دو صندوق/بانک (مثلاً تسویهٔ پولِ درگاه به حساب بانک)</summary>
    Task PostAccountTransferAsync(TransferBetweenAccountsDto dto, string? userId);

    // --- چرخهٔ چک: وصول و برگشت اوراق ---

    Task<IEnumerable<PendingChequeDto>> GetPendingChequesAsync();
    Task SettleCustomerChequeAsync(int receiptId, SettleChequeDto dto, string? userId);
    Task BounceCustomerChequeAsync(int receiptId, BounceChequeDto dto, string? userId);
    Task SettleSupplierChequeAsync(int paymentId, SettleChequeDto dto, string? userId);
    Task BounceSupplierChequeAsync(int paymentId, BounceChequeDto dto, string? userId);
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
        _notifications = notifications;
    }

    /// <summary>
    /// اجرای عملیات چندمرحله‌ای (رسید + قسط + شماره‌گذاری + سند حسابداری) در یک تراکنش —
    /// اگر سند حسابداری شکست بخورد، رسید و تغییر قسط هم commit نمی‌شوند (دفتر کل ناراست نمی‌شود).
    /// </summary>
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

        var receiptId = 0;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
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
                ChequeStatus = Enum.Parse<PaymentMethod>(dto.Method) == PaymentMethod.Cheque
                    ? ChequeStatus.Pending : null,
                Notes = dto.Notes,
                InstallmentId = dto.InstallmentId,
                CreatedByUserId = userId
            };

            await _unitOfWork.CustomerReceipts.AddAsync(receipt);

            if (dto.InstallmentId.HasValue)
            {
                var installment = await _unitOfWork.InstallmentPlans.GetInstallmentByIdAsync(dto.InstallmentId.Value)
                    ?? throw new NotFoundException("قسط", dto.InstallmentId!.Value);

                // قسط باید به همان مشتری تعلق داشته باشد
                var installmentCustomerId = installment.InstallmentPlan?.SalesInvoice?.CustomerId;
                if (installmentCustomerId != dto.CustomerId)
                    throw new BusinessRuleException("این قسط به مشتری انتخاب‌شده تعلق ندارد.");

                // پرداخت بیش از مانده قسط مجاز نیست. این بررسی اولیه فقط برای پیام دوستانه است؛
                // ملاک نهایی همان UPDATE شرطیِ اتمیک در TryAddPaymentAsync است (رقابت همزمانی را می‌بندد).
                var remaining = installment.Amount - installment.PaidAmount;
                if (dto.Amount > remaining)
                    throw new BusinessRuleException($"مبلغ دریافتی از مانده‌ی قسط بیشتر است (مانده: {remaining:0.##}).");

                if (!await _unitOfWork.InstallmentPlans.TryAddPaymentAsync(installment.Id, dto.Amount))
                    throw new BusinessRuleException("مانده‌ی این قسط همین حالا پر شده است؛ لطفاً مبلغ را دوباره بررسی کنید.");
            }

            await _unitOfWork.CompleteAsync(); // اینجا receipt.Id واقعی ساخته می‌شود
            receiptId = receipt.Id;

            receipt.ReceiptNumber = DocumentNumberGenerator.Generate(receipt.ReceiptDate, receipt.CustomerId, receipt.Id);
            await _unitOfWork.CustomerReceipts.UpdateAsync(receipt);
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("CustomerReceiptRegistered", userId, $"دریافت {receipt.ReceiptNumber} به مبلغ {dto.Amount} ثبت شد."));
            await _unitOfWork.CompleteAsync();

            await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.AccountingUser,
                "دریافت از مشتری ثبت شد", $"رسید {receipt.ReceiptNumber} به مبلغ {dto.Amount:0} تومان",
                NotificationType.System, "/accounting/customer-receipts");

            // دریافت پول: بدهکار صندوق/بانک، بستانکار حساب‌های دریافتنی (طلب از مشتری کم می‌شود)
            // ⚠️ چکِ وصول‌نشده پول نیست: تا سررسید به «اوراق دریافتنی» می‌نشیند و موجودی
            // صندوق/بانک را زیاد نمی‌کند (وگرنه چک ۳۰ روزه همان روز نقدی دیده می‌شود).
            // نقد شدنش هنگام وصول، سند جدا (انتقال از اوراق به صندوق/بانک) می‌خواهد.
            var isChequeReceipt = receipt.Method == PaymentMethod.Cheque;
            var receiptDebitAccount = isChequeReceipt
                ? SystemAccountCodes.NotesReceivable
                : financialAccount.Account!.Code;
            var receiptDebitDescription = isChequeReceipt
                ? "اوراق دریافتنی (چک وصول‌نشده)"
                : "افزایش موجودی صندوق/بانک";

            await _journalService.PostEntryAsync(
                description: $"دریافت وجه طبق رسید {receipt.ReceiptNumber}",
                lines: new List<JournalLineInput>
                {
                new(receiptDebitAccount, dto.Amount, 0, receiptDebitDescription),
                new(SystemAccountCodes.AccountsReceivable, 0, dto.Amount, "کاهش طلب از مشتری", "Customer", dto.CustomerId)
                },
                referenceType: nameof(CustomerReceipt),
                referenceId: receipt.Id,
                userId: userId);
        });

        return receiptId;
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

        var paymentId = 0;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
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
                ChequeStatus = Enum.Parse<PaymentMethod>(dto.Method) == PaymentMethod.Cheque
                    ? ChequeStatus.Pending : null,
                Notes = dto.Notes,
                CreatedByUserId = userId
            };

            await _unitOfWork.SupplierPayments.AddAsync(payment);
            await _unitOfWork.CompleteAsync(); // اینجا payment.Id واقعی ساخته می‌شود
            paymentId = payment.Id;

            payment.PaymentNumber = DocumentNumberGenerator.Generate(payment.PaymentDate, payment.SupplierId, payment.Id);
            await _unitOfWork.SupplierPayments.UpdateAsync(payment);
            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SupplierPaymentRegistered", userId, $"پرداخت {payment.PaymentNumber} به مبلغ {dto.Amount} ثبت شد."));
            await _unitOfWork.CompleteAsync();

            await _notifications.NotifyRoleAsync(Dashboard.Domain.Identity.Roles.AccountingUser,
                "پرداخت به تأمین‌کننده ثبت شد", $"سند {payment.PaymentNumber} به مبلغ {dto.Amount:0} تومان",
                NotificationType.System, "/accounting/supplier-payments");

            // پرداخت پول: بدهکار حساب‌های پرداختنی (بدهی کم می‌شود)، بستانکار صندوق/بانک
            // ⚠️ چکِ صادره هنوز از بانک خارج نشده: به «اوراق پرداختنی» بستانکار می‌شود تا
            // موجودی بانک پیش از وصول کم نشود (قرینهٔ رسید چکی در همین فایل).
            var isChequePayment = payment.Method == PaymentMethod.Cheque;
            var paymentCreditAccount = isChequePayment
                ? SystemAccountCodes.NotesPayable
                : financialAccount.Account!.Code;
            var paymentCreditDescription = isChequePayment
                ? "اوراق پرداختنی (چک وصول‌نشده)"
                : "کاهش موجودی صندوق/بانک";

            await _journalService.PostEntryAsync(
                description: $"پرداخت وجه طبق سند {payment.PaymentNumber}",
                lines: new List<JournalLineInput>
                {
                    new(SystemAccountCodes.AccountsPayable, dto.Amount, 0, "کاهش بدهی به تأمین‌کننده", "Supplier", dto.SupplierId),
                    new(paymentCreditAccount, 0, dto.Amount, paymentCreditDescription)
                },
                referenceType: nameof(SupplierPayment),
                referenceId: payment.Id,
                userId: userId);
        });

        return paymentId;
    }

    public async Task<IEnumerable<SupplierPaymentDto>> GetSupplierPaymentsAsync()
    {
        var payments = await _unitOfWork.SupplierPayments.GetAllAsync();
        return payments.Select(p => new SupplierPaymentDto(
            p.Id, p.PaymentNumber, p.PaymentDate, p.Supplier?.Name ?? "-", p.FinancialAccount?.Name ?? "-", p.Amount, p.Method.ToString()));
    }

    // ---------------- انتقال بین صندوق/بانک ----------------

    /// <summary>
    /// انتقال وجه بین دو صندوق/بانک — فقط یک سند دوطرفه (بدهکار مقصد، بستانکار مبدأ):
    /// واریز به حساب دارایی = بدهکار، برداشت از آن = بستانکار؛ برعکسش یعنی در دفتر،
    /// پول به مبدأ اضافه و از مقصد کم شده است.
    /// برای تسویهٔ پولِ پرداخت آنلاین لازم است: رسید خودکار، صندوق درگاه را بدهکار می‌کند و
    /// این سند، همان پول را به حساب بانکی منتقله می‌کند (وگرنه سود ناخالص از تراز می‌افتد).
    /// </summary>
    public async Task PostAccountTransferAsync(TransferBetweenAccountsDto dto, string? userId)
    {
        CommonValidations.ValidateAmountPositive(dto.Amount);

        if (dto.FromFinancialAccountId == dto.ToFinancialAccountId)
            throw new BusinessRuleException("صندوق/بانک مبدأ و مقصد نمی‌توانند یکسان باشند.");

        var from = await _unitOfWork.FinancialAccounts.GetByIdAsync(dto.FromFinancialAccountId)
            ?? throw new NotFoundException("صندوق/بانک مبدأ", dto.FromFinancialAccountId);
        var to = await _unitOfWork.FinancialAccounts.GetByIdAsync(dto.ToFinancialAccountId)
            ?? throw new NotFoundException("صندوق/بانک مقصد", dto.ToFinancialAccountId);

        if (!from.IsActive || !to.IsActive)
            throw new BusinessRuleException("انتقال فقط بین صندوق/بانک‌های فعال مجاز است.");

        if (from.Account is null || to.Account is null)
            throw new BusinessRuleException("سرفصل حساب یکی از صندوق/بانک‌ها پیدا نشد.");

        var datePart = dto.TransferDate == default ? string.Empty : $" ({dto.TransferDate:yyyy/MM/dd})";
        var description = (dto.Notes is { Length: > 0 } note
            ? $"انتقال بین حساب‌ها{datePart}: {note}"
            : $"انتقال بین حساب‌ها{datePart}");

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _journalService.PostEntryAsync(
                description: description,
                lines: new List<JournalLineInput>
                {
                    new(to.Account.Code, dto.Amount, 0, $"واریز به {to.Name}"),
                    new(from.Account.Code, 0, dto.Amount, $"برداشت از {from.Name}")
                },
                referenceType: "AccountTransfer",
                referenceId: null,
                userId: userId);

            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("AccountTransfer", userId,
                $"انتقال {dto.Amount:0} از «{from.Name}» به «{to.Name}» ثبت شد."));
            await _unitOfWork.CompleteAsync();
        });
    }

    // ---------------- چرخهٔ چک: وصول و برگشت اوراق ----------------

    public async Task<IEnumerable<PendingChequeDto>> GetPendingChequesAsync()
    {
        var receivedCheques = (await _unitOfWork.CustomerReceipts.GetPendingChequesAsync())
            .Select(r => new PendingChequeDto(
                r.Id, "receipt", r.ReceiptNumber, r.Customer?.Name,
                r.ChequeNumber, r.ChequeDueDate, r.Amount));

        var issuedCheques = (await _unitOfWork.SupplierPayments.GetPendingChequesAsync())
            .Select(p => new PendingChequeDto(
                p.Id, "payment", p.PaymentNumber, p.Supplier?.Name,
                p.ChequeNumber, p.ChequeDueDate, p.Amount));

        return receivedCheques.Concat(issuedCheques)
            .OrderBy(c => c.ChequeDueDate ?? DateTime.MaxValue);
    }

    /// <summary>
    /// وصول چکِ دریافتی: پول واقعاً نقد شده — بدهکار صندوق/بانکِ انتخابی، بستانکار اوراق دریافتنی.
    /// وضعیت با UPDATE شرطی claim می‌شود تا وصول دوباره (دوبار سند) غیرممکن باشد.
    /// </summary>
    public async Task SettleCustomerChequeAsync(int receiptId, SettleChequeDto dto, string? userId)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var receipt = await _unitOfWork.CustomerReceipts.GetByIdAsync(receiptId)
                ?? throw new NotFoundException("رسید", receiptId);
            if (receipt.Method != PaymentMethod.Cheque)
                throw new BusinessRuleException("این رسید چکی نیست.");

            var target = await _unitOfWork.FinancialAccounts.GetByIdAsync(dto.FinancialAccountId)
                ?? throw new NotFoundException("صندوق/بانک", dto.FinancialAccountId);
            if (!target.IsActive)
                throw new BusinessRuleException("صندوق/بانک مقصد غیرفعال است.");
            if (target.Account is null)
                throw new BusinessRuleException("سرفصل حسابِ صندوق/بانک مقصد پیدا نشد.");

            if (!await _unitOfWork.CustomerReceipts.TryClaimChequeStatusAsync(receiptId, ChequeStatus.Settled))
                throw new BusinessRuleException("این چک قبلاً وصول یا برگشت خورده است.");

            await _journalService.PostEntryAsync(
                description: $"وصول چک رسید {receipt.ReceiptNumber}" + (dto.Notes is { Length: > 0 } ? $" — {dto.Notes}" : string.Empty),
                lines: new List<JournalLineInput>
                {
                    new(target.Account.Code, receipt.Amount, 0, "نقد شدن اوراق دریافتنی"),
                    new(SystemAccountCodes.NotesReceivable, 0, receipt.Amount, "خروج چک از اوراق دریافتنی")
                },
                referenceType: "ChequeSettlement",
                referenceId: receipt.Id,
                userId: userId);

            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("CustomerChequeSettled", userId,
                $"چک رسید {receipt.ReceiptNumber} به مبلغ {receipt.Amount:0} وصول شد."));
        });
    }

    /// <summary>
    /// برگشت‌خوردن چکِ دریافتی: چک پاس نشد — طلب از مشتری برمی‌گردد
    /// (بدهکار حساب‌های دریافتنی با معین مشتری، بستانکار اوراق دریافتنی).
    /// </summary>
    public async Task BounceCustomerChequeAsync(int receiptId, BounceChequeDto dto, string? userId)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var receipt = await _unitOfWork.CustomerReceipts.GetByIdAsync(receiptId)
                ?? throw new NotFoundException("رسید", receiptId);
            if (receipt.Method != PaymentMethod.Cheque)
                throw new BusinessRuleException("این رسید چکی نیست.");
            if (receipt.CustomerId is null)
                throw new BusinessRuleException("این رسید بدون مشتری ثبت شده و برگشت چک آن قابل ثبت نیست.");

            if (!await _unitOfWork.CustomerReceipts.TryClaimChequeStatusAsync(receiptId, ChequeStatus.Bounced))
                throw new BusinessRuleException("این چک قبلاً وصول یا برگشت خورده است.");

            await _journalService.PostEntryAsync(
                description: $"برگشت چک رسید {receipt.ReceiptNumber}" + (dto.Notes is { Length: > 0 } ? $" — {dto.Notes}" : string.Empty),
                lines: new List<JournalLineInput>
                {
                    new(SystemAccountCodes.AccountsReceivable, receipt.Amount, 0,
                        "بازگشت طلب بابت چک برگشتی", "Customer", receipt.CustomerId),
                    new(SystemAccountCodes.NotesReceivable, 0, receipt.Amount, "خروج چک برگشتی از اوراق دریافتنی")
                },
                referenceType: "ChequeBounce",
                referenceId: receipt.Id,
                userId: userId);

            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("CustomerChequeBounced", userId,
                $"چک رسید {receipt.ReceiptNumber} به مبلغ {receipt.Amount:0} برگشت خورد."));
        });
    }

    /// <summary>
    /// وصول چکِ صادره به تأمین‌کننده: بانک پول را پرداخت کرده — بدهکار اوراق پرداختنی،
    /// بستانکار صندوق/بانک (اینجاست که موجودی بانک واقعاً کم می‌شود).
    /// </summary>
    public async Task SettleSupplierChequeAsync(int paymentId, SettleChequeDto dto, string? userId)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var payment = await _unitOfWork.SupplierPayments.GetByIdAsync(paymentId)
                ?? throw new NotFoundException("سند پرداخت", paymentId);
            if (payment.Method != PaymentMethod.Cheque)
                throw new BusinessRuleException("این سند چکی نیست.");

            var source = await _unitOfWork.FinancialAccounts.GetByIdAsync(dto.FinancialAccountId)
                ?? throw new NotFoundException("صندوق/بانک", dto.FinancialAccountId);
            if (!source.IsActive)
                throw new BusinessRuleException("صندوق/بانک مبدأ غیرفعال است.");
            if (source.Account is null)
                throw new BusinessRuleException("سرفصل حسابِ صندوق/بانک مبدأ پیدا نشد.");

            if (!await _unitOfWork.SupplierPayments.TryClaimChequeStatusAsync(paymentId, ChequeStatus.Settled))
                throw new BusinessRuleException("این چک قبلاً وصول یا برگشت خورده است.");

            await _journalService.PostEntryAsync(
                description: $"وصول چک سند {payment.PaymentNumber}" + (dto.Notes is { Length: > 0 } ? $" — {dto.Notes}" : string.Empty),
                lines: new List<JournalLineInput>
                {
                    new(SystemAccountCodes.NotesPayable, payment.Amount, 0, "تسویه اوراق پرداختنی"),
                    new(source.Account.Code, 0, payment.Amount, $"کاهش موجودی {source.Name} بابت وصول چک")
                },
                referenceType: "ChequeSettlement",
                referenceId: payment.Id,
                userId: userId);

            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SupplierChequeSettled", userId,
                $"چک سند {payment.PaymentNumber} به مبلغ {payment.Amount:0} وصول شد."));
        });
    }

    /// <summary>
    /// برگشت‌خوردن چکِ صادره: بانک پاس نکرد — بدهی به تأمین‌کننده برمی‌گردد
    /// (بدهکار اوراق پرداختنی، بستانکار حساب‌های پرداختنی با معین تأمین‌کننده).
    /// </summary>
    public async Task BounceSupplierChequeAsync(int paymentId, BounceChequeDto dto, string? userId)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var payment = await _unitOfWork.SupplierPayments.GetByIdAsync(paymentId)
                ?? throw new NotFoundException("سند پرداخت", paymentId);
            if (payment.Method != PaymentMethod.Cheque)
                throw new BusinessRuleException("این سند چکی نیست.");

            if (!await _unitOfWork.SupplierPayments.TryClaimChequeStatusAsync(paymentId, ChequeStatus.Bounced))
                throw new BusinessRuleException("این چک قبلاً وصول یا برگشت خورده است.");

            await _journalService.PostEntryAsync(
                description: $"برگشت چک سند {payment.PaymentNumber}" + (dto.Notes is { Length: > 0 } ? $" — {dto.Notes}" : string.Empty),
                lines: new List<JournalLineInput>
                {
                    new(SystemAccountCodes.NotesPayable, payment.Amount, 0, "خروج چک برگشتی از اوراق پرداختنی"),
                    new(SystemAccountCodes.AccountsPayable, 0, payment.Amount,
                        "بازگشت بدهی بابت چک برگشتی", "Supplier", payment.SupplierId)
                },
                referenceType: "ChequeBounce",
                referenceId: payment.Id,
                userId: userId);

            await _unitOfWork.AuditLogs.AddAsync(new AuditLog("SupplierChequeBounced", userId,
                $"چک سند {payment.PaymentNumber} به مبلغ {payment.Amount:0} برگشت خورد."));
        });
    }
}
