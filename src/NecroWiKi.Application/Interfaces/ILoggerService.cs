using NecroWiKi.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NecroWiKi.Application.Interfaces
{
    public interface ILoggerService
    {
        void Info(string message, LogContext? context = null);
        void Warn(string message, LogContext? context = null);
        void Error(string message, Exception ex, LogContext? context = null);
    }
}
