using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Comments;
using Tusk.Api.Models;
using Tusk.Api.Services.Implementations;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Services
{
    public sealed class CommentServiceTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose() => _db.Dispose();

        private async Task<Ticket> AddTicketAsync(string customerId)
        {
            var project = await _db.AddProjectAsync($"Project {Guid.NewGuid():N}", customerId);
            return await _db.AddTicketAsync(project.Id, customerId);
        }

        private async Task<Comment> AddCommentAsync(int ticketId, string userId, string message, DateTime createdAt)
        {
            var comment = new Comment
            {
                TicketId = ticketId,
                UserId = userId,
                Message = message,
                CreatedAt = createdAt
            };

            await using var context = _db.CreateContext();
            context.Comments.Add(comment);
            await context.SaveChangesAsync();
            return comment;
        }

        [Fact]
        public async Task GetTicketCustomerIdAsync_ReturnsReportingCustomer()
        {
            var customer = await _db.AddUserAsync();
            var ticket = await AddTicketAsync(customer.Id);

            await using var context = _db.CreateContext();
            Assert.Equal(customer.Id, await new CommentService(context).GetTicketCustomerIdAsync(ticket.Id));
        }

        [Fact]
        public async Task GetTicketCustomerIdAsync_ReturnsNull_WhenTicketDoesNotExist()
        {
            await using var context = _db.CreateContext();
            Assert.Null(await new CommentService(context).GetTicketCustomerIdAsync(999));
        }

        [Fact]
        public async Task GetCommentsForTicketAsync_ReturnsOnlyThatTicketsComments_OldestFirst()
        {
            var customer = await _db.AddUserAsync();
            var agent = await _db.AddUserAsync("Agent", "Smith");
            var ticket = await AddTicketAsync(customer.Id);
            var otherTicket = await AddTicketAsync(customer.Id);
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await AddCommentAsync(ticket.Id, agent.Id, "Reply", start.AddHours(1));
            await AddCommentAsync(ticket.Id, customer.Id, "Question", start);
            await AddCommentAsync(otherTicket.Id, customer.Id, "Elsewhere", start.AddHours(2));

            await using var context = _db.CreateContext();
            var result = await new CommentService(context).GetCommentsForTicketAsync(ticket.Id);

            Assert.Equal(["Question", "Reply"], result.Select(c => c.Message));
            Assert.Equal([customer.Id, agent.Id], result.Select(c => c.UserId));
            Assert.All(result, c => Assert.Equal(ticket.Id, c.TicketId));
        }

        [Fact]
        public async Task GetCommentsForTicketAsync_ReturnsEmpty_WhenTicketHasNoComments()
        {
            var customer = await _db.AddUserAsync();
            var ticket = await AddTicketAsync(customer.Id);

            await using var context = _db.CreateContext();
            Assert.Empty(await new CommentService(context).GetCommentsForTicketAsync(ticket.Id));
        }

        [Fact]
        public async Task CreateCommentAsync_PersistsComment_AttributedToCaller()
        {
            var customer = await _db.AddUserAsync();
            var agent = await _db.AddUserAsync("Agent", "Smith");
            var ticket = await AddTicketAsync(customer.Id);

            CommentDto? created;
            await using (var context = _db.CreateContext())
            {
                created = await new CommentService(context).CreateCommentAsync(
                    new CreateCommentDto { Message = "On it" }, agent.Id, ticket.Id);
            }

            Assert.NotNull(created);
            await using var verify = _db.CreateContext();
            var saved = await verify.Comments.SingleAsync();
            Assert.Equal(created.Id, saved.Id);
            Assert.Equal("On it", saved.Message);
            Assert.Equal(agent.Id, saved.UserId);
            Assert.Equal(ticket.Id, saved.TicketId);
        }

        [Fact]
        public async Task CreateCommentAsync_ReturnsCommentWithNoUpdatedAt()
        {
            var customer = await _db.AddUserAsync();
            var ticket = await AddTicketAsync(customer.Id);

            await using var context = _db.CreateContext();
            var created = await new CommentService(context).CreateCommentAsync(
                new CreateCommentDto { Message = "Hi" }, customer.Id, ticket.Id);

            Assert.Null(created!.UpdatedAt);
        }

        [Fact]
        public async Task GetCommentAsync_ReturnsComment_OnlyThroughItsOwnTicket()
        {
            var customer = await _db.AddUserAsync();
            var ticket = await AddTicketAsync(customer.Id);
            var otherTicket = await AddTicketAsync(customer.Id);
            var comment = await AddCommentAsync(ticket.Id, customer.Id, "Hi", DateTime.UtcNow);

            await using var context = _db.CreateContext();
            var service = new CommentService(context);

            Assert.Equal("Hi", (await service.GetCommentAsync(ticket.Id, comment.Id))!.Message);
            Assert.Null(await service.GetCommentAsync(otherTicket.Id, comment.Id));
        }

        [Fact]
        public async Task UpdateCommentAsync_PersistsMessage_AndSetsUpdatedAt()
        {
            var customer = await _db.AddUserAsync();
            var ticket = await AddTicketAsync(customer.Id);
            var comment = await AddCommentAsync(ticket.Id, customer.Id, "Original", DateTime.UtcNow);
            var before = DateTime.UtcNow;

            await using (var context = _db.CreateContext())
            {
                await new CommentService(context).UpdateCommentAsync(ticket.Id, comment.Id, new UpdateCommentDto { Message = "Edited" });
            }

            await using var verify = _db.CreateContext();
            var saved = await verify.Comments.SingleAsync();
            Assert.Equal("Edited", saved.Message);
            Assert.NotNull(saved.UpdatedAt);
            Assert.InRange(saved.UpdatedAt.Value, before, DateTime.UtcNow);
        }

        [Fact]
        public async Task UpdateCommentAsync_ReturnsNull_AndChangesNothing_ThroughAnotherTicket()
        {
            var customer = await _db.AddUserAsync();
            var ticket = await AddTicketAsync(customer.Id);
            var otherTicket = await AddTicketAsync(customer.Id);
            var comment = await AddCommentAsync(ticket.Id, customer.Id, "Original", DateTime.UtcNow);

            await using (var context = _db.CreateContext())
            {
                Assert.Null(await new CommentService(context).UpdateCommentAsync(
                    otherTicket.Id, comment.Id, new UpdateCommentDto { Message = "Hijacked" }));
            }

            await using var verify = _db.CreateContext();
            var saved = await verify.Comments.SingleAsync();
            Assert.Equal("Original", saved.Message);
            Assert.Null(saved.UpdatedAt);
        }

        [Fact]
        public async Task DeleteCommentAsync_RemovesComment_AndReturnsTrue()
        {
            var customer = await _db.AddUserAsync();
            var ticket = await AddTicketAsync(customer.Id);
            var comment = await AddCommentAsync(ticket.Id, customer.Id, "Bye", DateTime.UtcNow);

            await using (var context = _db.CreateContext())
            {
                Assert.True(await new CommentService(context).DeleteCommentAsync(ticket.Id, comment.Id));
            }

            await using var verify = _db.CreateContext();
            Assert.False(await verify.Comments.AnyAsync());
        }

        [Fact]
        public async Task DeleteCommentAsync_ReturnsFalse_AndKeepsComment_ThroughAnotherTicket()
        {
            var customer = await _db.AddUserAsync();
            var ticket = await AddTicketAsync(customer.Id);
            var otherTicket = await AddTicketAsync(customer.Id);
            var comment = await AddCommentAsync(ticket.Id, customer.Id, "Stay", DateTime.UtcNow);

            await using (var context = _db.CreateContext())
            {
                Assert.False(await new CommentService(context).DeleteCommentAsync(otherTicket.Id, comment.Id));
            }

            await using var verify = _db.CreateContext();
            Assert.True(await verify.Comments.AnyAsync());
        }

        [Fact]
        public async Task CreateCommentAsync_ReturnsNull_AndSavesNothing_WhenTicketDoesNotExist()
        {
            var user = await _db.AddUserAsync();

            await using (var context = _db.CreateContext())
            {
                var result = await new CommentService(context).CreateCommentAsync(
                    new CreateCommentDto { Message = "Hello?" }, user.Id, 999);

                Assert.Null(result);
            }

            await using var verify = _db.CreateContext();
            Assert.False(await verify.Comments.AnyAsync());
        }
    }
}
