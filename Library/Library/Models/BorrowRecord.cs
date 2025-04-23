using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Library.Models
{
    public class BorrowRecord
    {
        public int Id { get; set; }
        public string UserId { get; set; } // Links to User (MatricNumber or Staff ID)
        public string Department { get; set; }

        //public int Level { get; set; }
        public string School { get; set; }
        public string UserType { get; set; } // "Student", "Lecturer", or "Admin"
        public string SerialNumber { get; set; } // Book Serial Number
        public DateTime DueDate { get; set; }
        public DateTime BorrowTime { get; set; } = DateTime.UtcNow;
        public double AllowedBorrowHours { get; set; } // Different for Students vs Lecturers
        //public DateTime DueDate => BorrowTime.AddHours(AllowedBorrowHours);
        public DateTime? ReturnTime { get; set; }
        public bool IsReturned { get; set; } = false;
        public bool IsOnline { get; set; } = false;
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public bool Overdue { get; private set; }= false;
        public bool overdue()
        {
            return !IsReturned && DateTime.UtcNow > DueDate;
        }

        public bool IsLateReturn => ReturnTime.HasValue && ReturnTime.Value > DueDate;
        public bool IsEarlyReturn => ReturnTime.HasValue && ReturnTime.Value < DueDate;
    }
}
