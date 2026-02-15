namespace StudyFlow.API.DTOs
{
    public class AiExamDto
    {
        public List<AiMcqDto> Mcq { get; set; } = new();
        public List<AiTrueFalseDto> True_False { get; set; } = new();
    }
}
