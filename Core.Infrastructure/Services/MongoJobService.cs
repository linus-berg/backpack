using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Infrastructure.Services;

/// <summary>
///   Jobs in MongoDB (<c>backpack-jobs</c>). A running job holds a lock: a
///   field with a unique (partial) index, so a second job can't start even
///   from another API replica or the scheduler. A job that stops sending
///   heartbeats for two minutes is marked interrupted and releases the lock,
///   so a crashed pod doesn't block jobs forever.
/// </summary>
public sealed class MongoJobService : IJobService {
  private const string C_LOCK_ = "artifact-jobs";

  /* Runners heartbeat every 10 seconds. */
  private static readonly TimeSpan S_STALE_AFTER_ = TimeSpan.FromMinutes(2);

  /* Finished jobs are kept this long. */
  private static readonly TimeSpan S_KEEP_ = TimeSpan.FromDays(90);

  private readonly SemaphoreSlim index_lock_ = new(1, 1);
  private readonly IMongoCollection<Job> jobs_;
  private bool indexes_ready_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="MongoJobService" /> class.
  /// </summary>
  public MongoJobService() {
    MongoClient client = new(
      Configuration.GetBackpackVariable(CoreVariables.BP_MONGO_STR)
    );
    jobs_ = client.GetDatabase("backpack").GetCollection<Job>("backpack-jobs");
  }

  /// <inheritdoc />
  public async Task<JobStart> TryStart(string kind, string? processor,
                                       string started_by) {
    await Prepare();
    for (int attempt = 0; attempt < 2; attempt++) {
      DateTime now = DateTime.UtcNow;
      Job job = new() {
        kind = kind,
        processor = processor,
        started_by = started_by,
        state = JobStates.RUNNING,
        started_at = now,
        heartbeat_at = now,
        host = Environment.MachineName,
        lock_key = C_LOCK_
      };
      try {
        await jobs_.InsertOneAsync(job);
        return new JobStart(job, null);
      } catch (MongoWriteException e)
        when (e.WriteError.Category == ServerErrorCategory.DuplicateKey) {
        Job? running = await GetRunning();
        if (running != null) {
          return new JobStart(null, running);
        }
        /* It finished in between; try once more. */
      }
    }

    return new JobStart(null, null);
  }

  /// <inheritdoc />
  public async Task<Job> AddWaiting(string kind, string? processor,
                                    string started_by) {
    await Prepare();
    Job job = new() {
      kind = kind,
      processor = processor,
      started_by = started_by,
      scheduled = true,
      state = JobStates.WAITING,
      host = Environment.MachineName
    };
    await jobs_.InsertOneAsync(job);
    return job;
  }

  /// <inheritdoc />
  public async Task<bool> TryRun(Job job) {
    await RecoverStale();
    DateTime now = DateTime.UtcNow;
    try {
      UpdateResult result = await jobs_.UpdateOneAsync(
                              j => j.id == job.id && j.state == JobStates.WAITING,
                              Builders<Job>.Update
                                           .Set(j => j.lock_key, C_LOCK_)
                                           .Set(j => j.state, JobStates.RUNNING)
                                           .Set(j => j.started_at, now)
                                           .Set(j => j.heartbeat_at, now)
                                           .Set(j => j.host, Environment.MachineName)
                                           .Set(j => j.message, null)
                            );
      if (result.ModifiedCount != 1) {
        return false;
      }

      job.state = JobStates.RUNNING;
      job.started_at = now;
      job.heartbeat_at = now;
      job.message = null;
      return true;
    } catch (MongoWriteException e)
      when (e.WriteError.Category == ServerErrorCategory.DuplicateKey) {
      Job? running = await GetRunning();
      await jobs_.UpdateOneAsync(
        j => j.id == job.id && j.state == JobStates.WAITING,
        Builders<Job>.Update
                     .Set(j => j.heartbeat_at, now)
                     .Set(
                       j => j.message,
                       running == null ? "Waiting for the running job." : $"Waiting for {Describe(running)}."
                     )
      );
      return false;
    }
  }

  /// <inheritdoc />
  public async Task<JobSignal> Report(Job job) {
    Job? updated = await jobs_.FindOneAndUpdateAsync<Job, Job>(
                     j => j.id == job.id && j.state == JobStates.RUNNING,
                     Builders<Job>.Update
                                  .Set(j => j.total, job.total)
                                  .Set(j => j.done, job.done)
                                  .Set(j => j.failed, job.failed)
                                  .Set(j => j.current_processor, job.current_processor)
                                  .Set(j => j.message, job.message)
                                  .Set(j => j.heartbeat_at, DateTime.UtcNow),
                     new FindOneAndUpdateOptions<Job, Job> {
                       ReturnDocument = ReturnDocument.After
                     }
                   );
    if (updated == null) {
      return JobSignal.LOST;
    }

    if (updated.cancel_requested) {
      job.cancelled_by = updated.cancelled_by;
      return JobSignal.CANCEL;
    }

    return JobSignal.CONTINUE;
  }

  /// <inheritdoc />
  public async Task Finish(Job job, string state, string? message) {
    DateTime now = DateTime.UtcNow;
    job.state = state;
    job.message = message;
    job.finished_at = now;
    await jobs_.UpdateOneAsync(
      /* LINQ filters must stay translatable, so no IsActive() here. */
      j => j.id == job.id &&
           (j.state == JobStates.RUNNING || j.state == JobStates.WAITING),
      Builders<Job>.Update
                   .Set(j => j.state, state)
                   .Set(j => j.message, message)
                   .Set(j => j.total, job.total)
                   .Set(j => j.done, job.done)
                   .Set(j => j.failed, job.failed)
                   .Set(j => j.current_processor, null)
                   .Set(j => j.finished_at, now)
                   .Set(j => j.heartbeat_at, now)
                   .Unset(j => j.lock_key)
    );
  }

  /// <inheritdoc />
  public async Task<JobCancel> RequestCancel(string id, string user) {
    await RecoverStale();
    DateTime now = DateTime.UtcNow;
    UpdateResult waiting = await jobs_.UpdateOneAsync(
                             j => j.id == id && j.state == JobStates.WAITING,
                             Builders<Job>.Update
                                          .Set(j => j.state, JobStates.CANCELLED)
                                          .Set(j => j.cancelled_by, user)
                                          .Set(j => j.message, $"Cancelled by {user} before it started.")
                                          .Set(j => j.finished_at, now)
                           );
    if (waiting.ModifiedCount == 1) {
      return JobCancel.CANCELLED;
    }

    UpdateResult running = await jobs_.UpdateOneAsync(
                             j => j.id == id && j.state == JobStates.RUNNING,
                             Builders<Job>.Update
                                          .Set(j => j.cancel_requested, true)
                                          .Set(j => j.cancelled_by, user)
                           );
    if (running.MatchedCount == 1) {
      return JobCancel.REQUESTED;
    }

    return await Get(id) == null ? JobCancel.NOT_FOUND : JobCancel.NOT_ACTIVE;
  }

  /// <inheritdoc />
  public async Task<Job?> Get(string id) {
    await RecoverStale();
    return await jobs_.Find(j => j.id == id).FirstOrDefaultAsync();
  }

  /// <inheritdoc />
  public async Task<Job?> GetRunning() {
    await RecoverStale();
    return await jobs_.Find(j => j.state == JobStates.RUNNING)
                      .FirstOrDefaultAsync();
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<Job>> List(int limit) {
    await Prepare();
    return await jobs_.Find(FilterDefinition<Job>.Empty)
                      .SortByDescending(j => j.created_at)
                      .Limit(limit)
                      .ToListAsync();
  }

  /// <summary>
  ///   "Validate all artifacts", "Re-track of npm", with who started it.
  /// </summary>
  public static string Describe(Job job) {
    string what = job.kind == JobKinds.VALIDATE ? "validation" : "re-track";
    string scope = job.processor == null ? "of every processor" : $"of {job.processor}";
    return $"the {what} {scope} started by {job.started_by}";
  }

  private async Task Prepare() {
    await EnsureIndexes();
    await RecoverStale();
  }

  /// <summary>
  ///   Marks jobs whose service stopped sending heartbeats as interrupted,
  ///   which also releases the lock.
  /// </summary>
  private async Task RecoverStale() {
    await EnsureIndexes();
    DateTime now = DateTime.UtcNow;
    DateTime cutoff = now - S_STALE_AFTER_;
    await jobs_.UpdateManyAsync(
      j => (j.state == JobStates.RUNNING || j.state == JobStates.WAITING) &&
           j.heartbeat_at < cutoff,
      Builders<Job>.Update
                   .Set(j => j.state, JobStates.INTERRUPTED)
                   .Set(
                     j => j.message,
                     "Stopped responding: the service running it stopped or lost its connection to MongoDB. Start it again."
                   )
                   .Set(j => j.finished_at, now)
                   .Unset(j => j.lock_key)
    );
  }

  private async Task EnsureIndexes() {
    if (indexes_ready_) {
      return;
    }

    await index_lock_.WaitAsync();
    try {
      if (indexes_ready_) {
        return;
      }

      await jobs_.Indexes.CreateManyAsync(
        new[] {
          /* At most one document holds the lock: the running job. */
          new CreateIndexModel<Job>(
            Builders<Job>.IndexKeys.Ascending(j => j.lock_key),
            new CreateIndexOptions<Job> {
              Name = "one_running_job",
              Unique = true,
              PartialFilterExpression =
                Builders<Job>.Filter.Type(j => j.lock_key, BsonType.String)
            }
          ),
          new CreateIndexModel<Job>(
            Builders<Job>.IndexKeys.Descending(j => j.created_at),
            new CreateIndexOptions { Name = "newest_first" }
          ),
          /* Documents without finished_at (active jobs) never expire. */
          new CreateIndexModel<Job>(
            Builders<Job>.IndexKeys.Ascending(j => j.finished_at),
            new CreateIndexOptions {
              Name = "expire_finished",
              ExpireAfter = S_KEEP_
            }
          )
        }
      );
      indexes_ready_ = true;
    } finally {
      index_lock_.Release();
    }
  }
}
