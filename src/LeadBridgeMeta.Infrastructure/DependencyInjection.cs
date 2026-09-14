using LeadBridgeMeta.Application.Auth;
using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Ghl;
using LeadBridgeMeta.Application.Leads;
using LeadBridgeMeta.Application.Meta;
using LeadBridgeMeta.Infrastructure.Auth;
using LeadBridgeMeta.Infrastructure.Ghl;
using LeadBridgeMeta.Infrastructure.Identity;
using LeadBridgeMeta.Infrastructure.Leads;
using LeadBridgeMeta.Infrastructure.Meta;
using LeadBridgeMeta.Infrastructure.Persistence;
using LeadBridgeMeta.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LeadBridgeMeta.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddDataProtection();
        services.AddHttpContextAccessor();

        services.Configure<MetaOptions>(configuration.GetSection(MetaOptions.SectionName));
        services.Configure<GhlOptions>(configuration.GetSection(GhlOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddHttpClient<IMetaGraphClient, MetaGraphClient>();
        services.AddHttpClient<IGhlClient, GhlClient>();

        services.AddScoped<ITokenProtector, DataProtectionTokenProtector>();
        services.AddScoped<IOAuthStateService, OAuthStateService>();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ILeadProcessingService, LeadProcessingService>();

        services.AddSingleton<IMetaWebhookQueue, MetaWebhookQueue>();
        services.AddHostedService<MetaWebhookProcessingWorker>();

        return services;
    }
}
