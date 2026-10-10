namespace NecroWiKi.Infrastructure.Persistence;

public class SaveStateUser
{
    public string User { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Guid Uuid { get; set; }
}