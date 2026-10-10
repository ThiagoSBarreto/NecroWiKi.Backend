namespace NecroWiKi.Application.Interfaces;

public interface ISaveStateService
{
    Task SaveAsync(Guid browserId, string gameKey, Stream content, CancellationToken cancellationToken);
    Task<Stream?> LoadAsync(Guid browserId, string gameKey, CancellationToken cancellationToken);
    Task MergeAsync(Guid sourceUuid, Guid targetUuid, CancellationToken cancellationToken);
}