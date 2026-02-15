using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlow.Domain.Entities;
using StudyFlow.Infrastructure.DbContexts;
using System.Security.Claims;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Student")]
    public class LectureNotesController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;

        public LectureNotesController(StudyFlowDbContext context)
        {
            _context = context;
        }

        // ============================================
        // DTO For Adding Note
        // ============================================
        public class AddLectureNoteDto
        {
            public int LectureId { get; set; }
            public string Content { get; set; } = string.Empty;
            public string ResourceType { get; set; } = "General";
            public int? TimeStampSeconds { get; set; }
        }

        // ============================================
        // Add Note
        // ============================================
        [HttpPost]
        public async Task<IActionResult> AddNote([FromBody] AddLectureNoteDto model)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var lectureExists = await _context.Lectures
                .AnyAsync(l => l.Id == model.LectureId);

            if (!lectureExists)
                return NotFound("Lecture not found.");

            var note = new LectureNote
            {
                StudentId = studentId,
                LectureId = model.LectureId,
                Content = model.Content,
                ResourceType = model.ResourceType,
                TimeStampSeconds = model.TimeStampSeconds
            };

            _context.LectureNotes.Add(note);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Note added successfully",
                noteId = note.Id
            });
        }

        // ============================================
        // Get Notes For Lecture
        // ============================================
        [HttpGet("{lectureId}")]
        public async Task<IActionResult> GetNotes(int lectureId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var notes = await _context.LectureNotes
                .Where(n => n.StudentId == studentId && n.LectureId == lectureId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.Content,
                    n.ResourceType,
                    n.TimeStampSeconds,
                    n.CreatedAt
                })
                .ToListAsync();

            return Ok(notes);
        }

        // ============================================
        // Delete Note
        // ============================================
        [HttpDelete("{noteId}")]
        public async Task<IActionResult> DeleteNote(int noteId)
        {
            var studentId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (studentId == null)
                return Unauthorized();

            var note = await _context.LectureNotes
                .FirstOrDefaultAsync(n => n.Id == noteId && n.StudentId == studentId);

            if (note == null)
                return NotFound("Note not found.");

            _context.LectureNotes.Remove(note);
            await _context.SaveChangesAsync();

            return Ok("Note deleted successfully.");
        }
    }
}