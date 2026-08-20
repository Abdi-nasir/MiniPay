using Microsoft.AspNetCore.Mvc;
using MiniApy.Api.DTOs.Refunds;
using MiniApy.Api.Interfaces;

namespace MiniApy.Api.Controllers;

[ApiController]
[Route("api/refunds")]
public sealed class RefundsController(
    IRefundService refundService)
    : ControllerBase
{
    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(
        typeof(RefundResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RefundResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var refund = await refundService.GetByIdAsync(
            id,
            cancellationToken);

        return Ok(refund);
    }
}