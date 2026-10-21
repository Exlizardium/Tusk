using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Projects;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class ProjectEndpointTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        [Fact]
        public async Task GetProjects_IsVisibleToCustomers()
        {
            var admin = await _factory.AddUserAsync("Admin");
            await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await customer.Client.GetAsync("/api/projects");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetProject_Returns404_WhenProjectDoesNotExist()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await customer.Client.GetAsync("/api/projects/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("Customer")]
        [InlineData("Agent")]
        public async Task CreateProject_Returns403_ForNonAdmins(string role)
        {
            var user = await _factory.LoginAsNewUserAsync(role);

            var response = await user.Client.PostAsJsonAsync("/api/projects", new CreateProjectDto { Name = "Alpha" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Projects.AnyAsync()));
        }

        [Fact]
        public async Task CreateProject_Returns201_AttributedToCallingAdmin()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.PostAsJsonAsync("/api/projects", new CreateProjectDto { Name = "Alpha" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<ProjectDto>();
            Assert.Equal(admin.Id, created!.CreatedById);
        }

        [Theory]
        [InlineData("Customer")]
        [InlineData("Agent")]
        public async Task UpdateProject_Returns403_ForNonAdmins(string role)
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var user = await _factory.LoginAsNewUserAsync(role);

            var response = await user.Client.PutAsJsonAsync($"/api/projects/{project.Id}", new UpdateProjectDto { Name = "Hijacked", Version = 1 });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("Alpha", await _factory.QueryAsync(db => db.Projects.Select(p => p.Name).SingleAsync()));
        }

        [Fact]
        public async Task UpdateProject_Returns200_ForAnyAdmin_NotJustCreator()
        {
            var creator = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(creator.Id);
            var otherAdmin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await otherAdmin.Client.PutAsJsonAsync($"/api/projects/{project.Id}", new UpdateProjectDto { Name = "Beta", Version = 1 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Beta", await _factory.QueryAsync(db => db.Projects.Select(p => p.Name).SingleAsync()));
        }

        [Fact]
        public async Task UpdateProject_Returns404_WhenProjectDoesNotExist()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.PutAsJsonAsync("/api/projects/999", new UpdateProjectDto { Name = "Beta", Version = 1 });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("Customer")]
        [InlineData("Agent")]
        public async Task DeleteProject_Returns403_ForNonAdmins_AndKeepsTickets(string role)
        {
            var admin = await _factory.AddUserAsync("Admin");
            var otherCustomer = await _factory.AddUserAsync("Customer");
            var project = await _factory.SeedProjectAsync(admin.Id);
            await _factory.SeedTicketAsync(project.Id, otherCustomer.Id);
            var user = await _factory.LoginAsNewUserAsync(role);

            var response = await user.Client.DeleteAsync($"/api/projects/{project.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(await _factory.QueryAsync(db => db.Tickets.AnyAsync()));
        }

        [Fact]
        public async Task DeleteProject_Returns403_ForNonAdminWhoCreatedIt()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var otherCustomer = await _factory.AddUserAsync("Customer");
            var legacyProject = await _factory.SeedProjectAsync(customer.Id);
            await _factory.SeedTicketAsync(legacyProject.Id, otherCustomer.Id);

            var response = await customer.Client.DeleteAsync($"/api/projects/{legacyProject.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(await _factory.QueryAsync(db => db.Tickets.AnyAsync()));
        }

        [Fact]
        public async Task DeleteProject_Returns204_ForAdmin_AndCascadesToTickets()
        {
            var customer = await _factory.AddUserAsync("Customer");
            var creator = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(creator.Id);
            await _factory.SeedTicketAsync(project.Id, customer.Id);
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.DeleteAsync($"/api/projects/{project.Id}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Projects.AnyAsync()));
            Assert.False(await _factory.QueryAsync(db => db.Tickets.AnyAsync()));
        }

        [Fact]
        public async Task DeleteProject_Returns404_WhenProjectDoesNotExist()
        {
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.DeleteAsync("/api/projects/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
