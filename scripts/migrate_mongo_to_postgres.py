#!/usr/bin/env python3
"""
Backpack: MongoDB → PostgreSQL Migration Script
================================================

Migrates all data from the MongoDB 'backpack' database into a normalized
PostgreSQL schema.  Run with --help for usage.

Requirements:
    pip install pymongo psycopg2-binary

Environment variables (or pass via CLI flags):
    BP_MONGO_STR   – MongoDB connection string  (default: mongodb://localhost:27017)
    BP_PG_STR      – PostgreSQL connection string (default: postgresql://localhost:5432/backpack)
"""

import argparse
import json
import logging
import os
import sys
from datetime import datetime, timezone

try:
    import pymongo
except ImportError:
    sys.exit("ERROR: pymongo is not installed.  Run:  pip install pymongo")

try:
    import psycopg2
    import psycopg2.extras
except ImportError:
    sys.exit("ERROR: psycopg2 is not installed.  Run:  pip install psycopg2-binary")

# ---------------------------------------------------------------------------
# Logging
# ---------------------------------------------------------------------------
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S",
)
log = logging.getLogger("backpack-migration")

# ---------------------------------------------------------------------------
# PostgreSQL DDL
# ---------------------------------------------------------------------------
SCHEMA_DDL = """
-- ================================================================
-- Backpack PostgreSQL Schema
-- ================================================================

CREATE TABLE IF NOT EXISTS processors (
    id              TEXT PRIMARY KEY,
    direct_collect  BOOLEAN NOT NULL DEFAULT FALSE,
    description     TEXT NOT NULL DEFAULT '',
    requires_approval BOOLEAN NOT NULL DEFAULT FALSE,
    multi_add       BOOLEAN NOT NULL DEFAULT FALSE,
    is_external     BOOLEAN NOT NULL DEFAULT FALSE,
    preview_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    config          JSONB NOT NULL DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS artifacts (
    id          TEXT NOT NULL,
    processor   TEXT NOT NULL REFERENCES processors(id),
    filter      TEXT NOT NULL DEFAULT '',
    filter_type INTEGER NOT NULL DEFAULT 0,
    status      INTEGER NOT NULL DEFAULT 0,
    root        BOOLEAN NOT NULL DEFAULT FALSE,
    config      JSONB NOT NULL DEFAULT '{}',
    PRIMARY KEY (id, processor)
);

CREATE TABLE IF NOT EXISTS artifact_versions (
    artifact_id  TEXT NOT NULL,
    processor    TEXT NOT NULL,
    version      TEXT NOT NULL,
    status       INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (artifact_id, processor, version),
    FOREIGN KEY (artifact_id, processor)
        REFERENCES artifacts(id, processor) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS artifact_files (
    artifact_id  TEXT NOT NULL,
    processor    TEXT NOT NULL,
    version      TEXT NOT NULL,
    file_name    TEXT NOT NULL,
    uri          TEXT NOT NULL,
    folder       TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (artifact_id, processor, version, file_name),
    FOREIGN KEY (artifact_id, processor, version)
        REFERENCES artifact_versions(artifact_id, processor, version) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS artifact_dependencies (
    artifact_id          TEXT NOT NULL,
    processor            TEXT NOT NULL,
    dependency_id        TEXT NOT NULL,
    dependency_processor TEXT NOT NULL,
    config               JSONB NOT NULL DEFAULT '{}',
    PRIMARY KEY (artifact_id, processor, dependency_id),
    FOREIGN KEY (artifact_id, processor)
        REFERENCES artifacts(id, processor) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS events (
    id          TEXT PRIMARY KEY,
    timestamp   TIMESTAMP NOT NULL DEFAULT NOW(),
    source      TEXT NOT NULL,
    message     TEXT NOT NULL,
    severity    INTEGER NOT NULL DEFAULT 0,
    "user"      TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_events_timestamp ON events (timestamp DESC);

CREATE TABLE IF NOT EXISTS schedules (
    id          TEXT PRIMARY KEY,
    processor   TEXT NOT NULL,
    cron        TEXT NOT NULL,
    last_run    TIMESTAMP,
    next_run    TIMESTAMP
);

CREATE TABLE IF NOT EXISTS pending_artifacts (
    id            TEXT NOT NULL,
    processor     TEXT NOT NULL,
    filter        TEXT NOT NULL DEFAULT '',
    config        JSONB NOT NULL DEFAULT '{}',
    requested_by  TEXT NOT NULL,
    timestamp     TIMESTAMP NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, processor)
);

CREATE TABLE IF NOT EXISTS api_keys (
    id          TEXT PRIMARY KEY,
    name        TEXT NOT NULL DEFAULT '',
    key         TEXT NOT NULL UNIQUE,
    is_admin    BOOLEAN NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMP NOT NULL DEFAULT NOW(),
    created_by  TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_api_keys_key ON api_keys (key);

CREATE TABLE IF NOT EXISTS news_posts (
    id          TEXT PRIMARY KEY,
    title       TEXT NOT NULL,
    content     TEXT NOT NULL,
    author      TEXT NOT NULL DEFAULT '',
    timestamp   TIMESTAMP NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS idx_news_posts_timestamp ON news_posts (timestamp DESC);
"""

# ---------------------------------------------------------------------------
# Known fixed-name collections (not artifact data)
# ---------------------------------------------------------------------------
FIXED_COLLECTIONS = {
    "backpack-processors",
    "backpack-events",
    "backpack-schedules",
    "pending-approvals",
    "backpack-api-keys",
    "backpack-news",
}

# ---------------------------------------------------------------------------
# Enum mappings  (must match the C# enum definitions)
# ---------------------------------------------------------------------------
FILTER_TYPE_MAP = {
    "REGEX": 0,
    "SEMVER_RANGE": 1,
    0: 0,
    1: 1,
}

ARTIFACT_STATUS_MAP = {
    "PROCESSING": 0,
    0: 0,
}

VERSION_STATUS_MAP = {
    "SENT_FOR_COLLECTION": 0,
    0: 0,
}

SEVERITY_MAP = {
    "INFO": 0,
    "WARNING": 1,
    "ERROR": 2,
    "SUCCESS": 3,
    0: 0,
    1: 1,
    2: 2,
    3: 3,
}


def resolve_enum(value, mapping, default=0):
    """Resolve a MongoDB enum value (stored as int or string) to an integer."""
    if value is None:
        return default
    return mapping.get(value, default)


def safe_str(value, default=""):
    """Return value as string, or default if None."""
    return str(value) if value is not None else default


def safe_dict_to_json(value):
    """Convert a dict/value to a JSON string for JSONB columns."""
    if value is None:
        return "{}"
    if isinstance(value, str):
        return value
    return json.dumps(value, default=str)


def extract_id(doc):
    """
    Extract the document ID from a MongoDB document.

    MongoDB stores the primary key in the '_id' field.  The C# driver maps
    a property named 'id' to '_id' by convention, so the actual document
    will only have '_id'.
    """
    raw = doc.get("_id") or doc.get("id")
    if raw is None:
        return None
    return str(raw)


# ---------------------------------------------------------------------------
# Migration helpers
# ---------------------------------------------------------------------------

class Migrator:
    """Encapsulates a single migration run."""

    def __init__(self, mongo_uri, pg_uri, dry_run=False):
        self.dry_run = dry_run

        # ---- Mongo ----
        log.info("Connecting to MongoDB: %s", mongo_uri)
        self.mongo = pymongo.MongoClient(mongo_uri)
        self.mongo_db = self.mongo["backpack"]

        # ---- Postgres ----
        log.info("Connecting to PostgreSQL: %s", pg_uri)
        self.pg = psycopg2.connect(pg_uri)
        self.pg.autocommit = False
        self.cur = self.pg.cursor()

        # Counters
        self.counts = {}

    # ------------------------------------------------------------------
    # Schema
    # ------------------------------------------------------------------
    def create_schema(self):
        log.info("Creating PostgreSQL schema …")
        self.cur.execute(SCHEMA_DDL)
        self.pg.commit()
        log.info("Schema created.")

    # ------------------------------------------------------------------
    # Processors
    # ------------------------------------------------------------------
    def migrate_processors(self):
        collection = self.mongo_db["backpack-processors"]
        docs = list(collection.find())
        log.info("Migrating %d processors …", len(docs))

        for doc in docs:
            pid = extract_id(doc)
            config_raw = doc.get("config", {})
            # ProcessorAuxiliaryField objects are nested dicts — store as JSONB
            config_json = safe_dict_to_json(config_raw)

            self.cur.execute(
                """
                INSERT INTO processors (id, direct_collect, description,
                    requires_approval, multi_add, is_external,
                    preview_enabled, config)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (id) DO NOTHING
                """,
                (
                    pid,
                    bool(doc.get("direct_collect", False)),
                    safe_str(doc.get("description", "")),
                    bool(doc.get("requires_approval", False)),
                    bool(doc.get("multi_add", False)),
                    bool(doc.get("is_external", False)),
                    bool(doc.get("preview_enabled", True)),
                    config_json,
                ),
            )
        self.pg.commit()
        self.counts["processors"] = len(docs)

    # ------------------------------------------------------------------
    # Artifacts  (dynamic per-processor collections)
    # ------------------------------------------------------------------
    def discover_artifact_collections(self):
        """
        Identify artifact collections.

        In MongoDB, every processor stores its artifacts in a collection whose
        name equals the processor's ID.  We discover them by listing all
        collections and excluding the known fixed-name ones and any system
        collections.
        """
        all_names = self.mongo_db.list_collection_names()
        artifact_collections = [
            name
            for name in all_names
            if name not in FIXED_COLLECTIONS and not name.startswith("system.")
        ]
        log.info(
            "Discovered %d artifact collections: %s",
            len(artifact_collections),
            artifact_collections,
        )
        return artifact_collections

    def migrate_artifacts(self):
        collections = self.discover_artifact_collections()
        total_artifacts = 0
        total_versions = 0
        total_files = 0
        total_deps = 0

        for coll_name in collections:
            processor_id = coll_name
            collection = self.mongo_db[coll_name]
            docs = list(collection.find())
            log.info(
                "  [%s] Migrating %d artifacts …",
                processor_id,
                len(docs),
            )

            for doc in docs:
                artifact_id = extract_id(doc)
                if artifact_id is None:
                    log.warning("    Skipping document with no _id in %s", coll_name)
                    continue

                # -- Artifact row --
                config_json = safe_dict_to_json(doc.get("config", {}))
                filter_type = resolve_enum(doc.get("filter_type"), FILTER_TYPE_MAP)
                status = resolve_enum(doc.get("status"), ARTIFACT_STATUS_MAP)

                self.cur.execute(
                    """
                    INSERT INTO artifacts (id, processor, filter, filter_type,
                        status, root, config)
                    VALUES (%s, %s, %s, %s, %s, %s, %s)
                    ON CONFLICT (id, processor) DO NOTHING
                    """,
                    (
                        artifact_id,
                        processor_id,
                        safe_str(doc.get("filter", "")),
                        filter_type,
                        status,
                        bool(doc.get("root", False)),
                        config_json,
                    ),
                )
                total_artifacts += 1

                # -- Versions --
                versions = doc.get("versions", {})
                if versions and isinstance(versions, dict):
                    for ver_key, ver_data in versions.items():
                        ver_status = resolve_enum(
                            ver_data.get("status") if isinstance(ver_data, dict) else None,
                            VERSION_STATUS_MAP,
                        )
                        self.cur.execute(
                            """
                            INSERT INTO artifact_versions
                                (artifact_id, processor, version, status)
                            VALUES (%s, %s, %s, %s)
                            ON CONFLICT (artifact_id, processor, version) DO NOTHING
                            """,
                            (artifact_id, processor_id, ver_key, ver_status),
                        )
                        total_versions += 1

                        # -- Files --
                        files = (
                            ver_data.get("files", {})
                            if isinstance(ver_data, dict)
                            else {}
                        )
                        if files and isinstance(files, dict):
                            for file_key, file_data in files.items():
                                uri = (
                                    file_data.get("uri", "")
                                    if isinstance(file_data, dict)
                                    else ""
                                )
                                folder = (
                                    file_data.get("folder", "")
                                    if isinstance(file_data, dict)
                                    else ""
                                )
                                self.cur.execute(
                                    """
                                    INSERT INTO artifact_files
                                        (artifact_id, processor, version,
                                         file_name, uri, folder)
                                    VALUES (%s, %s, %s, %s, %s, %s)
                                    ON CONFLICT (artifact_id, processor, version, file_name) DO NOTHING
                                    """,
                                    (
                                        artifact_id,
                                        processor_id,
                                        ver_key,
                                        file_key,
                                        safe_str(uri),
                                        safe_str(folder),
                                    ),
                                )
                                total_files += 1

                # -- Dependencies --
                deps = doc.get("dependencies", [])
                if deps:
                    for dep in deps:
                        if not isinstance(dep, dict):
                            continue
                        dep_id = safe_str(dep.get("id", dep.get("_id")))
                        dep_processor = safe_str(
                            dep.get("processor", processor_id)
                        )
                        dep_config = safe_dict_to_json(dep.get("config", {}))

                        self.cur.execute(
                            """
                            INSERT INTO artifact_dependencies
                                (artifact_id, processor, dependency_id,
                                 dependency_processor, config)
                            VALUES (%s, %s, %s, %s, %s)
                            ON CONFLICT (artifact_id, processor, dependency_id) DO NOTHING
                            """,
                            (
                                artifact_id,
                                processor_id,
                                dep_id,
                                dep_processor,
                                dep_config,
                            ),
                        )
                        total_deps += 1

            # Commit per-collection to avoid holding a huge transaction
            self.pg.commit()

        self.counts["artifacts"] = total_artifacts
        self.counts["artifact_versions"] = total_versions
        self.counts["artifact_files"] = total_files
        self.counts["artifact_dependencies"] = total_deps

    # ------------------------------------------------------------------
    # Events
    # ------------------------------------------------------------------
    def migrate_events(self):
        collection = self.mongo_db["backpack-events"]
        docs = list(collection.find())
        log.info("Migrating %d events …", len(docs))

        for doc in docs:
            eid = extract_id(doc)
            timestamp = doc.get("timestamp")
            if isinstance(timestamp, datetime):
                pass  # already a datetime
            elif timestamp is not None:
                timestamp = datetime.fromisoformat(str(timestamp))
            else:
                timestamp = datetime.now(timezone.utc)

            severity = resolve_enum(doc.get("severity"), SEVERITY_MAP)

            self.cur.execute(
                """
                INSERT INTO events (id, timestamp, source, message, severity, "user")
                VALUES (%s, %s, %s, %s, %s, %s)
                ON CONFLICT (id) DO NOTHING
                """,
                (
                    eid,
                    timestamp,
                    safe_str(doc.get("source", "")),
                    safe_str(doc.get("message", "")),
                    severity,
                    safe_str(doc.get("user", "")),
                ),
            )
        self.pg.commit()
        self.counts["events"] = len(docs)

    # ------------------------------------------------------------------
    # Schedules
    # ------------------------------------------------------------------
    def migrate_schedules(self):
        collection = self.mongo_db["backpack-schedules"]
        docs = list(collection.find())
        log.info("Migrating %d schedules …", len(docs))

        for doc in docs:
            sid = extract_id(doc)
            self.cur.execute(
                """
                INSERT INTO schedules (id, processor, cron, last_run, next_run)
                VALUES (%s, %s, %s, %s, %s)
                ON CONFLICT (id) DO NOTHING
                """,
                (
                    sid,
                    safe_str(doc.get("processor", "")),
                    safe_str(doc.get("cron", "")),
                    doc.get("last_run"),
                    doc.get("next_run"),
                ),
            )
        self.pg.commit()
        self.counts["schedules"] = len(docs)

    # ------------------------------------------------------------------
    # Pending Artifacts
    # ------------------------------------------------------------------
    def migrate_pending_artifacts(self):
        collection = self.mongo_db["pending-approvals"]
        docs = list(collection.find())
        log.info("Migrating %d pending artifacts …", len(docs))

        for doc in docs:
            pid = extract_id(doc)
            timestamp = doc.get("timestamp")
            if isinstance(timestamp, datetime):
                pass
            elif timestamp is not None:
                timestamp = datetime.fromisoformat(str(timestamp))
            else:
                timestamp = datetime.now(timezone.utc)

            self.cur.execute(
                """
                INSERT INTO pending_artifacts
                    (id, processor, filter, config, requested_by, timestamp)
                VALUES (%s, %s, %s, %s, %s, %s)
                ON CONFLICT (id, processor) DO NOTHING
                """,
                (
                    pid,
                    safe_str(doc.get("processor", "")),
                    safe_str(doc.get("filter", "")),
                    safe_dict_to_json(doc.get("config", {})),
                    safe_str(doc.get("requested_by", "")),
                    timestamp,
                ),
            )
        self.pg.commit()
        self.counts["pending_artifacts"] = len(docs)

    # ------------------------------------------------------------------
    # API Keys
    # ------------------------------------------------------------------
    def migrate_api_keys(self):
        collection = self.mongo_db["backpack-api-keys"]
        docs = list(collection.find())
        log.info("Migrating %d API keys …", len(docs))

        for doc in docs:
            kid = extract_id(doc)
            created_at = doc.get("created_at")
            if isinstance(created_at, datetime):
                pass
            elif created_at is not None:
                created_at = datetime.fromisoformat(str(created_at))
            else:
                created_at = datetime.now(timezone.utc)

            self.cur.execute(
                """
                INSERT INTO api_keys
                    (id, name, key, is_admin, created_at, created_by)
                VALUES (%s, %s, %s, %s, %s, %s)
                ON CONFLICT (id) DO NOTHING
                """,
                (
                    kid,
                    safe_str(doc.get("name", "")),
                    safe_str(doc.get("key", "")),
                    bool(doc.get("is_admin", False)),
                    created_at,
                    safe_str(doc.get("created_by", "")),
                ),
            )
        self.pg.commit()
        self.counts["api_keys"] = len(docs)

    # ------------------------------------------------------------------
    # News Posts
    # ------------------------------------------------------------------
    def migrate_news_posts(self):
        collection = self.mongo_db["backpack-news"]
        docs = list(collection.find())
        log.info("Migrating %d news posts …", len(docs))

        for doc in docs:
            nid = extract_id(doc)
            timestamp = doc.get("timestamp")
            if isinstance(timestamp, datetime):
                pass
            elif timestamp is not None:
                timestamp = datetime.fromisoformat(str(timestamp))
            else:
                timestamp = datetime.now(timezone.utc)

            self.cur.execute(
                """
                INSERT INTO news_posts (id, title, content, author, timestamp)
                VALUES (%s, %s, %s, %s, %s)
                ON CONFLICT (id) DO NOTHING
                """,
                (
                    nid,
                    safe_str(doc.get("title", "")),
                    safe_str(doc.get("content", "")),
                    safe_str(doc.get("author", "")),
                    timestamp,
                ),
            )
        self.pg.commit()
        self.counts["news_posts"] = len(docs)

    # ------------------------------------------------------------------
    # Run all
    # ------------------------------------------------------------------
    def run(self):
        try:
            self.create_schema()

            # Processors must be migrated first (foreign key target)
            self.migrate_processors()

            # Artifacts (discovers dynamic collections automatically)
            self.migrate_artifacts()

            # Flat collections
            self.migrate_events()
            self.migrate_schedules()
            self.migrate_pending_artifacts()
            self.migrate_api_keys()
            self.migrate_news_posts()

            if self.dry_run:
                log.info("DRY RUN — rolling back all changes.")
                self.pg.rollback()
            else:
                log.info("All migrations committed.")

            self.print_summary()

        except Exception:
            log.exception("Migration failed — rolling back.")
            self.pg.rollback()
            raise
        finally:
            self.cur.close()
            self.pg.close()
            self.mongo.close()

    def print_summary(self):
        log.info("=" * 60)
        log.info("Migration Summary")
        log.info("=" * 60)
        for table, count in self.counts.items():
            log.info("  %-30s %d rows", table, count)
        total = sum(self.counts.values())
        log.info("-" * 60)
        log.info("  %-30s %d rows", "TOTAL", total)
        log.info("=" * 60)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Migrate Backpack data from MongoDB to PostgreSQL."
    )
    parser.add_argument(
        "--mongo",
        default=os.getenv("BP_MONGO_STR", "mongodb://localhost:27017"),
        help="MongoDB connection string (default: $BP_MONGO_STR or mongodb://localhost:27017)",
    )
    parser.add_argument(
        "--pg",
        default=os.getenv("BP_PG_STR", "postgresql://localhost:5432/backpack"),
        help="PostgreSQL connection string (default: $BP_PG_STR or postgresql://localhost:5432/backpack)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Run the full migration but rollback at the end (no data written).",
    )
    parser.add_argument(
        "--verbose", "-v",
        action="store_true",
        help="Enable debug logging.",
    )
    args = parser.parse_args()

    if args.verbose:
        logging.getLogger().setLevel(logging.DEBUG)

    migrator = Migrator(
        mongo_uri=args.mongo,
        pg_uri=args.pg,
        dry_run=args.dry_run,
    )
    migrator.run()


if __name__ == "__main__":
    main()
