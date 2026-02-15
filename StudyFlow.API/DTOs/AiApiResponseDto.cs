namespace StudyFlow.API.DTOs
{
    public class AiApiResponseDto
    {
        public string Status { get; set; } = null!;
        public AiApiDataDto Data { get; set; } = null!;
    }

    public class AiApiDataDto
    {
        public AiImportDto Exam { get; set; } = null!;
    }
}