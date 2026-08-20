using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniApy.Api.Authentication;
using MiniApy.Api.DTOs.Merchants;
using MiniApy.Api.Interfaces;
using MiniApy.Api.RateLimiting;

namespace MiniApy.Api.Controllers;

[Authorize(Policy = AuthConstants.Policies.Merchant )]
[ApiController]
[Route("api/merchants")]
public sealed class MerchantsController(
    IMerchantService merchantService)
    : ControllerBase
{
    [Authorize(Policy = AuthConstants.Policies.Admin)]
    [EnableRateLimiting(RateLimitPolicies.MerchantWrite)]
    [HttpPost]
    [ProducesResponseType(
        typeof(MerchantRegistrationResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MerchantRegistrationResponse>> Register(
        [FromBody] MerchantRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var merchant = await merchantService.RegisterAsync(
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = merchant.Id },
            merchant);
    }

    
    [Authorize(Policy = AuthConstants.Policies.Admin)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(
        typeof(MerchantResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MerchantResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var merchant = await merchantService.GetByIdAsync(
            id,
            cancellationToken);

        return Ok(merchant);
    }

    [Authorize(Policy = AuthConstants.Policies.Admin)]
    [HttpGet("list")]
    [ProducesResponseType(
        typeof(IReadOnlyList<MerchantResponse>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MerchantResponse>>> List(
       [FromQuery] MerchantsQuery query,
        CancellationToken cancellationToken)
    {
        var merchants = await merchantService.ListAsync(
            query,
            cancellationToken);

        return Ok(merchants);
    }
}