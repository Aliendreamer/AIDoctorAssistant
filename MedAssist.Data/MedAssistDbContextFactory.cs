using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MedAssist.Data;

public sealed class MedAssistDbContextFactory : IDesignTimeDbContextFactory<MedAssistDbContext>
{
    public MedAssistDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseNpgsql("Host=localhost;Database=medassist;Username=medassist;Password=medassist")
            .Options;
        return new MedAssistDbContext(options);
    }
}
