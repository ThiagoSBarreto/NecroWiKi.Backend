
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Extensions;
using NecroWiKi.Application.Models;
using NecroWiKi.Application.Services;
using NecroWiKi.API.Services;
using NecroWiKi.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using NecroWiKi.Infrastructure.Persistence;

namespace NecroWiKi.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            string? jwtKey = builder.Configuration["Authentication:JwtKey"];
            if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
            {
                throw new InvalidOperationException("Configure Authentication:JwtKey com pelo menos 32 bytes.");
            }

            string jwtIssuer = builder.Configuration["Authentication:Issuer"] ?? "NecroWiKi";
            string jwtAudience = builder.Configuration["Authentication:Audience"] ?? "NecroWiKi";

            builder.Services.AddControllers();

            builder.Services.AddSingleton<ILoggerService, LoggerService>();

            builder.Services.AddScoped<IRegisterService, RegisterService>();
            builder.Services.AddScoped<IGameSystemService, GameSystemService>();
            builder.Services.AddScoped<IDownloadService, DownloadService>();
            builder.Services.AddScoped<ISaveStateService, SaveStateService>();
            builder.Services.AddScoped<ISaveStateIdentityService, SaveStateIdentityService>();
            builder.Services.AddScoped<PortalIdentityService>();
            builder.Services.Configure<TutorialStorageSettings>(builder.Configuration.GetSection("Tutorials"));
            builder.Services.AddScoped<ITutorialService, TutorialService>();

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        NameClaimType = ClaimTypes.Name,
                        RoleClaimType = ClaimTypes.Role
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async context =>
                        {
                            string? accountIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                            string? roleClaim = context.Principal?.FindFirstValue(ClaimTypes.Role);
                            if (!Guid.TryParse(accountIdClaim, out Guid accountId) || string.IsNullOrWhiteSpace(roleClaim))
                            {
                                context.Fail("Token de conta inválido.");
                                return;
                            }

                            PortalIdentityService identityService = context.HttpContext.RequestServices
                                .GetRequiredService<PortalIdentityService>();
                            PortalAccountSummary? account = await identityService.GetAccountAsync(
                                accountId, context.HttpContext.RequestAborted);
                            if (account is null || !string.Equals(account.Role, roleClaim, StringComparison.Ordinal))
                            {
                                context.Fail("Conta inativa ou papel revogado.");
                            }
                        }
                    };
                });
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                var cs = builder.Configuration.GetConnectionString("DefaultConnection");
                options.UseMySql(cs, new MySqlServerVersion(new Version(8, 0, 36)));
            });

            builder.Services.Configure<LoggerSettings>(
                builder.Configuration.GetSection("LoggerSettings")
            );

            builder.Services.Configure<WoWSettings>(
                builder.Configuration.GetSection("Games:WoW")
            );

            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", policy =>
                {
                    policy
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
                });
            });


            var app = builder.Build();

            app.UseGlobalExceptionHandler();

            app.UseCors("CorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
