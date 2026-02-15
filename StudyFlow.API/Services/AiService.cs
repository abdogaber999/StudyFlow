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
        }

        // ======================================================
        // Return FULL RAW JSON (Questions + MindMap)
        // ======================================================
        public async Task<string?> ProcessPdfRawAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            using var form = new MultipartFormDataContent();

            await using var fileStream = File.OpenRead(filePath);
            using var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/pdf");

            form.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync("api/v1/process", form);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync();
        }

        // ======================================================
        // Return ONLY Exam Part (Question Bank)
        // ======================================================
        public async Task<AiImportDto?> ProcessPdfAsync(string filePath)
        {
            var rawJson = await ProcessPdfRawAsync(filePath);

            if (string.IsNullOrEmpty(rawJson))
                return null;

            var result = JsonSerializer.Deserialize<AiApiResponseDto>(
                rawJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            return result?.Data?.Exam;
        }
    }
}