using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Projects;
using Tusk.Api.Services.Implementations;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Services
{
    public sealed class ProjectServiceTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task GetProjectByIdAsync_ReturnsProject_WhenItExists()
        {
            var user = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", user.Id);

            await using var context = _db.CreateContext();
            var result = await new ProjectService(context).GetProjectByIdAsync(project.Id);

            Assert.NotNull(result);
            Assert.Equal("Alpha", result.Name);
            Assert.Equal(user.Id, result.CreatedById);
        }

        [Fact]
        public async Task GetProjectByIdAsync_ReturnsNull_WhenProjectDoesNotExist()
        {
            await using var context = _db.CreateContext();
            var result = await new ProjectService(context).GetProjectByIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllProjectsAsync_ReturnsRequestedPage_NewestFirst()
        {
            var user = await _db.AddUserAsync();
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 1; i <= 5; i++)
            {
                await _db.AddProjectAsync($"Project {i}", user.Id, start.AddDays(i));
            }

            await using var context = _db.CreateContext();
            var result = await new ProjectService(context)
                .GetAllProjectsAsync(new ProjectQueryParams { Page = 2, PageSize = 2 });

            Assert.Equal(["Project 3", "Project 2"], result.Items.Select(p => p.Name));
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
        }

        [Theory]
        [InlineData(0, 0, 1, 1)]
        [InlineData(-3, 500, 1, 100)]
        public async Task GetAllProjectsAsync_ClampsPageAndPageSize(int page, int pageSize, int expectedPage, int expectedPageSize)
        {
            await using var context = _db.CreateContext();
            var result = await new ProjectService(context)
                .GetAllProjectsAsync(new ProjectQueryParams { Page = page, PageSize = pageSize });

            Assert.Equal(expectedPage, result.Page);
            Assert.Equal(expectedPageSize, result.PageSize);
        }

        [Fact]
        public async Task CreateProjectAsync_PersistsProject_AttributedToCaller()
        {
            var user = await _db.AddUserAsync();

            await using (var context = _db.CreateContext())
            {
                await new ProjectService(context).CreateProjectAsync(
                    new CreateProjectDto { Name = "Alpha", Description = "First" }, user.Id);
            }

            await using var verify = _db.CreateContext();
            var saved = await verify.Projects.SingleAsync();
            Assert.Equal("Alpha", saved.Name);
            Assert.Equal("First", saved.Description);
            Assert.Equal(user.Id, saved.CreatedById);
        }

        [Fact]
        public async Task CreateProjectAsync_Throws_WhenNameAlreadyExists()
        {
            var user = await _db.AddUserAsync();
            await _db.AddProjectAsync("Alpha", user.Id);

            await using var context = _db.CreateContext();
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                new ProjectService(context).CreateProjectAsync(new CreateProjectDto { Name = "Alpha" }, user.Id));
        }

        [Fact]
        public async Task UpdateProjectAsync_PersistsChanges()
        {
            var user = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", user.Id);

            await using (var context = _db.CreateContext())
            {
                await new ProjectService(context).UpdateProjectAsync(
                    project.Id, new UpdateProjectDto { Name = "Beta", Description = "Renamed", Version = 1 });
            }

            await using var verify = _db.CreateContext();
            var saved = await verify.Projects.SingleAsync();
            Assert.Equal("Beta", saved.Name);
            Assert.Equal("Renamed", saved.Description);
        }

        [Fact]
        public async Task UpdateProjectAsync_Throws_AndChangesNothing_WhenClientVersionIsStale()
        {
            var user = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", user.Id);

            await using (var context = _db.CreateContext())
            {
                (await context.Projects.SingleAsync()).Description = "Edited by someone else";
                await context.SaveChangesAsync();
            }

            await using (var context = _db.CreateContext())
            {
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new ProjectService(context).UpdateProjectAsync(
                    project.Id, new UpdateProjectDto { Name = "Stale edit", Version = 1 }));
            }

            await using var verify = _db.CreateContext();
            var saved = await verify.Projects.SingleAsync();
            Assert.Equal("Alpha", saved.Name);
            Assert.Equal(2, saved.Version);
        }

        [Fact]
        public async Task UpdateProjectAsync_ReturnsNull_WhenProjectDoesNotExist()
        {
            await using var context = _db.CreateContext();
            var result = await new ProjectService(context)
                .UpdateProjectAsync(999, new UpdateProjectDto { Name = "Beta", Version = 1 });

            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteProjectAsync_RemovesProject_AndReturnsTrue()
        {
            var user = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", user.Id);

            await using (var context = _db.CreateContext())
            {
                Assert.True(await new ProjectService(context).DeleteProjectAsync(project.Id));
            }

            await using var verify = _db.CreateContext();
            Assert.False(await verify.Projects.AnyAsync());
        }

        [Fact]
        public async Task DeleteProjectAsync_ReturnsFalse_WhenProjectDoesNotExist()
        {
            await using var context = _db.CreateContext();
            Assert.False(await new ProjectService(context).DeleteProjectAsync(999));
        }
    }
}
