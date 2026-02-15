namespace StudyFlow.API.DTOs
{
    public class CreateSubjectDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int UniversityId { get; set; }
    }
}
