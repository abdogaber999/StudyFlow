using System;

namespace StudyFlow.Domain.Entities
{
    public class StudentQuestionAnswer : BaseEntity
    {
        public string StudentId { get; set; } = null!;

        public int QuestionId { get; set; }

        public int SelectedOptionId { get; set; }

        public bool IsCorrect { get; set; }

        public Question Question { get; set; } = null!;
    }
}
