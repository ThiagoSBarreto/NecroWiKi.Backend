namespace NecroWiKi.Application.Interfaces;

public interface ISaveStateIdentityService
{
    Task<bool> IsRegisteredAsync(Guid uuid, CancellationToken cancellationToken);
    Task<Guid?> ResolveAsync(string userName, string password, Guid? currentUuid, CancellationToken cancellationToken);
}