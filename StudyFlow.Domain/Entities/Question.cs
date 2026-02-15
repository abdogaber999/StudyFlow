using System.Collections.Generic;

namespace StudyFlow.Domain.Entities
{
    public class Question : BaseEntity
    {
        public string Text { get; set; } = null!;

        public string Type { get; set; } = null!;

        public int LectureId { get; set; }
        public Lecture Lecture { get; set; } = null!;

        public int? QuizId { get; set; }
        public Quiz? Quiz { get; set; }

        public ICollection<QuestionOption> QuestionOptions { get; set; }
            = new HashSet<QuestionOption>();
    }
}