using System.Collections.Generic;

namespace StudyFlow.Domain.Entities
{
    public class Subject : BaseEntity
    {
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int UniversityId { get; set; }
        public University University { get; set; } = null!;

        // 🔥 FK فقط
        public string DoctorId { get; set; } = null!;

        public ICollection<Lecture> Lectures { get; set; }
            = new HashSet<Lecture>();
    }
}