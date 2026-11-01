using System.Threading.Channels;
using Core.Kernel.Models;

namespace Integration.API.Jobs;

/// <summary>
///   Jobs started on this API replica, waiting for <see cref="JobWorker" />.
/// </summary>
public sealed class JobQueue {
  private readonly Channel<Job> channel_ = Channel.CreateUnbounded<Job>();

  /// <summary>The jobs to run.</summary>
  public ChannelReader<Job> Reader => channel_.Reader;

  /// <summary>Hands a started job to the worker.</summary>
  public void Enqueue(Job job) {
    channel_.Writer.TryWrite(job);
  }
}
