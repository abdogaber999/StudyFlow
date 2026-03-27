using System.Collections.Generic;

namespace StudyFlow.Domain.Entities
{
    public class Lecture : BaseEntity
    {
        public string Title { get; set; } = null!;

        public int Order { get; set; }

        public int SubjectId { get; set; }

        public Subject Subject { get; set; } = null!;

        // Mind Map JSON generated from AI
        // Stores the full mind map structure as JSON
        public string? MindMapJson { get; set; }

        // AI Generated Video URL
        public string? VideoUrl { get; set; }

        // AI Generated Podcast Audio URL
        public string? AudioUrl { get; set; }

        // 🔹 Video generation status (Processing / Ready / Failed)
        public string VideoStatus { get; set; } = "Processing";

        // 🔹 Audio generation status (Processing / Ready / Failed)
        public string AudioStatus { get; set; } = "Processing";

        // Lecture files (PDF / Video / Audio etc.)
        public ICollection<LectureFile> LectureFiles { get; set; }
            = new HashSet<LectureFile>();

        // Quizzes generated from wrong questions
        public ICollection<Quiz> Quizzes { get; set; }
            = new HashSet<Quiz>();

        // Question Bank questions linked directly to lecture
        public ICollection<Question> Questions { get; set; }
            = new HashSet<Question>();

        // Student Notes related to this lecture
        public ICollection<LectureNote> LectureNotes { get; set; }
            = new HashSet<LectureNote>();
    }
}