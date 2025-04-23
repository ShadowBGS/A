using Microsoft.AspNetCore.Mvc;

namespace Library.Controllers
{
  [ApiController]
    [Route("api/[controller]")]
    public class PingController : ControllerBase
  {
      [HttpGet]
      public IActionResult Get() => Ok("pong");
  }
}
