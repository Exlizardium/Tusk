namespace Tusk.Api.Dtos.Projects
{
    public class ProjectDto
    {
        public int Id { get; set; }
        public int Version { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedById { get; set; }
    }
}
