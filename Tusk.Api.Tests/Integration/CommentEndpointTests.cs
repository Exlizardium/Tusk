using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Comments;
using Tusk.Api.Models;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class CommentEndpointTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        private async Task<Ticket> SeedTicketAsync(string customerId)
        {
            var admin = await _factory.AddUserAsync("Admin");
            var project = await _factory.SeedProjectAsync(admin.Id);
            return await _factory.SeedTicketAsync(project.Id, customerId);
        }

        private Task<Comment> SeedCommentAsync(int ticketId, string userId, string message) =>
            _factory.SeedAsync(new Comment { TicketId = ticketId, UserId = userId, Message = message, CreatedAt = DateTime.UtcNow });

        [Fact]
        public async Task GetComments_Returns401_WithoutToken()
        {
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);

            var response = await _factory.CreateClient().GetAsync($"/api/tickets/{ticket.Id}/comments");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetComments_ReturnsThread_ForReportingCustomer()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            await SeedCommentAsync(ticket.Id, customer.Id, "Question");

            var comments = await customer.Client.GetFromJsonAsync<List<CommentDto>>($"/api/tickets/{ticket.Id}/comments");

            Assert.Equal("Question", Assert.Single(comments!).Message);
        }

        [Fact]
        public async Task GetComments_Returns403_ForOtherCustomer()
        {
            var owner = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(owner.Id);
            await SeedCommentAsync(ticket.Id, owner.Id, "Private details");
            var otherCustomer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await otherCustomer.Client.GetAsync($"/api/tickets/{ticket.Id}/comments");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Theory]
        [InlineData("Agent")]
        [InlineData("Admin")]
        public async Task GetComments_ReturnsThread_ForAgentOrAdmin(string role)
        {
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            await SeedCommentAsync(ticket.Id, customer.Id, "Question");
            var staff = await _factory.LoginAsNewUserAsync(role);

            var comments = await staff.Client.GetFromJsonAsync<List<CommentDto>>($"/api/tickets/{ticket.Id}/comments");

            Assert.Single(comments!);
        }

        [Fact]
        public async Task GetComments_Returns404_WhenTicketDoesNotExist()
        {
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.GetAsync("/api/tickets/999/comments");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateComment_Succeeds_ForReportingCustomer_AttributedToCaller()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);

            var response = await customer.Client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new CreateCommentDto { Message = "Any update?" });

            Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}");
            var saved = await _factory.QueryAsync(db => db.Comments.SingleAsync());
            Assert.Equal(customer.Id, saved.UserId);
            Assert.Equal("Any update?", saved.Message);
        }

        [Fact]
        public async Task CreateComment_Returns403_ForOtherCustomer_AndSavesNothing()
        {
            var owner = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(owner.Id);
            var otherCustomer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await otherCustomer.Client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new CreateCommentDto { Message = "Spam" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Comments.AnyAsync()));
        }

        [Fact]
        public async Task CreateComment_Succeeds_ForAgent()
        {
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new CreateCommentDto { Message = "On it" });

            Assert.True(response.IsSuccessStatusCode, $"Expected success, got {(int)response.StatusCode}");
            Assert.Equal(agent.Id, await _factory.QueryAsync(db => db.Comments.Select(c => c.UserId).SingleAsync()));
        }

        [Fact]
        public async Task CreateComment_Returns404_WhenTicketDoesNotExist()
        {
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PostAsJsonAsync("/api/tickets/999/comments", new CreateCommentDto { Message = "Hello?" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
