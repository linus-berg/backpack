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
      Configuration.GetBackpackVariable(
        CoreVariables.BP_COLLECTOR_CONTAINER_REGISTRY
      );

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
    logger_.LogInformation("Pull> {ImageUri}=>{InternalImage}", image.Uri, internal_image);
    try {
      CommandResult result =
        await cmd.ExecuteAsync();
    } catch (Exception) {
      logger_.LogError("{Error}", std_err.ToString());
      throw;
    }

    return true;
  }

  public async Task<SkopeoArchive> CopyToTar(string remote_image,
                                             string target_dir,
                                             bool force = false) {
    /* Extract tag */
    /* Image must always include docker-archive:// NOT docker:// */
    string img = remote_image.Replace("docker-archive://", "");
    string[] tag_split = img.Split(":");
    
    /* If no tag has been specified */
    if (tag_split.Length <= 1) {
      throw new SkopeoTagMissingException("No tag specified");
    }
    string tag = tag_split[1];
    string docker_str = remote_image.Replace("docker-archive://", "docker://");

    logger_.LogInformation(
      "Attempting to fetch manifest. Image: {Image} tag: {Tag}",
      img,
      tag
    );
    
    SkopeoManifest? manifest = await GetManifest(docker_str);
    /* If manifest cannot be found */
    if (manifest == null) {
      throw new ApplicationException($"Manifest for {docker_str} not found");
    }

    SkopeoArchive archive = new(manifest.name, tag, target_dir);
    if (File.Exists(archive.tar_path)) {
      if (force) {
        logger_.LogInformation("Force flag present, deleting existing archive: {Path}", archive.tar_path);
        File.Delete(archive.tar_path);
      } else {
        throw new SkopeoArchiveExistsException(
          $"File {archive.tar_path} already exists"
        );
      }
    }

    string internal_image =
      $"docker-archive:{archive.tar_path}:{archive.target}";

    StringBuilder std_out = new();
    StringBuilder std_err = new();
    Command cmd = Cli.Wrap("skopeo")
                     .WithArguments(
                       args => {
                         args.Add("copy");
                         args.Add($"docker://{archive.target}:{archive.tag}");
                         args.Add(internal_image);
                       }
                     )
                     .WithStandardOutputPipe(
                       PipeTarget.ToStringBuilder(std_out)
                     )
                     .WithStandardErrorPipe(
                       PipeTarget.ToStringBuilder(std_err)
                     );
    logger_.LogInformation("Pull> {RemoteImage}=>{InternalImage}", remote_image, internal_image);
    CommandResult? result = null;
    try {
      result = await cmd.ExecuteAsync();
    } catch (Exception e) {
      logger_.LogError("{Error}", e.ToString());
    }

    logger_.LogInformation("{StdOut}", std_out.ToString());
    logger_.LogInformation("{StdErr}", std_err.ToString());

    if (result is { IsSuccess: false }) {
      throw new ApplicationException("Skopeo exception");
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
    SkopeoListTagsOutput? tags;
    try {
      tags = await cmd.ExecuteWithResult<SkopeoListTagsOutput>();
    } catch (Exception e) {
      logger_.LogError(e, "Skopeo Error");
      return null;
    }

    return tags;
  }

  public async Task<SkopeoManifest?> ImageExists(string input) {
    Image image = new(input);
    string? registry =
      Configuration.GetBackpackVariable(
        CoreVariables.BP_COLLECTOR_CONTAINER_REGISTRY
      );
    return await GetManifest($"docker://{registry}/{image.Repository}");
  }

  private async Task<SkopeoManifest?> GetManifest(string image) {
    StringBuilder std_out = new();
    StringBuilder std_err = new();
    Command cmd = Cli.Wrap("skopeo")
                     .WithArguments(
                       args => {
                         args.Add("inspect");
                         args.Add("--tls-verify=false");
                         args.Add(image);
                       }
                     )
                     .WithStandardOutputPipe(
                       PipeTarget.ToStringBuilder(std_out)
                     )
                     .WithStandardErrorPipe(
                       PipeTarget.ToStringBuilder(std_err)
                     );
    SkopeoManifest? manifest;
    try {
      manifest = await cmd.ExecuteWithResult<SkopeoManifest>();
    } catch (Exception e) {
      logger_.LogError(e, "Skopeo exception");
      logger_.LogInformation("{StdOut}", std_out.ToString());
      logger_.LogInformation("{StdErr}", std_err.ToString());
      return null;
    }
    return manifest;
  }
}