using Core.Kernel.Models;
using Core.Services;
using Integration.API.Input;
using Integration.API.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Controllers;

/// <summary>
///   Background jobs: re-tracking and validating many artifacts. Only one
///   runs at a time.
/// </summary>
/* [Authorize] on the class and the role per action, as in
   ArtifactController, so admin API keys work too (a class-level role alone
   only accepts bearer tokens). */
[Route("api/jobs")]
[ApiController]
[Authorize]
public class JobsController : ControllerBase {
  private readonly IJobService jobs_;
  private readonly JobStarter starter_;

  public JobsController(IJobService jobs, JobStarter starter) {
    jobs_ = jobs;
    starter_ = starter;
  }

  /// <summary>
  ///   The newest jobs first, running and waiting ones included.
  /// </summary>
  [Authorize(Roles = "Administrator")]
  [HttpGet]
  public async Task<IReadOnlyList<Job>> List([FromQuery] int limit = 50) {
    return await jobs_.List(Math.Clamp(limit, 1, 200));
  }

  [Authorize(Roles = "Administrator")]
  [HttpGet("{id}")]
  public async Task<ActionResult<Job>> Get(string id) {
    Job? job = await jobs_.Get(id);
    return job == null ? NotFound(new { message = "No such job." }) : job;
  }

  /// <summary>
  ///   Starts a job: 202 with the job, or 409 with the job already running.
  /// </summary>
  [Authorize(Roles = "Administrator")]
  [HttpPost]
  public async Task<ActionResult> Start([FromBody] JobInput input) {
    return await starter_.Start(
             input.kind,
             string.IsNullOrWhiteSpace(input.processor) ? null : input.processor,
             User.Identity?.Name ?? "Unknown"
           );
  }

  /// <summary>
  ///   Cancels a waiting job, or asks the running one to stop after the
  ///   artifact it is sending.
  /// </summary>
  [Authorize(Roles = "Administrator")]
  [HttpPost("{id}/cancel")]
  public async Task<ActionResult> Cancel(string id) {
    return await jobs_.RequestCancel(id, User.Identity?.Name ?? "Unknown") switch {
      JobCancel.NOT_FOUND => NotFound(new { message = "No such job." }),
      JobCancel.NOT_ACTIVE => Conflict(new { message = "The job has already ended." }),
      _ => Accepted(await jobs_.Get(id))
    };
  }
}
