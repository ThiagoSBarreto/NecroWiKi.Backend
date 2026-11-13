namespace NecroWiKi.API.Models;

public sealed record PortalAccountSummary(Guid Id, string UserName, string Role, bool IsActive, DateTime CreatedAtUtc);

public sealed record PortalAuthResponse(string Token, DateTime ExpiresAtUtc, PortalAccountSummary Account);

public sealed class PortalRegisterRequest
{
    public string? UserName { get; init; }
    public string? Password { get; init; }
}

public sealed class PortalLoginRequest
{
    public string? UserName { get; init; }
    public string? Password { get; init; }
}

public sealed class CreatePortalAccountRequest
{
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public string? Role { get; init; }
}

public sealed class UpdatePortalAccountRequest
{
    public string? Role { get; init; }
    public bool IsActive { get; init; }
    public string? NewPassword { get; init; }
}