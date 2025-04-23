namespace Library.Models
{
    
        public class PendingReturn
        {
            public int Id { get; set; } // Unique ID for the request
            public string UserId { get; set; } // Student matric number
            public string SerialNumber { get; set; } // Book serial number
            public string ReturnCode { get; set; } // Random alphanumeric code
            public DateTime RequestTime { get; set; } // When request was made
            public bool IsApproved { get; set; } // True if admin approves
        }

    
}
