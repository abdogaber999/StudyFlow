using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlow.Domain.Entities;
using StudyFlow.Infrastructure.DbContexts;
using System.Security.Claims;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProgressController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;

        public ProgressController(StudyFlowDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // Helpers
        // =====================================================

        private async Task<StudentLectureProgress> GetOrCreateProgress(string studentId, int lectureId)
        {
            var progress = await _context.StudentLectureProgresses
                .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == lectureId);

            if (progress == null)
            {
                progress = new StudentLectureProgress
                {
                    StudentId = studentId,
                    LectureId = lectureId,
                    VideoCompleted = false,
                    AudioCompleted = false,
                    MindMapCompleted = false,
                    QuestionBankCompleted = false,
                    IsCompleted = false,
                    LastAccessedAt = DateTime.UtcNow
                };

                _context.StudentLectureProgresses.Add(progress);
            }

            return progress;
        }

        private async Task UpdateCompletionStatus(string studentId, int lectureId)
        {
            var progress = await _context.StudentLectureProgresses
                .FirstOrDefaultAsync(p => p.StudentId == studentId && p.LectureId == lectureId);

            if (progress == null) return;

            var hasQuizAttempt = await _context.StudentQuizAttempts
                .AnyAsync(a => a.StudentId == studentId && a.LectureId == lectureId);

            if (progress.VideoCompleted &&
                progress.AudioCompleted &&
                progress.MindMapCompleted &&
                progress.QuestionBankCompleted &&
                hasQuizAttempt)
            {
                progress.IsCompleted = true;
            }
        }

        // =====================================================
        // Mark Components
        // =====================================================

        [HttpPost("video/{lectureId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CompleteVideo(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null) return Unauthorized();

            var progress = await GetOrCreateProgress(studentId, lectureId);
            progress.VideoCompleted = true;
            progress.LastAccessedAt = DateTime.UtcNow;

            await UpdateCompletionStatus(studentId, lectureId);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("audio/{lectureId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CompleteAudio(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null) return Unauthorized();

            var progress = await GetOrCreateProgress(studentId, lectureId);
            progress.AudioCompleted = true;
            progress.LastAccessedAt = DateTime.UtcNow;

            await UpdateCompletionStatus(studentId, lectureId);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("mindmap/{lectureId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CompleteMindMap(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null) return Unauthorized();

            var progress = await GetOrCreateProgress(studentId, lectureId);
            progress.MindMapCompleted = true;
            progress.LastAccessedAt = DateTime.UtcNow;

            await UpdateCompletionStatus(studentId, lectureId);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("questionbank/{lectureId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CompleteQuestionBank(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null) return Unauthorized();

            var progress = await GetOrCreateProgress(studentId, lectureId);
            progress.QuestionBankCompleted = true;
            progress.LastAccessedAt = DateTime.UtcNow;

            await UpdateCompletionStatus(studentId, lectureId);
            await _context.SaveChangesAsync();

            return Ok();
        }

        // =====================================================
        // HOME SCREEN - Last Studied Subject
        // =====================================================

        [HttpGet("dashboard")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetDashboard()
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null) return Unauthorized();

            var lastProgress = await _context.StudentLectureProgresses
                .Include(p => p.Lecture)
                    .ThenInclude(l => l.Subject)
                .Where(p => p.StudentId == studentId)
                .OrderByDescending(p => p.LastAccessedAt)
                .FirstOrDefaultAsync();

            if (lastProgress == null)
                return Ok(null);

            var subject = lastProgress.Lecture.Subject;

            var totalLectures = await _context.Lectures
                .Where(l => l.SubjectId == subject.Id)
                .CountAsync();

            var completedLectures = await _context.StudentLectureProgresses
                .Include(p => p.Lecture)
                .Where(p =>
                    p.StudentId == studentId &&
                    p.Lecture.SubjectId == subject.Id &&
                    p.IsCompleted)
                .CountAsync();

            var nextLecture = await _context.Lectures
                .Where(l => l.SubjectId == subject.Id)
                .OrderBy(l => l.Order)
                .FirstOrDefaultAsync(l =>
                    !_context.StudentLectureProgresses.Any(p =>
                        p.StudentId == studentId &&
                        p.LectureId == l.Id &&
                        p.IsCompleted));

            int percentage = totalLectures == 0
                ? 0
                : (completedLectures * 100) / totalLectures;

            return Ok(new
            {
                subjectId = subject.Id,
                subjectName = subject.Name,
                totalLectures,
                completedLectures,
                percentage,
                nextLectureId = nextLecture?.Id,
                nextLectureTitle = nextLecture?.Title
            });
        }

        // =====================================================
        // MY COURSES LIST
        // =====================================================

        [HttpGet("my-courses")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetMyCoursesProgress()
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null) return Unauthorized();

            var universityId = int.Parse(User.FindFirst("UniversityId")!.Value);

            var subjects = await _context.Subjects
                .Include(s => s.Lectures)
                .Where(s => s.UniversityId == universityId)
                .ToListAsync();

            var result = new List<object>();

            foreach (var subject in subjects)
            {
                var lectureIds = subject.Lectures.Select(l => l.Id).ToList();
                var totalLectures = lectureIds.Count;

                var completedLectures = await _context.StudentLectureProgresses
                    .Where(p =>
                        p.StudentId == studentId &&
                        lectureIds.Contains(p.LectureId) &&
                        p.IsCompleted)
                    .CountAsync();

                int percentage = totalLectures == 0
                    ? 0
                    : (completedLectures * 100) / totalLectures;

                result.Add(new
                {
                    subjectId = subject.Id,
                    subjectName = subject.Name,
                    totalLectures,
                    completedLectures,
                    percentage
                });
            }

            return Ok(result);
        }
    }
}
