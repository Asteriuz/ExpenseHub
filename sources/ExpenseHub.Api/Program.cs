using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.Tasks;
using ExpenseHub.Api.Authorization;
using ExpenseHub.Api.Data;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseHub.Api;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        JwtOptions jwtOptions = new();
        builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
        jwtOptions.Validate();
        builder.Services.AddSingleton(jwtOptions);
        builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

        builder.Services.AddDbContext<ExpenseHubDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString(ExpenseHubDbContext.ConnectionStringName)));

        builder.Services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ExpenseHubDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = ExpenseHubClaims.Role,
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationHandler>();

        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        WebApplication app = builder.Build();

        await ApplyMigrationsAndSeedAsync(app);

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("GetHealth")
            .AllowAnonymous();

        app.MapControllers();

        await app.RunAsync();
    }

    private static async Task ApplyMigrationsAndSeedAsync(WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        ExpenseHubDbContext dbContext = scope.ServiceProvider.GetRequiredService<ExpenseHubDbContext>();
        await dbContext.Database.MigrateAsync();
        await IdentitySeeder.SeedAsync(app.Services, app.Configuration);
    }
}
