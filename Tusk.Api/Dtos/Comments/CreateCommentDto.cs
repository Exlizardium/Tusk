using System.ComponentModel.DataAnnotations;
using Tusk.Api.Models;

namespace Tusk.Api.Dtos.Comments
{
    public class CreateCommentDto
    {
        [Required]
        [MaxLength(4000)]
        public string Message { get; set; } = null!;
    }
}
