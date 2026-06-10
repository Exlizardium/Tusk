using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;
using Tusk.Api.Dtos.Projects;
using Tusk.Api.Models;
using Tusk.Api.Services.Interfaces;

namespace Tusk.Api.Services.Implementations
{
    public class ProjectService : IProjectService
    {
        private readonly AppDbContext _context;

        public ProjectService(AppDbContext context)
        {
            _context = context;
        }

        private async Task<Project?> LocateProjectAsync(int projectId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return null;
            return project;
        }

        private static ProjectDto MapToDto(Project project)
        {
            return new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                CreatedAt = project.CreatedAt,
                CreatedById = project.CreatedById
            };
        }

        public async Task<ProjectDto?> GetProjectByIdAsync(int projectId)
        {
            var project = await LocateProjectAsync(projectId);
            return MapToDto(project);
        }

        public async Task<List<ProjectDto>> GetAllProjectsAsync()
        {
            var projects = await _context.Projects.ToListAsync();
            return projects.Select(MapToDto).ToList();
        }

        public async Task<ProjectDto> CreateProjectAsync(CreateProjectDto dto, string userId)
        {
            var project = new Project
            {
                Name = dto.Name,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId
            };
            _context.Projects.Add(project);
            await _context.SaveChangesAsync();
            return MapToDto(project);
        }

        public async Task<ProjectDto?> UpdateProjectAsync(int projectId, UpdateProjectDto dto)
        {
            var project = await LocateProjectAsync(projectId);

            project.Name = dto.Name;
            project.Description = dto.Description;

            await _context.SaveChangesAsync();
            return MapToDto(project);
        }

        public async Task<bool> DeleteProjectAsync(int projectId)
        {
            var project = await LocateProjectAsync(projectId);
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
