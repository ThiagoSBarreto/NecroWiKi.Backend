using Microsoft.AspNetCore.Builder;
using NecroWiKi.Application.Middleware;
using System;
using System.Collections.Generic;
using System.Text;

namespace NecroWiKi.Application.Extensions
{
    public static  class ExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(
            this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionMiddleware>();
        }
    }
}
