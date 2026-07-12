using System.Text.Json;
using Core.Kernel;
using Core.Kernel.Models;
using Core.Services;
using Dapper;
using Npgsql;

namespace Core.Infrastructure;

/// <summary>
///   PostgreSQL implementation of <see cref="ICoreDatabase" />.
///   Uses Dapper for object mapping and raw SQL for all queries.
///   The schema mirrors the normalized design produced by the migration script.
/// </summary>
public class PostgresDatabase : ICoreDatabase {
  private readonly string connection_string_;

  public PostgresDatabase() {
    connection_string_ =
      Configuration.GetBackpackVariable(CoreVariables.BP_PG_STR)
      ?? throw new InvalidOperationException(
        "BP_PG_STR environment variable is not set."
      );
  }

  // ------------------------------------------------------------------
  // Processors
  // ------------------------------------------------------------------

  public async Task AddProcessor(Processor processor) {
    await using NpgsqlConnection conn = await OpenConnection();
    await conn.ExecuteAsync(
      @"INSERT INTO processors
          (id, direct_collect, description, requires_approval,
           multi_add, is_external, preview_enabled, config)
        VALUES
          (@Id, @DirectCollect, @Description, @RequiresApproval,
           @MultiAdd, @IsExternal, @PreviewEnabled, @Config::jsonb)
        ON CONFLICT (id) DO NOTHING",
      new {
        Id = processor.id,
        DirectCollect = processor.direct_collect,
        Description = processor.description,
        RequiresApproval = processor.requires_approval,
        MultiAdd = processor.multi_add,
        IsExternal = processor.is_external,
        PreviewEnabled = processor.preview_enabled,
        Config = SerializeJson(processor.config)
      }
    );
  }

  public async Task<bool> UpdateProcessor(Processor processor) {
    await using NpgsqlConnection conn = await OpenConnection();
    int rows = await conn.ExecuteAsync(
      @"UPDATE processors
        SET direct_collect = @DirectCollect,
            description = @Description,
            requires_approval = @RequiresApproval,
            multi_add = @MultiAdd,
            is_external = @IsExternal,
            preview_enabled = @PreviewEnabled,
            config = @Config::jsonb
        WHERE id = @Id",
      new {
        Id = processor.id,
        DirectCollect = processor.direct_collect,
        Description = processor.description,
        RequiresApproval = processor.requires_approval,
        MultiAdd = processor.multi_add,
        IsExternal = processor.is_external,
        PreviewEnabled = processor.preview_enabled,
        Config = SerializeJson(processor.config)
      }
    );
    return rows > 0;
  }

  public async Task<Processor> GetProcessor(string processor) {
    await using NpgsqlConnection conn = await OpenConnection();
    ProcessorRow? row = await conn.QueryFirstOrDefaultAsync<ProcessorRow>(
      "SELECT * FROM processors WHERE id = @Id",
      new { Id = processor }
    );
    return row == null ? null! : MapProcessor(row);
  }

  public async Task<IEnumerable<Processor>> GetProcessors() {
    await using NpgsqlConnection conn = await OpenConnection();
    IEnumerable<ProcessorRow> rows =
      await conn.QueryAsync<ProcessorRow>("SELECT * FROM processors");
    return rows.Select(MapProcessor);
  }

  public async Task<bool> DeleteProcessor(string processor_id) {
    await using NpgsqlConnection conn = await OpenConnection();
    int rows = await conn.ExecuteAsync(
      "DELETE FROM processors WHERE id = @Id",
      new { Id = processor_id }
    );
    return rows > 0;
  }

  // ------------------------------------------------------------------
  // Artifacts
  // ------------------------------------------------------------------

  public async Task AddArtifact(Artifact artifact) {
    await using NpgsqlConnection conn = await OpenConnection();
    await using NpgsqlTransaction tx = await conn.BeginTransactionAsync();

    await conn.ExecuteAsync(
      @"INSERT INTO artifacts
          (id, processor, filter, filter_type, status, root, config)
        VALUES
          (@Id, @Processor, @Filter, @FilterType, @Status, @Root, @Config::jsonb)
        ON CONFLICT (id, processor) DO NOTHING",
      new {
        Id = artifact.id,
        Processor = artifact.processor,
        Filter = artifact.filter,
        FilterType = (int)artifact.filter_type,
        Status = (int)artifact.status,
        Root = artifact.root,
        Config = SerializeJson(artifact.config)
      },
      tx
    );

    await UpsertArtifactChildren(conn, tx, artifact);
    await tx.CommitAsync();
  }

  public async Task<bool> UpdateArtifact(Artifact artifact) {
    await using NpgsqlConnection conn = await OpenConnection();
    await using NpgsqlTransaction tx = await conn.BeginTransactionAsync();

    int rows = await conn.ExecuteAsync(
      @"UPDATE artifacts
        SET filter = @Filter,
            filter_type = @FilterType,
            status = @Status,
            root = @Root,
            config = @Config::jsonb
        WHERE id = @Id AND processor = @Processor",
      new {
        Id = artifact.id,
        Processor = artifact.processor,
        Filter = artifact.filter,
        FilterType = (int)artifact.filter_type,
        Status = (int)artifact.status,
        Root = artifact.root,
        Config = SerializeJson(artifact.config)
      },
      tx
    );

    // Replace children: delete old, insert new
    await conn.ExecuteAsync(
      "DELETE FROM artifact_versions WHERE artifact_id = @Id AND processor = @Processor",
      new { Id = artifact.id, Processor = artifact.processor },
      tx
    );
    await conn.ExecuteAsync(
      "DELETE FROM artifact_dependencies WHERE artifact_id = @Id AND processor = @Processor",
      new { Id = artifact.id, Processor = artifact.processor },
      tx
    );

    await UpsertArtifactChildren(conn, tx, artifact);
    await tx.CommitAsync();

    return rows > 0;
  }

  public async Task<Artifact?> GetArtifact(string id, string processor) {
    await using NpgsqlConnection conn = await OpenConnection();
    ArtifactRow? row = await conn.QueryFirstOrDefaultAsync<ArtifactRow>(
      "SELECT * FROM artifacts WHERE id = @Id AND processor = @Processor",
      new { Id = id, Processor = processor }
    );
    if (row == null) {
      return null;
    }

    return await HydrateArtifact(conn, row);
  }

  public async Task<IEnumerable<Artifact>> GetArtifacts(
    string processor, bool only_roots = true) {
    await using NpgsqlConnection conn = await OpenConnection();

    string sql = only_roots
      ? "SELECT * FROM artifacts WHERE processor = @Processor AND root = TRUE"
      : "SELECT * FROM artifacts WHERE processor = @Processor";

    IEnumerable<ArtifactRow> rows =
      await conn.QueryAsync<ArtifactRow>(sql, new { Processor = processor });

    List<Artifact> result = new();
    foreach (ArtifactRow row in rows) {
      result.Add(await HydrateArtifact(conn, row));
    }

    return result;
  }

  public async Task<IEnumerable<ArtifactSummary>> GetArtifactSummaries(
    string processor, bool only_roots = true) {
    await using NpgsqlConnection conn = await OpenConnection();

    string sql = @"
      SELECT
        a.id,
        a.processor,
        a.filter,
        a.root,
        a.config,
        COALESCE(v.cnt, 0) AS versions,
        COALESCE(d.cnt, 0) AS dependencies
      FROM artifacts a
      LEFT JOIN (
        SELECT artifact_id, processor, COUNT(*) AS cnt
        FROM artifact_versions
        GROUP BY artifact_id, processor
      ) v ON v.artifact_id = a.id AND v.processor = a.processor
      LEFT JOIN (
        SELECT artifact_id, processor, COUNT(*) AS cnt
        FROM artifact_dependencies
        GROUP BY artifact_id, processor
      ) d ON d.artifact_id = a.id AND d.processor = a.processor
      WHERE a.processor = @Processor";

    if (only_roots) {
      sql += " AND a.root = TRUE";
    }

    IEnumerable<ArtifactSummaryRow> rows =
      await conn.QueryAsync<ArtifactSummaryRow>(sql, new { Processor = processor });

    return rows.Select(r => new ArtifactSummary {
      id = r.id,
      processor = r.processor,
      filter = r.filter,
      root = r.root,
      config = DeserializeDict(r.config),
      versions = r.versions,
      dependencies = r.dependencies
    });
  }

  public async Task<bool> DeleteArtifact(Artifact artifact) {
    await using NpgsqlConnection conn = await OpenConnection();
    int rows = await conn.ExecuteAsync(
      "DELETE FROM artifacts WHERE id = @Id AND processor = @Processor",
      new { Id = artifact.id, Processor = artifact.processor }
    );
    return rows > 0;
  }

  // ------------------------------------------------------------------
  // Events
  // ------------------------------------------------------------------

  public async Task AddEvent(Event @event) {
    await using NpgsqlConnection conn = await OpenConnection();
    await conn.ExecuteAsync(
      @"INSERT INTO events (id, timestamp, source, message, severity, ""user"")
        VALUES (@Id, @Timestamp, @Source, @Message, @Severity, @User)
        ON CONFLICT (id) DO NOTHING",
      new {
        Id = @event.id,
        @event.timestamp,
        @event.source,
        @event.message,
        Severity = (int)@event.severity,
        User = @event.user
      }
    );
  }

  public async Task<IEnumerable<Event>> GetEvents(int limit = 100) {
    await using NpgsqlConnection conn = await OpenConnection();
    IEnumerable<EventRow> rows = await conn.QueryAsync<EventRow>(
      @"SELECT id, timestamp, source, message, severity, ""user""
        FROM events ORDER BY timestamp DESC LIMIT @Limit",
      new { Limit = limit }
    );
    return rows.Select(r => new Event {
      id = r.id,
      timestamp = r.timestamp,
      source = r.source,
      message = r.message,
      severity = (EventSeverity)r.severity,
      user = r.user
    });
  }

  // ------------------------------------------------------------------
  // Schedules
  // ------------------------------------------------------------------

  public async Task AddSchedule(Schedule schedule) {
    await using NpgsqlConnection conn = await OpenConnection();
    await conn.ExecuteAsync(
      @"INSERT INTO schedules (id, processor, cron, last_run, next_run)
        VALUES (@Id, @Processor, @Cron, @LastRun, @NextRun)
        ON CONFLICT (id) DO NOTHING",
      new {
        Id = schedule.id,
        Processor = schedule.processor,
        Cron = schedule.cron,
        LastRun = schedule.last_run,
        NextRun = schedule.next_run
      }
    );
  }

  public async Task<IEnumerable<Schedule>> GetSchedules() {
    await using NpgsqlConnection conn = await OpenConnection();
    return await conn.QueryAsync<Schedule>(
      "SELECT id, processor, cron, last_run, next_run FROM schedules"
    );
  }

  public async Task UpdateSchedule(Schedule schedule) {
    await using NpgsqlConnection conn = await OpenConnection();
    await conn.ExecuteAsync(
      @"UPDATE schedules
        SET processor = @Processor, cron = @Cron,
            last_run = @LastRun, next_run = @NextRun
        WHERE id = @Id",
      new {
        Id = schedule.id,
        Processor = schedule.processor,
        Cron = schedule.cron,
        LastRun = schedule.last_run,
        NextRun = schedule.next_run
      }
    );
  }

  public async Task<bool> DeleteSchedule(string id) {
    await using NpgsqlConnection conn = await OpenConnection();
    int rows = await conn.ExecuteAsync(
      "DELETE FROM schedules WHERE id = @Id",
      new { Id = id }
    );
    return rows > 0;
  }

  // ------------------------------------------------------------------
  // Pending Artifacts
  // ------------------------------------------------------------------

  public async Task AddPendingArtifact(PendingArtifact artifact) {
    await using NpgsqlConnection conn = await OpenConnection();
    await conn.ExecuteAsync(
      @"INSERT INTO pending_artifacts
          (id, processor, filter, config, requested_by, timestamp)
        VALUES
          (@Id, @Processor, @Filter, @Config::jsonb, @RequestedBy, @Timestamp)
        ON CONFLICT (id, processor) DO NOTHING",
      new {
        Id = artifact.id,
        Processor = artifact.processor,
        Filter = artifact.filter,
        Config = SerializeJson(artifact.config),
        RequestedBy = artifact.requested_by,
        artifact.timestamp
      }
    );
  }

  public async Task<IEnumerable<PendingArtifact>> GetPendingArtifacts() {
    await using NpgsqlConnection conn = await OpenConnection();
    IEnumerable<PendingArtifactRow> rows =
      await conn.QueryAsync<PendingArtifactRow>(
        "SELECT * FROM pending_artifacts"
      );
    return rows.Select(MapPendingArtifact);
  }

  public async Task<PendingArtifact?> GetPendingArtifact(
    string processor, string id) {
    await using NpgsqlConnection conn = await OpenConnection();
    PendingArtifactRow? row =
      await conn.QueryFirstOrDefaultAsync<PendingArtifactRow>(
        "SELECT * FROM pending_artifacts WHERE processor = @Processor AND id = @Id",
        new { Processor = processor, Id = id }
      );
    return row == null ? null : MapPendingArtifact(row);
  }

  public async Task<bool> DeletePendingArtifact(string processor, string id) {
    await using NpgsqlConnection conn = await OpenConnection();
    int rows = await conn.ExecuteAsync(
      "DELETE FROM pending_artifacts WHERE processor = @Processor AND id = @Id",
      new { Processor = processor, Id = id }
    );
    return rows > 0;
  }

  // ------------------------------------------------------------------
  // API Keys
  // ------------------------------------------------------------------

  public async Task AddApiKey(ApiKey key) {
    await using NpgsqlConnection conn = await OpenConnection();
    await conn.ExecuteAsync(
      @"INSERT INTO api_keys (id, name, key, is_admin, created_at, created_by)
        VALUES (@Id, @Name, @Key, @IsAdmin, @CreatedAt, @CreatedBy)
        ON CONFLICT (id) DO NOTHING",
      new {
        Id = key.id,
        Name = key.name,
        Key = key.key,
        IsAdmin = key.is_admin,
        CreatedAt = key.created_at,
        CreatedBy = key.created_by
      }
    );
  }

  public async Task<IEnumerable<ApiKey>> GetApiKeys() {
    await using NpgsqlConnection conn = await OpenConnection();
    return await conn.QueryAsync<ApiKey>(
      "SELECT id, name, key, is_admin, created_at, created_by FROM api_keys"
    );
  }

  public async Task<ApiKey> GetApiKey(string key) {
    await using NpgsqlConnection conn = await OpenConnection();
    return (await conn.QueryFirstOrDefaultAsync<ApiKey>(
      "SELECT id, name, key, is_admin, created_at, created_by FROM api_keys WHERE key = @Key",
      new { Key = key }
    ))!;
  }

  public async Task<bool> DeleteApiKey(string id) {
    await using NpgsqlConnection conn = await OpenConnection();
    int rows = await conn.ExecuteAsync(
      "DELETE FROM api_keys WHERE id = @Id",
      new { Id = id }
    );
    return rows > 0;
  }

  // ------------------------------------------------------------------
  // News Posts
  // ------------------------------------------------------------------

  public async Task AddNewsPost(NewsPost post) {
    await using NpgsqlConnection conn = await OpenConnection();
    await conn.ExecuteAsync(
      @"INSERT INTO news_posts (id, title, content, author, timestamp)
        VALUES (@Id, @Title, @Content, @Author, @Timestamp)
        ON CONFLICT (id) DO NOTHING",
      new {
        Id = post.id,
        post.title,
        post.content,
        post.author,
        post.timestamp
      }
    );
  }

  public async Task<IEnumerable<NewsPost>> GetNewsPosts(int limit = 50) {
    await using NpgsqlConnection conn = await OpenConnection();
    return await conn.QueryAsync<NewsPost>(
      @"SELECT id, title, content, author, timestamp
        FROM news_posts ORDER BY timestamp DESC LIMIT @Limit",
      new { Limit = limit }
    );
  }

  public async Task<bool> DeleteNewsPost(string id) {
    await using NpgsqlConnection conn = await OpenConnection();
    int rows = await conn.ExecuteAsync(
      "DELETE FROM news_posts WHERE id = @Id",
      new { Id = id }
    );
    return rows > 0;
  }

  // ==================================================================
  // Private helpers
  // ==================================================================

  private async Task<NpgsqlConnection> OpenConnection() {
    NpgsqlConnection conn = new(connection_string_);
    await conn.OpenAsync();
    return conn;
  }

  /// <summary>
  ///   Inserts versions, files, and dependencies for an artifact.
  /// </summary>
  private async Task UpsertArtifactChildren(
    NpgsqlConnection conn, NpgsqlTransaction tx, Artifact artifact) {
    // -- Versions & files --
    foreach (KeyValuePair<string, ArtifactVersion> kv in artifact.versions) {
      await conn.ExecuteAsync(
        @"INSERT INTO artifact_versions (artifact_id, processor, version, status)
          VALUES (@ArtifactId, @Processor, @Version, @Status)
          ON CONFLICT (artifact_id, processor, version) DO UPDATE
            SET status = @Status",
        new {
          ArtifactId = artifact.id,
          Processor = artifact.processor,
          Version = kv.Key,
          Status = (int)kv.Value.status
        },
        tx
      );

      foreach (KeyValuePair<string, ArtifactFile> file in kv.Value.files) {
        await conn.ExecuteAsync(
          @"INSERT INTO artifact_files
              (artifact_id, processor, version, file_name, uri, folder)
            VALUES (@ArtifactId, @Processor, @Version, @FileName, @Uri, @Folder)
            ON CONFLICT (artifact_id, processor, version, file_name) DO UPDATE
              SET uri = @Uri, folder = @Folder",
          new {
            ArtifactId = artifact.id,
            Processor = artifact.processor,
            Version = kv.Key,
            FileName = file.Key,
            Uri = file.Value.uri,
            Folder = file.Value.folder
          },
          tx
        );
      }
    }

    // -- Dependencies --
    foreach (ArtifactDependency dep in artifact.dependencies) {
      await conn.ExecuteAsync(
        @"INSERT INTO artifact_dependencies
            (artifact_id, processor, dependency_id, dependency_processor, config)
          VALUES (@ArtifactId, @Processor, @DepId, @DepProcessor, @Config::jsonb)
          ON CONFLICT (artifact_id, processor, dependency_id) DO UPDATE
            SET dependency_processor = @DepProcessor, config = @Config::jsonb",
        new {
          ArtifactId = artifact.id,
          Processor = artifact.processor,
          DepId = dep.id,
          DepProcessor = dep.processor,
          Config = SerializeJson(dep.config)
        },
        tx
      );
    }
  }

  /// <summary>
  ///   Loads an artifact row and hydrates it with its versions, files, and dependencies.
  /// </summary>
  private async Task<Artifact> HydrateArtifact(
    NpgsqlConnection conn, ArtifactRow row) {
    Artifact artifact = new() {
      id = row.id,
      processor = row.processor,
      filter = row.filter,
      filter_type = (ArtifactFilterType)row.filter_type,
      status = (ArtifactStatus)row.status,
      root = row.root,
      config = DeserializeDict(row.config)
    };

    // Load versions
    IEnumerable<VersionRow> version_rows = await conn.QueryAsync<VersionRow>(
      @"SELECT version, status FROM artifact_versions
        WHERE artifact_id = @Id AND processor = @Processor",
      new { Id = row.id, Processor = row.processor }
    );

    foreach (VersionRow vr in version_rows) {
      ArtifactVersion version = new() {
        version = vr.version,
        status = (ArtifactVersionStatus)vr.status
      };

      // Load files for this version
      IEnumerable<FileRow> file_rows = await conn.QueryAsync<FileRow>(
        @"SELECT file_name, uri, folder FROM artifact_files
          WHERE artifact_id = @Id AND processor = @Processor AND version = @Version",
        new { Id = row.id, Processor = row.processor, Version = vr.version }
      );

      foreach (FileRow fr in file_rows) {
        version.files[fr.file_name] = new ArtifactFile {
          uri = fr.uri,
          folder = fr.folder
        };
      }

      artifact.versions[vr.version] = version;
    }

    // Load dependencies
    IEnumerable<DependencyRow> dep_rows = await conn.QueryAsync<DependencyRow>(
      @"SELECT dependency_id, dependency_processor, config
        FROM artifact_dependencies
        WHERE artifact_id = @Id AND processor = @Processor",
      new { Id = row.id, Processor = row.processor }
    );

    foreach (DependencyRow dr in dep_rows) {
      artifact.dependencies.Add(new ArtifactDependency {
        id = dr.dependency_id,
        processor = dr.dependency_processor,
        config = DeserializeDict(dr.config)
      });
    }

    return artifact;
  }

  // ------------------------------------------------------------------
  // JSON serialization helpers
  // ------------------------------------------------------------------

  private static string SerializeJson(object? value) {
    if (value == null) {
      return "{}";
    }

    return JsonSerializer.Serialize(value);
  }

  private static Dictionary<string, string> DeserializeDict(string? json) {
    if (string.IsNullOrEmpty(json)) {
      return new Dictionary<string, string>();
    }

    return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
           ?? new Dictionary<string, string>();
  }

  private static Dictionary<string, ProcessorAuxiliaryField>
    DeserializeProcessorConfig(string? json) {
    if (string.IsNullOrEmpty(json)) {
      return new Dictionary<string, ProcessorAuxiliaryField>();
    }

    return JsonSerializer.Deserialize<Dictionary<string, ProcessorAuxiliaryField>>(json)
           ?? new Dictionary<string, ProcessorAuxiliaryField>();
  }

  // ------------------------------------------------------------------
  // Mapping helpers — Dapper row types → domain models
  // ------------------------------------------------------------------

  private static Processor MapProcessor(ProcessorRow row) {
    return new Processor {
      id = row.id,
      direct_collect = row.direct_collect,
      description = row.description,
      requires_approval = row.requires_approval,
      multi_add = row.multi_add,
      is_external = row.is_external,
      preview_enabled = row.preview_enabled,
      config = DeserializeProcessorConfig(row.config)
    };
  }

  private static PendingArtifact MapPendingArtifact(PendingArtifactRow row) {
    return new PendingArtifact {
      id = row.id,
      processor = row.processor,
      filter = row.filter,
      config = DeserializeDict(row.config),
      requested_by = row.requested_by,
      timestamp = row.timestamp
    };
  }

  // ==================================================================
  // Dapper row-mapping record types
  // ==================================================================
  // These lightweight records let Dapper map snake_case columns from
  // PostgreSQL into typed properties without relying on the domain
  // models directly (which use JSONB strings for nested objects).

  private record ProcessorRow {
    public string id { get; init; } = "";
    public bool direct_collect { get; init; }
    public string description { get; init; } = "";
    public bool requires_approval { get; init; }
    public bool multi_add { get; init; }
    public bool is_external { get; init; }
    public bool preview_enabled { get; init; }
    public string config { get; init; } = "{}";
  }

  private record ArtifactRow {
    public string id { get; init; } = "";
    public string processor { get; init; } = "";
    public string filter { get; init; } = "";
    public int filter_type { get; init; }
    public int status { get; init; }
    public bool root { get; init; }
    public string config { get; init; } = "{}";
  }

  private record ArtifactSummaryRow {
    public string id { get; init; } = "";
    public string processor { get; init; } = "";
    public string filter { get; init; } = "";
    public bool root { get; init; }
    public string config { get; init; } = "{}";
    public int versions { get; init; }
    public int dependencies { get; init; }
  }

  private record VersionRow {
    public string version { get; init; } = "";
    public int status { get; init; }
  }

  private record FileRow {
    public string file_name { get; init; } = "";
    public string uri { get; init; } = "";
    public string folder { get; init; } = "";
  }

  private record DependencyRow {
    public string dependency_id { get; init; } = "";
    public string dependency_processor { get; init; } = "";
    public string config { get; init; } = "{}";
  }

  private record EventRow {
    public string id { get; init; } = "";
    public DateTime timestamp { get; init; }
    public string source { get; init; } = "";
    public string message { get; init; } = "";
    public int severity { get; init; }
    public string user { get; init; } = "";
  }

  private record PendingArtifactRow {
    public string id { get; init; } = "";
    public string processor { get; init; } = "";
    public string filter { get; init; } = "";
    public string config { get; init; } = "{}";
    public string requested_by { get; init; } = "";
    public DateTime timestamp { get; init; }
  }
}
