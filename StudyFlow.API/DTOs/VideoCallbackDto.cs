namespace StudyFlow.API.DTOs
{
    using System.Text.Json.Serialization;

    public class VideoCallbackDto
    {
        public int LectureId { get; set; }

        [JsonPropertyName("final_video_url")]
        public string FinalVideoUrl { get; set; }
    }
}
