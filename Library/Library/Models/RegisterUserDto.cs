using System.ComponentModel.DataAnnotations;

namespace Library.Models
{
    public class RegisterUserDto
    {
        [Required]
        public string UserId { get; set; } // MatricNumber for students, StaffID for lecturers

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string UserType { get; set; } // "Student", "Lecturer", or "Admin"

        public bool IsAdmin { get; set; } = false; // Only applies to Lecturers who are Admins

        public string Department { get; set; }
        public string School { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; } // Plain password before hashing
    }
}
