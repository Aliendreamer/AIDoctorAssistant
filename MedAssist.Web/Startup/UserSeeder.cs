using MedAssist.Web.Data;

namespace MedAssist.Web.Startup;

public static class UserSeederExtensions
{
    public static async Task SeedUsersAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<UserRepository>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebApplication>>();

        var adminCount = await repo.CountAdminsAsync();
        if (adminCount > 0)
        {
            return;
        }

        var configUsers = config.GetSection("Auth:Users").Get<SeedCredential[]>() ?? [];
        var adminEntry = Array.Find(configUsers, u =>
            string.Equals(u.Role, "Admin", StringComparison.OrdinalIgnoreCase));

        if (adminEntry is null)
        {
            throw new InvalidOperationException(
                "No Admin user found in Auth:Users config and no Admin exists in the database. " +
                "Add an Admin entry to Auth:Users to seed the first administrator account.");
        }

        await repo.CreateAsync(adminEntry.Username, "Admin", adminEntry.Password);
        logger.LogInformation("Seeded initial Admin user '{Username}' from config", adminEntry.Username);
    }

    private sealed class SeedCredential
    {
        public string Username { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
    }
}
