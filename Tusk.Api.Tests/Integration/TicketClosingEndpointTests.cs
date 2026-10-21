using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Dtos.Tickets;
using Tusk.Api.Models;
using Tusk.Api.Models.Enums;
using Tusk.Api.Tests.Infrastructure;

namespace Tusk.Api.Tests.Integration
{
    public sealed class TicketClosingEndpointTests : IDisposable
    {
        private readonly TuskApiFactory _factory = new();

        public void Dispose() => _factory.Dispose();

        private async Task<Project> SeedProjectAsync()
        {
            var admin = await _factory.AddUserAsync("Admin");
            return await _factory.SeedProjectAsync(admin.Id);
        }

        private Task<Ticket> SeedClosedTicketAsync(int projectId, string customerId) =>
            _factory.SeedAsync(new Ticket
            {
                Title = "Closed",
                Status = TicketStatus.Closed,
                Priority = TicketPriority.Medium,
                CustomerId = customerId,
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow,
                ClosedAt = DateTime.UtcNow,
                ClosedById = customerId,
                CloseReason = TicketCloseReason.Duplicate
            });

        private Task<Ticket> ReloadTicketAsync(int ticketId) =>
            _factory.QueryAsync(db => db.Tickets.SingleAsync(t => t.Id == ticketId));

        private static string Url(Ticket ticket, string action) =>
            $"/api/projects/{ticket.ProjectId}/tickets/{ticket.Id}/{action}";

        private static CloseTicketDto Because(TicketCloseReason reason) => new() { Reason = reason };

        [Theory]
        [InlineData("close")]
        [InlineData("reopen")]
        public async Task CloseAndReopen_Return401_WithoutToken(string action)
        {
            var project = await SeedProjectAsync();
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var response = await _factory.CreateClient().PutAsJsonAsync(Url(ticket, action), Because(TicketCloseReason.Fixed));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("Customer")]
        [InlineData("Agent")]
        [InlineData("Admin")]
        public async Task Close_ClosesTicket_ForReportingCustomerOrStaff(string role)
        {
            var project = await SeedProjectAsync();
            var caller = await _factory.LoginAsNewUserAsync(role);
            var reporterId = role == "Customer" ? caller.Id : (await _factory.AddUserAsync("Customer")).Id;
            var ticket = await _factory.SeedTicketAsync(project.Id, reporterId);

            var response = await caller.Client.PutAsJsonAsync(Url(ticket, "close"), Because(TicketCloseReason.WontFix));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal(TicketStatus.Closed, saved.Status);
            Assert.Equal(TicketCloseReason.WontFix, saved.CloseReason);
            Assert.Equal(caller.Id, saved.ClosedById);
            Assert.NotNull(saved.ClosedAt);
        }

        [Fact]
        public async Task Close_Returns403_ForOtherCustomer()
        {
            var project = await SeedProjectAsync();
            var reporter = await _factory.AddUserAsync("Customer");
            var otherCustomer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, reporter.Id);

            var response = await otherCustomer.Client.PutAsJsonAsync(Url(ticket, "close"), Because(TicketCloseReason.Fixed));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(TicketStatus.Open, (await ReloadTicketAsync(ticket.Id)).Status);
        }

        [Fact]
        public async Task Close_Returns409_WhenAlreadyClosed()
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedClosedTicketAsync(project.Id, customer.Id);

            var response = await agent.Client.PutAsJsonAsync(Url(ticket, "close"), Because(TicketCloseReason.Fixed));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal(TicketCloseReason.Duplicate, saved.CloseReason);
            Assert.Equal(customer.Id, saved.ClosedById);
        }

        public static TheoryData<string> InvalidCloseBodies => new()
        {
            "{}",
            """{ "reason": null }""",
            """{ "reason": 99 }""",
            """{ "reason": -1 }"""
        };

        [Theory]
        [MemberData(nameof(InvalidCloseBodies))]
        public async Task Close_Returns400_WhenReasonMissingOrInvalid(string body)
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var response = await agent.Client.PutAsync(
                Url(ticket, "close"), new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TicketStatus.Open, (await ReloadTicketAsync(ticket.Id)).Status);
        }

        [Theory]
        [InlineData("close")]
        [InlineData("reopen")]
        public async Task CloseAndReopen_Return404_WhenTicketDoesNotExist(string action)
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.LoginAsNewUserAsync("Agent");

            var response = await agent.Client.PutAsJsonAsync(
                $"/api/projects/{project.Id}/tickets/999/{action}", Because(TicketCloseReason.Fixed));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData("Customer")]
        [InlineData("Agent")]
        [InlineData("Admin")]
        public async Task Reopen_ReopensTicket_ForReportingCustomerOrStaff(string role)
        {
            var project = await SeedProjectAsync();
            var caller = await _factory.LoginAsNewUserAsync(role);
            var reporterId = role == "Customer" ? caller.Id : (await _factory.AddUserAsync("Customer")).Id;
            var ticket = await SeedClosedTicketAsync(project.Id, reporterId);

            var response = await caller.Client.PutAsync(Url(ticket, "reopen"), null);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal(TicketStatus.Open, saved.Status);
            Assert.Null(saved.CloseReason);
            Assert.Null(saved.ClosedById);
            Assert.Null(saved.ClosedAt);
        }

        [Fact]
        public async Task Reopen_Returns403_ForOtherCustomer()
        {
            var project = await SeedProjectAsync();
            var reporter = await _factory.AddUserAsync("Customer");
            var otherCustomer = await _factory.LoginAsNewUserAsync("Customer");
            var ticket = await SeedClosedTicketAsync(project.Id, reporter.Id);

            var response = await otherCustomer.Client.PutAsync(Url(ticket, "reopen"), null);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(TicketStatus.Closed, (await ReloadTicketAsync(ticket.Id)).Status);
        }

        [Fact]
        public async Task Reopen_Returns409_WhenTicketIsNotClosed()
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var response = await agent.Client.PutAsync(Url(ticket, "reopen"), null);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal(TicketStatus.Open, saved.Status);
            Assert.Null(saved.UpdatedAt);
        }

        [Fact]
        public async Task UpdateStatus_Returns400_WhenTargetIsClosed()
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await _factory.SeedTicketAsync(project.Id, customer.Id);

            var response = await agent.Client.PutAsJsonAsync(Url(ticket, "status"), new UpdateTicketStatusDto { Status = TicketStatus.Closed });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(TicketStatus.Open, (await ReloadTicketAsync(ticket.Id)).Status);
        }

        [Fact]
        public async Task UpdateStatus_Returns409_WhenTicketIsClosed()
        {
            var project = await SeedProjectAsync();
            var agent = await _factory.LoginAsNewUserAsync("Agent");
            var customer = await _factory.AddUserAsync("Customer");
            var ticket = await SeedClosedTicketAsync(project.Id, customer.Id);

            var response = await agent.Client.PutAsJsonAsync(Url(ticket, "status"), new UpdateTicketStatusDto { Status = TicketStatus.InProgress });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var saved = await ReloadTicketAsync(ticket.Id);
            Assert.Equal(TicketStatus.Closed, saved.Status);
            Assert.Equal(TicketCloseReason.Duplicate, saved.CloseReason);
        }
    }
}
