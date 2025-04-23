using DocumentFormat.OpenXml.Spreadsheet;
using Library.Data;
using System.Security;
using Library.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Library.Services;
using Microsoft.AspNetCore.Authorization;

namespace Library.Controllers
{
    [Route("api/userlogin")]
    [ApiController]
    public class UserLoginController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserLoginController> _logger;
        private readonly JwtService _jwtService;

        public UserLoginController(AppDbContext context, JwtService jwtService, ILogger<UserLoginController> logger)
        {
            _logger = logger;
            _context = context;
            _jwtService = jwtService;
        }

        // ✅ Student Login
        [HttpPost("student")]
        public async Task<IActionResult> StudentLogin([FromQuery] LoginUserDto dto)
        {
            _logger.LogInformation("Attempting student login for UserId: {UserId}", dto.UserId);
            return await AuthenticateUser(dto, "Student", false);
        }

        // ✅ Lecturer Login
        [HttpPost("lecturer")]
        public async Task<IActionResult> LecturerLogin([FromQuery] LoginUserDto dto)
        {
            _logger.LogInformation("Attempting lecturer login for UserId: {UserId}", dto.UserId);
            return await AuthenticateUser(dto, "Lecturer", false);
        }

        // ✅ Admin Login
        [HttpPost("admin")]
        public async Task<IActionResult> AdminLogin([FromQuery] LoginUserDto dto)
        {
            _logger.LogInformation("Attempting admin login for UserId: {UserId}", dto.UserId);
            return await AuthenticateUser(dto, "Admin", true);
        }

        // 🔹 Helper method to authenticate users
        private async Task<IActionResult> AuthenticateUser(LoginUserDto dto, string userType, bool? IsAdmin)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.Password))
            {
                _logger.LogWarning("Login attempt failed for {UserType} with UserId: {UserId}. Reason: Missing UserId or Password.", userType, dto.UserId);
                return BadRequest("UserId and Password are required.");
            }

            // Find user
            var user = _context.Users.FirstOrDefault(u => u.UserId == dto.UserId && (u.UserType == userType || u.IsAdmin == IsAdmin));

            var previouslogin = _context.UserLoginHistories.FirstOrDefault(u => u.UserId == dto.UserId && u.LogoutTime == null);
            if (previouslogin!= null)
            {
                previouslogin.LogoutTime = DateTime.UtcNow;
            }
            if (user == null)
            {
                _logger.LogWarning("Invalid {UserType} credentials for UserId: {UserId}.", userType, dto.UserId);
                return Unauthorized(new { message = $"Invalid {userType} credentials." });
            }

            // ❌ Check if the user is deactivated
            if (!user.IsActive)
            {
                _logger.LogWarning("Account for UserId: {UserId} is deactivated.", dto.UserId);
                return Unauthorized(new { message = "Account is deactivated. Please contact the admin." });
            }

            // Verify password
            if (HashPassword(dto.Password) != user.PasswordHash)
            {
                _logger.LogWarning("Invalid password for UserId: {UserId}.", dto.UserId);
                return Unauthorized(new { message = $"Invalid {userType} credentials." });
            }

            // Session handling
            var sessionDuration = TimeSpan.FromHours(1);
            var expiryTime = DateTime.UtcNow.Add(sessionDuration);

            var loginHistory = new UserLoginHistory
            {
                UserId = user.UserId,
                UserType = user.UserType,
                LoginTime = DateTime.UtcNow,
                SessionExpiry = expiryTime,
            };
            user.IsLoggedIn = true;
            _context.Users.Update(user);
            _context.UserLoginHistories.Add(loginHistory);
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateToken(user);

            _logger.LogInformation("{UserType} login successful for UserId: {UserId} at {LoginTime}. Session will expire at {ExpiryTime}.", userType, dto.UserId, DateTime.UtcNow, expiryTime);

            return Ok(new
            {   email=user.Email, 
                message = $"{userType} login successful",
                sessionExpiry = expiryTime,
                token = token
            });
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromQuery] string userId)
        {
            _logger.LogInformation("Attempting logout for UserId: {UserId}", userId);

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId && u.IsLoggedIn == true);
            var lastLogin = _context.UserLoginHistories
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.LoginTime)
                .FirstOrDefault();

            if (lastLogin == null || lastLogin.LogoutTime != null)
            {
                _logger.LogWarning("No active session found for UserId: {UserId}.", userId);
                return NotFound("No active login found.");
            }

            user.IsLoggedIn = false;
            lastLogin.LogoutTime = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("UserId: {UserId} has successfully logged out at {LogoutTime}.", userId, DateTime.UtcNow);

            return Ok(new { message = "Logout successful" });
        }
        [HttpGet("checkType")]
        public IActionResult CheckType([FromQuery] string userId, string usertype,bool?isAdmin)
        {
            _logger.LogInformation("Checking Type for UserId: {UserId}.", userId);

            var user = _context.Users
                .FirstOrDefault(u => (u.UserType == usertype||u.IsAdmin==isAdmin) && u.UserId == userId);  // Fetching a single record

            if (user == null)
            {
                _logger.LogWarning("No login record found for UserId: {UserId}.", userId);
                return NotFound("No user found");
            }

            return Ok(new { message = $"User: {userId} is of type {usertype}" });
        }
        [HttpGet("Isverified")]
        public IActionResult CheckVerification([FromQuery] string userId)
        {
            _logger.LogInformation("Checking Type for UserId: {UserId}.", userId);

            var user = _context.Users
                .FirstOrDefault(u => u.UserId == userId && u.IsEmailVerified==true);  // Fetching a single record

            if (user == null)
            {
                _logger.LogWarning("User is not verified: {UserId}.", userId);
                return NotFound("No verified user found");
            }

            return Ok(new { message = $"User: {userId} is verifed" });
        }


        [HttpGet("check-session")]
        [Authorize]
        public IActionResult CheckSession([FromQuery] string userId)
        {
            _logger.LogInformation("Checking session for UserId: {UserId}.", userId);

            var lastLogin = _context.UserLoginHistories
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.LoginTime)
                .FirstOrDefault();

            if (lastLogin == null)
            {
                _logger.LogWarning("No login record found for UserId: {UserId}.", userId);
                return NotFound("No login record found.");
            }

            if (lastLogin.LogoutTime != null || DateTime.UtcNow > lastLogin.SessionExpiry)
            {
                _logger.LogWarning("Session expired for UserId: {UserId}.", userId);
                return Unauthorized("Session expired. Please log in again.");
            }

            _logger.LogInformation("Session is still active for UserId: {UserId}, expires at {ExpiryTime}.", userId, lastLogin.SessionExpiry);

            return Ok(new { message = "Session is still active", sessionExpiry = lastLogin.SessionExpiry });
        }

        [HttpPost("auto-logout")]
        [Authorize]
        public async Task<IActionResult> AutoLogout([FromQuery] string userId)
        {
            _logger.LogInformation("Attempting auto-logout for UserId: {UserId}", userId);

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId && u.IsLoggedIn == true);
            var lastLogin = _context.UserLoginHistories
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.LoginTime)
                .FirstOrDefault();

            if (lastLogin == null || lastLogin.LogoutTime != null)
            {
                _logger.LogWarning("No active session found for UserId: {UserId}.", userId);
                return NotFound("No active session found.");
            }

            if (DateTime.UtcNow > lastLogin.SessionExpiry)
            {
                user.IsLoggedIn = false;
                lastLogin.LogoutTime = lastLogin.SessionExpiry; // Auto-logout at expiry time
                await _context.SaveChangesAsync();

                _logger.LogInformation("UserId: {UserId} auto-logged out due to session expiry at {ExpiryTime}.", userId, lastLogin.SessionExpiry);

                return Unauthorized("Session expired. You have been logged out.");
            }

            _logger.LogInformation("Session is still active for UserId: {UserId}.", userId);
            return Ok(new { message = "Session is still active" });
        }

        [HttpGet("Login-History")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<UserLoginHistory>>> GetAllLogin()
        {
            _logger.LogInformation("Fetching all user login history.");

            var history = await _context.UserLoginHistories.ToListAsync();

            _logger.LogInformation("Fetched {Count} login history records.", history.Count);

            return history;
        }
    }
}
