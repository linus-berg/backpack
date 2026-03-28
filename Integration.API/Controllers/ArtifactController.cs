using System.Security.Authentication;
using System.Security.Claims;
using Core.Infrastructure.Models;
using Core.Kernel;
using Core.Kernel.Messages;
using Core.Kernel.Models;
using Core.Services;
using Integration.API.Input;
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
  private readonly IEventService event_service_;
  private readonly ILogger log_;

  public ArtifactController(IArtifactService aps, ICoreDatabase database, IEventService event_service,
                            ILogger log) {
    database_ = database;
    aps_ = aps;
    event_service_ = event_service;
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

    await event_service_.LogEvent(
      "API",
      $"Artifact {input.processor}/{input.id} was added",
      EventSeverity.SUCCESS,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );

    return Ok(input);
  }

  // POST: api/Artifact/track
  [HttpPost("track")]
  public async Task<ActionResult>
    Track([FromBody] ArtifactTrackInput request) {
    if (await aps_.Track(request.id, request.processor)) {
      await event_service_.LogEvent(
        "API",
        $"Re-track triggered for {request.processor}/{request.id}",
        EventSeverity.INFO,
        HttpContext.User.Identity?.Name ?? "Unknown"
      );
      return Ok($"{request.processor}->{request.id} being reprocessed");
    }

    return BadRequest("Something went wrong");
  }

  [HttpPost("track/all")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> TrackAll() {
    await aps_.Track();
    await event_service_.LogEvent(
      "API",
      "Global re-track triggered",
      EventSeverity.WARNING,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    return Ok("Triggered re-tracking");
  }

  [HttpPost("validate/all")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> ValidateAllArtifacts() {
    await aps_.Validate();
    await event_service_.LogEvent(
      "API",
      "Global validation triggered",
      EventSeverity.WARNING,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    return Ok("Validating all artifacts!");
  }

  [HttpPost("validate")]
  public async Task<ActionResult> ValidateArtifact(
    [FromBody] ArtifactValidationInput input) {
    await aps_.Validate(input.id, input.processor, input.force);
    await event_service_.LogEvent(
      "API",
      $"Validation triggered for {input.processor}/{input.id}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
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

    await event_service_.LogEvent(
      "API",
      $"Artifact {input.processor}/{input.id} was deleted",
      EventSeverity.WARNING,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );

    return Ok(artifact);
  }

  [HttpPost("collect")]
  public async Task<ActionResult> Collect(ArtifactCollectRequest request) {
    log_.Information("Collecting {RequestLocation}", request.location);
    await aps_.Collect(request.location, request.module);
    await event_service_.LogEvent(
      "API",
      $"Manual collection triggered for {request.module}/{request.location}",
      EventSeverity.INFO,
      HttpContext.User.Identity?.Name ?? "Unknown"
    );
    return Ok("OK");
  }
}
