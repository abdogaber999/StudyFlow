using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlow.API.DTOs;
using StudyFlow.Infrastructure.DbContexts;
using StudyFlow.Domain.Entities;
using System.Security.Claims;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubjectsController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;

        public SubjectsController(StudyFlowDbContext context)
        {
            _context = context;
        }

        // 🔒 Doctor only - create subject
        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CreateSubject(CreateSubjectDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid data.");

            var universityClaim = User.FindFirst("UniversityId");
            if (universityClaim == null)
                return Unauthorized();

            var universityId = int.Parse(universityClaim.Value);

            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (doctorId == null)
                return Unauthorized();

            var subject = new Subject
            {
                Name = model.Name,
                Description = model.Description,
                UniversityId = universityId,
                DoctorId = doctorId
            };

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Subject created successfully",
                subjectId = subject.Id
            });
        }

        // 🔓 Student & Doctor - get subjects of their university
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetSubjects()
        {
            var universityId = GetUniversityIdFromToken();
            if (universityId == null)
                return Unauthorized("University information missing in token.");

            var subjects = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.UniversityId == universityId.Value)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    DoctorName = _context.Users
                        .Where(u => u.Id == s.DoctorId)
                        .Select(u => u.FullName)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(subjects);
        }

        // 🔓 Public - Get subjects by university (for register screen)
        [HttpGet("by-university/{universityId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSubjectsByUniversity(int universityId)
        {
            var subjects = await _context.Subjects
                .AsNoTracking()
                .Where(s => s.UniversityId == universityId)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description
                })
                .ToListAsync();

            return Ok(subjects);
        }

        // 🎯 Student - My Courses with Progress
        [HttpGet("my-courses")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyCourses()
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var universityId = GetUniversityIdFromToken();
            if (universityId == null)
                return Unauthorized();

            var subjects = await _context.Subjects
                .Where(s => s.UniversityId == universityId.Value)
                .ToListAsync();

            var result = new List<object>();

            foreach (var subject in subjects)
            {
                var totalLectures = await _context.Lectures
                    .CountAsync(l => l.SubjectId == subject.Id);

                var completedLectures = await _context.StudentLectureProgresses
                    .Where(p => p.StudentId == studentId &&
                                p.IsCompleted &&
                                p.Lecture.SubjectId == subject.Id)
                    .CountAsync();

                var percentage = totalLectures == 0
                    ? 0
                    : (completedLectures * 100) / totalLectures;

                var doctorName = await _context.Users
                    .Where(u => u.Id == subject.DoctorId)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync();

                result.Add(new
                {
                    subject.Id,
                    subject.Name,
                    subject.Description,
                    DoctorName = doctorName,
                    TotalLectures = totalLectures,
                    CompletedLectures = completedLectures,
                    ProgressPercentage = percentage
                });
            }

            return Ok(result);
        }

        // ======================================
        // Doctor Dashboard (Subject + Lectures)
        // ======================================
        [HttpGet("{subjectId}/dashboard")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetDoctorDashboard(int subjectId)
        {
            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (doctorId == null)
                return Unauthorized();

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(s =>
                    s.Id == subjectId &&
                    s.DoctorId == doctorId);

            if (subject == null)
                return NotFound("Subject not found.");

            var lectures = await _context.Lectures
                .Where(l => l.SubjectId == subjectId)
                .OrderBy(l => l.Order)
                .Select(l => new
                {
                    l.Id,
                    l.Title,
                    l.Order
                })
                .ToListAsync();

            return Ok(new
            {
                subjectId = subject.Id,
                subjectName = subject.Name,
                lectureCount = lectures.Count,
                lectures
            });
        }

        // 🔹 Helper method
        private int? GetUniversityIdFromToken()
        {
            var universityClaim = User.FindFirst("UniversityId");

            if (universityClaim == null)
                return null;

            return int.Parse(universityClaim.Value);
        }
    }
}