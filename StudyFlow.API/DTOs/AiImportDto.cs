using System.Collections.Generic;

namespace StudyFlow.API.DTOs
{
    public class AiImportDto
    {
        // Question Bank
        public List<McqDto> Mcq { get; set; } = new();
        public List<TrueFalseDto> TrueFalse { get; set; } = new();

        // Mind Map coming from AI
        public MindMapNodeDto? MindMap { get; set; }
    }

    public class McqDto
    {
        public string Question { get; set; } = string.Empty;
        public Dictionary<string, string> Options { get; set; } = new();
        public string Answer { get; set; } = string.Empty;
    }

    public class TrueFalseDto
    {
        public string Question { get; set; } = string.Empty;
        public bool Answer { get; set; }
    }

    // Recursive structure for Mind Map
    public class MindMapNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<MindMapNodeDto> Children { get; set; } = new();
    }
}