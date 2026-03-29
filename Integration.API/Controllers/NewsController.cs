using Core.Kernel.Models;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class NewsController : ControllerBase {
  private readonly ICoreDatabase database_;

  public NewsController(ICoreDatabase database) {
    database_ = database;
  }

  [HttpGet]
  public async Task<IEnumerable<NewsPost>> Get([FromQuery] int limit = 50) {
    return await database_.GetNewsPosts(limit);
  }

  [HttpPost]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Post([FromBody] NewsPost post) {
    post.author = User.Identity?.Name ?? "System";
    post.timestamp = DateTime.UtcNow;
    await database_.AddNewsPost(post);
    return Ok(post);
  }

  [HttpDelete("{id}")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Delete(string id) {
    bool result = await database_.DeleteNewsPost(id);
    if (result) {
      return Ok();
    }

    return NotFound();
  }
}