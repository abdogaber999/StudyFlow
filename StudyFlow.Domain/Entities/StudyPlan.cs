using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyFlow.Domain.Entities
{
    public class StudyPlan : BaseEntity
    {
        public int StudentId { get; set; }

        public int SubjectId { get; set; }

        public int? LectureId { get; set; }

        public string TaskType { get; set; } = null!;

        public DateTime PlannedDate { get; set; }

        public bool IsCompleted { get; set; }

        public Subject Subject { get; set; } = null!;
    }
}
