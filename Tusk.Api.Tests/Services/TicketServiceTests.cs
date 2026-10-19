using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Tickets;
using Tusk.Api.Models;
using Tusk.Api.Models.Enums;
using Tusk.Api.Services.Implementations;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Services
{
    public sealed class TicketServiceTests : IDisposable
    {
        private readonly TestDatabase _db = new();

        public void Dispose() => _db.Dispose();

        private async Task<Ticket> ReloadTicketAsync(int ticketId)
        {
            await using var context = _db.CreateContext();
            return await context.Tickets.SingleAsync(t => t.Id == ticketId);
        }

        [Fact]
        public async Task CreateTicketAsync_PersistsOpenTicket_ForCustomer()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);

            TicketDto created;
            await using (var context = _db.CreateContext())
            {
                created = await new TicketService(context).CreateTicketAsync(
                    project.Id,
                    new CreateTicketDto { Title = "Broken", Description = "Details", Priority = TicketPriority.High },
                    customer.Id);
            }

            var saved = await ReloadTicketAsync(created.Id);
            Assert.Equal("Broken", saved.Title);
            Assert.Equal("Details", saved.Description);
            Assert.Equal(TicketPriority.High, saved.Priority);
            Assert.Equal(TicketStatus.Open, saved.Status);
            Assert.Equal(customer.Id, saved.CustomerId);
            Assert.Equal(project.Id, saved.ProjectId);
            Assert.Null(saved.AssignedToId);
        }

        [Fact]
        public async Task CreateTicketAsync_ThrowsKeyNotFound_WhenProjectDoesNotExist()
        {
            var customer = await _db.AddUserAsync();

            await using var context = _db.CreateContext();
            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                new TicketService(context).CreateTicketAsync(999, new CreateTicketDto { Title = "Broken" }, customer.Id));
        }

        [Fact]
        public async Task GetTicketByIdAsync_ReturnsTicket_WhenItBelongsToProject()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id, "Broken");

            await using var context = _db.CreateContext();
            var result = await new TicketService(context).GetTicketByIdAsync(project.Id, ticket.Id);

            Assert.NotNull(result);
            Assert.Equal("Broken", result.Title);
            Assert.Equal(project.Id, result.ProjectId);
        }

        [Fact]
        public async Task GetTicketByIdAsync_ReturnsNull_WhenTicketDoesNotExist()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);

            await using var context = _db.CreateContext();
            Assert.Null(await new TicketService(context).GetTicketByIdAsync(project.Id, 999));
        }

        [Theory]
        [InlineData("get")]
        [InlineData("content")]
        [InlineData("status")]
        [InlineData("priority")]
        [InlineData("assign")]
        [InlineData("take")]
        [InlineData("unassign")]
        public async Task SingleTicketOperations_ReturnNull_AndChangeNothing_WhenTicketBelongsToAnotherProject(string operation)
        {
            var customer = await _db.AddUserAsync();
            var agent = await _db.AddUserAsync("Agent", "Smith");
            var owningProject = await _db.AddProjectAsync("Alpha", customer.Id);
            var otherProject = await _db.AddProjectAsync("Beta", customer.Id);
            var ticket = await _db.AddTicketAsync(owningProject.Id, customer.Id, "Original", assignedToId: agent.Id);

            await using (var context = _db.CreateContext())
            {
                var service = new TicketService(context);
                var wrongProjectId = otherProject.Id;

                var result = operation switch
                {
                    "get" => await service.GetTicketByIdAsync(wrongProjectId, ticket.Id),
                    "content" => await service.UpdateTicketContentAsync(wrongProjectId, ticket.Id, new UpdateTicketContentDto { Title = "Hijacked" }),
                    "status" => await service.UpdateTicketStatusAsync(wrongProjectId, ticket.Id, new UpdateTicketStatusDto { Status = TicketStatus.Closed }),
                    "priority" => await service.UpdateTicketPriorityAsync(wrongProjectId, ticket.Id, new UpdateTicketPriorityDto { Priority = TicketPriority.Critical }),
                    "assign" => await service.AssignTicketAsync(wrongProjectId, ticket.Id, new AssignTicketDto { AssignedToId = customer.Id }),
                    "take" => await service.TakeTicketAsync(wrongProjectId, ticket.Id, customer.Id),
                    "unassign" => await service.UnassignTicketAsync(wrongProjectId, ticket.Id),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation))
                };

                Assert.Null(result);
            }

            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal("Original", saved.Title);
            Assert.Equal(TicketStatus.Open, saved.Status);
            Assert.Equal(TicketPriority.Medium, saved.Priority);
            Assert.Equal(agent.Id, saved.AssignedToId);
            Assert.Null(saved.UpdatedAt);
        }

        [Fact]
        public async Task UpdateTicketContentAsync_PersistsTitleAndDescription()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id, "Original");

            await using (var context = _db.CreateContext())
            {
                await new TicketService(context).UpdateTicketContentAsync(
                    project.Id, ticket.Id, new UpdateTicketContentDto { Title = "Edited", Description = "More detail" });
            }

            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal("Edited", saved.Title);
            Assert.Equal("More detail", saved.Description);
        }

        [Fact]
        public async Task UpdateTicketStatusAsync_PersistsStatus()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id);

            await using (var context = _db.CreateContext())
            {
                await new TicketService(context).UpdateTicketStatusAsync(
                    project.Id, ticket.Id, new UpdateTicketStatusDto { Status = TicketStatus.Solved });
            }

            Assert.Equal(TicketStatus.Solved, (await ReloadTicketAsync(ticket.Id)).Status);
        }

        [Fact]
        public async Task UpdateTicketPriorityAsync_PersistsPriority()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id);

            await using (var context = _db.CreateContext())
            {
                await new TicketService(context).UpdateTicketPriorityAsync(
                    project.Id, ticket.Id, new UpdateTicketPriorityDto { Priority = TicketPriority.Critical });
            }

            Assert.Equal(TicketPriority.Critical, (await ReloadTicketAsync(ticket.Id)).Priority);
        }

        [Theory]
        [InlineData("content")]
        [InlineData("status")]
        [InlineData("priority")]
        public async Task FieldUpdates_SetUpdatedAt_ToCurrentTime(string operation)
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id);
            var before = DateTime.UtcNow;

            await using (var context = _db.CreateContext())
            {
                var service = new TicketService(context);
                _ = operation switch
                {
                    "content" => await service.UpdateTicketContentAsync(project.Id, ticket.Id, new UpdateTicketContentDto { Title = "Edited" }),
                    "status" => await service.UpdateTicketStatusAsync(project.Id, ticket.Id, new UpdateTicketStatusDto { Status = TicketStatus.Solved }),
                    "priority" => await service.UpdateTicketPriorityAsync(project.Id, ticket.Id, new UpdateTicketPriorityDto { Priority = TicketPriority.High }),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation))
                };
            }

            var updatedAt = (await ReloadTicketAsync(ticket.Id)).UpdatedAt;
            Assert.NotNull(updatedAt);
            Assert.InRange(updatedAt.Value, before, DateTime.UtcNow);
        }

        [Fact]
        public async Task UpdatedAt_IsReturned_FromUpdateAndFromListQuery()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id);

            TicketDto? updated;
            await using (var context = _db.CreateContext())
            {
                updated = await new TicketService(context).UpdateTicketStatusAsync(
                    project.Id, ticket.Id, new UpdateTicketStatusDto { Status = TicketStatus.Solved });
            }

            await using var verify = _db.CreateContext();
            var listed = await new TicketService(verify).GetAllTicketsAsync(project.Id, new TicketQueryParams(), customerId: null);

            Assert.NotNull(updated!.UpdatedAt);
            Assert.Equal(updated.UpdatedAt, Assert.Single(listed.Items).UpdatedAt);
        }

        [Fact]
        public async Task UpdatedAt_IsNull_ForNeverModifiedTicket()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id);

            await using var context = _db.CreateContext();
            var result = await new TicketService(context).GetTicketByIdAsync(project.Id, ticket.Id);

            Assert.Null(result!.UpdatedAt);
        }

        [Fact]
        public async Task AssignTicketAsync_SetsAssignee_AndUpdatedAt()
        {
            var customer = await _db.AddUserAsync();
            var agent = await _db.AddUserAsync("Agent", "Smith");
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id);

            await using (var context = _db.CreateContext())
            {
                await new TicketService(context).AssignTicketAsync(
                    project.Id, ticket.Id, new AssignTicketDto { AssignedToId = agent.Id });
            }

            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal(agent.Id, saved.AssignedToId);
            Assert.NotNull(saved.UpdatedAt);
        }

        [Fact]
        public async Task TakeTicketAsync_AssignsCaller_AndSetsUpdatedAt()
        {
            var customer = await _db.AddUserAsync();
            var agent = await _db.AddUserAsync("Agent", "Smith");
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id);

            await using (var context = _db.CreateContext())
            {
                await new TicketService(context).TakeTicketAsync(project.Id, ticket.Id, agent.Id);
            }

            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal(agent.Id, saved.AssignedToId);
            Assert.NotNull(saved.UpdatedAt);
        }

        [Fact]
        public async Task UnassignTicketAsync_ClearsAssignee_AndSetsUpdatedAt()
        {
            var customer = await _db.AddUserAsync();
            var agent = await _db.AddUserAsync("Agent", "Smith");
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var ticket = await _db.AddTicketAsync(project.Id, customer.Id, assignedToId: agent.Id);

            await using (var context = _db.CreateContext())
            {
                await new TicketService(context).UnassignTicketAsync(project.Id, ticket.Id);
            }

            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Null(saved.AssignedToId);
            Assert.NotNull(saved.UpdatedAt);
        }

        [Fact]
        public async Task GetAllTicketsAsync_ReturnsOnlyThatProjectsTickets_NewestFirst()
        {
            var customer = await _db.AddUserAsync();
            var alpha = await _db.AddProjectAsync("Alpha", customer.Id);
            var beta = await _db.AddProjectAsync("Beta", customer.Id);
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            await _db.AddTicketAsync(alpha.Id, customer.Id, "Alpha old", createdAt: start);
            await _db.AddTicketAsync(alpha.Id, customer.Id, "Alpha new", createdAt: start.AddDays(1));
            await _db.AddTicketAsync(beta.Id, customer.Id, "Beta", createdAt: start.AddDays(2));

            await using var context = _db.CreateContext();
            var result = await new TicketService(context).GetAllTicketsAsync(alpha.Id, new TicketQueryParams(), customerId: null);

            Assert.Equal(["Alpha new", "Alpha old"], result.Items.Select(t => t.Title));
            Assert.Equal(2, result.TotalCount);
        }

        [Fact]
        public async Task GetAllTicketsAsync_ReturnsRequestedPage_WithTotalCountOfAllMatches()
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 1; i <= 5; i++)
            {
                await _db.AddTicketAsync(project.Id, customer.Id, $"Ticket {i}", createdAt: start.AddDays(i));
            }

            await using var context = _db.CreateContext();
            var result = await new TicketService(context)
                .GetAllTicketsAsync(project.Id, new TicketQueryParams { Page = 2, PageSize = 2 }, customerId: null);

            Assert.Equal(["Ticket 3", "Ticket 2"], result.Items.Select(t => t.Title));
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(3, result.TotalPages);
        }

        [Theory]
        [InlineData(0, 0, 1, 1)]
        [InlineData(-3, 500, 1, 100)]
        public async Task GetAllTicketsAsync_ClampsPageAndPageSize(int page, int pageSize, int expectedPage, int expectedPageSize)
        {
            var customer = await _db.AddUserAsync();
            var project = await _db.AddProjectAsync("Alpha", customer.Id);

            await using var context = _db.CreateContext();
            var result = await new TicketService(context)
                .GetAllTicketsAsync(project.Id, new TicketQueryParams { Page = page, PageSize = pageSize }, customerId: null);

            Assert.Equal(expectedPage, result.Page);
            Assert.Equal(expectedPageSize, result.PageSize);
        }

        [Fact]
        public async Task GetAllTicketsAsync_AppliesFilters_BeforeCounting()
        {
            var customer = await _db.AddUserAsync();
            var agent = await _db.AddUserAsync("Agent", "Smith");
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            await _db.AddTicketAsync(project.Id, customer.Id, "Match", TicketStatus.InProgress, TicketPriority.High, agent.Id);
            await _db.AddTicketAsync(project.Id, customer.Id, "Wrong status", TicketStatus.Open, TicketPriority.High, agent.Id);
            await _db.AddTicketAsync(project.Id, customer.Id, "Wrong priority", TicketStatus.InProgress, TicketPriority.Low, agent.Id);
            await _db.AddTicketAsync(project.Id, customer.Id, "Unassigned", TicketStatus.InProgress, TicketPriority.High);

            await using var context = _db.CreateContext();
            var result = await new TicketService(context).GetAllTicketsAsync(project.Id, new TicketQueryParams
            {
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High,
                AssignedToId = agent.Id
            }, customerId: null);

            Assert.Equal("Match", Assert.Single(result.Items).Title);
            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task GetAllTicketsAsync_ReturnsOnlyCustomersOwnTickets_WhenCustomerFilterGiven()
        {
            var me = await _db.AddUserAsync("Me", "Customer");
            var someoneElse = await _db.AddUserAsync("Other", "Customer");
            var project = await _db.AddProjectAsync("Alpha", me.Id);
            await _db.AddTicketAsync(project.Id, me.Id, "Mine");
            await _db.AddTicketAsync(project.Id, someoneElse.Id, "Not mine");

            await using var context = _db.CreateContext();
            var result = await new TicketService(context)
                .GetAllTicketsAsync(project.Id, new TicketQueryParams(), customerId: me.Id);

            Assert.Equal("Mine", Assert.Single(result.Items).Title);
            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task GetMyTicketsAsync_ReturnsOnlyTicketsReportedByUser_AcrossProjects()
        {
            var me = await _db.AddUserAsync("Me", "Customer");
            var someoneElse = await _db.AddUserAsync("Other", "Customer");
            var alpha = await _db.AddProjectAsync("Alpha", me.Id);
            var beta = await _db.AddProjectAsync("Beta", me.Id);
            await _db.AddTicketAsync(alpha.Id, me.Id, "Mine in Alpha");
            await _db.AddTicketAsync(beta.Id, me.Id, "Mine in Beta");
            await _db.AddTicketAsync(alpha.Id, someoneElse.Id, "Not mine");

            await using var context = _db.CreateContext();
            var result = await new TicketService(context).GetMyTicketsAsync(me.Id, new TicketQueryParams());

            Assert.Equal(["Mine in Alpha", "Mine in Beta"], result.Items.Select(t => t.Title).Order());
            Assert.Equal(2, result.TotalCount);
        }

        [Fact]
        public async Task GetAssignedTicketsAsync_ReturnsOnlyTicketsAssignedToUser()
        {
            var customer = await _db.AddUserAsync();
            var me = await _db.AddUserAsync("Me", "Agent");
            var otherAgent = await _db.AddUserAsync("Other", "Agent");
            var project = await _db.AddProjectAsync("Alpha", customer.Id);
            await _db.AddTicketAsync(project.Id, customer.Id, "Assigned to me", assignedToId: me.Id);
            await _db.AddTicketAsync(project.Id, customer.Id, "Assigned to other", assignedToId: otherAgent.Id);
            await _db.AddTicketAsync(project.Id, customer.Id, "Unassigned");

            await using var context = _db.CreateContext();
            var result = await new TicketService(context).GetAssignedTicketsAsync(me.Id, new TicketQueryParams());

            Assert.Equal("Assigned to me", Assert.Single(result.Items).Title);
            Assert.Equal(1, result.TotalCount);
        }
    }
}
