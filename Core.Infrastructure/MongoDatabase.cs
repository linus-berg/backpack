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

  public async Task<Artifact> GetArtifact(string id, string processor) {
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
}