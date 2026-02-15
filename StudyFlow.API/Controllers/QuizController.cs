using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlow.API.DTOs;
using StudyFlow.Domain.Entities;
using StudyFlow.Infrastructure.DbContexts;
using System.Security.Claims;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;

        public QuizController(StudyFlowDbContext context)
        {
            _context = context;
        }

        // =====================================
        // Check if Quiz is Available
        // =====================================
        [HttpGet("available/{lectureId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> IsQuizAvailable(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var progress = await _context.StudentLectureProgresses
                .FirstOrDefaultAsync(p =>
                    p.StudentId == studentId &&
                    p.LectureId == lectureId);

            if (progress == null)
                return Ok(new { isAvailable = false });

            bool isAvailable =
                progress.VideoCompleted &&
                progress.AudioCompleted &&
                progress.MindMapCompleted &&
                progress.QuestionBankCompleted;

            return Ok(new { isAvailable });
        }

        // =====================================
        // Start Quiz (Wrong Questions Only)
        // =====================================
        [HttpGet("start/{lectureId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> StartQuiz(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var progress = await _context.StudentLectureProgresses
                .FirstOrDefaultAsync(p =>
                    p.StudentId == studentId &&
                    p.LectureId == lectureId);

            if (progress == null ||
                !progress.VideoCompleted ||
                !progress.AudioCompleted ||
                !progress.MindMapCompleted ||
                !progress.QuestionBankCompleted)
            {
                return StatusCode(403, "Quiz is not available yet.");
            }

            var wrongQuestionIds = await _context.StudentQuestionBankAnswers
                .Include(a => a.Question)
                .Where(a =>
                    a.StudentId == studentId &&
                    a.Question.LectureId == lectureId &&
                    !a.IsCorrect)
                .Select(a => a.QuestionId)
                .Distinct()
                .ToListAsync();

            if (!wrongQuestionIds.Any())
            {
                return Ok(new
                {
                    message = "Excellent! No wrong questions."
                });
            }

            var questions = await _context.Questions
                .Include(q => q.QuestionOptions)
                .Where(q => wrongQuestionIds.Contains(q.Id))
                .ToListAsync();

            var result = questions.Select(q => new
            {
                q.Id,
                q.Text,
                q.Type,
                Options = q.QuestionOptions.Select(o => new
                {
                    o.Id,
                    o.Text
                })
            });

            return Ok(result);
        }

        // =====================================
        // Submit Quiz
        // =====================================
        [HttpPost("submit")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitQuiz(SubmitQuizDto model)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var questionIds = model.Answers.Select(a => a.QuestionId).ToList();

            var questions = await _context.Questions
                .Include(q => q.QuestionOptions)
                .Where(q => questionIds.Contains(q.Id))
                .ToListAsync();

            int score = 0;
            var resultDetails = new List<object>();

            foreach (var answer in model.Answers)
            {
                var question = questions.FirstOrDefault(q => q.Id == answer.QuestionId);
                if (question == null)
                    continue;

                var correctOption = question.QuestionOptions
                    .FirstOrDefault(o => o.IsCorrect);

                bool isCorrect = correctOption != null &&
                                 correctOption.Id == answer.SelectedOptionId;

                if (isCorrect)
                    score++;

                // 🔥 نصلّح سؤال QuestionBank لو اتحل صح
                var bankAnswer = await _context.StudentQuestionBankAnswers
                    .FirstOrDefaultAsync(a =>
                        a.StudentId == studentId &&
                        a.QuestionId == question.Id);

                if (bankAnswer != null && isCorrect)
                {
                    bankAnswer.IsCorrect = true;
                    bankAnswer.SelectedOptionId = answer.SelectedOptionId;
                }

                resultDetails.Add(new
                {
                    questionId = question.Id,
                    isCorrect,
                    correctOptionId = correctOption?.Id
                });
            }

            await _context.SaveChangesAsync();

            var attempt = new StudentQuizAttempt
            {
                StudentId = studentId,
                LectureId = model.LectureId,
                Score = score,
                TotalQuestions = model.Answers.Count,
                TakenAt = DateTime.UtcNow
            };

            _context.StudentQuizAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                score,
                total = model.Answers.Count,
                percentage = model.Answers.Count == 0
                    ? 0
                    : (score * 100) / model.Answers.Count,
                details = resultDetails
            });
        }

        // =====================================
        // Get Quiz History
        // =====================================
        [HttpGet("history/{lectureId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetQuizHistory(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var attempts = await _context.StudentQuizAttempts
                .Where(a => a.StudentId == studentId && a.LectureId == lectureId)
                .OrderByDescending(a => a.TakenAt)
                .ToListAsync();

            if (!attempts.Any())
                return Ok(new { attempts = 0, avgScore = 0, lastScore = 0 });

            var avgScore = attempts.Average(a =>
                a.TotalQuestions == 0 ? 0 : (a.Score * 100.0) / a.TotalQuestions);

            return Ok(new
            {
                attempts = attempts.Count,
                avgScore = (int)avgScore,
                lastScore = attempts.First().TotalQuestions == 0
                    ? 0
                    : (attempts.First().Score * 100) / attempts.First().TotalQuestions
            });
        }
    }
}