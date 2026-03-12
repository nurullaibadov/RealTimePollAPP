using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RealTimePoll.Domain.Entities;
using RealTimePoll.Infrastructure.Identity;

namespace RealTimePoll.Infrastructure.Persistence.Context;

public class AppDbContext : IdentityDbContext<AppIdentityUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollOption> PollOptions => Set<PollOption>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<AppIdentityRefreshToken> RefreshTokens => Set<AppIdentityRefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppIdentityUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.Entity<AppIdentityRefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).HasMaxLength(500).IsRequired();
            e.ToTable("RefreshTokens");
            e.HasOne(x => x.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Poll>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property(x => x.CreatedByUserId).IsRequired();
        });

        builder.Entity<PollOption>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(200).IsRequired();
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.Poll)
             .WithMany(p => p.Options)
             .HasForeignKey(x => x.PollId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Vote>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasOne(x => x.Poll)
             .WithMany(p => p.Votes)
             .HasForeignKey(x => x.PollId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PollOption)
             .WithMany(o => o.Votes)
             .HasForeignKey(x => x.PollOptionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.UserId).IsRequired(false);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is Domain.Entities.BaseEntity entity && entry.State == EntityState.Modified)
                entity.UpdatedAt = DateTime.UtcNow;

            if (entry.Entity is AppIdentityUser identityUser && entry.State == EntityState.Modified)
                identityUser.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
