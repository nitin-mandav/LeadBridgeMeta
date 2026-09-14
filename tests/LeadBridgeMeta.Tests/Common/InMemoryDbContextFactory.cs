using LeadBridgeMeta.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LeadBridgeMeta.Tests.Common;

public static class InMemoryDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
