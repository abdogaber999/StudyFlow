using System;

namespace StudyFlow.Domain.Entities
{
    public class LectureNote : BaseEntity
    {
        // الطالب صاحب النوت
        public string StudentId { get; set; } = null!;

        // المحاضرة المرتبطة
        public int LectureId { get; set; }

        // محتوى النوت
        public string Content { get; set; } = null!;

        // نوع الريسورس (Video / PDF / MindMap / General)
        public string ResourceType { get; set; } = "General";

        // لو النوت مرتبطة بدقيقة في فيديو مثلاً
        public int? TimeStampSeconds { get; set; }

        // وقت الإنشاء
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        // Navigation Properties
        public Lecture Lecture { get; set; } = null!;
    }
}