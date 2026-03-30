# Core.Infrastructure

`Core.Infrastructure` provides the implementation for the core services, databases, and caching layers used throughout
the Backpack system. It bridges the gap between the interfaces defined in `Core.Services` and the actual underlying data
storage and messaging technologies.

## Role

The infrastructure project acts as the "Engine Room" of the Backpack ecosystem, handling the heavy lifting of data
persistence, message state management, and implementation of the business-level `IArtifactService`.

## Key Components

- **MongoDatabase**: The implementation of `ICoreDatabase` for MongoDB. It handles the storage and retrieval of
  artifacts, versions, and dependencies.
- **CoreCache**: The implementation of `ICoreCache` using Redis. It's used for deduplication of processing requests and
  managing temporary state across the system.
- **ArtifactService**: Implements `IArtifactService` to manage artifact ingestion, processing, routing, and collection
  requests.
- **RabbitMqStatusService**: Implements `IStatusService` for reporting RabbitMQ health and status.
- **DatabaseFactory**: Provides a way to create database connections.

## Interactions

- **Used By**: `Core.Gateway`, `Integration.API`, and potentially other services requiring direct access to the database
  or artifact services.
- **External Dependencies**:
    - **MongoDB**: For artifact metadata storage.
    - **Redis**: For distributed caching.
    - **RabbitMQ**: Indirectly through MassTransit for messaging state.
    - **Dapper**: Used for SQL-based database operations (when applicable).

## Implementation Details

`Core.Infrastructure` directly implements the interfaces defined in `Core.Services`. This separation allows for easier
testing and swapping of implementations (e.g., using a different database provider in the future).
