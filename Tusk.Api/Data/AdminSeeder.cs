using Microsoft.AspNetCore.Identity;
using Tusk.Api.Models;

namespace Tusk.Api.Data
{
    public class AdminSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider services)
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var email = configuration["Admin:Email"];
            var password = configuration["Admin:Password"];

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
                return;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Admin:Email and Admin:Password must be set together (or both left unset).");

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var logger = services.GetRequiredService<ILogger<AdminSeeder>>();

            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                if (!await userManager.IsInRoleAsync(existingUser, "Admin"))
                    logger.LogWarning("Admin:Email {Email} belongs to an existing non-Admin user; not promoting it.", email);
                return;
            }

            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Admin",
                LastName = "User"
            };

            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Could not create the initial admin: " + string.Join("; ", result.Errors.Select(e => e.Description)));

            await userManager.AddToRoleAsync(admin, "Admin");
            logger.LogInformation("Created initial admin {Email}.", email);
        }
    }
}
