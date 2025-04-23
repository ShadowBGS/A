//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading.Tasks;
//using ClosedXML.Excel;
//using DocumentFormat.OpenXml.Spreadsheet;
//using Library.Data;
//using Library.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//namespace Library.Controllers
//{
//    [Route("api/user")]
//    [ApiController]
//    [Authorize]
//    public class UserController : ControllerBase
//    {
//        private readonly ILogger<UserController> _logger;
//        private readonly AppDbContext _context;

//        public UserController(AppDbContext context, ILogger<UserController> logger)
//        {
//            _logger = logger;
//            _context = context;
//        }

//        // ✅ Get all users
//        [HttpGet]
//        public async Task<ActionResult<object>> GetUsers()
//        {


//            var users = await _context.Users
//                .Select(u => new
//                {
//                    u.Id,
//                    u.UserId,
//                    u.FirstName,
//                    u.LastName,
//                    u.Email,
//                    u.UserType,
//                    u.IsAdmin,
//                    u.Department,
//                    u.School,
//                    u.IsLoggedIn,
//                    u.IsActive,
//                    u.Rating,
//                    CurrentlyBorrowed = _context.BorrowRecords.Count(br => br.UserId == u.UserId && !br.IsReturned),

//                    borrowlimit=u.GetBorrowLimit()
//                }).ToListAsync();

//            return Ok(users);
//        }


//        [HttpGet("Users")]
//        public async Task<IActionResult> GetAllBorrowHistory([FromQuery] string? UserId, [FromQuery] string? UserType, [FromQuery] string? FirstName,[FromQuery] string? LastName, [FromQuery] bool? IsAdmin, [FromQuery] bool? IsActive,  [FromQuery] bool? IsLogged, [FromQuery] string? Department, [FromQuery] string? School ,[FromQuery] double? Rating, [FromQuery] string? email)// [FromQuery] int? Level 
//        {
//            if (!string.IsNullOrEmpty(UserId))
//            {
//                UserId = Uri.UnescapeDataString(UserId);
//            }
//            var query = _context.Users.AsQueryable();
//            if (Rating.HasValue)
//            {
//                query = query.Where(b => b.Rating == Rating);
//            }
//            if (!string.IsNullOrEmpty(Department))
//            {
//                query = query.Where(b => b.Department.Contains(Department));
//            }
//            if (!string.IsNullOrEmpty(UserType))
//            {
//                query = query.Where(b => b.UserType.Contains(UserType));
//            }
//            if (!string.IsNullOrEmpty(FirstName))
//            {
//                query = query.Where(b => b.FirstName.Contains(FirstName));
//            }

//            if (!string.IsNullOrEmpty(School))
//            {
//                query = query.Where(b => b.School.Contains(School));
//            }
//            if (!string.IsNullOrEmpty(UserId))
//            {
//                query = query.Where(b => b.UserId.Contains(UserId));
//            }
//            if (!string.IsNullOrEmpty(email))
//            {
//                query = query.Where(b => b.Email.Contains(email));
//            }
//            if (!string.IsNullOrEmpty(LastName))
//            {
//                query = query.Where(b => b.LastName.Contains(LastName));
//            }

//            if (IsActive.HasValue)
//            {
//                query = query.Where(b => b.IsActive == IsActive.Value);
//            }

//            if (IsAdmin.HasValue)
//            {
//                query = query.Where(b => b.IsAdmin == IsAdmin);
//            }


//            if (IsLogged.HasValue)
//            {
//                query = query.Where(b => b.IsLoggedIn == IsLogged.Value);
//            }

//            // Compute the statistics
//            var total = await query.CountAsync();
//            int currentlyBorrowed = await _context.BorrowRecords
//                .CountAsync(b => b.UserId == UserId && !b.IsReturned);

//            var Users = await query
//                .Select(b => new
//                {
//                    b.UserId,
//                    b.FirstName,
//                    b.LastName,
//                    b.Email,
//                    b.UserType,
//                    b.IsAdmin,
//                    b.Department,
//                    b.School,
//                    b.Rating,
//                    b.IsActive,
//                    b.IsLoggedIn,
//                    CurrentlyBorrowed = _context.BorrowRecords.Count(br => br.UserId == b.UserId && !br.IsReturned),
//                    borrowlimit = b.GetBorrowLimit()
//                })
//                .ToListAsync();

//            return Ok(new
//            {
//                total=total,
//                Users=Users
//            });
//        }


//        [HttpPost("register")]
//        public async Task<IActionResult> RegisterUser([FromQuery] RegisterUserDto dto)
//        {
//            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
//            {
//                return BadRequest("Email already in use.");
//            }

//            // Hash the password before storing it
//            string passwordHash = HashPassword(dto.Password);

//            var newUser = new User
//            {
//                UserId = dto.UserId,
//                FirstName = dto.FirstName,
//                LastName = dto.LastName,
//                Email = dto.Email,
//                UserType = dto.UserType,
//                IsAdmin = dto.IsAdmin,
//                Department = dto.Department,
//                School = dto.School,
//                PasswordHash = passwordHash,
//                Rating = (dto.UserType == "Student" || dto.UserType == "Lecturer") ? 5.0 : 1

//            };

//            _context.Users.Add(newUser);
//            await _context.SaveChangesAsync();

//            return CreatedAtAction(nameof(GetUser), new { userId = newUser.UserId }, new
//            {
//                newUser.UserId,
//                newUser.FirstName,
//                newUser.LastName,
//                newUser.Email,
//                newUser.UserType,
//                newUser.IsAdmin,
//                newUser.Department,
//                newUser.School,
//                newUser.Rating
//            });
//        }

//        [HttpGet("{UserId}")]
//        public async Task<ActionResult<object>> GetUser(string UserId)
//        {
//            int currentlyBorrowed = await _context.BorrowRecords
//                .CountAsync(b => b.UserId == UserId && !b.IsReturned);
//            UserId = Uri.UnescapeDataString(UserId);
//            var user = await _context.Users
//                .Where(u => u.UserId == UserId)
//                .Select(u => new
//                {
//                    u.Id,
//                    u.UserId,
//                    u.FirstName,
//                    u.LastName,
//                    u.Email,
//                    u.UserType,
//                    u.IsAdmin,
//                    u.Department,
//                    u.School,
//                    u.Rating,
//                    CurrentlyBorrowed = _context.BorrowRecords.Count(br => br.UserId == u.UserId && !br.IsReturned),
//                    borrowlimit = u.GetBorrowLimit(),
//                    RCategory =u.GetRatingCategory()
//                })
//                .FirstOrDefaultAsync();

//            if (user == null)
//                return NotFound();

//            return Ok(user);
//        }

//        // Helper function to hash passwords

//        // ✅ Update an existing user
//        [HttpPut("{UserId}")]
//        public async Task<IActionResult> UpdateUser(string UserId, User updatedUser)
//        {
//            UserId = Uri.UnescapeDataString(UserId);
//            var user = await _context.Users.FindAsync(UserId);
//            if (user == null)
//                return NotFound();

//            user.FirstName = updatedUser.FirstName;
//            user.LastName = updatedUser.LastName;
//            user.Email = updatedUser.Email;
//            user.UserType = updatedUser.UserType;
//            user.IsAdmin = updatedUser.IsAdmin;
//            user.Department = updatedUser.Department;
//            user.School = updatedUser.School;

//            if (!string.IsNullOrWhiteSpace(updatedUser.PasswordHash))
//                user.PasswordHash = HashPassword(updatedUser.PasswordHash); // Update password

//            await _context.SaveChangesAsync();
//            return NoContent();
//        }
//        [HttpGet("export-users")]
//        public async Task<IActionResult> ExportUsers()
//        {
//            var users = await _context.Users.ToListAsync();

//            using (var workbook = new XLWorkbook())
//            {
//                var worksheet = workbook.Worksheets.Add("Users");
//                worksheet.Cell(1, 1).Value = "User ID";
//                worksheet.Cell(1, 2).Value = "First Name";
//                worksheet.Cell(1, 3).Value = "Last Name";
//                worksheet.Cell(1, 4).Value = "Email";
//                worksheet.Cell(1, 5).Value = "User Type";
//                worksheet.Cell(1, 6).Value = "Department";
//                worksheet.Cell(1, 7).Value = "School";
//                worksheet.Cell(1, 8).Value = "Rating";
//                worksheet.Cell(1, 9).Value = "IsActive";


//                int row = 2;
//                foreach (var user in users)
//                {
//                    worksheet.Cell(row, 1).Value = user.UserId;
//                    worksheet.Cell(row, 2).Value = user.FirstName;
//                    worksheet.Cell(row, 3).Value = user.LastName;
//                    worksheet.Cell(row, 4).Value = user.Email;
//                    worksheet.Cell(row, 5).Value = user.UserType;
//                    worksheet.Cell(row, 6).Value = user.Department;
//                    worksheet.Cell(row, 7).Value = user.School;
//                    worksheet.Cell(row, 8).Value = user.Rating;
//                    worksheet.Cell(row, 9).Value = user.IsActive;
//                    row++;
//                }

//                using (var stream = new MemoryStream())
//                {
//                    workbook.SaveAs(stream);
//                    var content = stream.ToArray();
//                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Users.xlsx");
//                }
//            }
//        }

//        [HttpPost("import-users")]
//        [Consumes("multipart/form-data")]
//        public async Task<IActionResult> ImportUsers(IFormFile file)
//        {
//            if (file == null || file.Length == 0)
//                return BadRequest("Invalid file.");

//            using (var stream = new MemoryStream())
//            {
//                await file.CopyToAsync(stream);
//                using (var workbook = new XLWorkbook(stream))
//                {
//                    var worksheet = workbook.Worksheet(1);
//                    var rows = worksheet.RowsUsed().Skip(1);

//                    foreach (var row in rows)
//                    {
//                        var user = new User
//                        {
//                            UserId = row.Cell(1).GetString(),
//                            FirstName = row.Cell(2).GetString(),
//                            LastName = row.Cell(3).GetString(),
//                            Email = row.Cell(4).GetString(),
//                            UserType = row.Cell(5).GetString(),
//                            Department = row.Cell(6).GetString(),
//                            School = row.Cell(7).GetString(),
//                            Rating = row.Cell(8).GetDouble(),
//                            IsAdmin= false,
//                            IsActive=false
//                        };
//                        _context.Users.Add(user);
//                    }

//                    await _context.SaveChangesAsync();
//                }
//            }

//            return Ok("Users imported successfully.");
//        }

//         //✅ Delete a user
//         //✅ Deactivate User(Soft Delete)
//        [HttpPost("deactivate")]
//        public async Task<IActionResult> DeactivateUser([FromQuery] string userId)
//        {
//            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
//            if (user == null)
//                return NotFound("User not found.");

//            user.IsActive = false; // Set user as inactive
//            await _context.SaveChangesAsync();

//            return Ok(new { message = "User deactivated successfully." });
//        }

//        // ✅ Reactivate User
//        [HttpPost("reactivate")]
//        public async Task<IActionResult> ReactivateUser([FromQuery] string userId)
//        {
//            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
//            if (user == null)
//                return NotFound("User not found.");

//            user.IsActive = true; // Set user as active
//            await _context.SaveChangesAsync();

//            return Ok(new { message = "User reactivated successfully." });
//        }

//        // ❌ Delete User (Permanent)
//        [HttpDelete("delete")]
//        public async Task<IActionResult> DeleteUser([FromQuery] string userId)
//        {
//            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
//            if (user == null)
//                return NotFound("User not found.");

//            _context.Users.Remove(user);
//            await _context.SaveChangesAsync();

//            return Ok(new { message = "User deleted successfully." });
//        }


//        // ✅ Password Hashing Function
//        private string HashPassword(string password)
//        {
//            using (var sha256 = SHA256.Create())
//            {
//                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
//                StringBuilder builder = new StringBuilder();
//                foreach (byte b in bytes)
//                {
//                    builder.Append(b.ToString("x2"));
//                }
//                return builder.ToString();
//            }
//        }
//    }
//}
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Library.Data;
using Library.Models;
using Library.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.Controllers
{
    [Route("api/user")]
    [ApiController]
    //[Authorize]
    public class UserController : ControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private readonly AppDbContext _context;

        public UserController(AppDbContext context, ILogger<UserController> logger)
        {
            _logger = logger;
            _context = context;
        }

        // ✅ Get all users
        [HttpGet]
        public async Task<ActionResult<object>> GetUsers()
        {
            _logger.LogInformation("Fetching all users.");

            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.UserId,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.UserType,
                    u.IsAdmin,
                    u.Department,
                    u.School,
                    u.IsLoggedIn,
                    u.IsActive,
                    u.Rating,
                    CurrentlyBorrowed = _context.BorrowRecords.Count(br => br.UserId == u.UserId && !br.IsReturned),
                    borrowlimit = u.GetBorrowLimit()
                }).ToListAsync();

            _logger.LogInformation($"Fetched {users.Count} users.");
            return Ok(users);
        }

        [HttpGet("Users")]
        public async Task<IActionResult> GetAllBorrowHistory(
    [FromQuery] string? UserId,
    [FromQuery] string? UserType,
    [FromQuery] string? FirstName,
    [FromQuery] string? LastName,
    [FromQuery] bool? IsAdmin,
    [FromQuery] bool? IsActive,
    [FromQuery] bool? IsLogged,
    [FromQuery] string? Department,
    [FromQuery] string? School,
    [FromQuery] double? Rating,
    [FromQuery] string? email,
    [FromQuery]bool? IsEmailVerified,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation("Fetching filtered user history.");

            if (!string.IsNullOrEmpty(UserId))
            {
                UserId = Uri.UnescapeDataString(UserId);
            }

            var query = _context.Users.AsQueryable();

            if (Rating.HasValue)
                query = query.Where(b => b.Rating == Rating);
            if (!string.IsNullOrEmpty(Department))
                query = query.Where(b => b.Department.Contains(Department));
            if (!string.IsNullOrEmpty(UserType))
                query = query.Where(b => b.UserType.Contains(UserType));
            if (!string.IsNullOrEmpty(FirstName))
                query = query.Where(b => b.FirstName.Contains(FirstName));
            if (!string.IsNullOrEmpty(School))
                query = query.Where(b => b.School.Contains(School));
            if (!string.IsNullOrEmpty(UserId))
                query = query.Where(b => b.UserId.Contains(UserId));
            if (!string.IsNullOrEmpty(email))
                query = query.Where(b => b.Email.Contains(email));
            if (!string.IsNullOrEmpty(LastName))
                query = query.Where(b => b.LastName.Contains(LastName));
            if (IsActive.HasValue)
                query = query.Where(b => b.IsActive == IsActive.Value);
            if (IsEmailVerified.HasValue)
                query = query.Where(b => b.IsEmailVerified == IsEmailVerified.Value);
            if (IsAdmin.HasValue)
                query = query.Where(b => b.IsAdmin == IsAdmin);
            if (IsLogged.HasValue)
                query = query.Where(b => b.IsLoggedIn == IsLogged.Value);

            var total = await query.CountAsync();
            var totallogged = await query.CountAsync(b => b.IsLoggedIn == true);
            var totalactive = await query.CountAsync(b => b.IsActive == true);
            int currentlyBorrowed = await _context.BorrowRecords.CountAsync(b => b.UserId == UserId && b.IsReturned == false);
            var totalstudent = await _context.Users.CountAsync(b => b.UserType == "Student");
            var totallecturers = await _context.Users.CountAsync(b => b.UserType == "Lecturer");
            var totaladmin = await _context.Users.CountAsync(b => b.UserType == "Admin");

            var Users = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.UserId,
                    b.FirstName,
                    b.LastName,
                    b.Email,
                    b.UserType,
                    b.IsAdmin,
                    b.Department,
                    b.School,
                    b.Rating,
                    b.IsActive,
                    b.IsLoggedIn,
                    CurrentlyBorrowed = _context.BorrowRecords.Count(br => br.UserId == b.UserId && !br.IsReturned),
                    borrowlimit = b.GetBorrowLimit()
                })
                .ToListAsync();

            _logger.LogInformation($"Fetched {Users.Count} users after applying filters.");

            return Ok(new
            {
                total,
                pageNumber,
                pageSize,
                totalPages = (int)Math.Ceiling(total / (double)pageSize),
                TotalLecturers = totallecturers,
                TotalAdmins = totaladmin,
                TotalStudents = totalstudent,
                Totalactive = totalactive,
                TotalLogged = totallogged,
                Users
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser([FromQuery] RegisterUserDto dto)
        {
            _logger.LogInformation($"Registering new user with email: {dto.Email}");

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                _logger.LogWarning($"Registration failed for email {dto.Email}: Email already in use.");
                return BadRequest(new{message="Email already in use."});
            }
            if (await _context.Users.AnyAsync(u => u.UserId == dto.UserId))
            {
                _logger.LogWarning($"Registration failed for UserID {dto.UserId}: UserId already in use.");
                return BadRequest(new { message = "UserId already in use." });
            }
            string passwordHash = HashPassword(dto.Password);

            var newUser = new User
            {
                UserId = dto.UserId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                UserType = dto.UserType,
                IsAdmin = dto.IsAdmin,
                Department = dto.Department,
                School = dto.School,
                PasswordHash = passwordHash,
                Rating = (dto.UserType == "Student" || dto.UserType == "Lecturer") ? 5.0 : 1
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User with email {dto.Email} registered successfully.");
            return CreatedAtAction(nameof(GetUser), new { userId = newUser.UserId }, new
            {
                newUser.UserId,
                newUser.FirstName,
                newUser.LastName,
                newUser.Email,
                newUser.UserType,
                newUser.IsAdmin,
                newUser.Department,
                newUser.School,
                newUser.Rating
            });
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("register-admin")]
        public async Task<IActionResult> RegisterAdmin([FromQuery] RegisterUserDto dto)
        {
            _logger.LogInformation($"Registering new user with email: {dto.Email}");

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                _logger.LogWarning($"Registration failed for email {dto.Email}: Email already in use.");
                return BadRequest("Email already in use.");
            }

            string passwordHash = HashPassword(dto.Password);

            var newUser = new User
            {
                UserId = dto.UserId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                UserType = dto.UserType,
                IsAdmin = dto.IsAdmin,
                Department = dto.Department,
                School = dto.School,
                PasswordHash = passwordHash,
                Rating = (dto.UserType == "Student" || dto.UserType == "Lecturer") ? 5.0 : 1
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User with email {dto.Email} registered successfully.");
            return CreatedAtAction(nameof(GetUser), new { userId = newUser.UserId }, new
            {
                newUser.UserId,
                newUser.FirstName,
                newUser.LastName,
                newUser.Email,
                newUser.UserType,
                newUser.IsAdmin,
                newUser.Department,
                newUser.School,
                newUser.Rating
            });
        }
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromQuery] string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(s => s.Email == email&& s.IsEmailVerified==false);
            if (user == null)
                return NotFound(new { message = "Email not found or is already verified." });

            var otp = new Random().Next(100000, 999999).ToString();
            var expiration = DateTime.UtcNow.AddMinutes(10);

            var existingOtp = await _context.EmailOtps.FirstOrDefaultAsync(e => e.Email == email);
            if (existingOtp != null)
            {
                existingOtp.OtpCode = otp;
                existingOtp.Expiration = expiration;
            }
            else
            {
                await _context.EmailOtps.AddAsync(new EmailOtp
                {
                    Email = email,
                    OtpCode = otp,
                    Expiration = expiration
                });
            }

            await _context.SaveChangesAsync();

            var subject = "Your OTP Code";
            var body = $"<h1>You requested this code to verify your email on STAR LAS</h1><p>Your OTP is <b>{otp}</b>. It expires in 10 minutes.</p>";
            new EmailService().SendVerificationEmail(email, subject, body);

            return Ok(new { message = "OTP sent to email." });
        }
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromQuery] OtpVerificationRequest request)
        {
            var otpEntry = await _context.EmailOtps.FirstOrDefaultAsync(e => e.Email == request.Email);
            if (otpEntry == null || otpEntry.Expiration < DateTime.UtcNow)
                return BadRequest( new { message = "OTP expired or not found." });

            if (otpEntry.OtpCode != request.Otp)
                return BadRequest(new { message = "Invalid OTP." });

            var user = await _context.Users.FirstOrDefaultAsync(s => s.Email == request.Email);
            if (user == null)
                return NotFound(new { message = "Student not found." });

            user.IsEmailVerified = true;
            _context.EmailOtps.Remove(otpEntry);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Email verified successfully!" });
        }

        public class OtpVerificationRequest
        {
            public string Email { get; set; }
            public string Otp { get; set; }
        }



        [HttpGet("{UserId}")]
        public async Task<ActionResult<object>> GetUser(string UserId)
        {
            _logger.LogInformation($"Fetching details for user with ID: {UserId}");

            int currentlyBorrowed = await _context.BorrowRecords
                .CountAsync(b => b.UserId == UserId && !b.IsReturned);

            UserId = Uri.UnescapeDataString(UserId);
            var user = await _context.Users
                .Where(u => u.UserId == UserId)
                .Select(u => new
                {
                    u.Id,
                    u.UserId,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.UserType,
                    u.IsAdmin,
                    u.Department,
                    u.School,
                    u.Rating,
                    CurrentlyBorrowed = _context.BorrowRecords.Count(br => br.UserId == u.UserId && !br.IsReturned),
                    borrowlimit = u.GetBorrowLimit(),
                    RCategory = u.GetRatingCategory()
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                _logger.LogWarning($"User with ID {UserId} not found.");
                return NotFound();
            }

            _logger.LogInformation($"Fetched details for user with ID: {UserId}");
            return Ok(user);
        }
        [Authorize(Roles = "Admin")]
        [HttpPut("{UserId}")]
        public async Task<IActionResult> UpdateUser(string UserId, User updatedUser)
        {
            _logger.LogInformation($"Updating user with ID: {UserId}");

            UserId = Uri.UnescapeDataString(UserId);
            var user = await _context.Users.FindAsync(UserId);
            if (user == null)
            {
                _logger.LogWarning($"User with ID {UserId} not found.");
                return NotFound();
            }

            user.FirstName = updatedUser.FirstName;
            user.LastName = updatedUser.LastName;
            user.Email = updatedUser.Email;
            user.UserType = updatedUser.UserType;
            user.IsAdmin = updatedUser.IsAdmin;
            user.Department = updatedUser.Department;
            user.School = updatedUser.School;

            if (!string.IsNullOrWhiteSpace(updatedUser.PasswordHash))
                user.PasswordHash = HashPassword(updatedUser.PasswordHash);

            await _context.SaveChangesAsync();
            _logger.LogInformation($"User with ID {UserId} updated successfully.");
            return NoContent();
        }
        //[HttpPost("")]
        [Authorize(Roles = "Admin")]
        [HttpPatch("{userId}")]
        public IActionResult PatchUser(string userId, [FromBody] JsonPatchDocument<User> patchDoc)
        {
            if (patchDoc == null)
            {
                return BadRequest(new { message = "Invalid patch document." });
            }

            userId = Uri.UnescapeDataString(userId);
            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            try
            {
                patchDoc.ApplyTo(user, ModelState);

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                _context.SaveChanges();
                return Ok(new { message = "User updated successfully", user });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Patch failed", details = ex.Message });
            }
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("deactivate")]
        public async Task<IActionResult> DeactivateUser([FromQuery] string userId)
        {
            _logger.LogInformation($"Deactivating user with ID: {userId}");

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                _logger.LogWarning($"User with ID {userId} not found.");
                return NotFound("User not found.");
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User with ID {userId} deactivated.");
            return Ok(new { message = "User deactivated successfully." });
        }
        [Authorize(Roles = "Admin")]
        [HttpPost("reactivate")]
        public async Task<IActionResult> ReactivateUser([FromQuery] string userId)
        {
            _logger.LogInformation($"Reactivating user with ID: {userId}");

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                _logger.LogWarning($"User with ID {userId} not found.");
                return NotFound("User not found.");
            }

            user.IsActive = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User with ID {userId} reactivated.");
            return Ok(new { message = "User reactivated successfully." });
        }
        [Authorize(Roles = "Admin")]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteUser([FromQuery] string userId)
        {
            _logger.LogInformation($"Deleting user with ID: {userId}");

            var user = _context.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null)
            {
                _logger.LogWarning($"User with ID {userId} not found.");
                return NotFound(new { message = "User not found." });
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User with ID {userId} deleted.");
            return Ok(new { message = "User deleted successfully." });
        }
        [HttpGet("export-users")]
        public async Task<IActionResult> ExportUsers()
        {
            var users = await _context.Users.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Users");
                worksheet.Cell(1, 1).Value = "User ID";
                worksheet.Cell(1, 2).Value = "First Name";
                worksheet.Cell(1, 3).Value = "Last Name";
                worksheet.Cell(1, 4).Value = "Email";
                worksheet.Cell(1, 5).Value = "User Type";
                worksheet.Cell(1, 6).Value = "Department";
                worksheet.Cell(1, 7).Value = "School";
                worksheet.Cell(1, 8).Value = "Rating";
                worksheet.Cell(1, 9).Value = "IsAdmin";
                worksheet.Cell(1, 10).Value = "IsActive";
                worksheet.Cell(1, 11).Value = "Ticket";
                worksheet.Cell(1, 12).Value = "PasswordHash";  // Added PasswordHash column

                int row = 2;
                foreach (var user in users)
                {
                    worksheet.Cell(row, 1).Value = user.UserId ?? "N/A";
                    worksheet.Cell(row, 2).Value = user.FirstName ?? "N/A";
                    worksheet.Cell(row, 3).Value = user.LastName ?? "N/A";
                    worksheet.Cell(row, 4).Value = user.Email ?? "N/A";
                    worksheet.Cell(row, 5).Value = user.UserType ?? "N/A";
                    worksheet.Cell(row, 6).Value = user.Department ?? "N/A";
                    worksheet.Cell(row, 7).Value = user.School ?? "N/A";
                    worksheet.Cell(row, 8).Value = user.Rating;
                    worksheet.Cell(row, 9).Value = user.IsAdmin.ToString();
                    worksheet.Cell(row, 10).Value = user.IsActive.ToString();
                    worksheet.Cell(row, 11).Value = user.Ticket;
                    worksheet.Cell(row, 12).Value = user.PasswordHash ?? "N/A";  // Added PasswordHash value
                    row++;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Users.xlsx");
                }
            }
        }
        [HttpPost("import-users")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportUsers(IFormFile file)
        {
            var skippedUsers = new List<string>();
            var count = skippedUsers.Count();
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file.");

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RowsUsed().Skip(1);

                    

                    foreach (var row in rows)
                    {
                        string userId = row.Cell(1).GetString().Trim();
                        string firstName = row.Cell(2).GetString().Trim();
                        string lastName = row.Cell(3).GetString().Trim();
                        string email = row.Cell(4).GetString().Trim();
                        string userType = row.Cell(5).GetString().Trim();
                        string department = row.Cell(6).GetString().Trim();
                        string school = row.Cell(7).GetString().Trim();
                        double rating;
                        bool isAdmin;
                        bool isActive;
                        int ticket;
                        string passwordHash = row.Cell(12).GetString().Trim();  // Added password hash field

                        // Validation: Check for missing or invalid fields
                        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(email) ||
                            string.IsNullOrEmpty(userType) || string.IsNullOrEmpty(department) || string.IsNullOrEmpty(school) ||
                            !double.TryParse(row.Cell(8).GetString(), out rating) || !bool.TryParse(row.Cell(9).GetString(), out isAdmin) ||
                            !bool.TryParse(row.Cell(10).GetString(), out isActive) || !int.TryParse(row.Cell(11).GetString(), out ticket))
                        {
                            skippedUsers.Add($"Row {row.RowNumber()}: Invalid or missing data.");
                            continue;
                        }

                        // Check if user ID already exists in the database
                        if (await _context.Users.AnyAsync(u => u.UserId == userId))
                        {
                            skippedUsers.Add($"Row {row.RowNumber()}: User ID '{userId}' already exists.");
                            continue;
                        }

                        var user = new User
                        {
                            UserId = userId,
                            FirstName = firstName,
                            LastName = lastName,
                            Email = email,
                            UserType = userType,
                            Department = department,
                            School = school,
                            Rating = rating,
                            IsAdmin = isAdmin,
                            IsActive = isActive,
                            Ticket = ticket,
                            PasswordHash = passwordHash
                        };
                         
                        _context.Users.Add(user);
                    }

                    await _context.SaveChangesAsync();
                }
            }
            count = skippedUsers.Count();
            return Ok(new
            {
                message = "Users imported successfully. "+"Skipped Users: "+count,

                skippedUsers = skippedUsers.Count > 0 ? skippedUsers : null
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
    }
}
