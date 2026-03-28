using System.Text.Json;
using Core.Kernel.Models;
using Core.Services;
using Integration.API.Input;
using Integration.API.Output;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Integration.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProcessorController : ControllerBase {
  private readonly IArtifactService aps_;
  private readonly ICoreDatabase database_;

  public ProcessorController(IArtifactService aps, ICoreDatabase database) {
    database_ = database;
    aps_ = aps;
  }

  [HttpGet("processors")]
  public async Task<IEnumerable<ProcessorOutput>> GetProcessors() {
    IEnumerable<Processor> processors = await database_.GetProcessors();
    List<ProcessorOutput> proc_out = new();

    foreach (Processor processor in processors) {
      proc_out.Add(
        new ProcessorOutput {
          id = processor.id,
          config = processor.config.ToJson(),
          description = processor.description,
          direct_collect = processor.direct_collect
        }
      );
    }

    return proc_out;
  }

  [HttpPost("update")]
  [Authorize(Roles = "Administrator")]
  public async Task<Processor> UpdateProcessor(
    [FromBody] UpdateProcessorInput input) {
    Processor processor = await database_.GetProcessor(input.processor_id);

    processor.description = input.description;
    processor.direct_collect = input.direct_collect;
    if (!string.IsNullOrEmpty(input.config)) {
      processor.config =
        JsonSerializer.Deserialize<
          Dictionary<string, ProcessorAuxiliaryField>>(input.config) ??
        new Dictionary<string, ProcessorAuxiliaryField>();
    }

    await database_.UpdateProcessor(processor);
    await database_.AddEvent(
      new Event {
        source = "API",
        message = $"Processor {processor.id} was updated",
        severity = EventSeverity.INFO,
        user = HttpContext.User.Identity?.Name ?? "Unknown"
      }
    );
    return processor;
  }

  [HttpDelete("{id}")]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> DeleteProcessor(string id) {
    bool result = await database_.DeleteProcessor(id);
    if (result) {
      await database_.AddEvent(
        new Event {
          source = "API",
          message = $"Processor {id} was deleted",
          severity = EventSeverity.WARNING,
          user = HttpContext.User.Identity?.Name ?? "Unknown"
        }
      );
    }

    return result ? Ok() : BadRequest();
  }

  [HttpPost]
  [Authorize(Roles = "Administrator")]
  public async Task<ActionResult> Post([FromBody] AddProcessorInput input) {
    await database_.AddProcessor(
      new Processor {
        id = input.processor_id,
        description = "",
        config = new Dictionary<string, ProcessorAuxiliaryField>()
      }
    );
    await database_.AddEvent(
      new Event {
        source = "API",
        message = $"Processor {input.processor_id} was created",
        severity = EventSeverity.SUCCESS,
        user = HttpContext.User.Identity?.Name ?? "Unknown"
      }
    );
    return Ok(
      new {
        Message = $"Added {input.processor_id}!"
      }
    );
  }
}