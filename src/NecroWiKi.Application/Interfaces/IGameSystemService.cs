using NecroWiKi.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace NecroWiKi.Application.Interfaces
{
    public interface IGameSystemService
    {
        Task<List<GameListDTO>> GetGameList(string system);
    }
}
