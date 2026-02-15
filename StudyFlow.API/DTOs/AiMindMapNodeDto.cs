namespace StudyFlow.API.DTOs
{
    public class AiMindMapNodeDto
    {
        public string Id { get; set; } = null!;
        public string Label { get; set; } = null!;
        public List<AiMindMapNodeDto> Children { get; set; } = new();
    }
}
