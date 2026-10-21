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
            var project = await _factory.SeedProjectAsync(admin.Id, $"Project {Guid.NewGuid():N}");
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

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var saved = await _factory.QueryAsync(db => db.Comments.SingleAsync());
            Assert.Equal(customer.Id, saved.UserId);
            Assert.Equal("Any update?", saved.Message);
        }

        [Fact]
        public async Task CreateComment_ReturnsLocation_ThatServesTheNewComment()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);

            var response = await customer.Client.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new CreateCommentDto { Message = "Any update?" });

            var created = await response.Content.ReadFromJsonAsync<CommentDto>();
            Assert.Equal($"/api/tickets/{ticket.Id}/comments/{created!.Id}", response.Headers.Location?.AbsolutePath);
            var fetched = await customer.Client.GetFromJsonAsync<CommentDto>(response.Headers.Location);
            Assert.Equal("Any update?", fetched!.Message);
        }

        [Fact]
        public async Task GetComment_Returns401_WithoutToken()
        {
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id, "Question");

            var response = await _factory.CreateClient().GetAsync($"/api/tickets/{ticket.Id}/comments/{comment.Id}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("Customer")]
        [InlineData("Agent")]
        [InlineData("Admin")]
        public async Task GetComment_ReturnsComment_ForReportingCustomerOrStaff(string role)
        {
            var caller = await _factory.LoginAsNewUserAsync(role);
            var reporterId = role == "Customer" ? caller.Id : (await _factory.AddUserAsync("Customer")).Id;
            var ticket = await SeedTicketAsync(reporterId);
            var comment = await SeedCommentAsync(ticket.Id, reporterId, "Question");

            var fetched = await caller.Client.GetFromJsonAsync<CommentDto>($"/api/tickets/{ticket.Id}/comments/{comment.Id}");

            Assert.Equal("Question", fetched!.Message);
        }

        [Fact]
        public async Task GetComment_Returns403_ForOtherCustomer()
        {
            var owner = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(owner.Id);
            var comment = await SeedCommentAsync(ticket.Id, owner.Id, "Private details");
            var otherCustomer = await _factory.LoginAsNewUserAsync("Customer");

            var response = await otherCustomer.Client.GetAsync($"/api/tickets/{ticket.Id}/comments/{comment.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetComment_Returns404_WhenCommentBelongsToAnotherTicket()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ownTicket = await SeedTicketAsync(customer.Id);
            var otherTicket = await SeedTicketAsync((await _factory.AddUserAsync("Customer")).Id);
            var otherComment = await SeedCommentAsync(otherTicket.Id, otherTicket.CustomerId, "Not yours");

            var response = await customer.Client.GetAsync($"/api/tickets/{ownTicket.Id}/comments/{otherComment.Id}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task GetComment_Returns404_WhenTicketOrCommentDoesNotExist(bool ticketExists)
        {
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var ticketId = ticketExists ? (await SeedTicketAsync((await _factory.AddUserAsync("Customer")).Id)).Id : 999;

            var response = await agent.Client.GetAsync($"/api/tickets/{ticketId}/comments/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(agent.Id, await _factory.QueryAsync(db => db.Comments.Select(c => c.UserId).SingleAsync()));
        }

        [Fact]
        public async Task UpdateComment_Returns200_ForAuthor_AndMarksEdited()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id, "Original");

            var response = await customer.Client.PutAsJsonAsync(
                $"/api/tickets/{ticket.Id}/comments/{comment.Id}", new UpdateCommentDto { Message = "Edited" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = await response.Content.ReadFromJsonAsync<CommentDto>();
            Assert.Equal("Edited", updated!.Message);
            Assert.NotNull(updated.UpdatedAt);
        }

        [Theory]
        [InlineData("Agent")]
        [InlineData("Admin")]
        public async Task UpdateComment_Returns403_ForNonAuthorStaff(string role)
        {
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id, "Original");
            var staff = await _factory.LoginAsNewUserAsync(role);

            var response = await staff.Client.PutAsJsonAsync(
                $"/api/tickets/{ticket.Id}/comments/{comment.Id}", new UpdateCommentDto { Message = "Rewritten" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("Original", await _factory.QueryAsync(db => db.Comments.Select(c => c.Message).SingleAsync()));
        }

        [Fact]
        public async Task UpdateComment_Returns403_ForTicketOwner_OnAgentsComment()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var agent = await _factory.AddUserAsync("Agent");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, agent.Id, "We'll look into it");

            var response = await customer.Client.PutAsJsonAsync(
                $"/api/tickets/{ticket.Id}/comments/{comment.Id}", new UpdateCommentDto { Message = "Fixed for free" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UpdateComment_Returns404_ThroughAnotherTicket()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var otherTicket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id, "Original");

            var response = await customer.Client.PutAsJsonAsync(
                $"/api/tickets/{otherTicket.Id}/comments/{comment.Id}", new UpdateCommentDto { Message = "Edited" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateComment_Returns400_WhenMessageEmpty()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id, "Original");

            var response = await customer.Client.PutAsJsonAsync(
                $"/api/tickets/{ticket.Id}/comments/{comment.Id}", new UpdateCommentDto { Message = "" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task DeleteComment_Returns204_ForAuthor()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id, "Oops");

            var response = await customer.Client.DeleteAsync($"/api/tickets/{ticket.Id}/comments/{comment.Id}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Comments.AnyAsync()));
        }

        [Fact]
        public async Task DeleteComment_Returns204_ForAdmin_OnSomeoneElsesComment()
        {
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id, "Abusive");
            var admin = await _factory.LoginAsNewUserAsync("Admin");

            var response = await admin.Client.DeleteAsync($"/api/tickets/{ticket.Id}/comments/{comment.Id}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.False(await _factory.QueryAsync(db => db.Comments.AnyAsync()));
        }

        [Fact]
        public async Task DeleteComment_Returns403_ForAgent_OnCustomersComment()
        {
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, customer.Id, "Complaint");
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.DeleteAsync($"/api/tickets/{ticket.Id}/comments/{comment.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(await _factory.QueryAsync(db => db.Comments.AnyAsync()));
        }

        [Fact]
        public async Task DeleteComment_Returns403_ForTicketOwner_OnAgentsComment()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var agent = await _factory.AddUserAsync("Agent");
            var ticket = await SeedTicketAsync(customer.Id);
            var comment = await SeedCommentAsync(ticket.Id, agent.Id, "Please send logs");

            var response = await customer.Client.DeleteAsync($"/api/tickets/{ticket.Id}/comments/{comment.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.True(await _factory.QueryAsync(db => db.Comments.AnyAsync()));
        }

        [Fact]
        public async Task DeleteComment_Returns404_WhenCommentDoesNotExist()
        {
            var customer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedTicketAsync(customer.Id);

            var response = await customer.Client.DeleteAsync($"/api/tickets/{ticket.Id}/comments/999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
