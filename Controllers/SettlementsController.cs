using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniApy.Api.Authentication;
using MiniApy.Api.DTOs.Settlements;
using MiniApy.Api.Interfaces;
using MiniApy.Api.RateLimiting;

namespace MiniApy.Api.Controllers;

[ApiController]
[Route("api/settlements")]
public sealed class SettlementsController(
    ISettlementService settlementService)
    : ControllerBase
{
    [Authorize(Policy = AuthConstants.Policies.Settlement)]
    [EnableRateLimiting(RateLimitPolicies.Settlement)]
    [HttpPost("generate")]
    [ProducesResponseType(
        typeof(SettlementResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SettlementResponse>> Generate(
        [FromBody] GenerateSettlementRequest request,
        CancellationToken cancellationToken)
    {
        var settlement =
            await settlementService.GenerateAsync(
                request,
                cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = settlement.Id },
            settlement);
    }

    [Authorize(Policy = AuthConstants.Policies.Settlement)]
    [Authorize(Policy = AuthConstants.Policies.Admin)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(
        typeof(SettlementResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettlementResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var settlement =
            await settlementService.GetByIdAsync(
                id,
                cancellationToken);

        return Ok(settlement);
    }

    [Authorize(Policy = AuthConstants.Policies.Settlement)]
    [Authorize(Policy = AuthConstants.Policies.Admin)]
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<SettlementResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<
        ActionResult<IReadOnlyList<SettlementResponse>>> List(
        [FromQuery] SettlementQuery query,
        CancellationToken cancellationToken)
    {
        var settlements =
            await settlementService.ListAsync(
                query,
                cancellationToken);

        return Ok(settlements);
    }
}