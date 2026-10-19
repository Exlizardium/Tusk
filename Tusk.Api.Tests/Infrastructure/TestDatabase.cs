using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Data;
using Tusk.Api.Models;
using Tusk.Api.Models.Enums;

namespace Tusk.Api.Tests.Infrastructure
{
    public sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public AppDbContext CreateContext() => new(_options);

        public async Task<ApplicationUser> AddUserAsync(string firstName = "Test", string lastName = "User")
        {
            var user = new ApplicationUser
            {
                UserName = $"{firstName}.{lastName}.{Guid.NewGuid():N}",
                FirstName = firstName,
                LastName = lastName
            };

            await using var context = CreateContext();
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        public async Task<Project> AddProjectAsync(string name, string createdById, DateTime? createdAt = null)
        {
            var project = new Project
            {
                Name = name,
                CreatedById = createdById,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

            await using var context = CreateContext();
            context.Projects.Add(project);
            await context.SaveChangesAsync();
            return project;
        }

        public async Task<Ticket> AddTicketAsync(
            int projectId,
            string customerId,
            string title = "Ticket",
            TicketStatus status = TicketStatus.Open,
            TicketPriority priority = TicketPriority.Medium,
            string? assignedToId = null,
            DateTime? createdAt = null)
        {
            var ticket = new Ticket
            {
                Title = title,
                Status = status,
                Priority = priority,
                CustomerId = customerId,
                AssignedToId = assignedToId,
                ProjectId = projectId,
                CreatedAt = createdAt ?? DateTime.UtcNow
            };

            await using var context = CreateContext();
            context.Tickets.Add(ticket);
            await context.SaveChangesAsync();
            return ticket;
        }

        public void Dispose() => _connection.Dispose();
    }
}
