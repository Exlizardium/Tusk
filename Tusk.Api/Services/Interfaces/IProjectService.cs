using Tusk.Api.Dtos.Projects;

namespace Tusk.Api.Services.Interfaces
{
    public interface IProjectService
    { 
        Task<ProjectDto?> GetProjectByIdAsync(int id);
        Task<List<ProjectDto>> GetAllProjectsAsync();

        Task<ProjectDto> CreateProjectAsync(CreateProjectDto dto, string userId);
        Task<ProjectDto?> UpdateProjectAsync(int id, UpdateProjectDto dto);
        Task<bool> DeleteProjectAsync(int id);
    }
}
