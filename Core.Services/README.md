# Core.Services

Core.Services provides the business logic and service implementations for the Backpack ecosystem. It acts as an
abstraction layer between the message consumers (in Core.Gateway) and the underlying data storage and external systems.

## Key Components

- **ArtifactService**: Implements `IArtifactService`, providing methods for:
    - Ingesting new artifacts (`Process`)
    - Collecting physical files (`Collect`)
    - Routing collection requests (`Route`)
- **CoreDatabase**: Implements `ICoreDatabase`, handling interactions with MongoDB for storing artifact metadata and
  state.
- **CoreCache**: Implements `ICoreCache`, using Redis for caching frequently accessed data and managing locks.

## Interaction with Other Services

This project is a library used by `Core.Gateway` and `Integration.API`. It encapsulates the core domain logic, ensuring
consistency across different entry points to the system.
