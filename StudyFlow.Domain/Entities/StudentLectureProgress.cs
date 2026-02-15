using System;

namespace StudyFlow.Domain.Entities
{
    public class StudentLectureProgress : BaseEntity
    {
        public string StudentId { get; set; } = null!;

        public int LectureId { get; set; }

        public bool VideoCompleted { get; set; }

        public bool AudioCompleted { get; set; }

        public bool MindMapCompleted { get; set; }

        public bool QuestionBankCompleted { get; set; }

        public bool IsCompleted { get; set; }

        public Lecture Lecture { get; set; } = null!;
        public DateTime LastAccessedAt { get; set; }

    }
}
