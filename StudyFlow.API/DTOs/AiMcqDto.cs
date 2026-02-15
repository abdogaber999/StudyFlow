namespace StudyFlow.API.DTOs
{
    public class AiMcqDto
    {
        public int Id { get; set; }
        public string Question { get; set; } = null!;
        public Dictionary<string, string> Options { get; set; } = new();
        public string Answer { get; set; } = null!;
    }
}
