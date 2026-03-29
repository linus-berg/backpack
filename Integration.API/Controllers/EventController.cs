using Core.Kernel.Models;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class EventController : ControllerBase {
  private readonly ICoreDatabase database_;

  public EventController(ICoreDatabase database) {
    database_ = database;
  }

  [HttpGet]
  public async Task<IEnumerable<Event>> Get([FromQuery] int limit = 100) {
    return await database_.GetEvents(limit);
  }
}