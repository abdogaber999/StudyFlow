namespace StudyFlow.API.DTOs
{
    public class CreateLectureDto
    {
        public string Title { get; set; } = null!;
        public int Order { get; set; }
        public int SubjectId { get; set; }
    }
}
