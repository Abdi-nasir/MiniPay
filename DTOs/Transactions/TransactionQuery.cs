using System.ComponentModel.DataAnnotations;
using MiniApy.Api.Enums;
namespace MiniApy.Api.DTOs.Transactions;

public sealed class TransactionQuery
{
    public Guid? MerchantId { get; set; }

    public Guid? PaymentId { get; set; }

    public TransactionStatus? Status { get; set; }

    public TransactionType? Type { get; set; }

    [Range(1, 1_000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}