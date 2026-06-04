using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using MineWatch.Infrastructure.Data;

namespace MineWatch.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DbName { get; set; } = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove Npgsql and existing DbContext registrations
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<MineWatchDbContext>) ||
                            d.ServiceType == typeof(MineWatchDbContext) ||
                            d.ServiceType.FullName?.Contains("Npgsql") == true ||
                            d.ServiceType.FullName?.Contains("DbContextOptions") == true)
                .ToList();
            foreach (var d in descriptorsToRemove)
                services.Remove(d);

            services.AddDbContext<MineWatchDbContext>(options =>
                options.UseInMemoryDatabase(DbName));

            // Replace JWT auth with test auth
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    public HttpClient CreateClientWithAuth(string userId = "test-user", string[]? roles = null)
    {
        var client = CreateClient();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.NameIdentifier, userId),
        };
        if (roles != null)
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        // Encode claims into a header that TestAuthHandler can read
        var claimsEncoded = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                string.Join(";", claims.Select(c => $"{c.Type}={c.Value}"))));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestAuthHandler.SchemeName, claimsEncoded);
        return client;
    }

    public async Task SeedAsync(Action<MineWatchDbContext> seedAction)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MineWatchDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        seedAction(dbContext);
        await dbContext.SaveChangesAsync();
    }
}
