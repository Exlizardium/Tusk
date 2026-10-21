using System.ComponentModel.DataAnnotations;

namespace Tusk.Api.Dtos.Projects
{
    public class UpdateProjectDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = null!;

        [MaxLength(250)]
        public string? Description { get; set; }

        [Required]
        public int? Version { get; set; }
    }
}
