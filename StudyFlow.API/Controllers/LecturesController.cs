using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using StudyFlow.API.DTOs;
using StudyFlow.API.Services;
using StudyFlow.Domain.Entities;
using StudyFlow.Infrastructure.DbContexts;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LecturesController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;
        private readonly AiService _aiService;
        private readonly IWebHostEnvironment _env;
        private readonly NotificationService _notificationService;
        private readonly LecturePipelineService _PipelineService;
        private readonly IServiceScopeFactory _scopeFactory;

        public LecturesController(
    StudyFlowDbContext context,
    AiService aiService,
    IWebHostEnvironment env,
    NotificationService notificationService,
    LecturePipelineService PipelineService,
    IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _aiService = aiService;
            _env = env;
            _notificationService = notificationService;
            _PipelineService = PipelineService;
            _scopeFactory = scopeFactory;
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

            // 🔥 شغل الـ pipeline في الخلفية (FIXED DI SCOPE)
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var pipeline = scope.ServiceProvider.GetRequiredService<LecturePipelineService>();

                    Console.WriteLine("🔥 PIPELINE STARTED");

                    await pipeline.StartPipeline(lectureId);

                    Console.WriteLine("✅ PIPELINE FINISHED");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("❌ PIPELINE ERROR: " + ex.Message);
                    Console.WriteLine("❌ PIPELINE ERROR for Lecture " + lectureId + ": " + ex.Message);
                    Console.WriteLine("❌ STACK TRACE: " + ex.StackTrace);
                    if (ex.InnerException != null)
                    Console.WriteLine("❌ INNER: " + ex.InnerException.Message);
                }
            });

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
                // 🔥 Ping AI Server (UPDATED)
                // =====================================
                try
                {
                    using var pingClient = new HttpClient();
                    pingClient.Timeout = TimeSpan.FromSeconds(10);

                    var ping = await pingClient.GetAsync("http://187.124.217.251/");

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
                // 🔥 Direct AI Call (UPDATED)
                // =====================================
                var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);

                using var form = new MultipartFormDataContent();

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

                form.Add(fileContent, "file", Path.GetFileName(physicalPath));

                using var aiClient = new HttpClient();
                aiClient.Timeout = TimeSpan.FromMinutes(5);
                aiClient.BaseAddress = new Uri("http://187.124.217.251/");

                var response = await aiClient.PostAsync(
                    "api/v1/text/generate-question-bank",
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
                // 🔥 Call AI via Service (NO CHANGE)
                // =====================================
                var aiResult = await _aiService.ProcessPdfAsync(physicalPath);

                if (aiResult == null)
                {
                    return Ok(new
                    {
                        step = "AI_FAILED"
                    });
                }

                return Ok(new
                {
                    step = "SUCCESS",
                    mcqCount = aiResult.Mcq.Count,
                    tfCount = aiResult.TrueFalse.Count
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
        // 🔥 Test Timeout
        // ===============================
        [HttpGet("test-timeout")]
        [AllowAnonymous]
        public async Task<IActionResult> TestTimeout()
        {
            Console.WriteLine("TEST TIMEOUT STARTED");

            await Task.Delay(TimeSpan.FromMinutes(4)); // غيرها زي ما تحب

            Console.WriteLine("TEST TIMEOUT FINISHED");

            return Ok(new
            {
                message = "Request completed after delay"
            });
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

            // 🔥 بنرجع Full URL عشان الفلاتر يقدر يفتحه مباشرة
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            return Ok(new
            {
                pdf.Id,
                pdf.FileName,
                pdf.FilePath,
                fullUrl = $"{baseUrl}{Uri.EscapeUriString(pdf.FilePath)}"
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
                    path = physicalPath
                });
            }

            // ===============================
            // 🔥 Call AI (NEW CLEAN WAY)
            // ===============================
            var aiResult = await _aiService.ProcessPdfAsync(physicalPath);

            if (aiResult == null)
                return BadRequest("AI failed");

            var newQuestions = new List<Question>();

            // ===============================
            // 🔥 MCQ
            // ===============================
            foreach (var mcq in aiResult.Mcq)
            {
                var options = mcq.Options.Select(opt => new QuestionOption
                {
                    Text = opt.Value,
                    IsCorrect = opt.Key == mcq.Answer
                }).ToList();

                newQuestions.Add(new Question
                {
                    Text = mcq.Question,
                    Type = "MCQ",
                    LectureId = lectureId,
                    QuestionOptions = options
                });
            }

            // ===============================
            // 🔥 True / False
            // ===============================
            foreach (var tf in aiResult.TrueFalse)
            {
                newQuestions.Add(new Question
                {
                    Text = tf.Question,
                    Type = "TrueFalse",
                    LectureId = lectureId,
                    QuestionOptions = new List<QuestionOption>
    {
        new QuestionOption { Text = "True", IsCorrect = tf.Answer },
        new QuestionOption { Text = "False", IsCorrect = !tf.Answer }
    }
                });
            }

            if (!newQuestions.Any())
                return BadRequest("No questions generated");

            _context.Questions.AddRange(newQuestions);
            await _context.SaveChangesAsync();

            // 🔔 Notification
            await _notificationService.SendToLectureUsers(lectureId, "Questions");

            return Ok(new
            {
                message = "Questions generated successfully",
                count = newQuestions.Count
            });
        }



        // ===============================
        // 🔥 Get Question Bank (Random Limited)
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

            // 🔥 شيلنا شرط QuizId عشان يرجع كل الأسئلة حتى اللي اتكررت
            var allQuestions = await _context.Questions
                .Where(q => q.LectureId == lectureId)
                .Include(q => q.QuestionOptions)
                .ToListAsync();

            // ✅ لو 50 أو أقل → رجّع كله
            if (allQuestions.Count <= 50)
            {
                var result = allQuestions.Select(q => new
                {
                    q.Id,
                    q.Text,
                    q.Type,
                    options = q.QuestionOptions.Select(o => new
                    {
                        o.Id,
                        o.Text
                    }).ToList()
                });

                return Ok(new
                {
                    count = result.Count(),
                    data = result
                });
            }

            // 🔥 لو أكتر من 50 → Random Selection

            var mcq = allQuestions
                .Where(q => q.Type == "MCQ")
                .OrderBy(x => Guid.NewGuid())
                .Take(30);

            var tf = allQuestions
                .Where(q => q.Type == "TrueFalse")
                .OrderBy(x => Guid.NewGuid())
                .Take(20);

            var selected = mcq.Concat(tf)
                .OrderBy(x => Guid.NewGuid()) // shuffle النهائي
                .ToList();

            var data = selected.Select(q => new
            {
                q.Id,
                q.Text,
                q.Type,
                options = q.QuestionOptions.Select(o => new
                {
                    o.Id,
                    o.Text
                }).ToList()
            });

            return Ok(new
            {
                count = data.Count(),
                data = data
            });
        }

        // ===============================
        // 🔥 Generate MindMap From AI (SYNC)
        // ===============================
        [HttpPost("generate-mindmap/{lectureId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GenerateMindMap(int lectureId)
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
                return BadRequest("File not found");

            // 🔥 set processing
            lecture.MindMapStatus = "Processing";
            lecture.MindMapError = null;
            lecture.MindMapUrl = null;

            await _context.SaveChangesAsync();

            try
            {
                var mindMapUrl = await _aiService.GenerateMindMapAsync(physicalPath);

                if (string.IsNullOrEmpty(mindMapUrl))
                {
                    lecture.MindMapStatus = "Failed";
                    await _context.SaveChangesAsync();

                    return BadRequest(new
                    {
                        status = "failed",
                        message = "AI failed to generate mindmap"
                    });
                }

                // ✅ success
                lecture.MindMapUrl = mindMapUrl;
                lecture.MindMapStatus = "Ready";

                await _context.SaveChangesAsync();

                // 🔔 Notification
                await _notificationService.SendToLectureUsers(lectureId, "MindMap");

                return Ok(new
                {
                    status = "ready",
                    mindMapUrl = mindMapUrl
                });
            }
            catch (Exception ex)
            {
                lecture.MindMapStatus = "Failed";
                lecture.MindMapError = ex.Message;

                await _context.SaveChangesAsync();

                return BadRequest(new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }


        // ===============================
        // Get MindMap (Production Clean)
        // ===============================
        [HttpGet("{lectureId}/mindmap")]
        [Authorize]
        public async Task<IActionResult> GetMindMap(int lectureId)
        {
            var lecture = await _context.Lectures
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return NotFound("Lecture not found.");

            if (lecture.MindMapStatus == "Processing")
            {
                return Ok(new
                {
                    status = "processing",
                    message = "MindMap is still being generated"
                });
            }

            if (lecture.MindMapStatus == "Failed")
            {
                return Ok(new
                {
                    status = "failed",
                    message = "MindMap generation failed",
                    error = lecture.MindMapError
                });
            }

            if (lecture.MindMapStatus == "Ready" && !string.IsNullOrEmpty(lecture.MindMapUrl))
            {
                return Ok(new
                {
                    status = "ready",
                    mindMapUrl = lecture.MindMapUrl
                });
            }

            return Ok(new
            {
                status = "not_started",
                message = "MindMap generation not started yet"
            });
        }

        // ===============================
        // 🔥 Generate Video (SYNC DIRECT)
        // ===============================
        [HttpPost("generate-video/{lectureId}")]
        [Authorize]
        public async Task<IActionResult> GenerateVideo(int lectureId)
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
                return BadRequest("File not found");

            // 🔥 set processing
            lecture.VideoStatus = "Processing";
            lecture.VideoError = null;
            lecture.VideoUrl = null;

            await _context.SaveChangesAsync();

            try
            {
                
                var videoUrl = await _aiService.GenerateVideoAsync(physicalPath);

                if (string.IsNullOrEmpty(videoUrl))
                {
                    lecture.VideoStatus = "Failed";
                    await _context.SaveChangesAsync();

                    return BadRequest(new
                    {
                        status = "failed",
                        message = "AI failed to generate video"
                    });
                }

                // ✅ success
                lecture.VideoUrl = videoUrl;
                lecture.VideoStatus = "Ready";

                await _context.SaveChangesAsync();

                // 🔔 Notification
                await _notificationService.SendToLectureUsers(lectureId, "Video");

                return Ok(new
                {
                    status = "ready",
                    videoUrl = videoUrl
                });
            }
            catch (Exception ex)
            {
                lecture.VideoStatus = "Failed";
                await _context.SaveChangesAsync();

                return BadRequest(new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }

        //// ===============================
        //// 🔥 Video Callback (Webhook)
        //// ===============================
        //[HttpPost("video-callback")]
        //[AllowAnonymous]
        //public async Task<IActionResult> VideoCallback([FromBody] VideoCallbackDto dto)
        //{
        //    if (dto == null || dto.LectureId == 0 || string.IsNullOrEmpty(dto.FinalVideoUrl))
        //    {
        //        return BadRequest("Invalid data");
        //    }

        //    var lecture = await _context.Lectures.FindAsync(dto.LectureId);

        //    if (lecture == null)
        //        return NotFound("Lecture not found");

        //    // 🔥 نحفظ الفيديو
        //    lecture.VideoUrl = dto.FinalVideoUrl;
        //    lecture.VideoStatus = "Ready";
        //    lecture.VideoError = null;

        //    await _context.SaveChangesAsync();

        //    Console.WriteLine($"VIDEO CALLBACK RECEIVED: Lecture {dto.LectureId}");

        //    return Ok(new
        //    {
        //        message = "Video saved successfully"
        //    });
        //}


        // ===============================
        // Get Lecture Video (Production Clean)
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

            if (lecture.VideoStatus == "Failed")
            {
                return Ok(new
                {
                    status = "failed",
                    message = "Video generation failed",
                    error = lecture.VideoError
                });
            }

            if (lecture.VideoStatus == "Ready" && !string.IsNullOrEmpty(lecture.VideoUrl))
            {
                return Ok(new
                {
                    status = "ready",
                    videoUrl = lecture.VideoUrl
                });
            }

            return Ok(new
            {
                status = "not_started",
                message = "Video generation not started yet"
            });
        }

        // ===============================
        // 🔥 Generate Podcast From AI (SYNC DIRECT)
        // ===============================
        [HttpPost("generate-audio/{lectureId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GenerateAudio(int lectureId)
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
                return BadRequest("File not found");

            // 🔥 set processing
            lecture.AudioStatus = "Processing";
            await _context.SaveChangesAsync();

            try
            {
                // 🔥 call AI مباشرة (هيستنى)
                var audioUrl = await _aiService.GeneratePodcastAsync(physicalPath);

                if (string.IsNullOrEmpty(audioUrl))
                {
                    lecture.AudioStatus = "Failed";
                    await _context.SaveChangesAsync();

                    return BadRequest(new
                    {
                        status = "failed",
                        message = "AI failed to generate audio"
                    });
                }

                // ✅ success
                lecture.AudioUrl = audioUrl;
                lecture.AudioStatus = "Ready";

                await _context.SaveChangesAsync();

                // 🔔 Notification
                await _notificationService.SendToLectureUsers(lectureId, "Audio");

                return Ok(new
                {
                    status = "ready",
                    audioUrl = audioUrl
                });
            }
            catch (Exception ex)
            {
                lecture.AudioStatus = "Failed";
                await _context.SaveChangesAsync();

                return BadRequest(new
                {
                    status = "error",
                    message = ex.Message
                });
            }
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
            // Delete Notifications
            // ===============================
            var notifications = await _context.Notifications
                .Where(n => n.LectureId == lectureId)
                .ToListAsync();

            _context.Notifications.RemoveRange(notifications);

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
        // 🔥 Toggle PDF Complete
        // ===============================
        [HttpPost("{lectureId}/progress/pdf")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkPdfComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.PdfCompleted = !p.PdfCompleted);
        }


        // ===============================
        // 🔥 Toggle Video Complete
        // ===============================
        [HttpPost("{lectureId}/progress/video")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkVideoComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.VideoCompleted = !p.VideoCompleted);
        }

        // ===============================
        // 🔥 Toggle Audio Complete
        // ===============================
        [HttpPost("{lectureId}/progress/audio")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkAudioComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.AudioCompleted = !p.AudioCompleted);
        }

        // ===============================
        // 🔥 Toggle MindMap Complete
        // ===============================
        [HttpPost("{lectureId}/progress/mindmap")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkMindMapComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.MindMapCompleted = !p.MindMapCompleted);
        }

        // ===============================
        // 🔥 Toggle QuestionBank Complete
        // ===============================
        [HttpPost("{lectureId}/progress/questionbank")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MarkQuestionBankComplete(int lectureId)
        {
            return await UpdateProgress(lectureId, p => p.QuestionBankCompleted = !p.QuestionBankCompleted);
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
            if (progress.PdfCompleted) completed++; 

            int percentage = (completed * 100) / 5; 

            progress.IsCompleted = percentage == 100;
            progress.LastAccessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                progress.PdfCompleted,
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

                bool pdf = progress?.PdfCompleted ?? false;
                bool video = progress?.VideoCompleted ?? false;
                bool audio = progress?.AudioCompleted ?? false;
                bool mindmap = progress?.MindMapCompleted ?? false;
                bool questionBank = progress?.QuestionBankCompleted ?? false;

                bool isCompleted = pdf && video && mindmap && questionBank;

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
                        pdfCompleted = pdf,
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