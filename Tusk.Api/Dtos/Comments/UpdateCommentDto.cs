using System.ComponentModel.DataAnnotations;

namespace Tusk.Api.Dtos.Comments
{
    public class UpdateCommentDto
    {
        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = null!;
    }
}
