using System.Security.Authentication;
using System.Security.Claims;
using Core.Infrastructure.Models;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Core.Services;
using Integration.API.Input;
using Integration.API.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ILogger = Serilog.ILogger;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ArtifactController : ControllerBase {
  private readonly IArtifactService aps_;
  private readonly ICoreDatabase database_;
  private readonly ILogger log_;

  public ArtifactController(IArtifactService aps, ICoreDatabase database,
                            ILogger log) {
    database_ = database;
    aps_ = aps;
    log_ = log.ForContext<ArtifactController>();
  }

  // GET: api/Artifact
  [HttpGet("all")]
  public async Task<IEnumerable<ArtifactSummary>> Get(
    [FromQuery] string processor,
    [FromQuery] bool only_roots) {
    IEnumerable<ArtifactSummary> artifacts =
      await database_.GetArtifactSummaries(processor, only_roots);
    return artifacts;
  }

  // GET: api/Artifact
  [HttpGet]
  public async Task<ActionResult<Artifact>> GetById(
    [FromQuery] string processor,
    [FromQuery] string id) {
    Artifact? artifact = await database_.GetArtifact(id, processor);
    if (artifact == null) {
      return NotFound("Artifact not found");
    }

    return Ok(artifact);
  }

  // POST: api/Artifact
  [HttpPost]
  public async Task<ActionResult> Post([FromBody] ArtifactInput input) {
    ClaimsPrincipal u = HttpContext.User;
    if (u?.Identity == null) {
      throw new AuthenticationException("Unauthenticated user.");
    }

    log_.Information(
      "{IdentityName} added {InputId}",
      u.Identity.Name,
      input.id
    );
    Artifact artifact =
      await database_.GetArtifact(input.id, input.processor);
    if (artifact == null) {
      artifact =
        await aps_.AddArtifact(
          input.id,
          input.processor,
          input.filter,
          input.config,
          true
        );
    } else if (!artifact.root) {
      artifact.root = true;
      await database_.UpdateArtifact(artifact);
    } else {
      return Ok(
        new {
          Message = $"{input.processor}/{input.id} already Exists!"
        }
      );
    }

    Processor proc = await database_.GetProcessor(input.processor);
    if (proc.direct_collect) {
      await aps_.Collect(input.id, input.processor);
    } else {
      await aps_.Ingest(artifact);
    }

    return Ok(input);
  }

  // POST: api/Artifact/track
  [HttpPost("track")]
  public async Task<ActionResult>
    Track([FromBody] ArtifactTrackInput request) {
    if (await aps_.Track(request.id, request.processor)) {
      return Ok($"{request.processor}->{request.id} being reprocessed");
    }

    return BadRequest("Something went wrong");
  }

  [HttpPost("track/all")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> TrackAll() {
    await aps_.Track();
    return Ok("Triggered re-tracking");
  }

  [HttpPost("validate/all")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> ValidateAllArtifacts() {
    await aps_.Validate();
    return Ok("Validating all artifacts!");
  }

  [HttpPost("validate")]
  public async Task<ActionResult> ValidateArtifact(
    [FromBody] ArtifactValidationInput input) {
    await aps_.Validate(input.id, input.processor, input.force);
    return Ok($"Validating {input.id} artifacts!");
  }

  // DELETE: api/Artifact/
  [HttpDelete]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Delete([FromBody] DeleteArtifactInput input) {
    Artifact artifact = await database_.GetArtifact(input.id, input.processor);
    if (artifact == null) {
      return NotFound();
    }

    if (!await database_.DeleteArtifact(artifact)) {
      return Problem();
    }

    return Ok(artifact);
  }

  [HttpPost("collect")]
  public async Task<ActionResult> Collect(ArtifactCollectRequest request) {
    log_.Information("Collecting {RequestLocation}", request.location);
    await aps_.Collect(request.location, request.module);
    return Ok("OK");
  }
}