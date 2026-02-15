using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudyFlow.API.DTOs;
using StudyFlow.Infrastructure.Identity;
using System.Security.Claims;

namespace StudyFlow.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // ============================
        // Get Current User Profile
        // ============================
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.PhoneNumber,
                user.FullName
            });
        }

        // ============================
        // Update Full Name
        // ============================
        [HttpPut("fullname")]
        public async Task<IActionResult> UpdateFullName(UpdateFullNameDto model)
        {
            if (string.IsNullOrWhiteSpace(model.FullName))
                return BadRequest("Full name cannot be empty.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound();

            user.FullName = model.FullName;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new
            {
                message = "Full name updated successfully.",
                fullName = user.FullName
            });
        }

        // ============================
        // Update Phone Number
        // ============================
        [HttpPut("phone")]
        public async Task<IActionResult> UpdatePhone(UpdatePhoneDto model)
        {
            if (string.IsNullOrWhiteSpace(model.PhoneNumber))
                return BadRequest("Phone number cannot be empty.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.PhoneNumber = model.PhoneNumber;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new
            {
                message = "Phone number updated successfully.",
                phoneNumber = user.PhoneNumber
            });
        }

        // ============================
        // Change Password
        // ============================
        [HttpPut("password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
        {
            if (string.IsNullOrWhiteSpace(model.CurrentPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword))
                return BadRequest("Password fields cannot be empty.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var result = await _userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword
            );

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new
            {
                message = "Password changed successfully."
            });
        }



    }
}
