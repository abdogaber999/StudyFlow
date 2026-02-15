using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyFlow.Domain.Entities
{
    public class Quiz : BaseEntity
    {
        public int LectureId { get; set; }

        public Lecture Lecture { get; set; } = null!;

        public ICollection<Question> Questions { get; set; } = new HashSet<Question>();
    }
}
