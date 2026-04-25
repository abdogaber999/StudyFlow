using StudyFlow.Domain.Entities;

public class Lecture : BaseEntity
{
    public string Title { get; set; } = null!;

    public int Order { get; set; }

    public int SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    // 🔥 Mind Map
    public string? MindMapUrl { get; set; }

    public string MindMapStatus { get; set; } = "NotStarted"; // ✅ جديد

    public string? MindMapError { get; set; } // ✅ جديد

    // 🎥 Video
    public string? VideoUrl { get; set; }

    public string VideoStatus { get; set; } = "NotStarted";

    public string? VideoError { get; set; }

    // 🎧 Audio
    public string? AudioUrl { get; set; }

    public string AudioStatus { get; set; } = "NotStarted";

    public string? AudioError { get; set; }

    // Files
    public ICollection<LectureFile> LectureFiles { get; set; }
        = new HashSet<LectureFile>();

    public ICollection<Quiz> Quizzes { get; set; }
        = new HashSet<Quiz>();

    public ICollection<Question> Questions { get; set; }
        = new HashSet<Question>();

    public ICollection<LectureNote> LectureNotes { get; set; }
        = new HashSet<LectureNote>();
}