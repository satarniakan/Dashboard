namespace Dashboard.Domain.Enums;

public enum StockMovementReason
{
    InitialStock,
    Purchase,
    Sale,
    SaleCancellation,
    TransferOut,
    TransferIn,
    AdjustmentPositive,
    AdjustmentNegative
}