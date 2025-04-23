namespace Library.Models
{
    public class LateReturn
    {
        public int Id { get; set; }
        public string UserId { get; set; } // Track user
        public int LateReturnCount { get; set; } = 0; // Count of late returns
    }
}
