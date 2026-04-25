using System.Net.Http.Headers;
using System.Text.Json;
using StudyFlow.API.DTOs;

namespace StudyFlow.API.Services
{
    public class AiService
    {
        private readonly HttpClient _httpClient;

        public AiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromMinutes(15);
        }

        // ==========================================
        // 🔥 Generate Questions (NEW ENDPOINT)
        // ==========================================
        public async Task<string?> GenerateQuestionsRawAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return "FILE_NOT_FOUND";

            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath);

                using var form = new MultipartFormDataContent();

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType =
                    MediaTypeHeaderValue.Parse("application/pdf");

                form.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync(
                    "/api/v1/text/generate-question-bank",
                    form
                );

                var content = await response.Content.ReadAsStringAsync();

                // 🔥 التعديل هنا
                if (!response.IsSuccessStatusCode)
                    return $"AI_ERROR: {response.StatusCode} - {content}";

                if (string.IsNullOrWhiteSpace(content))
                    return "AI_EMPTY_RESPONSE";

                var cleanJson = content.Trim();

                if (cleanJson.StartsWith("\""))
                {
                    cleanJson = JsonSerializer.Deserialize<string>(cleanJson);
                }

                return cleanJson;
            }
            catch (Exception ex)
            {
                return $"EXCEPTION: {ex.Message}";
            }
        }

        // ==========================================
        // 🔥 Parse Questions DTO (UPDATED)
        // ==========================================
        public async Task<AiImportDto?> ProcessPdfAsync(string filePath)
        {
            var cleanJson = await GenerateQuestionsRawAsync(filePath);

            // 🔥 التعديل هنا
            if (string.IsNullOrEmpty(cleanJson) || cleanJson.StartsWith("AI_ERROR") || cleanJson.StartsWith("EXCEPTION"))
                throw new Exception(cleanJson);

            try
            {
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("questions", out var questions))
                    return null;

                var result = new AiImportDto
                {
                    Mcq = new List<McqDto>(),
                    TrueFalse = new List<TrueFalseDto>()
                };

                foreach (var q in questions.EnumerateArray())
                {
                    var options = q.GetProperty("options").EnumerateArray().ToList();

                    if (options.Count == 2) // TF
                    {
                        var correctOption = options.First(o => o.GetProperty("isCorrect").GetBoolean());

                        var answer = correctOption.GetProperty("text").GetString() == "True"
                                     || correctOption.GetProperty("text").GetString() == "صح";

                        result.TrueFalse.Add(new TrueFalseDto
                        {
                            Question = q.GetProperty("text").GetString()!,
                            Answer = answer
                        });
                    }
                    else // MCQ
                    {
                        var mcq = new McqDto
                        {
                            Question = q.GetProperty("text").GetString()!,
                            Options = new Dictionary<string, string>()
                        };

                        char optionKey = 'A';

                        foreach (var opt in options)
                        {
                            mcq.Options.Add(optionKey.ToString(), opt.GetProperty("text").GetString()!);

                            if (opt.GetProperty("isCorrect").GetBoolean())
                                mcq.Answer = optionKey.ToString();

                            optionKey++;
                        }

                        result.Mcq.Add(mcq);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception($"PARSE_ERROR: {ex.Message} | RAW: {cleanJson}");
            }
        }

        // ==========================================
        // 🧠 Generate MindMap (NEW)
        // ==========================================
        public async Task<string?> GenerateMindMapAsync(string filePath)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath);

                using var form = new MultipartFormDataContent();

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue("application/pdf");

                form.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync(
                    "/api/v1/text/generate-mindmap",
                    form
                );

                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(content))
                    return null;

                var cleanJson = content.Trim();

                if (cleanJson.StartsWith("\""))
                {
                    cleanJson = JsonSerializer.Deserialize<string>(cleanJson);
                }

                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("mindmap_image_url", out var url))
                {
                    return url.GetString();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // ==========================================
        // 🎥 Generate Video (SYNC DIRECT)
        // ==========================================
        public async Task<string?> GenerateVideoAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine("VIDEO ERROR: File not found -> " + filePath);
                    return null;
                }

                var bytes = await File.ReadAllBytesAsync(filePath);

                using var form = new MultipartFormDataContent();

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue("application/pdf");

                // 🔥 نفس الأوديو بالظبط
                form.Add(fileContent, "files", Path.GetFileName(filePath));

                Console.WriteLine("VIDEO REQUEST STARTED");

                var response = await _httpClient.PostAsync(
                    "/api/v1/media/video/generate",
                    form
                );

                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine("VIDEO RESPONSE STATUS: " + response.StatusCode);
                Console.WriteLine("VIDEO RESPONSE BODY: " + content);

                if (!response.IsSuccessStatusCode)
                    return null;

                if (string.IsNullOrWhiteSpace(content))
                    return null;

                var cleanJson = content.Trim();

                // 🔥 لو رجع string مباشر
                if (cleanJson.StartsWith("\""))
                {
                    return JsonSerializer.Deserialize<string>(cleanJson);
                }

                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                // 🔥 الشكل المتوقع
                if (root.TryGetProperty("final_video_url", out var videoUrl))
                {
                    return videoUrl.GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("VIDEO EXCEPTION: " + ex.Message);
                return null;
            }
        }

        // ==========================================
        // 🎧 Generate Podcast
        // ==========================================
        public async Task<string?> GeneratePodcastAsync(string filePath)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath);

                using var form = new MultipartFormDataContent();

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType =
                    new MediaTypeHeaderValue("application/pdf");

                form.Add(fileContent, "files", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync(
                    "/api/v1/media/podcast/generate",
                    form
                );

                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return null;

                var cleanJson = content.Trim();

                // 🔥 لو رجع string مباشر
                if (cleanJson.StartsWith("\""))
                {
                    cleanJson = JsonSerializer.Deserialize<string>(cleanJson);
                    return cleanJson; // ✅ رجعه مباشرة
                }

                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                // 🔥 الشكل الجديد من AI
                if (root.TryGetProperty("final_audio_url", out var audioUrl))
                {
                    return audioUrl.GetString();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}