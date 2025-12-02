using System.ComponentModel.DataAnnotations;

namespace Memora.DTOs
{
    public class CreateFlashcardSetRequest
    {
        [Required]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public string CreatedByUID { get; set; } = null!;

        [Required]
        public bool Visibility { get; set; } = false;
        
        // The user can send an optional list of Tag IDs
        public List<string>? TagIds { get; set; }

        // This property will hold the list of cards (Term, Definition, etc.)
        // that you send from your frontend.
        public List<CardDto> Cards { get; set; } = new List<CardDto>();
    }
}