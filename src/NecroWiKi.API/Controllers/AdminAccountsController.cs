using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NecroWiKi.API.Models;
using NecroWiKi.API.Services;

namespace NecroWiKi.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/accounts")]
public sealed class AdminAccountsController : ControllerBase
{
    private readonly PortalIdentityService _identityService;

    public AdminAccountsController(PortalIdentityService identityService)
    {
        _identityService = identityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken) =>
        Ok(await _identityService.GetAccountsAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateAccount(
        [FromBody] CreatePortalAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityService.CreateAccountAsync(
                request.UserName, request.Password, request.Role, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAccount(
        [FromRoute] Guid id,
        [FromBody] UpdatePortalAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            PortalAccountSummary? account = await _identityService.UpdateAccountAsync(
                id, request.Role, request.IsActive, request.NewPassword, cancellationToken);
            return account is null ? NotFound() : Ok(account);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAccount([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        try
        {
            string? requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(requesterId, out Guid accountId))
            {
                return Unauthorized();
            }

            return await _identityService.DeleteAccountAsync(id, accountId, cancellationToken)
                ? NoContent()
                : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}