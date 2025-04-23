namespace Library.Models
{
    public class PendingBorrow
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string SerialNumber { get; set; }
        public string BorrowCode { get; set; }
        public DateTime RequestTime { get; set; }
        public bool IsApproved { get; set; }
    }

}
