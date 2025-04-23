using System.ComponentModel.DataAnnotations;

namespace Library.Models.Dtos
{
    public class UpdateBookDto
    {
        [Required(ErrorMessage = "Name is required.")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Author is required.")]
        public string Author { get; set; }

        public int Year { get; set; }

        public string Description { get; set; }

        [Required(ErrorMessage = "Quantity is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }
    }
}
