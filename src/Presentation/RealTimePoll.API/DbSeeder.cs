using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RealTimePoll.Domain.Entities;
using RealTimePoll.Domain.Enums;
using RealTimePoll.Infrastructure.Identity;
using RealTimePoll.Infrastructure.Persistence.Context;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<AppIdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        await context.Database.MigrateAsync();

        string[] roles = ["SuperAdmin", "Admin", "User"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        const string adminEmail = "admin@realtimepoll.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new AppIdentityUser
            {
                FirstName = "Super",
                LastName = "Admin",
                Email = adminEmail,
                UserName = adminEmail,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin@123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
                await userManager.AddToRoleAsync(adminUser, "Admin");

                var poll1 = new Poll
                {
                    Title = "En sevdiğiniz programlama dili nedir?",
                    Description = "Günlük geliştirme sürecinizde en çok kullandığınız dil hangisi?",
                    StartsAt = DateTime.UtcNow.AddMinutes(-10),
                    EndsAt = DateTime.UtcNow.AddDays(7),
                    IsActive = true,
                    Status = PollStatus.Active,
                    CreatedByUserId = adminUser.Id,
                    Options = new List<PollOption>
                    {
                        new() { Text = "C#", OrderIndex = 0 },
                        new() { Text = "Python", OrderIndex = 1 },
                        new() { Text = "JavaScript/TypeScript", OrderIndex = 2 },
                        new() { Text = "Go", OrderIndex = 3 },
                        new() { Text = "Rust", OrderIndex = 4 }
                    }
                };

                var poll2 = new Poll
                {
                    Title = "Tercih ettiğiniz veritabanı?",
                    Description = "Projelerinizde en çok hangi veritabanını kullanıyorsunuz?",
                    StartsAt = DateTime.UtcNow.AddMinutes(-5),
                    EndsAt = DateTime.UtcNow.AddDays(14),
                    IsActive = true,
                    Status = PollStatus.Active,
                    CreatedByUserId = adminUser.Id,
                    Options = new List<PollOption>
                    {
                        new() { Text = "SQL Server", OrderIndex = 0 },
                        new() { Text = "PostgreSQL", OrderIndex = 1 },
                        new() { Text = "MySQL", OrderIndex = 2 },
                        new() { Text = "MongoDB", OrderIndex = 3 },
                        new() { Text = "Redis", OrderIndex = 4 }
                    }
                };

                context.Polls.AddRange(poll1, poll2);
                await context.SaveChangesAsync();
            }
        }
    }
}
