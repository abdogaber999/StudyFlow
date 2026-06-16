using Microsoft.EntityFrameworkCore;
using StudyFlow.Domain.Entities;
using StudyFlow.Infrastructure.DbContexts;

namespace StudyFlow.API.Services
{
    public class LecturePipelineService
    {
        private readonly StudyFlowDbContext _context;
        private readonly AiService _aiService;
        private readonly NotificationService _notificationService;
        private readonly IWebHostEnvironment _env;

        public LecturePipelineService(
            StudyFlowDbContext context,
            AiService aiService,
            NotificationService notificationService,
            IWebHostEnvironment env)
        {
            _context = context;
            _aiService = aiService;
            _notificationService = notificationService;
            _env = env;
        }

        public async Task StartPipeline(int lectureId)
        {
            var lecture = await _context.Lectures
                .Include(l => l.LectureFiles)
                .FirstOrDefaultAsync(l => l.Id == lectureId);

            if (lecture == null)
                return;

            var pdf = lecture.LectureFiles
                .FirstOrDefault(f => f.FileCategory == "PDF");

            if (pdf == null)
                return;

            var physicalPath = Path.Combine(
                _env.WebRootPath,
                "lectures",
                Path.GetFileName(pdf.FilePath)
            );

            if (!File.Exists(physicalPath))
                return;

            // 🔥 Track failed steps for retry
            var failedSteps = new List<string>();

            // ===============================
            // 🔥 1. QUESTIONS
            // ===============================
            if (!await RunQuestions(lecture, physicalPath, lectureId))
                failedSteps.Add("Questions");

            // ===============================
            // 🔥 2. MINDMAP
            // ===============================
            if (!await RunMindMap(lecture, physicalPath, lectureId))
                failedSteps.Add("MindMap");

            // ===============================
            // 🔥 3. AUDIO
            // ===============================
            if (!await RunAudio(lecture, physicalPath, lectureId))
                failedSteps.Add("Audio");

            // ===============================
            // 🔥 4. VIDEO
            // ===============================
            if (!await RunVideo(lecture, physicalPath, lectureId))
                failedSteps.Add("Video");

            // ===============================
            // 🔄 RETRY FAILED STEPS (مرة واحدة كمان)
            // ===============================
            if (failedSteps.Any())
            {
                Console.WriteLine($"🔄 RETRYING {failedSteps.Count} FAILED STEPS: {string.Join(", ", failedSteps)}");

                foreach (var step in failedSteps)
                {
                    switch (step)
                    {
                        case "Questions":
                            await RunQuestions(lecture, physicalPath, lectureId);
                            break;
                        case "MindMap":
                            await RunMindMap(lecture, physicalPath, lectureId);
                            break;
                        case "Audio":
                            await RunAudio(lecture, physicalPath, lectureId);
                            break;
                        case "Video":
                            await RunVideo(lecture, physicalPath, lectureId);
                            break;
                    }
                }

                Console.WriteLine("🔄 RETRY ROUND FINISHED");
            }
        }

        // ===============================
        // 🔥 Questions Step
        // ===============================
        private async Task<bool> RunQuestions(Lecture lecture, string physicalPath, int lectureId)
        {
            try
            {
                var aiResult = await _aiService.ProcessPdfAsync(physicalPath);

                if (aiResult != null)
                {
                    var questions = new List<Question>();

                    foreach (var mcq in aiResult.Mcq)
                    {
                        var options = mcq.Options.Select(opt => new QuestionOption
                        {
                            Text = opt.Value,
                            IsCorrect = opt.Key == mcq.Answer
                        }).ToList();

                        questions.Add(new Question
                        {
                            Text = mcq.Question,
                            Type = "MCQ",
                            LectureId = lectureId,
                            QuestionOptions = options
                        });
                    }

                    foreach (var tf in aiResult.TrueFalse)
                    {
                        questions.Add(new Question
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

                    _context.Questions.AddRange(questions);
                    await _context.SaveChangesAsync();

                    await _notificationService.SendToLectureUsers(lectureId, "Questions");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ QUESTIONS ERROR: " + ex.Message);
                return false;
            }
        }

        // ===============================
        // 🔥 MindMap Step
        // ===============================
        private async Task<bool> RunMindMap(Lecture lecture, string physicalPath, int lectureId)
        {
            try
            {
                lecture.MindMapStatus = "Processing";
                await _context.SaveChangesAsync();

                var url = await _aiService.GenerateMindMapAsync(physicalPath);

                if (!string.IsNullOrEmpty(url))
                {
                    lecture.MindMapUrl = url;
                    lecture.MindMapStatus = "Ready";

                    await _context.SaveChangesAsync();

                    await _notificationService.SendToLectureUsers(lectureId, "MindMap");
                }

                return true;
            }
            catch (Exception ex)
            {
                lecture.MindMapStatus = "Failed";
                lecture.MindMapError = ex.Message;
                await _context.SaveChangesAsync();
                Console.WriteLine("❌ MINDMAP ERROR: " + ex.Message);
                return false;
            }
        }

        // ===============================
        // 🔥 Audio Step
        // ===============================
        private async Task<bool> RunAudio(Lecture lecture, string physicalPath, int lectureId)
        {
            try
            {
                lecture.AudioStatus = "Processing";
                await _context.SaveChangesAsync();

                var audioUrl = await _aiService.GeneratePodcastAsync(physicalPath);

                if (!string.IsNullOrEmpty(audioUrl))
                {
                    lecture.AudioUrl = audioUrl;
                    lecture.AudioStatus = "Ready";

                    await _context.SaveChangesAsync();

                    await _notificationService.SendToLectureUsers(lectureId, "Audio");
                }

                return true;
            }
            catch (Exception ex)
            {
                lecture.AudioStatus = "Failed";
                lecture.AudioError = ex.Message;
                await _context.SaveChangesAsync();
                Console.WriteLine("❌ AUDIO ERROR: " + ex.Message);
                return false;
            }
        }

        // ===============================
        // 🔥 Video Step
        // ===============================
        private async Task<bool> RunVideo(Lecture lecture, string physicalPath, int lectureId)
        {
            try
            {
                lecture.VideoStatus = "Processing";
                await _context.SaveChangesAsync();

                var videoUrl = await _aiService.GenerateVideoAsync(physicalPath);

                if (!string.IsNullOrEmpty(videoUrl))
                {
                    lecture.VideoUrl = videoUrl;
                    lecture.VideoStatus = "Ready";

                    await _context.SaveChangesAsync();

                    await _notificationService.SendToLectureUsers(lectureId, "Video");
                }

                return true;
            }
            catch (Exception ex)
            {
                lecture.VideoStatus = "Failed";
                lecture.VideoError = ex.Message;
                await _context.SaveChangesAsync();
                Console.WriteLine("❌ VIDEO ERROR: " + ex.Message);
                return false;
            }
        }
    }
}