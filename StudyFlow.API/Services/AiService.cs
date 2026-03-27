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
            _httpClient.Timeout = TimeSpan.FromMinutes(10);
        }

        // ==========================================
        // 🔥 Generate Educational Package (RAW CLEAN JSON)
        // ==========================================
        public async Task<string?> ProcessPdfRawAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath);

                using var form = new MultipartFormDataContent();

                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType =
                    MediaTypeHeaderValue.Parse("application/pdf");

                // 🔥 FIX: send file only (no text)
                form.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync(
                    "/api/v1/text/generate-educational-package",
                    form
                );

                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content))
                    return null;

                // 🔥 FIX: clean string JSON
                var cleanJson = content.Trim();

                if (cleanJson.StartsWith("\""))
                {
                    cleanJson = JsonSerializer.Deserialize<string>(cleanJson);
                }

                return cleanJson;
            }
            catch
            {
                return null;
            }
        }

        // ==========================================
        // 🎥 Generate Video
        // ==========================================
        public async Task<string?> GenerateVideoAsync(string filePath)
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
                    "/api/v1/media/video/generate",
                    form
                );

                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return null;

                var cleanJson = content.Trim();

                if (cleanJson.StartsWith("\""))
                {
                    cleanJson = JsonSerializer.Deserialize<string>(cleanJson);
                }

                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("final_video_url", out var videoUrl))
                {
                    return videoUrl.GetString();
                }

                return null;
            }
            catch
            {
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

                form.Add(fileContent, "file", Path.GetFileName(filePath));

                var response = await _httpClient.PostAsync(
                    "/api/v1/media/podcast/generate",
                    form
                );

                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return null;

                var cleanJson = content.Trim();

                if (cleanJson.StartsWith("\""))
                {
                    cleanJson = JsonSerializer.Deserialize<string>(cleanJson);
                }

                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

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

        // ==========================================
        // 🔥 Parse Questions DTO (Clean)
        // ==========================================
        public async Task<AiImportDto?> ProcessPdfAsync(string filePath)
        {
            var cleanJson = await ProcessPdfRawAsync(filePath);

            if (string.IsNullOrEmpty(cleanJson))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty("question_bank", out var questionBank))
                    return null;

                if (!questionBank.TryGetProperty("questions", out var questions))
                    return null;

                var result = new AiImportDto
                {
                    Mcq = new List<McqDto>(),
                    TrueFalse = new List<TrueFalseDto>()
                };

                foreach (var q in questions.EnumerateArray())
                {
                    var options = q.GetProperty("options").EnumerateArray().ToList();

                    if (options.Count == 2)
                    {
                        var answer = options.First(o => o.GetProperty("is_correct").GetBoolean())
                            .GetProperty("text").GetString() == "True";

                        result.TrueFalse.Add(new TrueFalseDto
                        {
                            Question = q.GetProperty("question").GetString()!,
                            Answer = answer
                        });
                    }
                    else
                    {
                        var mcq = new McqDto
                        {
                            Question = q.GetProperty("question").GetString()!,
                            Options = new Dictionary<string, string>()
                        };

                        char optionKey = 'A';

                        foreach (var opt in options)
                        {
                            mcq.Options.Add(optionKey.ToString(), opt.GetProperty("text").GetString()!);

                            if (opt.GetProperty("is_correct").GetBoolean())
                                mcq.Answer = optionKey.ToString();

                            optionKey++;
                        }

                        result.Mcq.Add(mcq);
                    }
                }

                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}