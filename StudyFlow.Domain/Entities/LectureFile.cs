using System;

namespace StudyFlow.Domain.Entities
{
    public class LectureFile : BaseEntity
    {
        public string FileName { get; set; } = null!;

        public string FilePath { get; set; } = null!;

        // PDF / Video / Audio / MindMap
        public string FileCategory { get; set; } = null!;

        // Doctor / AI
        public string GeneratedBy { get; set; } = null!;

        public int LectureId { get; set; }

        public Lecture Lecture { get; set; } = null!;
    }
}