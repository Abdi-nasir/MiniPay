using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MiniApy.Api.Authentication;
using MiniApy.Api.DTOs.Payments;
using MiniApy.Api.DTOs.Refunds;
using MiniApy.Api.Interfaces;
using MiniApy.Api.RateLimiting;

namespace MiniApy.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(
    IPaymentService paymentService,
    IRefundService refundService,
    CurrentUser currentUser,
    ILogger<PaymentsController> logger)
    : ControllerBase
{




    [HttpPost]
    [ProducesResponseType(
        typeof(PaymentResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Create(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await paymentService.CreateAsync(
            request.MerchantId,
            idempotencyKey,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = payment.Id },
            payment);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(
        typeof(PaymentResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var payment = await paymentService.GetByIdAsync(
            id,
            cancellationToken);

        return Ok(payment);
    }
    [Authorize(Policy = AuthConstants.Policies.Merchant)]
    [EnableRateLimiting(RateLimitPolicies.MerchantRead)]
    [HttpGet]
    public async Task<ActionResult<PaymentListResponse>> GetMyPayments(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var merchantId = currentUser.GetMerchantId();
        logger.LogWarning("Merchant ID: {MerchantId}", merchantId);

        var response = await paymentService.GetByMerchantAsync(
            merchantId,
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    [Authorize(Policy = AuthConstants.Policies.Merchant)]
    [HttpGet("{merchantId:guid}/payments")]
    [ProducesResponseType(typeof(PaymentListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentListResponse>> GetPayments(
    Guid merchantId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
    {
        var response = await paymentService.GetByMerchantAsync(
            merchantId,
            page,
            pageSize,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(
        typeof(PaymentResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        StatusCodes.Status501NotImplemented)]
    public async Task<ActionResult<PaymentResponse>> Confirm(
        [FromRoute] Guid id,
        [FromBody] ConfirmPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await paymentService.ConfirmAsync(
            id,
            request,
            cancellationToken);

        return Ok(payment);
    }

    [Authorize(Policy = AuthConstants.Policies.Merchant)]
    [EnableRateLimiting(RateLimitPolicies.MerchantWrite)]
    [HttpPost("{id:guid}/refund")]
    [ProducesResponseType(
        typeof(RefundResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RefundResponse>> Refund(
        [FromRoute] Guid id,
        [FromBody] CreateRefundRequest request,
        CancellationToken cancellationToken)
    {
        var refund = await refundService.CreateAsync(
            id,
            request,
            cancellationToken);

        return Created(
            $"/api/refunds/{refund.Id}",
            refund);
    }
}