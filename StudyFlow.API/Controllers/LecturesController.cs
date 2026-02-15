using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlow.API.DTOs;
using StudyFlow.Infrastructure.DbContexts;
using StudyFlow.Domain.Entities;
using StudyFlow.API.Services;
using System.Text.Json;
using System.Security.Claims;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LecturesController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;
        private readonly AiService _aiService;

        public LecturesController(
            StudyFlowDbContext context,
            AiService aiService)
        {
            _context = context;
            _aiService = aiService;
        }

        // ===============================
        // Create Lecture (Doctor must own subject)
        // ===============================
        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CreateLecture(CreateLectureDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid data.");

            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (doctorId == null)
                return Unauthorized();

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(s =>
                    s.Id == model.SubjectId &&
                    s.DoctorId == doctorId);

            if (subject == null)
                return Forbid("You are not allowed to create lecture for this subject.");

            var lastOrder = await _context.Lectures
                .Where(l => l.SubjectId == model.SubjectId)
                .OrderByDescending(l => l.Order)
                .Select(l => l.Order)
                .FirstOrDefaultAsync();

            var lecture = new Lecture
            {
                Title = model.Title,
                Order = lastOrder + 1,
                SubjectId = model.SubjectId
            };

            _context.Lectures.Add(lecture);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Lecture created successfully",
                lectureId = lecture.Id,
                order = lecture.Order
            });
        }

        // ===============================
        // Upload PDF + Generate Question Bank + Save MindMap
        // Doctor must own lecture
        // ===============================
        [HttpPost("upload/{lectureId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> UploadFile(int lectureId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file.");

            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (doctorId == null)
                return Unauthorized();

            var lecture = await _context.Lectures
                .Include(l => l.Subject)
                .Include(l => l.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            if (lecture.Subject.DoctorId != doctorId)
                return Forbid("You are not allowed to upload to this lecture.");

            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "lectures");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid() + "_" + file.FileName;
            var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var lectureFile = new LectureFile
            {
                LectureId = lectureId,
                FileName = file.FileName,
                FilePath = "/lectures/" + uniqueFileName,
                FileCategory = "PDF",
                GeneratedBy = "Doctor"
            };

            _context.LectureFiles.Add(lectureFile);
            await _context.SaveChangesAsync();

            var rawJson = await _aiService.ProcessPdfRawAsync(physicalPath);

            if (string.IsNullOrEmpty(rawJson))
                return Ok("File uploaded but AI processing failed.");

            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataElement))
                return Ok("File uploaded but AI response format invalid.");

            if (!dataElement.TryGetProperty("exam", out var examElement))
                return Ok("File uploaded but no exam found.");

            var aiResult = JsonSerializer.Deserialize<AiImportDto>(
                examElement.GetRawText(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (aiResult == null)
                return Ok("File uploaded but AI exam parsing failed.");

            if (dataElement.TryGetProperty("mind_map", out var mindMapElement))
            {
                lecture.MindMapJson = mindMapElement.GetRawText();
                await _context.SaveChangesAsync();
            }

            var oldBankQuestions = lecture.Questions
                .Where(q => q.QuizId == null)
                .ToList();

            if (oldBankQuestions.Any())
            {
                _context.QuestionOptions.RemoveRange(
                    oldBankQuestions.SelectMany(q => q.QuestionOptions)
                );

                _context.Questions.RemoveRange(oldBankQuestions);
                await _context.SaveChangesAsync();
            }

            var newQuestions = new List<Question>();

            if (aiResult.Mcq != null)
            {
                foreach (var mcq in aiResult.Mcq)
                {
                    newQuestions.Add(new Question
                    {
                        Text = mcq.Question,
                        Type = "MCQ",
                        LectureId = lectureId,
                        QuestionOptions = mcq.Options.Select(o =>
                            new QuestionOption
                            {
                                Text = o.Value,
                                IsCorrect = o.Key == mcq.Answer
                            }).ToList()
                    });
                }
            }

            if (aiResult.TrueFalse != null)
            {
                foreach (var tf in aiResult.TrueFalse)
                {
                    newQuestions.Add(new Question
                    {
                        Text = tf.Question,
                        Type = "TrueFalse",
                        LectureId = lectureId,
                        QuestionOptions = new List<QuestionOption>
                        {
                            new QuestionOption
                            {
                                Text = "True",
                                IsCorrect = tf.Answer
                            },
                            new QuestionOption
                            {
                                Text = "False",
                                IsCorrect = !tf.Answer
                            }
                        }
                    });
                }
            }

            _context.Questions.AddRange(newQuestions);
            await _context.SaveChangesAsync();

            return Ok("File uploaded and AI questions + mind map generated successfully.");
        }

        // ===============================
        // Get Full Lecture Content
        // ===============================
        [HttpGet("{lectureId}/content")]
        [Authorize]
        public async Task<IActionResult> GetLectureContent(int lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.LectureFiles)
                .Include(l => l.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            var questions = lecture.Questions
                .Where(q => q.QuizId == null)
                .Select(q => new
                {
                    q.Id,
                    q.Text,
                    q.Type,
                    Options = q.QuestionOptions.Select(o => new
                    {
                        o.Id,
                        o.Text
                    }).ToList()
                })
                .ToList();

            return Ok(new
            {
                lecture.Id,
                lecture.Title,
                lecture.Order,
                files = lecture.LectureFiles.Select(f => new
                {
                    f.Id,
                    f.FileName,
                    f.FilePath,
                    f.FileCategory
                }).ToList(),
                questions,
                mindMap = string.IsNullOrEmpty(lecture.MindMapJson)
                    ? null
                    : JsonSerializer.Deserialize<object>(lecture.MindMapJson)
            });
        }

        // ===============================
        // Delete Lecture (Doctor must own it)
        // ===============================
        [HttpDelete("{lectureId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> DeleteLecture(int lectureId)
        {
            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (doctorId == null)
                return Unauthorized();

            var lecture = await _context.Lectures
                .Include(l => l.Subject)
                .Include(l => l.Questions)
                    .ThenInclude(q => q.QuestionOptions)
                .Include(l => l.LectureFiles)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            if (lecture.Subject.DoctorId != doctorId)
                return Forbid("You are not allowed to delete this lecture.");

            _context.QuestionOptions.RemoveRange(
                lecture.Questions.SelectMany(q => q.QuestionOptions)
            );

            _context.Questions.RemoveRange(lecture.Questions);
            _context.LectureFiles.RemoveRange(lecture.LectureFiles);
            _context.Lectures.Remove(lecture);

            await _context.SaveChangesAsync();

            return Ok("Lecture deleted successfully.");
        }
    }
}