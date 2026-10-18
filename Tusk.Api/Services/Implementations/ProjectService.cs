using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;
using Tusk.Api.Dtos.Common;
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

            if (project == null) return null;

            return MapToDto(project);
        }

        public async Task<PagedResultDto<ProjectDto>> GetAllProjectsAsync(ProjectQueryParams query)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            var totalCount = await _context.Projects.CountAsync();

            var projects = await _context.Projects
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResultDto<ProjectDto>
            {
                Items = projects.Select(MapToDto).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
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

            if (project == null) return null;

            project.Name = dto.Name;
            project.Description = dto.Description;

            await _context.SaveChangesAsync();
            return MapToDto(project);
        }

        public async Task<bool> DeleteProjectAsync(int projectId)
        {
            var project = await LocateProjectAsync(projectId);

            if (project == null) return false;

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
