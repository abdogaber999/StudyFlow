using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudyFlow.API.DTOs;
using StudyFlow.Infrastructure.Identity;
using StudyFlow.API.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly JwtService _jwtService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            JwtService jwtService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid input data.");

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
                return BadRequest("Email already exists.");

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Email = model.Email,
                UserName = model.Email,
                NationalId = model.NationalId,
                UniversityId = model.UniversityId
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors.Select(e => e.Description));

            // Add role
            var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
            if (!roleResult.Succeeded)
                return BadRequest(roleResult.Errors.Select(e => e.Description));

            return Ok("User Registered Successfully");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid input data.");

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
                return Unauthorized("Invalid Email or Password.");

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);

            if (!result.Succeeded)
                return Unauthorized("Invalid Email or Password.");

            var roles = await _userManager.GetRolesAsync(user);

            var token = _jwtService.GenerateToken(user, roles);

            return Ok(new
            {
                token,
                expiration = DateTime.UtcNow.AddMinutes(60),
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.Email,
                    roles,
                    user.UniversityId
                }
            });
        }

        [HttpGet("whoami")]
        [Authorize]
        public IActionResult WhoAmI()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var roles = User.Claims
                            .Where(c => c.Type == ClaimTypes.Role)
                            .Select(c => c.Value)
                            .ToList();

            return Ok(new
            {
                userId,
                email,
                roles
            });
        }

        [HttpGet("test")]
        [Authorize]
        public IActionResult Test()
        {
            return Ok("You are authorized!");
        }

        [HttpGet("doctor-only")]
        [Authorize(Roles = "Doctor")]
        public IActionResult DoctorOnly()
        {
            return Ok("Welcome Doctor 👨‍🏫");
        }

        [HttpGet("student-only")]
        [Authorize(Roles = "Student")]
        public IActionResult StudentOnly()
        {
            return Ok("Welcome Student 🎓");
        }
    }
}
