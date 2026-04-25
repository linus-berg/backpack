using Core.Kernel.Messages;
using Core.Kernel.Models;
using Core.Services;
using MassTransit;

namespace Core.Gateway;

public class GatewayProcessingService : IGatewayProcessingService {
  private readonly IArtifactService aps_;
  private readonly ICoreCache cache_;
  private readonly ICoreDatabase db_;
  private readonly ILogger<GatewayProcessingService> logger_;

  public GatewayProcessingService(IArtifactService aps, ICoreDatabase db,
                                  ICoreCache cache,
                                  ILogger<GatewayProcessingService> logger) {
    db_ = db;
    cache_ = cache;
    aps_ = aps;
    logger_ = logger;
  }

  public async Task ProcessArtifact(
    ConsumeContext<ArtifactProcessedRequest> context) {
    ArtifactProcessedRequest request = context.Message;
    Artifact artifact = request.artifact;
    Artifact? stored = await db_.GetArtifact(artifact.id, artifact.processor);

    bool changed = stored == null || !AreDeepEqual(stored, artifact);

    /* If the artifact changed, we need to collect new data */
    if (changed) {
      await db_.UpdateArtifact(artifact);
      /* Collecting artifact files due to artifact being updated */
      await Collect(request);
      logger_.LogInformation("ARTIFACT:UPDATED:{ArtifactId}", artifact.id);
    }

    if (stored != null && !changed) {
      /* If everything is identical, end */
      return;
    }

    /* Process all dependencies not already processed in this context */
    HashSet<ArtifactDependency> dependencies = artifact.dependencies;
    foreach (ArtifactDependency dependency in dependencies) {
      if (await cache_.InCache(dependency.id, request.context)) {
        continue;
      }

      /* Memorize this dependency */
      Artifact dep =
        await aps_.AddArtifact(
          dependency.id,
          dependency.processor,
          "",
          dependency.config
        );
      await aps_.Process(dep, request.context);
    }
  }

  private bool AreDeepEqual(Artifact a, Artifact b) {
    // Basic fields
    if (a.id != b.id ||
        a.processor != b.processor ||
        a.filter != b.filter ||
        a.root != b.root) {
      return false;
    }

    // Config dictionary comparison
    if (!DictionariesAreEqual(a.config, b.config)) {
      return false;
    }

    // Versions dictionary comparison
    if (a.versions.Count != b.versions.Count) {
      return false;
    }

    foreach (KeyValuePair<string, ArtifactVersion> kv in a.versions) {
      if (!b.versions.TryGetValue(kv.Key, out ArtifactVersion? b_val)) {
        return false;
      }

      // We could do deeper here if ArtifactVersion has complex nested data
      if (kv.Value.status != b_val.status) {
        return false;
      }

      if (kv.Value.files.Count != b_val.files.Count) {
        return false;
      }
    }

    // Dependencies comparison
    if (a.dependencies.Count != b.dependencies.Count) {
      return false;
    }

    foreach (ArtifactDependency dep in a.dependencies) {
      if (!b.dependencies.Contains(dep)) {
        return false;
      }
    }

    return true;
  }

  private bool DictionariesAreEqual<TKey, TValue>(
    IDictionary<TKey, TValue> dict1, IDictionary<TKey, TValue> dict2) {
    if (dict1.Count != dict2.Count) {
      return false;
    }

    foreach (KeyValuePair<TKey, TValue> kv in dict1) {
      if (!dict2.TryGetValue(kv.Key, out TValue? value) ||
          !EqualityComparer<TValue>.Default.Equals(kv.Value, value)) {
        return false;
      }
    }

    return true;
  }

  private async Task Collect(ArtifactProcessedRequest request) {
    Artifact artifact = request.artifact;
    foreach (ArtifactCollectRequest collect in request.collect_requests) {
      await aps_.Collect(collect);
    }

    await aps_.Route(artifact);
  }
}