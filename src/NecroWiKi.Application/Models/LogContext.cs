using System;
using System.Collections.Generic;
using System.Text;

namespace NecroWiKi.Application.Models
{
    public class LogContext
    {
        public string? ClassName { get; set; }
        public string? MethodName { get; set; }
        
        public string? HttpMethod { get; set; }
        public string? Route { get; set; }

        public string? User { get; set; }
    }
}
