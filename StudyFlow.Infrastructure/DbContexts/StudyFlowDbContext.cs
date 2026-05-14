using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyFlow.Domain.Entities;
using StudyFlow.Infrastructure.Identity;

namespace StudyFlow.Infrastructure.DbContexts
{
    public class StudyFlowDbContext : IdentityDbContext<ApplicationUser>
    {
        public StudyFlowDbContext(DbContextOptions<StudyFlowDbContext> options)
            : base(options)
        {
        }

        public DbSet<University> Universities { get; set; }
        public DbSet<StudentLectureProgress> StudentLectureProgresses { get; set; }

        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Lecture> Lectures { get; set; }
        public DbSet<LectureFile> LectureFiles { get; set; }

        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }

        public DbSet<StudentQuizAttempt> StudentQuizAttempts { get; set; }
        public DbSet<StudentQuestionBankAnswer> StudentQuestionBankAnswers { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<StudyPlan> StudyPlans { get; set; }

        // =========================
        // Lecture Notes
        // =========================
        public DbSet<LectureNote> LectureNotes { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<StudentLectureProgress>()
                .ToTable("StudentLectureProgress");

            // =========================
            // Lecture -> LectureFiles
            // =========================
            builder.Entity<LectureFile>()
                .HasOne(lf => lf.Lecture)
                .WithMany(l => l.LectureFiles)
                .HasForeignKey(lf => lf.LectureId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================
            // Lecture -> Quiz
            // =========================
            builder.Entity<Quiz>()
                .HasOne(q => q.Lecture)
                .WithMany(l => l.Quizzes)
                .HasForeignKey(q => q.LectureId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================
            // Quiz -> Question
            // =========================
            builder.Entity<Question>()
                .HasOne(q => q.Quiz)
                .WithMany(qz => qz.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================
            // Question -> Lecture (NO CASCADE)
            // =========================
            builder.Entity<Question>()
                .HasOne(q => q.Lecture)
                .WithMany(l => l.Questions)
                .HasForeignKey(q => q.LectureId)
                .OnDelete(DeleteBehavior.Restrict);

            // =========================
            // Question -> Options
            // =========================
            builder.Entity<QuestionOption>()
                .HasOne(o => o.Question)
                .WithMany(q => q.QuestionOptions)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // =========================
            // Lecture -> LectureNotes (FIXED RELATION)
            // =========================
            builder.Entity<LectureNote>()
                .HasOne(n => n.Lecture)
                .WithMany(l => l.LectureNotes)
                .HasForeignKey(n => n.LectureId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}