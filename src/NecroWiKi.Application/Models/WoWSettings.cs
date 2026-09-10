using System;
using System.Collections.Generic;
using System.Text;

namespace NecroWiKi.Application.Models
{
    public class WoWSettings
    {
        public string ConnectionStringName { get; set; } = "WoW";

        public int Expansion { get; set; } = 2;

        public int Generator { get; set; } = 7;

        public string Modulus { get; set; } =
            "894B645E89E1535BBDAD5B8B290650530801B18EBFBF5E8FAB3C82872A3E9BB7";
    }
}
