using System;

namespace Library.Models
{
    public class UserLoginHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string UserType { get; set; }
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; } // Nullable for logout tracking
        public DateTime SessionExpiry { get; set; } // Expiry time for login session
    }
}
