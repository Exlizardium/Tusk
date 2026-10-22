using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Comments;
using Tusk.Api.Dtos.Common;
using Tusk.Api.Dtos.Tickets;
using Tusk.Api.Models;
using Tusk.Api.Models.Enums;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class UserNameEndpointTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        private async Task<Project> SeedProjectAsync()
        {
            var admin = await _factory.AddUserAsync("Admin");
            return await _factory.SeedProjectAsync(admin.Id);
        }

        private static string Url(Ticket ticket, string action = "") =>
            $"/api/projects/{ticket.ProjectId}/tickets/{ticket.Id}" + (action == "" ? "" : $"/{action}");

        private static async Task<TicketDto> ReadTicketAsync(HttpResponseMessage response)
        {
            Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}");
            return (await response.Content.ReadFromJsonAsync<TicketDto>())!;
        }

        private static async Task<CommentDto> ReadCommentAsync(HttpResponseMessage response)
        {
            Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}");
            return (await response.Content.ReadFromJsonAsync<CommentDto>())!;
        }

        [Fact]
        public async Task GetAll_ReturnsCustomerAndAssigneeNames()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            await _factory.SeedTicketAsync(project.Id, customer.Id, assignedToId: agent.Id);

            var result = await agent.Client.GetFromJsonAsync<PagedResultDto<TicketDto>>($"/api/projects/{project.Id}/tickets");

            var ticket = Assert.Single(result!.Items);
            Assert.Equal("Customer Test", ticket.CustomerName);
            Assert.Equal("Agent Test", ticket.AssignedToName);
            Assert.Null(ticket.ClosedByName);
        }

        [Fact]
        public async Task GetById_ReturnsAllNames_ForClosedAssignedTicket()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var agent = await _factory.AddUserAsync("Agent");
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var ticket = await _factory.SeedAsync(new Ticket
            {
                Title = "Closed",
                Status = TicketStatus.Closed,
                Priority = TicketPriority.Medium,
                CustomerId = customer.Id,
                AssignedToId = agent.Id,
                ProjectId = project.Id,
                CreatedAt = DateTime.UtcNow,
                ClosedAt = DateTime.UtcNow,
                ClosedById = admin.Id,
                CloseReason = TicketCloseReason.Fixed
            });

            var dto = await ReadTicketAsync(await admin.Client.GetAsync(Url(ticket)));

            Assert.Equal("Customer Test", dto.CustomerName);
            Assert.Equal("Agent Test", dto.AssignedToName);
            Assert.Equal("Admin Test", dto.ClosedByName);
        }

        [Fact]
        public async Task GetById_ReturnsNullAssigneeAndClosedByNames_ForOpenUnassignedTicket()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var dto = await ReadTicketAsync(await customer.Client.GetAsync(Url(ticket)));

            Assert.Equal("Customer Test", dto.CustomerName);
            Assert.Null(dto.AssignedToName);
            Assert.Null(dto.ClosedByName);
        }

        [Fact]
        public async Task Mine_ReturnsNames()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var agent = await _factory.AddUserAsync("Agent");
            await _factory.SeedTicketAsync(project.Id, customer.Id, assignedToId: agent.Id);

            var result = await customer.Client.GetFromJsonAsync<PagedResultDto<TicketDto>>("/api/tickets/mine");

            var ticket = Assert.Single(result!.Items);
            Assert.Equal("Customer Test", ticket.CustomerName);
            Assert.Equal("Agent Test", ticket.AssignedToName);
        }

        [Fact]
        public async Task Assigned_ReturnsNames()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            await _factory.SeedTicketAsync(project.Id, customer.Id, assignedToId: agent.Id);

            var result = await agent.Client.GetFromJsonAsync<PagedResultDto<TicketDto>>("/api/tickets/assigned");

            var ticket = Assert.Single(result!.Items);
            Assert.Equal("Customer Test", ticket.CustomerName);
            Assert.Equal("Agent Test", ticket.AssignedToName);
        }

        [Fact]
        public async Task Create_ReturnsCustomerName_AndNullAssigneeName()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");

            var dto = await ReadTicketAsync(await customer.Client.PostAsJsonAsync(
                $"/api/projects/{project.Id}/tickets", new CreateTicketDto { Title = "New", Priority = TicketPriority.Low }));

            Assert.Equal("Customer Test", dto.CustomerName);
            Assert.Null(dto.AssignedToName);
            Assert.Null(dto.ClosedByName);
        }

        [Fact]
        public async Task Assign_ReturnsAssigneeName()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var assignee = await _factory.AddUserAsync("Agent");
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var dto = await ReadTicketAsync(await admin.Client.PutAsJsonAsync(
                Url(ticket, "assign"), new AssignTicketDto { AssignedToId = assignee.Id }));

            Assert.Equal("Customer Test", dto.CustomerName);
            Assert.Equal("Agent Test", dto.AssignedToName);
        }

        [Fact]
        public async Task Assign_ReturnsNewAssigneeName_WhenReassigning()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var previous = await _factory.AddUserAsync("Agent");
            var admin = await _factory.LoginAsNewUserAsync("Admin");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id, assignedToId: previous.Id);

            var dto = await ReadTicketAsync(await admin.Client.PutAsJsonAsync(
                Url(ticket, "assign"), new AssignTicketDto { AssignedToId = admin.Id }));

            Assert.Equal("Admin Test", dto.AssignedToName);
        }

        [Fact]
        public async Task Take_ReturnsTakerName()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var dto = await ReadTicketAsync(await agent.Client.PutAsync(Url(ticket, "take"), null));

            Assert.Equal("Customer Test", dto.CustomerName);
            Assert.Equal("Agent Test", dto.AssignedToName);
        }

        [Fact]
        public async Task Unassign_ReturnsNullAssigneeName()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id, assignedToId: agent.Id);

            var dto = await ReadTicketAsync(await agent.Client.PutAsync(Url(ticket, "unassign"), null));

            Assert.Equal("Customer Test", dto.CustomerName);
            Assert.Null(dto.AssignedToId);
            Assert.Null(dto.AssignedToName);
        }

        [Fact]
        public async Task Close_ReturnsClosedByName()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var dto = await ReadTicketAsync(await agent.Client.PutAsJsonAsync(
                Url(ticket, "close"), new CloseTicketDto { Reason = TicketCloseReason.Fixed }));

            Assert.Equal("Customer Test", dto.CustomerName);
            Assert.Null(dto.AssignedToName);
            Assert.Equal("Agent Test", dto.ClosedByName);
        }

        [Fact]
        public async Task Reopen_ReturnsCustomerName_AndNullClosedByName()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var agent = await _factory.AddUserAsync("Agent");
            var ticket = await _factory.SeedAsync(new Ticket
            {
                Title = "Closed",
                Status = TicketStatus.Closed,
                Priority = TicketPriority.Medium,
                CustomerId = customer.Id,
                ProjectId = project.Id,
                CreatedAt = DateTime.UtcNow,
                ClosedAt = DateTime.UtcNow,
                ClosedById = agent.Id,
                CloseReason = TicketCloseReason.Fixed
            });

            var dto = await ReadTicketAsync(await customer.Client.PutAsync(Url(ticket, "reopen"), null));

            Assert.Equal("Customer Test", dto.CustomerName);
            Assert.Null(dto.AssignedToName);
            Assert.Null(dto.ClosedByName);
        }

        [Fact]
        public async Task Names_AreTrimmed_WhenLastNameIsEmpty()
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            await _factory.QueryAsync(db => db.Users
                .Where(u => u.Id == customer.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.FirstName, "Solo").SetProperty(u => u.LastName, "")));
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var dto = await ReadTicketAsync(await customer.Client.GetAsync(Url(ticket)));

            Assert.Equal("Solo", dto.CustomerName);
        }

        private async Task<Ticket> SeedTicketForCommentsAsync(string customerId)
        {
            var project = await SeedProjectAsync();
            return await _factory.SeedTicketAsync(project.Id, customerId);
        }

        private Task<Comment> SeedCommentAsync(int ticketId, string userId) =>
            _factory.SeedAsync(new Comment { TicketId = ticketId, UserId = userId, Message = "Hello", CreatedAt = DateTime.UtcNow });

        [Fact]
        public async Task GetComments_ReturnsUserNames()
        {
            var customer = await _factory.AddUserAsync("Customer");
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var ticket = await SeedTicketForCommentsAsync(customer.Id);
            await SeedCommentAsync(ticket.Id, customer.Id);
            await SeedCommentAsync(ticket.Id, agent.Id);

            var comments = await agent.Client.GetFromJsonAsync<List<CommentDto>>($"/api/tickets/{ticket.Id}/comments");

            Assert.Equal(["Customer Test", "Agent Test"], comments!.Select(c => c.UserName));
        }

        [Fact]
        public async Task GetComment_ReturnsUserName()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketForCommentsAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id);

            var dto = await ReadCommentAsync(await customer.Client.GetAsync($"/api/tickets/{ticket.Id}/comments/{comment.Id}"));

            Assert.Equal("Customer Test", dto.UserName);
        }

        [Fact]
        public async Task CreateComment_ReturnsUserName()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketForCommentsAsync(customer.Id);

            var response = await customer.Client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new CreateCommentDto { Message = "Hi" });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("Customer Test", (await ReadCommentAsync(response)).UserName);
        }

        [Fact]
        public async Task UpdateComment_ReturnsUserName()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketForCommentsAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id);

            var dto = await ReadCommentAsync(await customer.Client.PutAsJsonAsync(
                $"/api/tickets/{ticket.Id}/comments/{comment.Id}", new UpdateCommentDto { Message = "Edited" }));

            Assert.Equal("Customer Test", dto.UserName);
        }
    }
}
