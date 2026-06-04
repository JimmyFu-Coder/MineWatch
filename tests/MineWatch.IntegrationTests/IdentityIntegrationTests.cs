using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MineWatch.Api.Controllers;
using MineWatch.Infrastructure.Data;

namespace MineWatch.IntegrationTests;

public class IdentityIntegrationTests
{
    private async Task<(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, AuthController controller)> CreateAuthController(string dbName)
    {
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName).Options;

        var services = new ServiceCollection();
        services.AddDbContext<MineWatchDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<MineWatchDbContext>();
        services.AddLogging();
        services.AddAuthentication();
        var provider = services.BuildServiceProvider();

        var userManager = provider.GetRequiredService<UserManager<IdentityUser>>();
        var signInManager = provider.GetRequiredService<SignInManager<IdentityUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

        // Seed roles
        foreach (var role in new[] { "Admin", "Operator", "Viewer" })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-secret-key-at-least-16-chars",
                ["Jwt:Issuer"] = "Test",
                ["Jwt:Audience"] = "Test"
            })
            .Build();

        var controller = new AuthController(userManager, signInManager, configuration);
        return (userManager, roleManager, controller);
    }

    [Fact]
    public async Task Register_CreatesUser_Successfully()
    {
        var (userManager, _, controller) = await CreateAuthController("Identity_Register");

        var result = await controller.Register(new RegisterRequest("testuser", "Test@12345", null));

        var okResult = Assert.IsType<OkObjectResult>(result);
        var user = await userManager.FindByNameAsync("testuser");
        Assert.NotNull(user);
    }

    [Fact]
    public async Task Register_DuplicateUser_ReturnsBadRequest()
    {
        var (_, _, controller) = await CreateAuthController("Identity_Duplicate");

        await controller.Register(new RegisterRequest("dupuser", "Test@12345", null));
        var result = await controller.Register(new RegisterRequest("dupuser", "Test@12345", null));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var (_, _, controller) = await CreateAuthController("Identity_Login");

        await controller.Register(new RegisterRequest("loginuser", "Test@12345", null));
        var result = await controller.Login(new RegisterRequest("loginuser", "Test@12345", null));

        var okResult = Assert.IsType<OkObjectResult>(result);
        var tokenProp = okResult.Value!.GetType().GetProperty("token");
        Assert.NotNull(tokenProp);
        var token = tokenProp!.GetValue(okResult.Value) as string;
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var (_, _, controller) = await CreateAuthController("Identity_BadPw");

        await controller.Register(new RegisterRequest("badpwuser", "Test@12345", null));
        var result = await controller.Login(new RegisterRequest("badpwuser", "WrongPassword", null));

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Register_WithRole_AssignsCorrectRole()
    {
        var (userManager, roleManager, controller) = await CreateAuthController("Identity_Role");

        await roleManager.CreateAsync(new IdentityRole("Operator"));
        await controller.Register(new RegisterRequest("opuser", "Test@12345", "Operator"));

        var user = await userManager.FindByNameAsync("opuser");
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user!);
        Assert.Contains("Operator", roles);
    }
}
