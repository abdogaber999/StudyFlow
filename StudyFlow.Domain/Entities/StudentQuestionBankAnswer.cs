using System;

namespace StudyFlow.Domain.Entities
{
    public class StudentQuestionBankAnswer : BaseEntity
    {
        public string StudentId { get; set; }

        public int QuestionId { get; set; }

        public int SelectedOptionId { get; set; }

        public bool IsCorrect { get; set; }

        public Question Question { get; set; } = null!;
    }
}
