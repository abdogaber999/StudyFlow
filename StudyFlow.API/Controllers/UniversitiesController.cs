using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyFlow.Infrastructure.DbContexts;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UniversitiesController : ControllerBase
    {
        private readonly StudyFlowDbContext _context;

        public UniversitiesController(StudyFlowDbContext context)
        {
            _context = context;
        }

        // 🔓 Public - Get All Universities
        [HttpGet]
        public async Task<IActionResult> GetUniversities()
        {
            var universities = await _context.Universities
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.Name
                })
                .ToListAsync();

            return Ok(universities);
        }
    }
}