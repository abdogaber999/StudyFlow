namespace StudyFlow.Domain.Entities
{
    public class StudentQuizAttempt : BaseEntity
    {
        public string StudentId { get; set; } = null!;

        public int LectureId { get; set; }

        public int Score { get; set; }

        public int TotalQuestions { get; set; }

        public DateTime TakenAt { get; set; }
    }
}
