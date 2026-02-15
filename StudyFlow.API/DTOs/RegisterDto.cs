namespace StudyFlow.API.DTOs
{
    public class RegisterDto
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string NationalId { get; set; } = null!;
        public string Role { get; set; } = null!;
        public int UniversityId { get; set; }
    }
}
