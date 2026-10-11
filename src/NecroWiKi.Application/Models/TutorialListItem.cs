namespace NecroWiKi.Application.Models
{
    public class TutorialListItem
    {
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public Guid OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }
    }
}