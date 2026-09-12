
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Extensions;
using NecroWiKi.Application.Models;
using NecroWiKi.Application.Services;

namespace NecroWiKi.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            builder.Services.AddSingleton<ILoggerService, LoggerService>();

            builder.Services.AddScoped<IRegisterService, RegisterService>();
            builder.Services.AddScoped<IGameSystemService, GameSystemService>();

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
