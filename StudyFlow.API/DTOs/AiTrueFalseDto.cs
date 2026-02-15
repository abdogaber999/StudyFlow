namespace StudyFlow.API.DTOs
{
    public class AiTrueFalseDto
    {
        public int Id { get; set; }
        public string Question { get; set; } = null!;
        public bool Answer { get; set; }
    }
}
