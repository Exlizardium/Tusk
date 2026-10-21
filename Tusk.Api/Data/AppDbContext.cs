using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tusk.Api.Models;

namespace Tusk.Api.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Ticket> Tickets { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<Project> Projects { get; set; } = null!;

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            IncrementVersionsOfModifiedEntities();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            IncrementVersionsOfModifiedEntities();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public void ExpectVersion(IVersioned entity, int version)
        {
            Entry(entity).Property(nameof(IVersioned.Version)).OriginalValue = version;
        }

        private void IncrementVersionsOfModifiedEntities()
        {
            foreach (var entry in ChangeTracker.Entries<IVersioned>().Where(e => e.State == EntityState.Modified))
            {
                entry.Entity.Version++;
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Project>(entity =>
            {
                entity.Property(p => p.Version)
                    .IsConcurrencyToken();

                entity.Property(p => p.Name)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.HasIndex(p => p.Name)
                    .IsUnique();

                entity.Property(p => p.Description)
                    .HasMaxLength(250);

                entity.HasMany(p => p.Tickets)
                    .WithOne(t => t.Project)
                    .HasForeignKey(t => t.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.CreatedBy)
                    .WithMany()
                    .HasForeignKey(p => p.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Ticket>(entity =>
            {
                entity.Property(t => t.Version)
                    .IsConcurrencyToken();

                entity.Property(t => t.Title)
                    .HasMaxLength(200)
                    .IsRequired();

                entity.Property(t => t.Status)
                    .IsRequired();

                entity.Property(t => t.Priority)
                    .IsRequired();

                entity.Property(t => t.CreatedAt)
                    .IsRequired();

                entity.HasOne(t => t.Customer)
                    .WithMany()
                    .HasForeignKey(t => t.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.AssignedTo)
                    .WithMany()
                    .HasForeignKey(t => t.AssignedToId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.ClosedBy)
                    .WithMany()
                    .HasForeignKey(t => t.ClosedById)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasMany(t => t.Comments)
                    .WithOne(c => c.Ticket)
                    .HasForeignKey(c => c.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Comment>(entity =>
            {
                entity.Property(c => c.Message)
                    .IsRequired();

                entity.Property(c => c.CreatedAt)
                    .IsRequired();

                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
