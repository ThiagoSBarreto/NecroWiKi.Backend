using NecroWiKi.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NecroWiKi.Application.Interfaces
{
    public interface IRegisterService
    {
        Task<string> RegisterWoWAsync(RegisterModel model);
        Task<string> RegisterRagnarokAsync(RegisterModel model);
    }
}
