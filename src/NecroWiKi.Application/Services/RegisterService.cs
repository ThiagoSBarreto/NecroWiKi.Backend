using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NecroWiKi.Application.Services
{
    public class RegisterService : IRegisterService
    {
        public Task<string> RegisterWoWAsync(RegisterModel model)
        {
            throw new NotImplementedException();
        }

        public Task<string> RegisterRagnarokAsync(RegisterModel model)
        {
            throw new NotImplementedException();
        }
    }
}
