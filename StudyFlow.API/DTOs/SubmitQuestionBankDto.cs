namespace StudyFlow.API.DTOs
{
    public class SubmitQuestionBankDto
    {
        public int LectureId { get; set; }

        public List<QuestionAnswerDto> Answers { get; set; } = new();
    }

    public class QuestionAnswerDto
    {
        public int QuestionId { get; set; }

        public int SelectedOptionId { get; set; }
    }
}
