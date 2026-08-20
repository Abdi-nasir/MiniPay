using System.ComponentModel.DataAnnotations;
namespace MiniApy.Api.DTOs.Merchants;

public sealed class MerchantsQuery
{
    [Range(1, 1_000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

 
}