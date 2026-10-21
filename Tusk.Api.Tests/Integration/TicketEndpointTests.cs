using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Common;
using Tusk.Api.Dtos.Tickets;
using Tusk.Api.Models;
using Tusk.Api.Models.Enums;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class TicketEndpointTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        private async Task<Project> SeedProjectAsync()
        {
            var admin = await _factory.AddUserAsync("Admin");
            return await _factory.SeedProjectAsync(admin.Id);
        }

        private Task<Ticket> ReloadTicketAsync(int ticketId) =>
            _factory.QueryAsync(db => db.Tickets.SingleAsync(t => t.Id == ticketId));

        [Fact]
        public async Task GetAll_Returns401_WithoutToken()
        {
            var project = await SeedProjectAsync();

            var response = await _factory.CreateClient().GetAsync($"/api/projects/{project.Id}/tickets");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetAll_ReturnsOnlyOwnTickets_ForCustomer()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var otherCustomer = await _factory.AddUserAsync("Customer");
            await _factory.SeedTicketAsync(project.Id, customer.Id, "Mine");
            await _factory.SeedTicketAsync(project.Id, otherCustomer.Id, "Not mine");

            var result = await customer.Client.GetFromJsonAsync<PagedResultDto<TicketDto>>($"/api/projects/{project.Id}/tickets");

            Assert.Equal("Mine", Assert.Single(result!.Items).Title);
            Assert.Equal(1, result.TotalCount);
        }

        [Theory]
        [InlineData("Agent")]
        [InlineData("Admin")]
        public async Task GetAll_ReturnsAllTickets_ForAgentOrAdmin(string role)
        {
            var project = await SeedProjectAsync();
            var customerA = await _factory.AddUserAsync("Customer");
            var customerB = await _factory.AddUserAsync("Customer");
            await _factory.SeedTicketAsync(project.Id, customerA.Id);
            await _factory.SeedTicketAsync(project.Id, customerB.Id);
            var staff = await _factory.LoginAsNewUserAsync(role);

            var result = await staff.Client.GetFromJsonAsync<PagedResultDto<TicketDto>>($"/api/projects/{project.Id}/tickets");

            Assert.Equal(2, result!.TotalCount);
        }

        [Fact]
        public async Task GetById_Returns200_ForReportingCustomer()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var response = await customer.Client.GetAsync($"/api/projects/{project.Id}/tickets/{ticket.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetById_Returns403_ForOtherCustomer()
        {
            var project = await SeedProjectAsync();
            var owner = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, owner.Id);
            var otherCustomer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await otherCustomer.Client.GetAsync($"/api/projects/{project.Id}/tickets/{ticket.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetById_Returns200_ForAgent()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.GetAsync($"/api/projects/{project.Id}/tickets/{ticket.Id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetById_Returns404_WhenTicketDoesNotExist()
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.GetAsync($"/api/projects/{project.Id}/tickets/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_Returns201_AttributedToCallingCustomer()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await customer.Client.PostAsJsonAsync(
                $"/api/projects/{project.Id}/tickets", new CreateTicketDto { Title = "Broken", Priority = TicketPriority.High });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<TicketDto>();
            Assert.Equal(customer.Id, created!.CustomerId);
            Assert.Equal(TicketStatus.Open, created.Status);
            Assert.Equal($"/api/projects/{project.Id}/tickets/{created.Id}", response.Headers.Location!.AbsolutePath);
        }

        [Fact]
        public async Task Create_Returns404_WhenProjectDoesNotExist()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await customer.Client.PostAsJsonAsync("/api/projects/999/tickets", new CreateTicketDto { Title = "Broken", Priority = TicketPriority.High });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Tickets.AnyAsync()));
        }

        [Fact]
        public async Task UpdateContent_Returns200_ForReportingCustomer()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id, "Original");

            var response = await customer.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/content", new UpdateTicketContentDto { Title = "Edited", Version = 1 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Edited", (await ReloadTicketAsync(ticket.Id)).Title);
        }

        [Fact]
        public async Task UpdateContent_Returns403_ForOtherCustomer_AndChangesNothing()
        {
            var project = await SeedProjectAsync();
            var owner = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, owner.Id, "Original");
            var otherCustomer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await otherCustomer.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/content", new UpdateTicketContentDto { Title = "Hijacked", Version = 1 });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("Original", (await ReloadTicketAsync(ticket.Id)).Title);
        }

        [Fact]
        public async Task UpdateContent_Returns200_ForAgent()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id, "Original");
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/content", new UpdateTicketContentDto { Title = "Clarified", Version = 1 });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Clarified", (await ReloadTicketAsync(ticket.Id)).Title);
        }

        [Theory]
        [InlineData("status")]
        [InlineData("priority")]
        [InlineData("assign")]
        [InlineData("take")]
        [InlineData("unassign")]
        public async Task StaffOnlyActions_Return403_EvenForReportingCustomer(string action)
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.AddUserAsync("Agent");
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id, assignedToId: agent.Id);
            var url = $"/api/projects/{project.Id}/tickets/{ticket.Id}/{action}";

            var response = action switch
            {
                "status" => await customer.Client.PutAsJsonAsync(url, new UpdateTicketStatusDto { Status = TicketStatus.Closed }),
                "priority" => await customer.Client.PutAsJsonAsync(url, new UpdateTicketPriorityDto { Priority = TicketPriority.Critical }),
                "assign" => await customer.Client.PutAsJsonAsync(url, new AssignTicketDto { AssignedToId = customer.Id }),
                _ => await customer.Client.PutAsync(url, null)
            };

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal(TicketStatus.Open, saved.Status);
            Assert.Equal(TicketPriority.Medium, saved.Priority);
            Assert.Equal(agent.Id, saved.AssignedToId);
        }

        [Fact]
        public async Task UpdateStatus_Returns200_ForAgent()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/status", new UpdateTicketStatusDto { Status = TicketStatus.InProgress });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(TicketStatus.InProgress, (await ReloadTicketAsync(ticket.Id)).Status);
        }

        [Fact]
        public async Task Take_AssignsCallingAgent()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PutAsync($"/api/projects/{project.Id}/tickets/{ticket.Id}/take", null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(agent.Id, (await ReloadTicketAsync(ticket.Id)).AssignedToId);
        }

        [Theory]
        [InlineData("Agent")]
        [InlineData("Admin")]
        public async Task Assign_Returns200_WhenAssigneeIsAgentOrAdmin(string assigneeRole)
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);
            var assignee = await _factory.AddUserAsync(assigneeRole);
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/assign", new AssignTicketDto { AssignedToId = assignee.Id });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(assignee.Id, (await ReloadTicketAsync(ticket.Id)).AssignedToId);
        }

        [Fact]
        public async Task Assign_Returns400_WhenAssigneeIsCustomer()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/assign", new AssignTicketDto { AssignedToId = customer.Id });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null((await ReloadTicketAsync(ticket.Id)).AssignedToId);
        }

        [Fact]
        public async Task Assign_Returns400_WhenAssigneeDoesNotExist()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/{ticket.Id}/assign", new AssignTicketDto { AssignedToId = "no-such-user" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null((await ReloadTicketAsync(ticket.Id)).AssignedToId);
        }

        [Fact]
        public async Task Assign_Returns404_WhenTicketDoesNotExist()
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/999/assign", new AssignTicketDto { AssignedToId = agent.Id });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Mine_ReturnsOnlyTicketsReportedByCaller()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var otherCustomer = await _factory.AddUserAsync("Customer");
            await _factory.SeedTicketAsync(project.Id, customer.Id, "Mine");
            await _factory.SeedTicketAsync(project.Id, otherCustomer.Id, "Not mine");

            var result = await customer.Client.GetFromJsonAsync<PagedResultDto<TicketDto>>("/api/tickets/mine");

            Assert.Equal("Mine", Assert.Single(result!.Items).Title);
        }

        [Fact]
        public async Task Assigned_Returns403_ForCustomer()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await customer.Client.GetAsync("/api/tickets/assigned");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Assigned_ReturnsOnlyTicketsAssignedToCallingAgent()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var otherAgent = await _factory.AddUserAsync("Agent");
            await _factory.SeedTicketAsync(project.Id, customer.Id, "Mine", assignedToId: agent.Id);
            await _factory.SeedTicketAsync(project.Id, customer.Id, "Theirs", assignedToId: otherAgent.Id);

            var result = await agent.Client.GetFromJsonAsync<PagedResultDto<TicketDto>>("/api/tickets/assigned");

            Assert.Equal("Mine", Assert.Single(result!.Items).Title);
        }
    }
}
