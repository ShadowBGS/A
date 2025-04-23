namespace Library.Controllers
{
  [ApiController]
  [Route("[controller]")]
  public class PingController : ControllerBase
  {
      [HttpGet]
      public IActionResult Get() => Ok("pong");
  }
}
