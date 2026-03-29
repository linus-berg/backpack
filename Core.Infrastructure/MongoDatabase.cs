using Core.Infrastructure.Models;
using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Core.Infrastructure;

public class MongoDatabase : ICoreDatabase {
  private const string C_PROCESSOR_COLLECTION_ = "backpack-processors";
  private readonly MongoClient client_;
  private readonly IMongoDatabase database_;

  public MongoDatabase() {
    string? c_str =
      Configuration.GetBackpackVariable(CoreVariables.BP_MONGO_STR);
    client_ = new MongoClient(c_str);
    database_ = client_.GetDatabase("backpack");
  }

  public async Task AddArtifact(Artifact artifact) {
    IMongoCollection<Artifact> collection =
      GetCollection<Artifact>(artifact.processor);
    await collection.InsertOneAsync(artifact);
  }

  public async Task AddProcessor(Processor processor) {
    IMongoCollection<Processor> collection =
      GetCollection<Processor>(C_PROCESSOR_COLLECTION_);
    await collection.InsertOneAsync(processor);
  }

  public async Task<bool> UpdateArtifact(Artifact artifact) {
    IMongoCollection<Artifact> collection =
      GetCollection<Artifact>(artifact.processor);
    ReplaceOneResult result =
      await collection.ReplaceOneAsync(a => a.id == artifact.id, artifact);
    return result.IsAcknowledged;
  }

  public async Task<bool> UpdateProcessor(Processor processor) {
    IMongoCollection<Processor> collection =
      GetCollection<Processor>(C_PROCESSOR_COLLECTION_);
    ReplaceOneResult result =
      await collection.ReplaceOneAsync(a => a.id == processor.id, processor);
    return result.IsAcknowledged;
  }

  public async Task<Processor> GetProcessor(string processor) {
    IMongoCollection<Processor> collection =
      GetCollection<Processor>(C_PROCESSOR_COLLECTION_);
    IAsyncCursor<Processor> cursor =
      await collection.FindAsync(a => a.id == processor);
    return await cursor.FirstOrDefaultAsync();
  }

  public async Task<IEnumerable<Processor>> GetProcessors() {
    IMongoCollection<Processor> collection =
      GetCollection<Processor>(C_PROCESSOR_COLLECTION_);
    return await (await collection.FindAsync(a => true))
             .ToListAsync();
  }

  public async Task<Artifact?> GetArtifact(string id, string processor) {
    IAsyncCursor<Artifact> cursor =
      await GetCollection<Artifact>(processor).FindAsync(a => a.id == id);
    return await cursor.FirstOrDefaultAsync();
  }

  public async Task<IEnumerable<Artifact>> GetArtifacts(
    string processor, bool only_roots = true) {
    IAsyncCursor<Artifact> cursor =
      await GetCollection<Artifact>(processor)
        .FindAsync(a => a.root || !only_roots);
    return await cursor.ToListAsync();
  }

  public async Task<IEnumerable<ArtifactSummary>> GetArtifactSummaries(
    string processor, bool only_roots = true) {
    IMongoCollection<Artifact> collection = GetCollection<Artifact>(processor);

    FilterDefinition<Artifact>? filter =
      Builders<Artifact>.Filter.Eq("root", true);
    BsonDocument project_stage = new BsonDocument {
      {
        "$project", new BsonDocument {
          {
            "_id", 1
          }, {
            "processor", 1
          }, {
            "root", 1
          }, {
            "config", 1
          }, {
            "filter", 1
          },
          {
            "versions", new BsonDocument {
              {
                "$size", new BsonDocument {
                  {
                    "$objectToArray", new BsonDocument {
                      {
                        "$ifNull", new BsonArray {
                          "$versions",
                          new BsonDocument()
                        }
                      }
                    }
                  }
                }
              }
            }
          }, {
            "dependencies", new BsonDocument {
              {
                "$size", "$dependencies"
              }
            }
          }
        }
      }
    };

    return await collection.Aggregate()
                           /* Match only roots if only_roots is checked */
                           .Match(
                             only_roots
                               ? filter
                               : FilterDefinition<Artifact>.Empty
                           )
                           .AppendStage<ArtifactSummary>(project_stage)
                           .ToListAsync();
  }

  public async Task<bool> DeleteArtifact(Artifact artifact) {
    IMongoCollection<Artifact> collection =
      GetCollection<Artifact>(artifact.processor);
    Artifact a =
      await collection.FindOneAndDeleteAsync(a => a.id == artifact.id);
    return a != null;
  }

  public async Task<bool> DeleteProcessor(string processor_id) {
    IMongoCollection<Processor> collection =
      GetCollection<Processor>(C_PROCESSOR_COLLECTION_);
    Processor p =
      await collection.FindOneAndDeleteAsync(p => p.id == processor_id);
    return p != null;
  }

  private IMongoCollection<T> GetCollection<T>(string collection) {
    return database_.GetCollection<T>(collection);
  }

  public async Task AddProcessor(ArtifactProcessor processor) {
    IMongoCollection<ArtifactProcessor> collection =
      GetCollection<ArtifactProcessor>("processors");

    await collection.InsertOneAsync(processor);
  }

  public async Task<bool> ArtifactExists(string id, string processor) {
    return await GetArtifact(id, processor) != null;
  }

  public async Task AddEvent(Event @event) {
    IMongoCollection<Event> collection = GetCollection<Event>("backpack-events");
    await collection.InsertOneAsync(@event);
  }

  public async Task<IEnumerable<Event>> GetEvents(int limit = 100) {
    IMongoCollection<Event> collection = GetCollection<Event>("backpack-events");
    return await collection.Find(a => true)
                           .SortByDescending(a => a.timestamp)
                           .Limit(limit)
                           .ToListAsync();
  }

  public async Task<IEnumerable<Schedule>> GetSchedules() {
    IMongoCollection<Schedule> collection = GetCollection<Schedule>("backpack-schedules");
    return await (await collection.FindAsync(a => true)).ToListAsync();
  }

  public async Task UpdateSchedule(Schedule schedule) {
    IMongoCollection<Schedule> collection = GetCollection<Schedule>("backpack-schedules");
    await collection.ReplaceOneAsync(a => a.id == schedule.id, schedule);
  }

  public async Task AddSchedule(Schedule schedule) {
    IMongoCollection<Schedule> collection = GetCollection<Schedule>("backpack-schedules");
    await collection.InsertOneAsync(schedule);
  }

  public async Task AddPendingArtifact(PendingArtifact artifact) {
    IMongoCollection<PendingArtifact> collection = GetCollection<PendingArtifact>("pending-approvals");
    await collection.InsertOneAsync(artifact);
  }

  public async Task<IEnumerable<PendingArtifact>> GetPendingArtifacts() {
    IMongoCollection<PendingArtifact> collection = GetCollection<PendingArtifact>("pending-approvals");
    return await (await collection.FindAsync(a => true)).ToListAsync();
  }

  public async Task<PendingArtifact> GetPendingArtifact(string processor, string id) {
    IMongoCollection<PendingArtifact> collection = GetCollection<PendingArtifact>("pending-approvals");
    return await (await collection.FindAsync(a => a.processor == processor && a.id == id)).FirstOrDefaultAsync();
  }

  public async Task<bool> DeletePendingArtifact(string processor, string id) {
    IMongoCollection<PendingArtifact> collection = GetCollection<PendingArtifact>("pending-approvals");
    DeleteResult? res = await collection.DeleteOneAsync(a => a.processor == processor && a.id == id);
    return res.DeletedCount > 0;
  }
  
  public async Task AddApiKey(ApiKey key) {
    IMongoCollection<ApiKey> collection = GetCollection<ApiKey>("backpack-api-keys");
    await collection.InsertOneAsync(key);
  }

  public async Task<IEnumerable<ApiKey>> GetApiKeys() {
    IMongoCollection<ApiKey> collection = GetCollection<ApiKey>("backpack-api-keys");
    return await (await collection.FindAsync(a => true)).ToListAsync();
  }

  public async Task<ApiKey> GetApiKey(string key) {
    IMongoCollection<ApiKey> collection = GetCollection<ApiKey>("backpack-api-keys");
    return await (await collection.FindAsync(a => a.key == key)).FirstOrDefaultAsync();
  }

  public async Task<bool> DeleteApiKey(string id) {
    IMongoCollection<ApiKey> collection = GetCollection<ApiKey>("backpack-api-keys");
    DeleteResult? res = await collection.DeleteOneAsync(a => a.id == id);
    return res.DeletedCount > 0;
  }

  public async Task AddNewsPost(NewsPost post) {
    IMongoCollection<NewsPost> collection = GetCollection<NewsPost>("backpack-news");
    await collection.InsertOneAsync(post);
  }

  public async Task<IEnumerable<NewsPost>> GetNewsPosts(int limit = 50) {
    IMongoCollection<NewsPost> collection = GetCollection<NewsPost>("backpack-news");
    return await collection.Find(a => true)
                           .SortByDescending(a => a.timestamp)
                           .Limit(limit)
                           .ToListAsync();
  }

  public async Task<bool> DeleteNewsPost(string id) {
    IMongoCollection<NewsPost> collection = GetCollection<NewsPost>("backpack-news");
    DeleteResult? res = await collection.DeleteOneAsync(a => a.id == id);
    return res.DeletedCount > 0;
  }
}