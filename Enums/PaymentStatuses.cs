namespace MiniApy.Api.Enums;

    public enum PaymentStatuses
    {
    CREATED,
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED,
    REFUNDED
    }

    public enum TransactionStatus
{
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED
}
public enum TransactionType
{
    PAYMENT,
    REFUND
}
public enum RefundStatus
{
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED
}

public enum SettlementStatus
{
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED
}

public enum WebhookStatus
{
    PENDING,
    PROCESSING,
    DELIVERED,
    FAILED
}