using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyFlow.Domain.Entities
{
    public class QuestionOption : BaseEntity
    {
        public string Text { get; set; } = null!;

        public bool IsCorrect { get; set; }

        public int QuestionId { get; set; }

        public Question Question { get; set; } = null!;
    }
}
