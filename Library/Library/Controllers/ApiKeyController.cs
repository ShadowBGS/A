using Library.Data;
using Microsoft.AspNetCore.Mvc;
using Library.Models;
using Microsoft.EntityFrameworkCore;

namespace Library.Controllers
{
    [ApiKeyAuthorize("0ca45e40-1a28-48ac-aeba-514dfbeb96f8")]
    [Route("api/apikeys")]
    [ApiController]
    public class ApiKeysController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ApiKeysController(AppDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> GetApiKeys()
        {
            var apiKeys = await _context.ApiKeys.Select(u => new
            {
                u.CreatedAt,
                u.Email,
                u.IsActive
            }).ToListAsync();
            return Ok(apiKeys);
        }
        [HttpGet("Email")]
        public async Task<IActionResult> GetApiKeyByEmail([FromQuery] string email)
        {
            var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(b => b.Email == email);
            if (apiKey == null)
            {
                return NotFound("No apikey found for " + email);
            }
            return Ok(new { apiKey.Email, apiKey.IsActive,apiKey.CreatedAt });
        }
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateApiKey([FromQuery] string email)
        {
            var apiKey = new ApiKey { Email = email };
            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();

            return Ok(new { ApiKey = apiKey.Key });
        }
        [HttpPost("deactivate")]
        public async Task<IActionResult> DeactivateApiKey([FromQuery] string email)
        {
            var apikey = await _context.ApiKeys.FirstOrDefaultAsync(b => b.Email == email);
            if (apikey == null)
            {
                return NotFound("No apikey found for " + email);
            }
            apikey.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok("Successfully Deactivated the  apikey of "+ email);
        }
        [HttpPost("activate")]
        public async Task<IActionResult> activateApiKey([FromQuery] string email)
        {
            var apikey = await _context.ApiKeys.FirstOrDefaultAsync(b => b.Email == email);
            if (apikey==null)
            {
                return NotFound("No apikey found for " + email);
            }
            apikey.IsActive = true;
            await _context.SaveChangesAsync();

            return Ok("Successfully Activated the  apikey of " + email);
        }
    }

}
