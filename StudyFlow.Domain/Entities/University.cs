using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyFlow.Domain.Entities
{
    public class University : BaseEntity
    {
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public ICollection<Subject> Subjects { get; set; } = new HashSet<Subject>();
    }
}
