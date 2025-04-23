using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Library.Data;
using Library.Models;
using Library.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using static System.Reflection.Metadata.BlobBuilder;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Library.Controllers
{
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BooksController> _logger;
        private readonly string _imageFolderPath;
        private readonly string _PDFFolderPath;
        public BooksController(AppDbContext context, ILogger<BooksController> logger)
        {
            _logger = logger;
            _context = context;
            _imageFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            _PDFFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Book");

            if (!Directory.Exists(_imageFolderPath))
            {
                Directory.CreateDirectory(_imageFolderPath);
            }
            if (!Directory.Exists(_PDFFolderPath))
            {
                Directory.CreateDirectory(_PDFFolderPath);
            }

            _logger = logger;
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<Book>>> GetAllBooks()
        {
            var books = await _context.Books.ToListAsync();
            _logger.LogInformation("Fetched all books from the database.");
            return books;
        }

        // GET: api/Books/books
        [HttpGet("books")]
        public async Task<IActionResult> GetBooks(
            [FromQuery] string? search = null,
            [FromQuery] string? sort = "Id",
            [FromQuery] string? order = "asc",
            [FromQuery] string? filter = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation("Fetching books with search: {Search}, sort: {Sort}, order: {Order}, filter: {Filter}, page: {PageNumber}, pageSize: {PageSize}",
                search, sort, order, filter, pageNumber, pageSize);

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var books = _context.Books.AsQueryable();

            // 🔹 Search by Name or Serial Number
            if (!string.IsNullOrEmpty(search))
            {
                books = books.Where(b => b.Name.Contains(search) || b.SerialNumber.Contains(search));
            }

            // 🔹 Filtering Logic
            if (!string.IsNullOrEmpty(filter))
            {
                switch (filter.ToLower())
                {
                    case "available":
                        books = books.Where(b => b.Quantity > 0 || b.PDFPath.Length>1);
                        break;
                    case "unavailable":
                        books = books.Where(b => b.Quantity == 0 || b.PDFPath == null);
                        break;
                }
            }

            // 🔹 Sorting Logic
            var validColumns = new HashSet<string> { "Id", "Name", "Author", "SerialNumber", "Quantity" };
            if (validColumns.Contains(sort))
            {
                books = order.ToLower() == "asc"
                    ? books.OrderBy(b => EF.Property<object>(b, sort))
                    : books.OrderByDescending(b => EF.Property<object>(b, sort));
            }
            else
            {
                books = books.OrderBy(b => b.Id);
            }

            // 🔹 Pagination
            var totalRecords = await books.CountAsync();
            var pagedBooks = await books
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new
            {
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = pagedBooks
            };

            _logger.LogInformation("Fetched {TotalRecords} books, page {PageNumber} of {TotalPages}.", totalRecords, pageNumber, (int)Math.Ceiling(totalRecords / (double)pageSize));
            return Ok(response);
        }

        // GET: api/Books/{serialNumber} (Get a book by Serial Number)
        [HttpGet("{serialNumber}")]
        public async Task<IActionResult> GetBook(string serialNumber)
        {
            _logger.LogInformation("Fetching book with serial number: {SerialNumber}", serialNumber);

            var book = await _context.Books
                .Where(b => b.SerialNumber == serialNumber)
                .Select(b => new
                {
                    b.SerialNumber,
                    b.Name,
                    b.Author,
                    b.Year,
                    b.Description,
                    b.Quantity,
                    b.PDFPath,
                    Image = $"{Request.Scheme}://{Request.Host}/uploads/{b.ImagePath}".Replace("//", "/") // FIXED DOUBLE SLASH ISSUE
                })
                .FirstOrDefaultAsync();

            if (book == null)
            {
                _logger.LogWarning("Book with serial number {SerialNumber} not found.", serialNumber);
                return NotFound(new { message = "Book not found" });
            }

            _logger.LogInformation("Fetched book details for serial number: {SerialNumber}", serialNumber);
            return Ok(book);
        }

        // PATCH: api/Books/upload-image
        //bbbbbbb
        [HttpPatch("upload-image")]
        public async Task<IActionResult> UploadBookImage([FromQuery] string serialNumber, IFormFile file)
        {
            if (string.IsNullOrEmpty(serialNumber))
            {
                _logger.LogWarning("Serial Number is required for image upload.");
                return BadRequest(new { message = "Serial Number is required." });
            }

            var book = await _context.Books.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber);
            if (book == null)
            {
                _logger.LogWarning("Book with serial number {SerialNumber} not found for image upload.", serialNumber);
                return NotFound(new { message = "Book not found." });
            }

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No image uploaded for book with serial number {SerialNumber}.", serialNumber);
                return BadRequest(new { message = "No image uploaded." });
            }

            try
            {
                var filePath = await SaveAndResizeImage(file);
                book.ImagePath = filePath ?? string.Empty;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Image uploaded successfully for book with serial number {SerialNumber}.", serialNumber);
                return Ok(new { message = "Image uploaded successfully.", imagePath = book.ImagePath });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while uploading image for book with serial number {SerialNumber}.", serialNumber);
                return StatusCode(500, new { message = "An error occurred while saving the image.", error = ex.Message });
            }
        }

        // POST: api/Books/request-borrow/{serialNumber}/{UserId}
        [HttpPost("request-borrow/{serialNumber}/{UserId}")]
        public async Task<IActionResult> RequestBorrowCode(string serialNumber, string UserId)
        {
            UserId = Uri.UnescapeDataString(UserId);

            var student = await _context.Users.FirstOrDefaultAsync(s => s.UserId == UserId);
            if (student == null)
            {
                _logger.LogWarning("Student with UserId {UserId} not found for borrow request.", UserId);
                return NotFound(new { message = "Student not found." });
            }

            var book = await _context.Books.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber);
            if (book == null)
            {
                _logger.LogWarning("Book with serial number {SerialNumber} not found for borrow request.", serialNumber);
                return NotFound(new { message = "Book not found." });
            }

            if (book.Quantity <= 0)
            {
                _logger.LogWarning("Book with serial number {SerialNumber} is out of stock.", serialNumber);
                return BadRequest(new { message = "Book is out of stock." });
            }

            int borrowLimit = student.GetBorrowLimit();
            int currentlyBorrowed = await _context.BorrowRecords
                .CountAsync(b => b.UserId == UserId && !b.IsReturned);

            if (currentlyBorrowed >= borrowLimit)
            {
                _logger.LogWarning("User {UserId} has reached their borrow limit. Current borrow limit: {BorrowLimit}.", UserId, borrowLimit);
                return BadRequest(new { message = $"Borrow limit reached. You can only borrow {borrowLimit} books at a time." });
            }

            var existingBorrow = await _context.BorrowRecords
                   .FirstOrDefaultAsync(b => b.UserId == UserId && b.SerialNumber == serialNumber && !b.IsReturned);

            if (existingBorrow != null)
            {
                _logger.LogWarning("User {UserId} has already borrowed the book with serial number {SerialNumber}.", UserId, serialNumber);
                return BadRequest(new { message = "You have already borrowed this book." });
            }

            var borrowCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            var pendingBorrow = new PendingBorrow
            {
                UserId = UserId,
                SerialNumber = serialNumber,
                BorrowCode = borrowCode,
                RequestTime = DateTime.UtcNow,
                IsApproved = false
            };

            _context.PendingBorrows.Add(pendingBorrow);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Borrow request submitted for UserId {UserId} and book {SerialNumber}. Awaiting admin approval.", UserId, serialNumber);
            return Ok(new { message = "Borrow request submitted. Await admin approval.", borrowCode });
        }

        [HttpGet("pending-borrows")]
        public async Task<IActionResult> GetPendingBorrows()
        {
            var currentTime = DateTime.UtcNow;
            var expiryDuration = TimeSpan.FromHours(0.1);

            // Log fetching pending borrows
            _logger.LogInformation("Fetching pending borrow requests at {Time}", currentTime);

            var pendingBorrows = await _context.PendingBorrows
                .Where(pb => !pb.IsApproved)
                .Select(pb => new
                {
                    pb.Id,
                    pb.UserId,
                    pb.SerialNumber,
                    pb.BorrowCode,
                    pb.RequestTime,
                    ExpiryTime = pb.RequestTime.Add(expiryDuration)
                })
                .ToListAsync();

            var expiredRequests = pendingBorrows
                .Where(pb => pb.ExpiryTime <= currentTime)
                .ToList();

            if (expiredRequests.Any())
            {
                _logger.LogInformation("{ExpiredCount} borrow requests have expired.", expiredRequests.Count);
                var expiredIds = expiredRequests.Select(pb => pb.Id).ToList();
                var expiredEntities = await _context.PendingBorrows
                    .Where(pb => expiredIds.Contains(pb.Id))
                    .ToListAsync();

                _context.PendingBorrows.RemoveRange(expiredEntities);
                await _context.SaveChangesAsync();
                _logger.LogInformation("{ExpiredCount} expired borrow requests removed from database.", expiredRequests.Count);
            }

            return Ok(pendingBorrows);
        }

        [HttpPost("approve-borrow/{borrowCode}")]
        public async Task<IActionResult> ApproveBorrowRequest(string borrowCode)
        {
            _logger.LogInformation("Attempting to approve borrow request with BorrowCode: {BorrowCode}.", borrowCode);

            var pendingBorrow = await _context.PendingBorrows
                .FirstOrDefaultAsync(pb => pb.BorrowCode == borrowCode && !pb.IsApproved);

            if (pendingBorrow == null)
            {
                _logger.LogWarning("Borrow request with code {BorrowCode} not found or already approved.", borrowCode);
                return NotFound(new { message = "Borrow request not found or already approved." });
            }

            pendingBorrow.IsApproved = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Borrow request with code {BorrowCode} approved.", borrowCode);
            return Ok(new { message = "Borrow request approved.", borrowCode });
        }

        [HttpPost("borrow/{serialNumber}/{UserId}/{borrowCode}")]
        public async Task<IActionResult> BorrowBook(string serialNumber, string UserId, string borrowCode)
        {
            _logger.LogInformation("Attempting to borrow book with SerialNumber: {SerialNumber} for User: {UserId}.", serialNumber, UserId);

            UserId = Uri.UnescapeDataString(UserId);
            var student = await _context.Users.FirstOrDefaultAsync(s => s.UserId == UserId);

            if (student == null)
            {
                _logger.LogWarning("User with UserId {UserId} not found.", UserId);
                return NotFound(new { message = "Student not found." });
            }

            var UserType = student.UserType;
            var department = student.Department;
            var school = student.School;
            var book = await _context.Books.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber);

            if (book == null)
            {
                _logger.LogWarning("Book with SerialNumber {SerialNumber} not found.", serialNumber);
                return NotFound(new { message = "Book not found." });
            }

            var pendingBorrow = await _context.PendingBorrows
                .FirstOrDefaultAsync(pb => pb.UserId == UserId && pb.SerialNumber == serialNumber && pb.BorrowCode == borrowCode);

            if (pendingBorrow == null || !pendingBorrow.IsApproved)
            {
                _logger.LogWarning("Invalid or unapproved borrow code: {BorrowCode}.", borrowCode);
                return BadRequest(new { message = "Invalid or unapproved borrow code." });
            }

            // Expiration check
            if ((DateTime.UtcNow - pendingBorrow.RequestTime).TotalHours > 0.1)
            {
                _logger.LogInformation("Borrow code {BorrowCode} has expired, removing request.", borrowCode);
                _context.PendingBorrows.Remove(pendingBorrow);
                await _context.SaveChangesAsync();
                return BadRequest(new { message = "Borrow code has expired. Request a new one." });
            }

            if (book.Quantity <= 0)
            {
                _logger.LogWarning("Book with SerialNumber {SerialNumber} is out of stock.", serialNumber);
                return BadRequest(new { message = "Book is out of stock." });
            }

            // Check if student has reached their borrow limit
            int borrowLimit = student.GetBorrowLimit();
            int currentlyBorrowed = await _context.BorrowRecords
                .CountAsync(b => b.UserId == UserId && !b.IsReturned && !b.IsOnline);

            if (currentlyBorrowed >= borrowLimit)
            {
                _logger.LogWarning("User with UserId {UserId} has reached their borrow limit.", UserId);
                return BadRequest(new { message = $"Borrow limit reached. You can only borrow {borrowLimit} books at a time." });
            }

            // Check if the user already borrowed this book
            var existingBorrow = await _context.BorrowRecords
                .FirstOrDefaultAsync(b => b.UserId == UserId && b.SerialNumber == serialNumber && !b.IsReturned);

            if (existingBorrow != null)
            {
                _logger.LogWarning("User with UserId {UserId} has already borrowed book with SerialNumber {SerialNumber}.", UserId, serialNumber);
                return BadRequest(new { message = "You have already borrowed this book." });
            }

            // Allowed borrow time
            float allowedBorrowHours = student.UserType == "Lecturer" ? 96 : 48;
            DateTime dueDate = DateTime.UtcNow.AddHours(allowedBorrowHours);

            // Create borrow record
            var borrowRecord = new BorrowRecord
            {
                Department = department,
                School = school,
                UserId = UserId,
                UserType = UserType,
                SerialNumber = serialNumber,
                BorrowTime = DateTime.UtcNow,
                AllowedBorrowHours = allowedBorrowHours,
                DueDate = dueDate,
                IsReturned = false,
            };

            book.Quantity -= 1; // Reduce book quantity
            _context.BorrowRecords.Add(borrowRecord);

            // Remove the borrow request from PendingBorrows
            _context.PendingBorrows.Remove(pendingBorrow);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User with UserId {UserId} successfully borrowed book with SerialNumber {SerialNumber}.", UserId, serialNumber);

            return Ok(new
            {
                message = "Book borrowed successfully.",
                borrowTime = borrowRecord.BorrowTime,
                allowedBorrowHours,
                dueDate,
                borrowedBooks = _context.BorrowRecords.Count(b => b.UserId == UserId && !b.IsReturned),
                borrowLimit = borrowLimit
            });
        }

        [HttpPost("borrowOnline/{serialNumber}/{userId}")]
        public async Task<IActionResult> BorrowBookOnline(string serialNumber, string userId)
        {
            _logger.LogInformation("Attempting to borrow book online with SerialNumber: {SerialNumber} for User: {UserId}.", serialNumber, userId);

            userId = Uri.UnescapeDataString(userId);
            var student = await _context.Users.FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null)
            {
                _logger.LogWarning("User with UserId {UserId} not found.", userId);
                return NotFound(new { message = "Student not found." });
            }

            var UserType = student.UserType;
            var department = student.Department;
            var school = student.School;
            var book = await _context.Books.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber);

            if (book == null)
            {
                _logger.LogWarning("Book with SerialNumber {SerialNumber} not found.", serialNumber);
                return NotFound(new { message = "Book not found." });
            }

            // Check if the user already borrowed this book
            var existingBorrow = await _context.BorrowRecords
                .FirstOrDefaultAsync(b => b.UserId == userId && b.SerialNumber == serialNumber && !b.IsReturned && b.IsOnline);

            if (existingBorrow != null)
            {
                _logger.LogWarning("User with UserId {UserId} has already borrowed book online with SerialNumber {SerialNumber}.", userId, serialNumber);
                return BadRequest(new { message = "You have already borrowed this book." });
            }

            // Allowed borrow time for online borrow (1 hour)
            float allowedBorrowHours = student.UserType == "Lecturer" ? 1 : 1;
            DateTime dueDate = DateTime.UtcNow.AddHours(allowedBorrowHours);

            // Create borrow record
            var borrowRecord = new BorrowRecord
            {
                Department = department,
                School = school,
                UserId = userId,
                UserType = UserType,
                SerialNumber = serialNumber,
                BorrowTime = DateTime.UtcNow,
                AllowedBorrowHours = allowedBorrowHours,
                DueDate = dueDate,
                IsReturned = false,
                IsOnline = true
            };

            _context.BorrowRecords.Add(borrowRecord);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User with UserId {UserId} successfully borrowed book online with SerialNumber {SerialNumber}.", userId, serialNumber);

            return Ok(new
            {
                message = "Book borrowed successfully.",
                borrowTime = borrowRecord.BorrowTime,
                allowedBorrowHours,
                dueDate,
            });
        }
        [HttpGet("books-by-borrow-history")]
        public async Task<IActionResult> GetBooksByBorrowHistory(
    [FromQuery] string? search = null,
    [FromQuery] string? sort = "Id",
    [FromQuery] string? order = "asc",
    [FromQuery] string? filter = null,
    [FromQuery] bool? IsOnline = null,
    [FromQuery] string? UserId = null,
    [FromQuery] string? UserType = null,
    [FromQuery] string? SerialNumber = null,
    [FromQuery] bool? overdue = null,
    [FromQuery] bool? IsReturned = null,
    [FromQuery] DateTime? startDate = null,
    [FromQuery] DateTime? endDate = null,
    [FromQuery] string? Department = null,
    [FromQuery] string? School = null,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10)
        {
            _logger.LogInformation("Fetching books via borrow history filters...");

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            // Base borrow history query
            var borrowQuery = _context.BorrowRecords.AsQueryable();

            // Apply filters on BorrowHistory
            if (!string.IsNullOrEmpty(UserId)) borrowQuery = borrowQuery.Where(b => b.UserId == UserId);
            if (!string.IsNullOrEmpty(UserType)) borrowQuery = borrowQuery.Where(b => b.UserType == UserType);
            if (!string.IsNullOrEmpty(SerialNumber)) borrowQuery = borrowQuery.Where(b => b.SerialNumber == SerialNumber);
            if (IsReturned.HasValue) borrowQuery = borrowQuery.Where(b => b.IsReturned == IsReturned);
            if (IsOnline.HasValue) borrowQuery = borrowQuery.Where(b => b.IsOnline == IsOnline);
            if (overdue.HasValue) borrowQuery = borrowQuery.Where(b => b.Overdue == overdue);
            if (startDate.HasValue) borrowQuery = borrowQuery.Where(b => b.BorrowTime >= startDate.Value);
            if (endDate.HasValue) borrowQuery = borrowQuery.Where(b => b.BorrowTime <= endDate.Value);
            if (!string.IsNullOrEmpty(Department)) borrowQuery = borrowQuery.Where(b => b.Department == Department);
            if (!string.IsNullOrEmpty(School)) borrowQuery = borrowQuery.Where(b => b.School == School);

            // Extract serial numbers from borrow history
            var borrowedSerials = await borrowQuery
                .Select(b => b.SerialNumber)
                .Distinct()
                .ToListAsync();

            // Base book query using those serial numbers
            var books = _context.Books.AsQueryable();
            books = books.Where(b => borrowedSerials.Contains(b.SerialNumber));

            // Apply book-level filters
            if (!string.IsNullOrEmpty(search))
            {
                books = books.Where(b => b.Name.Contains(search) || b.SerialNumber.Contains(search));
            }

            if (!string.IsNullOrEmpty(filter))
            {
                switch (filter.ToLower())
                {
                    case "available":
                        books = books.Where(b => b.Quantity > 0 || !string.IsNullOrEmpty(b.PDFPath));
                        break;
                    case "unavailable":
                        books = books.Where(b => b.Quantity == 0 || string.IsNullOrEmpty(b.PDFPath));
                        break;
                }
            }

            //if (IsOnline.HasValue)
            //{
            //    books = IsOnline.Value
            //        ? books.Where(b => !string.IsNullOrEmpty(b.PDFPath))
            //        : books.Where(b => string.IsNullOrEmpty(b.PDFPath));
            //}

            // Sorting
            var validSorts = new[] { "Id", "Name", "Author", "SerialNumber", "Quantity" };
            if (validSorts.Contains(sort))
            {
                books = order.ToLower() == "desc"
                    ? books.OrderByDescending(b => EF.Property<object>(b, sort))
                    : books.OrderBy(b => EF.Property<object>(b, sort));
            }
            else
            {
                books = books.OrderBy(b => b.Id);
            }

            // Pagination
            var totalRecords = await books.CountAsync();
            var pagedBooks = await books
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new
            {
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                Data = pagedBooks
            };

            return Ok(result);
        }


        [HttpPost("request-return/{serialNumber}/{UserId}")]
        public async Task<IActionResult> RequestReturnCode(string serialNumber, string UserId)
        {
            UserId = Uri.UnescapeDataString(UserId);
            var borrowRecord = await _context.BorrowRecords
                .FirstOrDefaultAsync(b => b.UserId == UserId && b.SerialNumber == serialNumber && !b.IsReturned);

            if (borrowRecord == null)
            {
                _logger.LogWarning($"No active borrow record found for UserId: {UserId}, SerialNumber: {serialNumber}");
                return BadRequest(new { message = "No active borrow record found." });
            }

            // Generate a random alphanumeric return code
            string returnCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            // Save in PendingReturns table
            var pendingReturn = new PendingReturn
            {
                UserId = UserId,
                SerialNumber = serialNumber,
                ReturnCode = returnCode,
                RequestTime = DateTime.UtcNow,
                IsApproved = false
            };

            _context.PendingReturns.Add(pendingReturn);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Return request submitted for UserId: {UserId}, SerialNumber: {serialNumber}, ReturnCode: {returnCode}");
            return Ok(new { message = "Return request submitted. Await admin approval.", returnCode });
        }

        [HttpGet("pending-returns")]
        public async Task<IActionResult> GetPendingReturns()
        {
            var currentTime = DateTime.UtcNow;
            var expiryDuration = TimeSpan.FromHours(0.1);

            // Get pending returns including expired ones
            var pendingReturns = await _context.PendingReturns
                .Where(pr => !pr.IsApproved)
                .Select(pr => new
                {
                    pr.Id,
                    pr.UserId,
                    pr.SerialNumber,
                    pr.ReturnCode,
                    pr.RequestTime,
                    ExpiryTime = pr.RequestTime.Add(expiryDuration) // Show expiration time
                })
                .ToListAsync();

            // Find expired return requests
            var expiredRequests = pendingReturns
                .Where(pr => pr.ExpiryTime <= currentTime)
                .ToList();

            // Remove expired return requests from the database
            if (expiredRequests.Any())
            {
                var expiredIds = expiredRequests.Select(pr => pr.Id).ToList();
                var expiredEntities = await _context.PendingReturns
                    .Where(pr => expiredIds.Contains(pr.Id))
                    .ToListAsync();

                _context.PendingReturns.RemoveRange(expiredEntities);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Expired return requests removed. Total expired: {expiredRequests.Count}");
            }

            return Ok(pendingReturns);
        }

        [HttpPost("approve-return/{returnCode}")]
        public async Task<IActionResult> ApproveReturn(string returnCode)
        {
            var pendingReturn = await _context.PendingReturns
                .FirstOrDefaultAsync(pb => pb.ReturnCode == returnCode && !pb.IsApproved);

            if (pendingReturn == null)
            {
                _logger.LogWarning($"Return request not found or already approved. ReturnCode: {returnCode}");
                return NotFound(new { message = "Return request not found or already approved." });
            }

            pendingReturn.IsApproved = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Return request approved for ReturnCode: {returnCode}");
            return Ok(new { message = "Return request approved.", returnCode });
        }

        [HttpPost("return/{serialNumber}/{UserId}/{returnCode}")]
        public async Task<IActionResult> ReturnBook(string serialNumber, string UserId, string returnCode)
        {
            UserId = Uri.UnescapeDataString(UserId);
            var book = await _context.Books.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber);
            if (book == null)
            {
                _logger.LogWarning($"Book not found for SerialNumber: {serialNumber}");
                return NotFound(new { message = "Book not found." });
            }

            var borrowRecord = await _context.BorrowRecords
                .FirstOrDefaultAsync(b => b.UserId == UserId && b.SerialNumber == serialNumber && !b.IsReturned && !b.IsOnline);

            if (borrowRecord == null)
            {
                _logger.LogWarning($"No active borrow record found for UserId: {UserId}, SerialNumber: {serialNumber}");
                return BadRequest(new { message = "No active borrow record found." });
            }

            // Check if return code is valid
            var pendingReturn = await _context.PendingReturns
                .FirstOrDefaultAsync(pr => pr.UserId == UserId && pr.SerialNumber == serialNumber && pr.ReturnCode == returnCode);

            if (pendingReturn == null || !pendingReturn.IsApproved)
            {
                _logger.LogWarning($"Invalid or unapproved return code for ReturnCode: {returnCode}");
                return BadRequest(new { message = "Invalid or unapproved return code." });
            }

            // Expiration check (e.g., 24 hours)
            if ((DateTime.UtcNow - pendingReturn.RequestTime).TotalHours > 0.1)
            {
                _context.PendingReturns.Remove(pendingReturn);
                await _context.SaveChangesAsync();
                _logger.LogWarning($"Return code expired for UserId: {UserId}, SerialNumber: {serialNumber}");
                return BadRequest(new { message = "Return code has expired. Request a new one." });
            }

            DateTime returnTime = DateTime.UtcNow;
            bool isLate = returnTime > borrowRecord.DueDate;

            borrowRecord.IsReturned = true;
            borrowRecord.ReturnTime = returnTime;
            book.Quantity += 1;

            var student = await _context.Users.FirstOrDefaultAsync(s => s.UserId == UserId);
            if (student != null)
            {
                student.Rating = isLate ? Math.Max(1.0, student.Rating - 0.25) : Math.Min(10.0, student.Rating + 0.15);
            }

            // Remove the used return request
            _context.PendingReturns.Remove(pendingReturn);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Book returned successfully for UserId: {UserId}, SerialNumber: {serialNumber}, ReturnTime: {returnTime}, IsLate: {isLate}");
            return Ok(new { message = "Book returned successfully.", returnTime, isLate, newRating = student?.Rating });
        }

        [HttpGet("image/{serialNumber}")]
        public IActionResult GetBookImage(string serialNumber)
        {
            var book = _context.Books.FirstOrDefault(b => b.SerialNumber == serialNumber);
            if (book == null || string.IsNullOrEmpty(book.ImagePath))
            {
                _logger.LogWarning($"Book or image not found for SerialNumber: {serialNumber}");
                return NotFound(new { message = "Book or image not found" });
            }

            // Construct the full image path correctly
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            var imagePath = Path.Combine(uploadsFolder, Path.GetFileName(book.ImagePath));

            if (!System.IO.File.Exists(imagePath))
            {
                _logger.LogWarning($"Image file does not exist for path: {imagePath}");
                return NotFound(new { message = "Image file does not exist", path = imagePath });
            }

            var imageFileStream = System.IO.File.OpenRead(imagePath);
            return File(imageFileStream, "image/png"); // Ensure the correct MIME type
        }

        [HttpGet("PDF/{serialNumber}")]
        public IActionResult GetBookPDF(string serialNumber)
        {
            var book = _context.Books.FirstOrDefault(b => b.SerialNumber == serialNumber);
            if (book == null || string.IsNullOrEmpty(book.PDFPath))
            {
                _logger.LogWarning($"Book or PDF not found for SerialNumber: {serialNumber}");
                return NotFound(new { message = "Book or PDF not found" });
            }

            // Build full path to the file
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Book");
            var PDFPath = Path.Combine(uploadsFolder, Path.GetFileName(book.PDFPath));

            // Check if the file actually exists
            if (!System.IO.File.Exists(PDFPath))
            {
                _logger.LogWarning($"PDF file does not exist for path: {PDFPath}");
                return NotFound(new { message = "PDF file does not exist", path = PDFPath });
            }

            var fileStream = System.IO.File.OpenRead(PDFPath);
            var fileName = Path.GetFileName(PDFPath);

            return File(fileStream, "application/pdf", fileName);
        }


        [HttpGet("PDFReader/{serialNumber}/{userId}")]
        public IActionResult GetBookPDF(string serialNumber, string userId)
        {
            userId = Uri.UnescapeDataString(userId);
            try
            {
                // Log the start of the method
                _logger.LogInformation($"Attempting to retrieve PDF for book {serialNumber} by user {userId}");

                var book = _context.Books.FirstOrDefault(b => b.SerialNumber == serialNumber);
                if (book == null || string.IsNullOrEmpty(book.PDFPath))
                {
                    _logger.LogWarning($"Book with serial number {serialNumber} not found or PDF not available.");
                    return NotFound(new { message = "Book or PDF not found" });
                }

                var borrowRecord = _context.BorrowRecords
                    .FirstOrDefault(br => br.UserId == userId && br.SerialNumber == serialNumber && br.IsOnline == true && !br.IsReturned);

                if (borrowRecord == null)
                {
                    _logger.LogWarning($"No active borrow record found for user {userId} and book {serialNumber}.");
                    return BadRequest(new { message = "No active borrow record found, or the book has been returned" });
                }

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Book");
                var PDFPath = Path.Combine(uploadsFolder, Path.GetFileName(book.PDFPath));

                if (!System.IO.File.Exists(PDFPath))
                {
                    _logger.LogWarning($"PDF file does not exist at path: {PDFPath}");
                    return NotFound(new { message = "PDF file does not exist", path = PDFPath });
                }

                var fileStream = System.IO.File.OpenRead(PDFPath);
                var fileName = book.Name;

                _logger.LogInformation($"Successfully retrieved PDF for book {serialNumber} by user {userId}");

                return File(fileStream, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "An error occurred while retrieving the PDF file.");
                return StatusCode(500, new { message = "An error occurred while processing your request", error = ex.Message });
            }
        }

        // POST: api/Books (Create Book with Image)
        [HttpPost]
        public async Task<ActionResult<Book>> CreateBook([FromForm] CreateBookDto createBookDto, IFormFile file, IFormFile? PDF)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid.");
                return BadRequest(ModelState);
            }

            if (file == null)
            {
                _logger.LogWarning("An image is required.");
                return BadRequest(new { message = "An image is required." });
            }

            if (await _context.Books.AnyAsync(b => b.SerialNumber == createBookDto.SerialNumber))
            {
                _logger.LogWarning($"Book with serial number {createBookDto.SerialNumber} already exists.");
                return BadRequest(new { message = "Serial Number must be unique." });
            }

            var book = new Book
            {
                SerialNumber = createBookDto.SerialNumber,
                Name = createBookDto.Name,
                Author = createBookDto.Author,
                Year = createBookDto.Year,
                Description = createBookDto.Description,
                Quantity = createBookDto.Quantity,
                ImagePath = await SaveAndResizeImage(file),
                PDFPath = await SavePDF(PDF)
            };

            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully created book with serial number {book.SerialNumber}");

            return CreatedAtAction(nameof(GetBook), new { serialNumber = book.SerialNumber }, book);
        }

        // PUT: api/Books/{serialNumber} (Update Book Details)
        [HttpPut("{serialNumber}")]
        public async Task<IActionResult> UpdateBook(string serialNumber, [FromForm] UpdateBookDto updatedBook, IFormFile file)
        {
            var book = await _context.Books.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber);
            if (book == null)
            {
                _logger.LogWarning($"Book with serial number {serialNumber} not found.");
                return NotFound(new { message = "Book not found." });
            }

            book.Name = updatedBook.Name;
            book.Author = updatedBook.Author;
            book.Year = updatedBook.Year;
            book.Description = updatedBook.Description;
            book.Quantity = updatedBook.Quantity;

            if (file != null)
            {
                if (!string.IsNullOrEmpty(book.ImagePath) && book.ImagePath != "/images/default-book-cover.jpg")
                {
                    var oldImagePath = Path.Combine(_imageFolderPath, Path.GetFileName(book.ImagePath));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                book.ImagePath = await SaveAndResizeImage(file);
            }

            _context.Entry(book).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully updated book with serial number {serialNumber}");

            return NoContent();
        }

        [HttpGet("borrow-history")]
        public async Task<IActionResult> GetAllBorrowHistory(
            [FromQuery] string? UserId,
            [FromQuery] string? UserType,
            [FromQuery] string? serialnumber,
            [FromQuery] bool? overdue,
            [FromQuery] bool? IsReturned,
            [FromQuery] bool? IsOnline,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? Department,
            [FromQuery] string? School,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            if (!string.IsNullOrEmpty(UserId))
            {
                UserId = Uri.UnescapeDataString(UserId);
            }

            var query = _context.BorrowRecords.AsQueryable();

            if (startDate.HasValue) query = query.Where(b => b.BorrowTime >= startDate.Value);
            if (!string.IsNullOrEmpty(Department)) query = query.Where(b => b.Department.Contains(Department));
            if (!string.IsNullOrEmpty(UserType)) query = query.Where(b => b.UserType.Contains(UserType));
            if (!string.IsNullOrEmpty(serialnumber)) query = query.Where(b => b.SerialNumber.Contains(serialnumber));
            if (!string.IsNullOrEmpty(School)) query = query.Where(b => b.School.Contains(School));
            if (!string.IsNullOrEmpty(UserId)) query = query.Where(b => b.UserId.Contains(UserId));
            if (endDate.HasValue) query = query.Where(b => b.BorrowTime <= endDate.Value);
            if (overdue.HasValue) query = query.Where(b => b.Overdue == overdue.Value);
            if (IsReturned.HasValue) query = query.Where(b => b.IsReturned == IsReturned.Value);
            if (IsOnline.HasValue) query = query.Where(b => b.IsOnline == IsOnline.Value);

            int totalRecords = await query.CountAsync();
            int totalBooks = await _context.Books.CountAsync();
            var totalDistinctBooks = await query.Select(b => b.SerialNumber).Distinct().CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            int currentlyBorrowed = await _context.BorrowRecords
                .CountAsync(b => b.UserId == UserId && !b.IsReturned);
            var totalBorrowed = await query.CountAsync();
            var totalReturned = await query.CountAsync(b => b.IsReturned);
            var totalOvedue = await query.CountAsync(b => b.Overdue);
            var totalLate = await query.CountAsync(b => b.IsReturned && b.ReturnTime > b.DueDate);
            var totalNotReturned = await query.CountAsync(b => !b.IsReturned);

            var borrowHistory = await query.OrderByDescending(b => b.BorrowTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    b.Department,
                    b.School,
                    b.UserId,
                    b.UserType,
                    b.IsLateReturn,
                    b.IsReturned,
                    b.SerialNumber,
                    b.BorrowTime,
                    b.AllowedBorrowHours,
                    b.DueDate,
                    b.ReturnTime,
                    b.Overdue
                })
                .ToListAsync();

            _logger.LogInformation($"Fetched borrow history with {borrowHistory.Count} records for page {pageNumber}");

            return Ok(new
            {  
                TotalBooks=totalBooks,
                TotalDistinctBooks=totalDistinctBooks,
                TotalOvedue=totalOvedue,
                currentlyborrowed = currentlyBorrowed,
                TotalBorrowed = totalBorrowed,
                TotalReturned = totalReturned,
                TotalLate = totalLate,
                TotalNotReturned = totalNotReturned,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                PageSize = pageSize,
                CurrentPage = pageNumber,
                BorrowHistory = borrowHistory
            });
        }
        [HttpGet("borrowed-daily")]
        public async Task<IActionResult> GetDailyBorrowStats()
        {
            var today = DateTime.UtcNow.Date;
            var sevenDaysAgo = today.AddDays(-6);

            var stats = await _context.BorrowRecords
                .Where(b => b.BorrowTime.Date >= sevenDaysAgo)
                .GroupBy(b => b.BorrowTime.Date)
                .Select(g => new {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Ok(stats);
        }


        [HttpGet("borrow-history/{UserId}")]
        public async Task<IActionResult> GetBorrowHistory(string UserId, [FromQuery] bool? overdue, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string? Department)
        {
            UserId = Uri.UnescapeDataString(UserId);
            _logger.LogInformation($"Fetching borrow history for UserId: {UserId}");

            var query = _context.BorrowRecords
                .Where(b => b.UserId == UserId)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(b => b.BorrowTime >= startDate.Value);
                _logger.LogInformation($"Filtering borrow records from {startDate.Value}");
            }
            if (!string.IsNullOrEmpty(Department))
            {
                query = query.Where(b => b.Department == Department);
                _logger.LogInformation($"Filtering borrow records for Department: {Department}");
            }

            if (endDate.HasValue)
            {
                query = query.Where(b => b.BorrowTime <= endDate.Value);
                _logger.LogInformation($"Filtering borrow records until {endDate.Value}");
            }

            // Compute the statistics
            int currentlyBorrowed = await _context.BorrowRecords
                .CountAsync(b => b.UserId == UserId && !b.IsReturned);
            var totalBorrowed = await query.CountAsync();
            var totalReturned = await query.CountAsync(b => b.IsReturned);
            var totalLate = await query.CountAsync(b => b.IsReturned && b.ReturnTime > b.DueDate);
            var totalNotReturned = await query.CountAsync(b => !b.IsReturned);

            _logger.LogInformation($"Currently Borrowed: {currentlyBorrowed}, Total Borrowed: {totalBorrowed}, Total Returned: {totalReturned}, Total Late: {totalLate}, Total Not Returned: {totalNotReturned}");

            var borrowHistory = await query.OrderByDescending(b => b.BorrowTime)
                 .Select(b => new
                 {
                     b.IsReturned,
                     b.SerialNumber,
                     b.BorrowTime,
                     b.AllowedBorrowHours,
                     b.DueDate,
                     b.ReturnTime,
                     b.Overdue,
                     b.IsLateReturn
                 })
                .ToListAsync();

            return Ok(new
            {
                currentlyBorrowed = currentlyBorrowed,
                TotalBorrowed = totalBorrowed,
                TotalReturned = totalReturned,
                TotalLate = totalLate,
                TotalNotReturned = totalNotReturned,
                BorrowHistory = borrowHistory
            });
        }

        [HttpPatch("{serialNumber}")]
        public async Task<IActionResult> PatchBook(string serialNumber, [FromBody] JsonPatchDocument<Book> patchDoc)
        {
            if (patchDoc == null || patchDoc.Operations.Count == 0)
            {
                return BadRequest(new { message = "Invalid or empty patch document." });
            }

            _logger.LogInformation($"Patch request for Book with Serial Number: {serialNumber}");

            var book = await _context.Books.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber);
            if (book == null)
            {
                return NotFound(new { message = "Book not found." });
            }

            // Validate operations before applying them
            var validPaths = new HashSet<string> { "/name", "/author", "/year", "/description", "/quantity" };
            foreach (var operation in patchDoc.Operations)
            {
                if (operation.op.ToLower() != "replace")
                {
                    return BadRequest(new { message = $"Unsupported operation '{operation.op}'. Only 'replace' is allowed." });
                }

                if (!validPaths.Contains(operation.path.ToLower()))
                {
                    return BadRequest(new { message = $"Invalid field '{operation.path}'. You can only update: Name, Author, Year, Description, Quantity." });
                }
            }

            patchDoc.ApplyTo(book, ModelState);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                _context.Entry(book).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Book with Serial Number: {serialNumber} updated successfully.");
                return Ok(new { message = "Book updated successfully.", book });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error occurred while updating book with Serial Number: {serialNumber}. Error: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while updating the book.", error = ex.Message });
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportBooks()
        {
            _logger.LogInformation("Exporting books to Excel...");

            var books = await _context.Books.ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Books");
                worksheet.Cell(1, 1).Value = "Serial Number";
                worksheet.Cell(1, 2).Value = "Name";
                worksheet.Cell(1, 3).Value = "Author";
                worksheet.Cell(1, 4).Value = "Year";
                worksheet.Cell(1, 5).Value = "Quantity";
                worksheet.Cell(1, 6).Value = "Image Path";
                worksheet.Cell(1, 7).Value = "Pdf Path";
                worksheet.Cell(1, 8).Value = "Description";

                int row = 2;
                foreach (var book in books)
                {
                    worksheet.Cell(row, 1).Value = book.SerialNumber;
                    worksheet.Cell(row, 2).Value = book.Name;
                    worksheet.Cell(row, 3).Value = book.Author;
                    worksheet.Cell(row, 4).Value = book.Year;
                    worksheet.Cell(row, 5).Value = book.Quantity;
                    worksheet.Cell(row, 6).Value = book.ImagePath ?? "N/A";
                    worksheet.Cell(row, 7).Value = book.PDFPath ?? "N/A";
                    worksheet.Cell(row, 8).Value = book.Description;
                    row++;
                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    _logger.LogInformation("Books exported successfully.");
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Books.xlsx");
                }
            }
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportBooks(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            _logger.LogInformation($"Importing books from file: {file.FileName}");

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var workbook = new XLWorkbook(stream))
                {
                    var worksheet = workbook.Worksheet(1);
                    var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header row

                    var books = new List<Book>();
                    var skippedBooks = new List<string>();

                    foreach (var row in rows)
                    {
                        string serialNumber = row.Cell(1).GetString().Trim();
                        string name = row.Cell(2).GetString().Trim();
                        string author = row.Cell(3).GetString().Trim();
                        int year;
                        int quantity;
                        
                        string image_path = row.Cell(6).GetString().Trim();
                        string pdf_path = row.Cell(7).GetString().Trim();
                        string description = row.Cell(8).GetString().Trim();
                        

                        if (string.IsNullOrEmpty(serialNumber) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(author) || string.IsNullOrEmpty(description) || string.IsNullOrEmpty(image_path) || string.IsNullOrEmpty(pdf_path) ||
                            !int.TryParse(row.Cell(4).GetString(), out year) || !int.TryParse(row.Cell(5).GetString(), out quantity))
                        {
                            skippedBooks.Add($"Row {row.RowNumber()}: Invalid or missing data.");
                            continue;
                        }

                        if (await _context.Books.AnyAsync(b => b.SerialNumber == serialNumber))
                        {
                            skippedBooks.Add($"Row {row.RowNumber()}: Serial Number '{serialNumber}' already exists.");
                            continue;
                        }

                        var book = new Book
                        {
                            SerialNumber = serialNumber,
                            Name = name,
                            Author = author,
                            Year = year,
                            Quantity = quantity,
                            Description = description,
                            ImagePath = image_path,
                            PDFPath=pdf_path
                        };

                        books.Add(book);
                    }

                    if (books.Count > 0)
                    {
                        _context.Books.AddRange(books);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"{books.Count} books imported successfully.");
                    }

                    return Ok(new
                    {
                        message = $"{books.Count} books imported successfully.",
                        skippedBooks = skippedBooks.Count > 0 ? skippedBooks : null
                    });
                }
            }
        }

        [HttpDelete("{serialNumber}")]
        public async Task<IActionResult> DeleteBook(string serialNumber)
        {
            _logger.LogInformation($"Deleting book with Serial Number: {serialNumber}");

            var book = await _context.Books.FirstOrDefaultAsync(b => b.SerialNumber == serialNumber);
            if (book == null)
            {
                return NotFound(new { message = "Book not found." });
            }

            var imagePath = Path.Combine(_imageFolderPath, Path.GetFileName(book.ImagePath));
            if (System.IO.File.Exists(imagePath))
            {
                _logger.LogInformation($"Deleting image for book with Serial Number: {serialNumber}");
                System.IO.File.Delete(imagePath);
            }

            var PDFPath = Path.Combine(_imageFolderPath, Path.GetFileName(book.PDFPath));
            if (System.IO.File.Exists(PDFPath))
            {
                _logger.LogInformation($"Deleting PDF for book with Serial Number: {serialNumber}");
                System.IO.File.Delete(PDFPath);
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Book with Serial Number: {serialNumber} deleted successfully.");

            return NoContent();
        }

        private async Task<string> SaveAndResizeImage(IFormFile file)
        {
            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(_imageFolderPath, uniqueFileName);

            using (var stream = file.OpenReadStream())
            using (var image = await Image.LoadAsync(stream))
            {
                int newWidth = 300;
                image.Mutate(x => x.Resize(newWidth, 0));
                await image.SaveAsync(filePath, new JpegEncoder());
            }

            return "/images/" + uniqueFileName;
        }
        private async Task<string> SavePDF(IFormFile file)
        {
            // Ensure it's a PDF
            if (file == null || Path.GetExtension(file.FileName).ToLower() != ".pdf")
                throw new InvalidOperationException("Invalid file type. Only PDFs are allowed.");

            // Generate unique name and path
            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(_PDFFolderPath, uniqueFileName);

            // Save to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return the relative URL or path
            return "/pdfs/" + uniqueFileName; // Adjust based on how your static files are served
        }

    }

}
   