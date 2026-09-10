using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;

namespace NecroWiKi.Application.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly ILoggerService _fileLogger;

        public ExceptionMiddleware(
            RequestDelegate next,
            ILogger<ExceptionMiddleware> logger,
            ILoggerService fileLogger
            )
        {
            _next = next;
            _logger = logger;
            _fileLogger = fileLogger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (UnauthorizedAccessException ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                var response = new
                {
                    success = false,
                    message = ex.Message
                };

                await context.Response.WriteAsync(JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                _fileLogger.Error("Error while treating the request", ex,
                    new LogContext
                    {
                        ClassName = nameof(ExceptionMiddleware),
                        MethodName = nameof(InvokeAsync),
                        HttpMethod = context.Request.Method,
                        Route = context.Request.Path
                    }
                );

                var response = new
                {
                    success = false,
                    message = "Internal server error",
                    details = ex.Message
                };
                
                await context.Response.WriteAsync(JsonConvert.SerializeObject(response));
            }
        }
    }
}
