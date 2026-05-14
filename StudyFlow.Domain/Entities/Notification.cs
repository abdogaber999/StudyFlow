using StudyFlow.Domain.Entities;

public class Notification : BaseEntity
{
    public string UserId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string Type { get; set; } = null!; // Questions / MindMap / Audio / Video

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? LectureId { get; set; }
}