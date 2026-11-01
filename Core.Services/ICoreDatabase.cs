using Core.Kernel.Models;

namespace Core.Services;

public interface ICoreDatabase {
  public Task AddProcessor(Processor processor);
  public Task AddArtifact(Artifact artifact);
  public Task<bool> UpdateArtifact(Artifact artifact);
  public Task<bool> UpdateProcessor(Processor processor);
  public Task<Processor> GetProcessor(string processor);
  public Task<IEnumerable<Processor>> GetProcessors();
  public Task<Artifact?> GetArtifact(string name, string processor);

  public Task<IEnumerable<Artifact>> GetArtifacts(
    string processor, bool only_roots = true);

  /// <summary>
  ///   Reads a processor's artifacts in batches instead of loading them all,
  ///   for jobs over large mirrors.
  /// </summary>
  /// <param name="processor">The processor ID.</param>
  /// <param name="only_roots">Root artifacts only; otherwise all of them.</param>
  /// <param name="token">Stops reading.</param>
  public IAsyncEnumerable<Artifact> StreamArtifacts(
    string processor, bool only_roots, CancellationToken token = default);

  public Task<IEnumerable<ArtifactSummary>> GetArtifactSummaries(
    string processor, bool only_roots = true);

  /// <summary>
  ///   Counts a processor's artifacts in MongoDB, without loading them.
  /// </summary>
  /// <param name="processor">The processor ID.</param>
  /// <param name="only_roots">Count root artifacts only; otherwise all of them.</param>
  /// <returns>The number of artifacts.</returns>
  public Task<long> CountArtifacts(string processor, bool only_roots = true);

  public Task<bool> DeleteArtifact(Artifact artifact);
  public Task<bool> DeleteProcessor(string processor_id);

  public Task AddEvent(Event @event);
  public Task<IEnumerable<Event>> GetEvents(int limit = 100);

  public Task<IEnumerable<Schedule>> GetSchedules();
  public Task UpdateSchedule(Schedule schedule);
  public Task AddSchedule(Schedule schedule);
  public Task<bool> DeleteSchedule(string id);

  public Task AddPendingArtifact(PendingArtifact artifact);
  public Task<IEnumerable<PendingArtifact>> GetPendingArtifacts();
  public Task<PendingArtifact?> GetPendingArtifact(string processor, string id);
  public Task<bool> DeletePendingArtifact(string processor, string id);

  public Task AddApiKey(ApiKey key);
  public Task<IEnumerable<ApiKey>> GetApiKeys();
  public Task<ApiKey> GetApiKey(string key);
  public Task<bool> DeleteApiKey(string id);

  public Task AddNewsPost(NewsPost post);
  public Task<IEnumerable<NewsPost>> GetNewsPosts(int limit = 50);
  public Task<bool> DeleteNewsPost(string id);
}