using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniApy.Api.Authentication;
using MiniApy.Api.DTOs.Transactions;
using MiniApy.Api.Interfaces;

namespace MiniApy.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(
    ITransactionService transactionService)
    : ControllerBase
{
    [Authorize(Policy = AuthConstants.Policies.Admin)]
    [HttpGet]
    [ProducesResponseType(
        typeof(IReadOnlyList<TransactionResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TransactionResponse>>> List(
        [FromQuery] TransactionQuery query,
        CancellationToken cancellationToken)
    {
        var transactions = await transactionService.ListAsync(
            query,
            cancellationToken);

        return Ok(transactions);
    }
}