using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Library.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } // MatricNumber for students, StaffID for lecturers

        [Required]
        public string FirstName { get; set; }

        [Required]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string UserType { get; set; } // "Student", "Lecturer", or "Admin"
        public bool IsActive { get; set; }=true;
        public bool IsAdmin { get; set; } = false;
        public bool IsEmailVerified { get; set; } = false;
        public bool IsLoggedIn { get; set; } = false;// Only applies to Lecturers who are Admins

        public string Department { get; set; }
        public string School { get; set; }
        [Range(1.0, 10.0)]
        public double Rating { get; set; } = 5.0; // Only for students, nullable for lecturers
        public int? Ticket { get; set; }
        public List<BorrowRecord> BorrowRecords { get; set; }

        [Required]
        public string PasswordHash { get; set; } // Store hashed password

        public string GetRatingCategory()
        {
            if (Rating >= 10) return "Excellent";
            if (Rating >= 8) return "Very Good";
            if (Rating >= 6) return "Good";
            if (Rating >= 4) return "Fair";
            return "Bad";
        }

        public int GetBorrowLimit()
        {
            return (int)Math.Floor(Rating);
        }
    }
}
