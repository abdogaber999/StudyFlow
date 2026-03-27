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
        private readonly IWebHostEnvironment _env;

        public LecturesController(
    StudyFlowDbContext context,
    AiService aiService,
    IWebHostEnvironment env)
        {
            _context = context;
            _aiService = aiService;
            _env = env;
        }

        // ===============================
        // Create Lecture
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
        // Upload Files Only
        // ===============================
        [HttpPost("upload/{lectureId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> UploadFile(int lectureId, List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");

            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (doctorId == null)
                return Unauthorized();

            var lecture = await _context.Lectures
                .Include(l => l.Subject)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            if (lecture.Subject.DoctorId != doctorId)
                return Forbid("You are not allowed to upload to this lecture.");

            // 🔥 المسار الموحد للملفات
            var uploadsFolder = Path.Combine(_env.WebRootPath, "lectures");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".ppt", ".pptx" };

            var uploadedFiles = new List<string>();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                    continue;

                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                    continue;

                // 🔥 اسم ملف آمن
                var safeFileName = Path.GetFileName(file.FileName);

                var uniqueFileName = $"{Guid.NewGuid()}_{safeFileName}";

                var physicalPath = Path.Combine(
                    uploadsFolder,
                    uniqueFileName
                );

                // حفظ الملف
                using var stream = new FileStream(
                    physicalPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true
                );

                await file.CopyToAsync(stream);

                var lectureFile = new LectureFile
                {
                    LectureId = lectureId,
                    FileName = safeFileName,
                    FilePath = "/lectures/" + uniqueFileName,
                    FileCategory = extension.Replace(".", "").ToUpper(),
                    GeneratedBy = "Doctor"
                };

                _context.LectureFiles.Add(lectureFile);

                uploadedFiles.Add(uniqueFileName);
            }

            // 🔥 Save مرة واحدة فقط
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Files uploaded successfully",
                filesCount = uploadedFiles.Count,
                files = uploadedFiles
            });
        }


        // ===============================
        // 🔥 Test AI RAW (Direct Call)
        // ===============================
        [HttpGet("test-ai-raw")]
        [AllowAnonymous]
        public async Task<IActionResult> TestAiRaw(int lectureId)
        {
            try
            {
                var lecture = await _context.Lectures
                    .Include(l => l.LectureFiles)
                    .FirstOrDefaultAsync(l => l.Id == lectureId);

                if (lecture == null)
                    return Ok(new { step = "LECTURE_NOT_FOUND" });

                var pdf = lecture.LectureFiles
                    .FirstOrDefault(f => f.FileCategory == "PDF");

                if (pdf == null)
                    return Ok(new { step = "PDF_NOT_FOUND" });

                var physicalPath = Path.Combine(
                    _env.WebRootPath,
                    "lectures",
                    Path.GetFileName(pdf.FilePath)
                );

                if (!System.IO.File.Exists(physicalPath))
                {
                    return Ok(new
                    {
                        step = "FILE_CHECK",
                        path = physicalPath,
                        webRoot = _env.WebRootPath
                    });
                }

                // =====================================
                // 🔥 Ping AI Server
                // =====================================
                try
                {
                    using var pingClient = new HttpClient();
                    pingClient.Timeout = TimeSpan.FromSeconds(10);

                    var ping = await pingClient.GetAsync("http://187.124.161.116/");

                    if (!ping.IsSuccessStatusCode)
                    {
                        return Ok(new
                        {
                            step = "SERVER_CONNECTION",
                            status = ping.StatusCode.ToString()
                        });
                    }
                }
                catch (Exception ex)
                {
                    return Ok(new
                    {
                        step = "SERVER_CONNECTION_FAILED",
                        error = ex.Message
                    });
                }

                // =====================================
                // 🔥 Direct AI Call
                // =====================================
                var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);

                using var form = new MultipartFormDataContent();

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

                // 🔥 بدون text
                form.Add(fileContent, "file", Path.GetFileName(physicalPath));

                using var aiClient = new HttpClient();
                aiClient.Timeout = TimeSpan.FromMinutes(5);
                aiClient.BaseAddress = new Uri("http://187.124.161.116/");

                var response = await aiClient.PostAsync(
                    "api/v1/text/generate-educational-package",
                    form
                );

                var content = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    step = "RAW_RESPONSE",
                    status = response.StatusCode.ToString(),
                    length = content?.Length,
                    response = content
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    step = "EXCEPTION",
                    error = ex.Message
                });
            }
        }


        // ===============================
        // 🔥 Test AI (Production Flow)
        // ===============================
        [HttpGet("test-ai")]
        [AllowAnonymous]
        public async Task<IActionResult> TestAI(int lectureId)
        {
            try
            {
                var lecture = await _context.Lectures
                    .Include(l => l.LectureFiles)
                    .FirstOrDefaultAsync(l => l.Id == lectureId);

                if (lecture == null)
                    return Ok(new { step = "LECTURE_NOT_FOUND" });

                var pdf = lecture.LectureFiles
                    .FirstOrDefault(f => f.FileCategory == "PDF");

                if (pdf == null)
                    return Ok(new { step = "PDF_NOT_FOUND" });

                var physicalPath = Path.Combine(
                    _env.WebRootPath,
                    "lectures",
                    Path.GetFileName(pdf.FilePath)
                );

                if (!System.IO.File.Exists(physicalPath))
                {
                    return Ok(new
                    {
                        step = "FILE_CHECK",
                        path = physicalPath
                    });
                }

                // =====================================
                // 🔥 Call AI via Service
                // =====================================
                var cleanJson = await _aiService.ProcessPdfRawAsync(physicalPath);

                if (string.IsNullOrEmpty(cleanJson))
                {
                    return Ok(new
                    {
                        step = "AI_EMPTY_RESPONSE"
                    });
                }

                JsonDocument doc;

                try
                {
                    doc = JsonDocument.Parse(cleanJson);
                }
                catch
                {
                    return Ok(new
                    {
                        step = "INVALID_JSON",
                        response = cleanJson
                    });
                }

                var root = doc.RootElement;

                return Ok(new
                {
                    step = "SUCCESS",
                    hasQuestionBank = root.TryGetProperty("question_bank", out _),
                    hasMindMap = root.TryGetProperty("mindmap", out _),
                    sample = cleanJson.Substring(0, Math.Min(300, cleanJson.Length))
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    step = "EXCEPTION",
                    error = ex.Message
                });
            }
        }


        // ===============================
        // 🔥 Get PDF Only
        // ===============================
        [HttpGet("{lectureId}/pdf")]
        [Authorize]
        public async Task<IActionResult> GetPdf(int lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.LectureFiles)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            var pdf = lecture.LectureFiles
                .FirstOrDefault(f => f.FileCategory == "PDF");

            if (pdf == null)
                return Ok(null);

            return Ok(new
            {
                pdf.Id,
                pdf.FileName,
                pdf.FilePath
            });
        }

        // ===============================
        // 🔥 Generate Question Bank From AI (Clean)
        // ===============================
        [HttpPost("generate-questions/{lectureId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GenerateQuestions(int lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.LectureFiles)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found");

            var pdf = lecture.LectureFiles
                .FirstOrDefault(f => f.FileCategory == "PDF");

            if (pdf == null)
                return BadRequest("PDF file not found");

            var fileName = Path.GetFileName(pdf.FilePath);

            var physicalPath = Path.Combine(
                _env.WebRootPath,
                "lectures",
                fileName
            );

            if (!System.IO.File.Exists(physicalPath))
            {
                return Ok(new
                {
                    message = "File not found on server",
                    path = physicalPath,
                    webRoot = _env.WebRootPath,
                    fileFromDb = pdf.FilePath
                });
            }

            // ===============================
            // 🔥 Call AI (clean JSON already)
            // ===============================
            var cleanJson = await _aiService.ProcessPdfRawAsync(physicalPath);

            if (string.IsNullOrEmpty(cleanJson))
                return BadRequest("AI returned empty response");

            JsonDocument doc;

            try
            {
                doc = JsonDocument.Parse(cleanJson);
            }
            catch
            {
                return Ok(new
                {
                    message = "Invalid JSON from AI",
                    rawResponse = cleanJson
                });
            }

            var root = doc.RootElement;

            // ===============================
            // 🔍 VALIDATION
            // ===============================
            if (!root.TryGetProperty("question_bank", out var questionBankElement))
            {
                return Ok(new
                {
                    message = "AI response structure different",
                    rawResponse = cleanJson
                });
            }

            if (!questionBankElement.TryGetProperty("questions", out var questionsElement))
            {
                return Ok(new
                {
                    message = "questions array not found",
                    rawResponse = cleanJson
                });
            }

            var newQuestions = new List<Question>();

            foreach (var q in questionsElement.EnumerateArray())
            {
                if (!q.TryGetProperty("question", out var questionProp))
                    continue;

                var questionText = questionProp.GetString() ?? "";

                var options = new List<QuestionOption>();

                if (q.TryGetProperty("options", out var optionsElement))
                {
                    foreach (var opt in optionsElement.EnumerateArray())
                    {
                        var text = opt.GetProperty("text").GetString() ?? "";
                        var isCorrect = opt.GetProperty("is_correct").GetBoolean();

                        options.Add(new QuestionOption
                        {
                            Text = text,
                            IsCorrect = isCorrect
                        });
                    }
                }

                var type = options.Count == 2 ? "TrueFalse" : "MCQ";

                newQuestions.Add(new Question
                {
                    Text = questionText,
                    Type = type,
                    LectureId = lectureId,
                    QuestionOptions = options
                });
            }

            if (!newQuestions.Any())
                return BadRequest("No questions generated");

            _context.Questions.AddRange(newQuestions);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Questions generated successfully",
                count = newQuestions.Count
            });
        }



        // ===============================
        // 🔥 Get Question Bank (Clean + Production)
        // ===============================
        [HttpGet("{lectureId}/questions")]
        [Authorize]
        public async Task<IActionResult> GetQuestionBank(int lectureId)
        {
            // 🔍 Check lecture exists
            var lectureExists = await _context.Lectures
                .AnyAsync(l => l.Id == lectureId);

            if (!lectureExists)
                return NotFound(new
                {
                    message = "Lecture not found"
                });

            var questions = await _context.Questions
                .Where(q => q.LectureId == lectureId && q.QuizId == null)
                .Include(q => q.QuestionOptions)
                .Select(q => new
                {
                    q.Id,
                    q.Text,
                    q.Type,
                    options = q.QuestionOptions.Select(o => new
                    {
                        o.Id,
                        o.Text
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new
            {
                count = questions.Count,
                data = questions
            });
        }

        // ===============================
        // 🔥 Get MindMap Only
        // ===============================
        [HttpGet("{lectureId}/mindmap")]
        [Authorize]
        public async Task<IActionResult> GetMindMap(int lectureId)
        {
            var lecture = await _context.Lectures
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            if (string.IsNullOrEmpty(lecture.MindMapJson))
                return Ok(null);

            return Ok(JsonSerializer.Deserialize<object>(lecture.MindMapJson));
        }

        // ===============================
        // Get Lecture Video
        // ===============================
        [HttpGet("{lectureId}/video")]
        [Authorize]
        public async Task<IActionResult> GetLectureVideo(int lectureId)
        {
            var lecture = await _context.Lectures
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            if (lecture.VideoStatus == "Processing")
            {
                return Ok(new
                {
                    status = "processing",
                    message = "Video is still being generated"
                });
            }

            if (lecture.VideoStatus == "Failed" || string.IsNullOrEmpty(lecture.VideoUrl))
            {
                return NotFound(new
                {
                    status = "not_found",
                    message = "Video not available"
                });
            }

            return Ok(new
            {
                status = "ready",
                videoUrl = lecture.VideoUrl
            });
        }

        // ===============================
        // Get Lecture Podcast
        // ===============================
        [HttpGet("{lectureId}/audio")]
        [Authorize]
        public async Task<IActionResult> GetLectureAudio(int lectureId)
        {
            var lecture = await _context.Lectures
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            if (lecture.AudioStatus == "Processing")
            {
                return Ok(new
                {
                    status = "processing",
                    message = "Podcast is still being generated"
                });
            }

            if (lecture.AudioStatus == "Failed" || string.IsNullOrEmpty(lecture.AudioUrl))
            {
                return NotFound(new
                {
                    status = "not_found",
                    message = "Podcast not available"
                });
            }

            return Ok(new
            {
                status = "ready",
                audioUrl = lecture.AudioUrl
            });
        }

        // ===============================
        // Delete Lecture
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

            // ===============================
            // Delete Physical Files
            // ===============================
            foreach (var file in lecture.LectureFiles)
            {
                try
                {
                    if (!string.IsNullOrEmpty(file.FilePath) && file.FilePath.StartsWith("/lectures/"))
                    {
                        var fileName = Path.GetFileName(file.FilePath);

                        var physicalPath = Path.Combine(
                            _env.WebRootPath,
                            "lectures",
                            fileName
                        );

                        if (System.IO.File.Exists(physicalPath))
                        {
                            System.IO.File.Delete(physicalPath);
                        }
                    }
                }
                catch
                {
                    // ignore file delete errors
                }
            }

            // ===============================
            // Delete Questions + Options
            // ===============================
            _context.QuestionOptions.RemoveRange(
                lecture.Questions.SelectMany(q => q.QuestionOptions));

            _context.Questions.RemoveRange(lecture.Questions);

            // ===============================
            // Delete Files Records
            // ===============================
            _context.LectureFiles.RemoveRange(lecture.LectureFiles);

            // ===============================
            // Delete Lecture
            // ===============================
            _context.Lectures.Remove(lecture);

            await _context.SaveChangesAsync();

            return Ok("Lecture and all related data deleted successfully.");
        }



        // ===============================
        // 🔥 Mark Video Complete
        // ===============================
        [HttpPost("{lectureId}/progress/video")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkVideoComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.VideoCompleted = true);
        }

        // ===============================
        // 🔥 Mark Audio Complete
        // ===============================
        [HttpPost("{lectureId}/progress/audio")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkAudioComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.AudioCompleted = true);
        }

        // ===============================
        // 🔥 Mark MindMap Complete
        // ===============================
        [HttpPost("{lectureId}/progress/mindmap")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkMindMapComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.MindMapCompleted = true);
        }

        // ===============================
        // 🔥 Mark QuestionBank Complete
        // ===============================
        [HttpPost("{lectureId}/progress/questionbank")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkQuestionBankComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.QuestionBankCompleted = true);
        }

        // ===============================
        // 🔥 Get Progress
        // ===============================
        [HttpGet("{lectureId}/progress")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetProgress(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var progress = await _context.StudentLectureProgresses
                .FirstOrDefaultAsync(p =>
                    p.StudentId == studentId &&
                    p.LectureId == lectureId);

            if (progress == null)
                return Ok(new { percentage = 0 });

            int completed = 0;
            if (progress.VideoCompleted) completed++;
            if (progress.AudioCompleted) completed++;
            if (progress.MindMapCompleted) completed++;
            if (progress.QuestionBankCompleted) completed++;

            int percentage = (completed * 100) / 4;

            progress.IsCompleted = percentage == 100;
            progress.LastAccessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                progress.VideoCompleted,
                progress.AudioCompleted,
                progress.MindMapCompleted,
                progress.QuestionBankCompleted,
                progress.IsCompleted,
                percentage
            });
        }

        // ======================================
        // Get Lectures By Subject
        // ======================================
        [HttpGet("by-subject/{subjectId}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetLecturesBySubject(int subjectId)
        {
            var doctorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (doctorId == null)
                return Unauthorized();

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(s => s.Id == subjectId && s.DoctorId == doctorId);

            if (subject == null)
                return Forbid("You don't own this subject.");

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
                subjectId,
                lectureCount = lectures.Count,
                lectures
            });
        }

        // ======================================
        // Student Dashboard (Lectures + Progress)
        // ======================================
        [HttpGet("student-dashboard/{subjectId}")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetStudentDashboard(int subjectId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (studentId == null)
                return Unauthorized();

            var lectures = await _context.Lectures
                .Where(l => l.SubjectId == subjectId)
                .OrderBy(l => l.Order)
                .ToListAsync();

            var result = new List<object>();

            int completedLectures = 0;

            foreach (var lecture in lectures)
            {
                var progress = await _context.StudentLectureProgresses
                    .FirstOrDefaultAsync(p =>
                        p.StudentId == studentId &&
                        p.LectureId == lecture.Id);

                bool video = progress?.VideoCompleted ?? false;
                bool audio = progress?.AudioCompleted ?? false;
                bool mindmap = progress?.MindMapCompleted ?? false;
                bool questionBank = progress?.QuestionBankCompleted ?? false;

                bool isCompleted = video && mindmap && questionBank;

                if (isCompleted)
                    completedLectures++;

                string status = "NotStarted";

                if (progress != null)
                    status = isCompleted ? "Completed" : "InProgress";

                result.Add(new
                {
                    lectureId = lecture.Id,
                    lectureTitle = lecture.Title,
                    lectureOrder = lecture.Order,
                    status,
                    progress = new
                    {
                        videoCompleted = video,
                        audioCompleted = audio,
                        mindMapCompleted = mindmap,
                        questionBankCompleted = questionBank
                    }
                });
            }

            return Ok(new
            {
                subjectId,
                totalLectures = lectures.Count,
                completedLectures,
                lectures = result
            });
        }

        // ======================================
        // Continue Studying (Last Subject)
        // ======================================
        [HttpGet("continue")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> ContinueSubject()
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (studentId == null)
                return Unauthorized();

            var lastProgress = await _context.StudentLectureProgresses
                .Include(p => p.Lecture)
                .ThenInclude(l => l.Subject)
                .Where(p =>
                    p.StudentId == studentId &&
                    (
                        p.VideoCompleted ||
                        p.AudioCompleted ||
                        p.MindMapCompleted ||
                        p.QuestionBankCompleted
                    ))
                .OrderByDescending(p => p.LastAccessedAt)
                .Select(p => new
                {
                    subjectId = p.Lecture.Subject.Id,
                    subjectName = p.Lecture.Subject.Name
                })
                .FirstOrDefaultAsync();

            if (lastProgress == null)
                return Ok(null);

            return Ok(lastProgress);
        }

        // ===============================
        // 🔥 Private Helper
        // ===============================
        private async Task<IActionResult> UpdateProgress(
            int lectureId,
            Action<StudentLectureProgress> updateAction)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var progress = await _context.StudentLectureProgresses
                .FirstOrDefaultAsync(p =>
                    p.StudentId == studentId &&
                    p.LectureId == lectureId);

            if (progress == null)
            {
                progress = new StudentLectureProgress
                {
                    StudentId = studentId,
                    LectureId = lectureId
                };

                _context.StudentLectureProgresses.Add(progress);
            }

            updateAction(progress);
            progress.LastAccessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Progress updated successfully." });
        }
    }
}