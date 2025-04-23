namespace Library.Models.Dtos
{
    public class ChangePasswordDto
    {
        public string MatricNumber { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
}
