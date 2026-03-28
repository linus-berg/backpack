using Core.Infrastructure.Models;
using Core.Kernel.Models;

namespace Core.Services;

public interface ICoreDatabase {
  public Task AddProcessor(Processor processor);
  public Task AddArtifact(Artifact artifact);
  public Task<bool> UpdateArtifact(Artifact artifact);
  public Task<bool> UpdateProcessor(Processor processor);
  public Task<Processor> GetProcessor(string processor);
  public Task<IEnumerable<Processor>> GetProcessors();
  public Task<Artifact> GetArtifact(string name, string processor);

  public Task<IEnumerable<Artifact>> GetArtifacts(
    string processor, bool only_roots = true);
  public Task<IEnumerable<ArtifactSummary>> GetArtifactSummaries(
    string processor, bool only_roots = true);

  public Task<bool> DeleteArtifact(Artifact artifact);
  public Task<bool> DeleteProcessor(string processor_id);
}