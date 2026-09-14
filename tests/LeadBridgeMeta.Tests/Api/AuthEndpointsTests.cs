using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LeadBridgeMeta.Api.Controllers;
using LeadBridgeMeta.Tests.Common;
using Xunit;

namespace LeadBridgeMeta.Tests.Api;

public class AuthEndpointsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthEndpointsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithNewEmail_CreatesTenantAndReturnsToken()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest("Acme Ads", $"{Guid.NewGuid()}@test.local", "Password123!", "Owner");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal(request.Email, body.Email);
        Assert.NotEqual(Guid.Empty, body.TenantId);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.local";
        var request = new RegisterRequest("Acme Ads", email, "Password123!", "Owner");

        await client.PostAsJsonAsync("/api/auth/register", request);
        var second = await client.PostAsJsonAsync("/api/auth/register", request with { DisplayName = "Someone Else" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_ReturnsToken()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Acme Ads", email, "Password123!", "Owner"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.local";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Acme Ads", email, "Password123!", "Owner"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/leads");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid()}@test.local";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Acme Ads", email, "Password123!", "Owner"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var response = await client.GetAsync("/api/leads");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var leads = await response.Content.ReadFromJsonAsync<List<LeadEventResponse>>();
        Assert.NotNull(leads);
        Assert.Empty(leads!);
    }

    [Fact]
    public async Task DifferentTenants_CannotSeeEachOthersLeads()
    {
        var client = _factory.CreateClient();

        var registerA = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Tenant A", $"{Guid.NewGuid()}@test.local", "Password123!", "A"));
        var authA = await registerA.Content.ReadFromJsonAsync<AuthResponse>();

        var registerB = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Tenant B", $"{Guid.NewGuid()}@test.local", "Password123!", "B"));
        var authB = await registerB.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotEqual(authA!.TenantId, authB!.TenantId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        var metaConnections = await client.GetAsync("/api/meta/connections");
        Assert.Equal(HttpStatusCode.OK, metaConnections.StatusCode);
    }
}
