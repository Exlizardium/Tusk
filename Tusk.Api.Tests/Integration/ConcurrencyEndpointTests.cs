using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Projects;
using Tusk.Api.Dtos.Tickets;
using Tusk.Api.Models.Enums;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class ConcurrencyEndpointTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        [Fact]
        public async Task UpdateTicketContent_Returns200_WithIncrementedVersion_WhenVersionIsCurrent()
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id, "Original");

            var response = await customer.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/content", new UpdateTicketContentDto { Title = "Edited", Version = 1 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, (await response.Content.ReadFromJsonAsync<TicketDto>())!.Version);
        }

        [Fact]
        public async Task UpdateTicketContent_Returns409_WithCurrentTicket_WhenVersionIsStale()
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id, "Original");
            var url = $"/api/projects/{project.Id}/tickets/{ticket.Id}";

            (await agent.Client.PutAsJsonAsync($"{url}/content", new UpdateTicketContentDto { Title = "Agent's title", Version = 1 }))
                .EnsureSuccessStatusCode();

            var response = await customer.Client.PutAsJsonAsync($"{url}/content", new UpdateTicketContentDto { Title = "Customer's title", Version = 1 });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var current = await response.Content.ReadFromJsonAsync<TicketDto>();
            Assert.Equal("Agent's title", current!.Title);
            Assert.Equal(2, current.Version);
            Assert.Equal("Agent's title", await _factory.QueryAsync(db => db.Tickets.Select(t => t.Title).SingleAsync()));
        }

        [Fact]
        public async Task ActionEndpoint_BumpsVersion_SoAPendingContentEditConflicts()
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);
            var url = $"/api/projects/{project.Id}/tickets/{ticket.Id}";

            var priorityChange = await agent.Client.PutAsJsonAsync($"{url}/priority", new UpdateTicketPriorityDto { Priority = TicketPriority.Critical });
            var edit = await customer.Client.PutAsJsonAsync($"{url}/content", new UpdateTicketContentDto { Title = "Edited", Version = 1 });

            Assert.Equal(2, (await priorityChange.Content.ReadFromJsonAsync<TicketDto>())!.Version);
            Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
            Assert.Equal(TicketPriority.Critical, (await edit.Content.ReadFromJsonAsync<TicketDto>())!.Priority);
        }

        [Fact]
        public async Task UpdateProject_Returns409_WithCurrentProject_WhenVersionIsStale()
        {
            var adminA = await _factory.LoginAsNewUserAsync("Admin");
            var adminB = await _factory.LoginAsNewUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(adminA.Id);

            (await adminA.Client.PutAsJsonAsync($"/api/projects/{project.Id}", new UpdateProjectDto { Name = "Renamed by A", Version = 1 }))
                .EnsureSuccessStatusCode();

            var response = await adminB.Client.PutAsJsonAsync($"/api/projects/{project.Id}", new UpdateProjectDto { Name = "Renamed by B", Version = 1 });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var current = await response.Content.ReadFromJsonAsync<ProjectDto>();
            Assert.Equal("Renamed by A", current!.Name);
            Assert.Equal(2, current.Version);
        }
    }
}
