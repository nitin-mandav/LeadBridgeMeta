using LeadBridgeMeta.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LeadBridgeMeta.Tests.Common;

/// <summary>Boots the real Api pipeline (controllers, auth, DI wiring) against an isolated in-memory database
/// per factory instance, with fixed test config so tests don't depend on real Meta/GHL credentials or user-secrets.</summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-please-do-not-use-in-production-0123456789",
                ["Jwt:Issuer"] = "LeadBridgeMeta.Tests",
                ["Jwt:Audience"] = "LeadBridgeMeta.Tests.Clients",
                ["Meta:AppId"] = "test-meta-app-id",
                ["Meta:AppSecret"] = "test-meta-app-secret",
                ["Meta:WebhookVerifyToken"] = "test-verify-token",
                ["Ghl:ClientId"] = "test-ghl-client-id",
                ["Ghl:ClientSecret"] = "test-ghl-client-secret",
                ["App:ApiBaseUrl"] = "https://api.test.local",
                ["App:FrontendBaseUrl"] = "https://app.test.local",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();

            // AddInfrastructure() already registered the SqlServer provider's services into this container via
            // AddDbContext(UseSqlServer). Just swapping DbContextOptions isn't enough - EF still sees both
            // SqlServer's and InMemory's IDatabaseProvider in the same collection and refuses to pick one. Giving
            // this DbContext its own isolated internal service provider (which only knows about InMemory) sidesteps
            // that entirely, per EF Core's documented fix for this exact "multiple providers registered" scenario.
            var inMemoryServiceProvider = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });
        });
    }
}
