using Microsoft.EntityFrameworkCore;
using Tusk.Api.Models.Enums;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Data
{
    public sealed class VersioningTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task Ticket_StartsAtVersion1_AndIncrementsOnEachSavedChange()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id);

            Assert.Equal(1, ticket.Version);

            await using (var context = _db.CreateContext())
            {
                var loaded = await context.Tickets.SingleAsync();
                loaded.Status = TicketStatus.InProgress;
                await context.SaveChangesAsync();
                loaded.Priority = TicketPriority.High;
                await context.SaveChangesAsync();
            }

            await using var verify = _db.CreateContext();
            Assert.Equal(3, (await verify.Tickets.SingleAsync()).Version);
        }

        [Fact]
        public async Task Project_IncrementsVersion_OnSavedChange()
        {
            var user = await _db.AddUserAsync();
            await _db.AddProjectAsync("Alpha", user.Id);

            await using (var context = _db.CreateContext())
            {
                (await context.Projects.SingleAsync()).Name = "Beta";
                await context.SaveChangesAsync();
            }

            await using var verify = _db.CreateContext();
            Assert.Equal(2, (await verify.Projects.SingleAsync()).Version);
        }

        [Fact]
        public async Task SaveWithoutChanges_DoesNotIncrementVersion()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            await _db.AddTicketAsync(project.Id, customer.Id);

            await using (var context = _db.CreateContext())
            {
                _ = await context.Tickets.SingleAsync();
                await context.SaveChangesAsync();
            }

            await using var verify = _db.CreateContext();
            Assert.Equal(1, (await verify.Tickets.SingleAsync()).Version);
        }

        [Fact]
        public async Task SavingStaleTicket_Throws_AndKeepsTheFirstWrite()
        {
            var customer = await _db.AddUserAsync();
            var agentA = await _db.AddUserAsync("Agent", "A");
            var agentB = await _db.AddUserAsync("Agent", "B");
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            await _db.AddTicketAsync(project.Id, customer.Id);

            await using var first = _db.CreateContext();
            await using var second = _db.CreateContext();
            var seenByFirst = await first.Tickets.SingleAsync();
            var seenBySecond = await second.Tickets.SingleAsync();

            seenByFirst.AssignedToId = agentA.Id;
            await first.SaveChangesAsync();

            seenBySecond.AssignedToId = agentB.Id;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

            await using var verify = _db.CreateContext();
            var saved = await verify.Tickets.SingleAsync();
            Assert.Equal(agentA.Id, saved.AssignedToId);
            Assert.Equal(2, saved.Version);
        }

        [Fact]
        public async Task DeletingStaleProject_Throws()
        {
            var user = await _db.AddUserAsync();
            await _db.AddProjectAsync("Alpha", user.Id);

            await using var first = _db.CreateContext();
            await using var second = _db.CreateContext();
            var seenByFirst = await first.Projects.SingleAsync();
            var seenBySecond = await second.Projects.SingleAsync();

            seenByFirst.Name = "Renamed";
            await first.SaveChangesAsync();

            second.Projects.Remove(seenBySecond);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

            await using var verify = _db.CreateContext();
            Assert.Equal("Renamed", (await verify.Projects.SingleAsync()).Name);
        }
    }
}
