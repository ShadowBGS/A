using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Library.Models.Dtos
{
    public class RegisterStudentDto
    {
        [Required]
        [RegularExpression(@"^\d{2}/\d{4}$", ErrorMessage = "Matric number must be in the format XX/XXXX (e.g., 22/2464).")]
        [Column(TypeName = "nvarchar(10)")] // Ensure correct DB storage
        public string MatricNumber { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        [Required]
        [EmailAddress]
        
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@student\.babcock\.edu\.ng$",
        ErrorMessage = "Email must be a valid student email (@student.babcock.edu.ng).")]
        public string Email { get; set; }
        public string School { get; set; }
        public string Department { get; set; }
        
        //[Required]
        //[Range(100, 600, ErrorMessage = "Level must be between 100 and 600.")]
        //public int Level { get; set; }
    }
}
