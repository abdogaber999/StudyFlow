using Microsoft.EntityFrameworkCore;
using StudyFlow.Domain.Entities;
using StudyFlow.Infrastructure.DbContexts;

namespace StudyFlow.API.Services
{
    public class NotificationService
    {
        private readonly StudyFlowDbContext _context;

        public NotificationService(StudyFlowDbContext context)
        {
            _context = context;
        }

        public async Task SendToLectureUsers(int lectureId, string type)
        {
            var lecture = await _context.Lectures
                .Include(l => l.Subject)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return;

            var subject = lecture.Subject;

            // =========================
            // 🧠 Generate Message Dynamically
            // =========================
            string title = "";
            string message = "";

            switch (type)
            {
                case "Questions":
                    title = "Questions Ready 🧠";
                    message = $"Questions for lecture '{lecture.Title}' in subject '{subject.Name}' are now ready";
                    break;

                case "MindMap":
                    title = "MindMap Ready 🧩";
                    message = $"MindMap for lecture '{lecture.Title}' in subject '{subject.Name}' is now ready";
                    break;

                case "Audio":
                    title = "Audio Ready 🎧";
                    message = $"Audio for lecture '{lecture.Title}' in subject '{subject.Name}' is now ready";
                    break;

                case "Video":
                    title = "Video Ready 🎥";
                    message = $"Video for lecture '{lecture.Title}' in subject '{subject.Name}' is now ready";
                    break;
            }

            // =========================
            // 👨‍⚕️ Doctor
            // =========================
            await _context.Notifications.AddAsync(new Notification
            {
                UserId = subject.DoctorId,
                Title = title,
                Message = message,
                Type = type,
                LectureId = lectureId
            });

            // =========================
            // 👨‍🎓 Students (same university)
            // =========================
            var studentRoleId = await _context.Roles
                .Where(r => r.Name == "Student")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            var students = await _context.UserRoles
                .Where(ur => ur.RoleId == studentRoleId)
                .Join(_context.Users,
                    ur => ur.UserId,
                    u => u.Id,
                    (ur, u) => u)
                .Where(u => u.UniversityId == subject.UniversityId)
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var studentId in students)
            {
                await _context.Notifications.AddAsync(new Notification
                {
                    UserId = studentId,
                    Title = title,
                    Message = message,
                    Type = type,
                    LectureId = lectureId
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}