using Microsoft.Extensions.Options;
using NecroWiKi.Application.Enums;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;
using System.Text;

namespace NecroWiKi.Application.Services
{
    public class LoggerService : ILoggerService
    {
        private readonly object _loggerLock = new();
        private readonly LoggerSettings _settings;

        public LoggerService(IOptions<LoggerSettings> options)
        {
            _settings = options.Value;
        }

        public void Info(string message, LogContext? context = null)
        {
            Write(LoggerLevel.Info, message, context);
        }

        public void Warn(string message, LogContext? context = null)
        {
            Write(LoggerLevel.Warn, message, context);
        }

        public void Error(string message, Exception ex, LogContext? context = null)
        {
            Write(LoggerLevel.Error, message, context, ex);
        }

        public void Error(Exception ex, LogContext? context = null)
        {
            Write(LoggerLevel.Error, ex.Message, context, ex);
        }

        private void Write(LoggerLevel level, string message, LogContext? context = null, Exception? exception = null)
        {
            StringBuilder sb = new();

            sb.AppendLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss:fff} [{level.ToString().ToUpper()}]");

            if (!string.IsNullOrWhiteSpace(context?.ClassName))
            {
                sb.AppendLine($"Classe: {context.ClassName}");
            }

            if (!string.IsNullOrWhiteSpace(context?.MethodName))
            {
                sb.AppendLine($"Método: {context.MethodName}");
            }

            if (!string.IsNullOrWhiteSpace(context?.HttpMethod) || !string.IsNullOrWhiteSpace(context?.Route))
            {
                sb.AppendLine($"Request: {context?.HttpMethod} {context?.Route}");
            }

            if (!string.IsNullOrWhiteSpace(context?.User))
            {
                sb.AppendLine($"Usuário: {context.User}");
            }

            sb.AppendLine($"Mensagem: {message}");

            if (exception != null)
            {
                AppendException(sb, exception);
            }

            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine();

            SaveToFile(sb.ToString());
        }

        private void SaveToFile(string content)
        {
            DateTime now = DateTime.Now;

            string yearFolder = Path.Combine(_settings.RootPath, now.Year.ToString());
            string monthFolder = Path.Combine(yearFolder, GetMonthName(now.Month));

            Directory.CreateDirectory(monthFolder);

            string filePath = Path.Combine(monthFolder, $"{now.Day:00}.log");

            lock (_loggerLock)
            {
                File.AppendAllText(filePath, content, Encoding.UTF8);
            }
        }

        private void AppendException(StringBuilder sb, Exception exception, int level = 0)
        {
            string indent = new(' ', level * 2);

            sb.AppendLine();
            sb.AppendLine($"{indent}Exception:");
            sb.AppendLine($"{indent}{exception.Message}");

            sb.AppendLine();
            sb.AppendLine($"{indent}StackTrace:");
            sb.AppendLine($"{indent}------------------");
            sb.AppendLine(exception.StackTrace);

            if (exception.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine($"{indent}InnerException:");
                sb.AppendLine($"{indent}------------------");

                AppendException(sb, exception.InnerException, level + 1);
            }
        }

        private string GetMonthName(int month)
        {
            switch (month)
            {
                case 1: return "Janeiro";
                case 2: return "Fevereiro";
                case 3: return "Marco";
                case 4: return "Abril";
                case 5: return "Maio";
                case 6: return "Junho";
                case 7: return "Julho";
                case 8: return "Agosto";
                case 9: return "Setembro";
                case 10: return "Outubro";
                case 11: return "Novembro";
                case 12: return "Dezembro";
                default: throw new ArgumentException("Mes inválido");
            }
        }
    }
}