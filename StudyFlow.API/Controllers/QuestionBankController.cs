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
    public class QuestionBankController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;

        public QuestionBankController(StudyFlowDbContext context)
        {
            _context = context;
        }

        // =====================================
        // Submit Question Bank
        // =====================================
        [HttpPost("submit")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> SubmitQuestionBank(SubmitQuestionBankDto model)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var lecture = await _context.Lectures
                .FirstOrDefaultAsync(l => l.Id == model.LectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            var questions = await _context.Questions
                .Include(q => q.QuestionOptions)
                .Where(q =>
                    q.LectureId == model.LectureId &&
                    q.QuizId == null)   // 🔥 Question Bank only
                .ToListAsync();

            if (!questions.Any())
                return BadRequest("No question bank found for this lecture.");

            int score = 0;
            var wrongQuestions = new List<Question>();
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
                else
                    wrongQuestions.Add(question);

                var existingAnswer = await _context.StudentQuestionBankAnswers
                    .FirstOrDefaultAsync(a =>
                        a.StudentId == studentId &&
                        a.QuestionId == question.Id);

                if (existingAnswer == null)
                {
                    _context.StudentQuestionBankAnswers.Add(new StudentQuestionBankAnswer
                    {
                        StudentId = studentId,
                        QuestionId = question.Id,
                        SelectedOptionId = answer.SelectedOptionId,
                        IsCorrect = isCorrect
                    });
                }
                else
                {
                    existingAnswer.SelectedOptionId = answer.SelectedOptionId;
                    existingAnswer.IsCorrect = isCorrect;
                }

                resultDetails.Add(new
                {
                    questionId = question.Id,
                    isCorrect,
                    correctOptionId = correctOption?.Id
                });
            }

            // =====================================
            // 🔥 Generate Quiz From Wrong Questions
            // =====================================
            int? quizId = null;

            if (wrongQuestions.Any())
            {
                var quiz = new Quiz
                {
                    LectureId = model.LectureId
                };

                _context.Quizzes.Add(quiz);
                await _context.SaveChangesAsync();

                quizId = quiz.Id;

                foreach (var question in wrongQuestions)
                {
                    question.QuizId = quiz.Id;
                }
            }

            // =====================================
            // 🔥 Mark Question Bank As Completed
            // =====================================
            var progress = await _context.StudentLectureProgresses
                .FirstOrDefaultAsync(p =>
                    p.StudentId == studentId &&
                    p.LectureId == model.LectureId);

            if (progress == null)
            {
                progress = new StudentLectureProgress
                {
                    StudentId = studentId,
                    LectureId = model.LectureId,
                    QuestionBankCompleted = true
                };

                _context.StudentLectureProgresses.Add(progress);
            }
            else
            {
                progress.QuestionBankCompleted = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                score,
                total = model.Answers.Count,
                percentage = model.Answers.Count == 0
                    ? 0
                    : (score * 100) / model.Answers.Count,
                quizGenerated = quizId != null,
                quizId,
                details = resultDetails
            });
        }

        // =====================================
        // Get Question Bank Stats
        // =====================================
        [HttpGet("stats/{lectureId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetStats(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var questionBankAnswers = await _context.StudentQuestionBankAnswers
                .Include(a => a.Question)
                .Where(a =>
                    a.StudentId == studentId &&
                    a.Question.LectureId == lectureId &&
                    a.Question.QuizId == null)
                .ToListAsync();

            int solved = questionBankAnswers.Count;
            int correct = questionBankAnswers.Count(a => a.IsCorrect);

            return Ok(new
            {
                solved,
                correct,
                wrong = solved - correct
            });
        }
    }
}