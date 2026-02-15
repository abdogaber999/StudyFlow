namespace StudyFlow.API.DTOs
{
    public class SubmitQuizDto
    {
        public int LectureId { get; set; }

        public List<QuizAnswerDto> Answers { get; set; } = new();
    }

    public class QuizAnswerDto
    {
        public int QuestionId { get; set; }
        public int SelectedOptionId { get; set; }
    }
}
