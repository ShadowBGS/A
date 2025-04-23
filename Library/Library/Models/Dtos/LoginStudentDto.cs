using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Library.Models.Dtos
{
    public class LoginStudentDto
    {
        [Required]
        [RegularExpression(@"^\d{2}/\d{4}$", ErrorMessage = "Matric number must be in the format XX/XXXX (e.g., 22/2464).")]
        [Column(TypeName = "nvarchar(10)")] // Ensure correct DB storage
        public string MatricNumber { get; set; }
        public string Password { get; set; }
    }
}
