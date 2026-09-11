using Microsoft.AspNetCore.Mvc;
using NecroWiKi.Application.Interfaces;
using NecroWiKi.Application.Models;

namespace NecroWiKi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegisterController : ControllerBase
    {
        private readonly IRegisterService _registerService;

        public RegisterController(
            IRegisterService registerService
            )
        {
            _registerService = registerService;
        }


        [HttpPost("wow")]
        public async Task<IActionResult> RegisterWoW(RegisterModel model)
        {
            string result = await _registerService.RegisterWoWAsync(model);
            return Ok(new { message = result });
        }

        [HttpPost("ragnarok")]
        public async Task<IActionResult> RegisterRagnarok(RegisterModel model)
        {
            string result = await _registerService.RegisterRagnarokAsync(model);
            return Ok(new { message = result });
        }
    }
}
