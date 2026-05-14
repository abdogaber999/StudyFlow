using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlow.Infrastructure.DbContexts;
using System.Security.Claims;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;

        public NotificationsController(StudyFlowDbContext context)
        {
            _context = context;
        }

        // ===============================
        // 🔥 Send Custom Notification (FIXED)
        // ===============================
        [HttpPost("send")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> SendNotification([FromBody] SendNotificationDto model)
        {
            if (model == null)
                return BadRequest("Invalid data");

            var lecture = await _context.Lectures
                .Include(l => l.Subject)
                .FirstOrDefaultAsync(l => l.Id == model.LectureId);

            if (lecture == null)
                return NotFound("Lecture not found");

            var subject = lecture.Subject;

            // =========================
            // 👨‍⚕️ Doctor
            // =========================
            await _context.Notifications.AddAsync(new Notification
            {
                UserId = subject.DoctorId,
                Title = model.Title,
                Message = model.Message,
                Type = model.Type,
                LectureId = model.LectureId
            });

            // =========================
            // 👨‍🎓 Students
            // =========================
            var studentRoleId = await _context.Roles
                .Where(r => r.Name == "Student")
                .Select(r => r.Id)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(studentRoleId))
                return BadRequest("Student role not found");

            var students = await _context.UserRoles
                .Where(r => r.RoleId == studentRoleId)
                .Select(r => r.UserId)
                .ToListAsync();

            foreach (var studentId in students)
            {
                await _context.Notifications.AddAsync(new Notification
                {
                    UserId = studentId,
                    Title = model.Title,
                    Message = model.Message,
                    Type = model.Type,
                    LectureId = model.LectureId
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Notification sent successfully",
                studentsCount = students.Count
            });
        }



        // ===============================
        // 🔔 Get My Notifications
        // ===============================
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
                return Unauthorized();

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.LectureId,
                    n.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                count = notifications.Count,
                data = notifications
            });
        }
    }
}