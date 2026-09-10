using NecroWiKi.Application.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace NecroWiKi.Application.Models
{
    public class RegisterModel
    {
        public GamesEnum Game { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string? Genero { get; set; }
    }
}
