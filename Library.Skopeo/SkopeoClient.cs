using System.Text;
using CliWrap;
using Core.Kernel;
using Core.Kernel.Extensions;
using Library.Skopeo.Exceptions;
using Library.Skopeo.Models;
using Microsoft.Extensions.Logging;

namespace Library.Skopeo;

public class SkopeoClient {
  private readonly ILogger<SkopeoClient> logger_;

  public SkopeoClient(ILogger<SkopeoClient> logger) {
    logger_ = logger;
  }

  public async Task<bool> CopyToRegistry(string remote_image) {
    Image image = new(remote_image);
    string? registry =
      Configuration.GetBackpackVariable(CoreVariables.BP_COLLECTOR_CONTAINER_REGISTRY);

    string internal_image = $"docker://{registry}/{image.Repository}";
    StringBuilder std_out = new();
    StringBuilder std_err = new();
    Command cmd = Cli.Wrap("skopeo")
                     .WithArguments(
                       args => {
                         args.Add("copy");
                         args.Add("--dest-tls-verify=false");
                         args.Add(image.Uri);
                         args.Add(internal_image);
                       }
                     )
                     .WithStandardOutputPipe(
                       PipeTarget.ToStringBuilder(std_out)
                     )
                     .WithStandardErrorPipe(
                       PipeTarget.ToStringBuilder(std_err)
                     );
    logger_.LogInformation($"Pull> {image.Uri}=>{internal_image}");
    try {
      CommandResult result =
        await cmd.ExecuteAsync();
    } catch (Exception e) {
      logger_.LogError(std_err.ToString());
      throw;
    }

    return true;
  }

  public async Task<SkopeoArchive> CopyToTar(string remote_image,
                                             string target_dir) {
    SkopeoArchive archive = new(remote_image, target_dir);
    if (File.Exists(archive.TarPath)) {
      throw new SkopeoArchiveExistsException(
        $"File {archive.TarPath} already exists"
      );
    }

    string internal_image =
      $"docker-archive:{archive.TarPath}:{archive.Target}";

    StringBuilder std_out = new();
    StringBuilder std_err = new();
    Command cmd = Cli.Wrap("skopeo")
                     .WithArguments(
                       args => {
                         args.Add("copy");
                         args.Add($"docker://{archive.Target}");
                         args.Add(internal_image);
                       }
                     )
                     .WithStandardOutputPipe(
                       PipeTarget.ToStringBuilder(std_out)
                     )
                     .WithStandardErrorPipe(
                       PipeTarget.ToStringBuilder(std_err)
                     );
    logger_.LogInformation($"Pull> {remote_image}=>{internal_image}");
    CommandResult? result = null;
    try {
      result = await cmd.ExecuteAsync();
    } catch (Exception e) {
      logger_.LogError("{Error}", e.ToString());
    }
    
    logger_.LogInformation("{StdOut}", std_out.ToString());
    logger_.LogInformation("{StdErr}", std_err.ToString());
    
    if (result is { IsSuccess: false }) {
      throw new ExecutionEngineException("Skopeo exception");
    }

    return archive;
  }

  public async Task<SkopeoListTagsOutput?> GetTags(string image) {
    Command cmd = Cli.Wrap("skopeo")
                     .WithArguments(
                       args => {
                         args.Add("list-tags");
                         args.Add($"docker://{image}");
                       }
                     );
    SkopeoListTagsOutput tags;
    try {
      tags = await cmd.ExecuteWithResult<SkopeoListTagsOutput>();
    } catch (Exception e) {
      logger_.LogError(e.ToString());
      return null;
    }

    return tags;
  }

  public async Task<SkopeoManifest?> ImageExists(string input) {
    Image image = new(input);
    string? registry =
      Configuration.GetBackpackVariable(CoreVariables.BP_COLLECTOR_CONTAINER_REGISTRY);
    Command cmd = Cli.Wrap("skopeo")
                     .WithArguments(
                       args => {
                         args.Add("inspect");
                         args.Add("--tls-verify=false");
                         args.Add($"docker://{registry}/{image.Repository}");
                       }
                     );
    SkopeoManifest manifest;
    try {
      manifest = await cmd.ExecuteWithResult<SkopeoManifest>();
    } catch (Exception e) {
      return null;
    }

    return manifest;
  }
}