using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MedAssist.Data;

public sealed class MedAssistDbContextFactory : IDesignTimeDbContextFactory<MedAssistDbContext>
{
    public MedAssistDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseNpgsql("Host=postgres;Database=medassist;Username=medassist;Password=medassist", p => p.MapEnum<BookStatus>("book_status"))
            .Options;
        return new MedAssistDbContext(options);
    }
}
