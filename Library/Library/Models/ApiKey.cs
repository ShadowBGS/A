namespace Library.Models
{
    public class ApiKey
    {
        public int Id { get; set; }
        public string Key { get; set; } = Guid.NewGuid().ToString(); // Auto-generate API key
        public string Email { get; set; } // Who owns the key
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }

}
