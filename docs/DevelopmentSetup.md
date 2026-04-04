# Development Setup & Environment Configuration

This guide outlines the technical requirements and configuration steps for establishing a local development environment for the Backpack ecosystem.

## 🛠 Prerequisites

Ensure the following tools are installed on your workstation:
- **.NET 8.0 SDK**
- **Docker & Docker Compose**
- **Node.js (LTS)** & **Yarn** (for `backpack-gui` development)
- **IDE**: JetBrains Rider (preferred) or Visual Studio 2022 with C# Dev Kit.

---

## 🏗 Orchestrating Infrastructure Services

Backpack relies on several external state and messaging providers. The most efficient way to provision these locally is via the provided Docker Compose configuration.

### Provisioning via Docker Compose
Navigate to the `backpack/Compose` directory and initiate the infrastructure stack:

```bash
cd backpack/Compose
docker compose up -d
```

This stack includes:
- **RabbitMQ**: The asynchronous message broker for MassTransit.
- **MongoDB**: Primary persistent store for artifact metadata and tracking state.
- **Redis**: Distributed cache for ingestion rate-limiting and OIDC token validation.
- **MinIO**: S3-compatible object storage for artifact persistence.
- **Keycloak**: Pre-configured OIDC provider for identity management.

---

## 🔐 Identity & Access Management (OIDC)

The `Integration.API` and `backpack-gui` require a functional OpenID Connect (OIDC) provider for authentication and Role-Based Access Control (RBAC).

### 1. Provider Configuration
While any OIDC-compliant provider is supported, the local environment is optimized for Keycloak.
- **Authority**: The realm URL (e.g., `http://localhost:8090/realms/backpack`).
- **Audience**: The Client ID, typically `backpack`.

### 2. Client Configuration
Ensure your OIDC client is configured as follows:
- **Client Protocol**: `openid-connect`.
- **Access Type**: `public`.
- **Redirect URIs**: `*` (development only).
- **Web Origins**: `*` (development only).

### 3. Role Mapping
Backpack expects an `Administrator` role within the token claims to grant elevated privileges. By default, the API inspects the `resource_access` claim (Keycloak style) or a flat `roles` array.

---

## 🗄 Object Storage Preparation (MinIO)

To facilitate development of the Artifact Collection Modules (ACM):
1. Access the MinIO Console (typically `http://localhost:9001`).
2. Create a primary bucket named `backpack`.
3. Provision an Access Key and Secret Key. For development, `minio-bp` is recommended for both fields to maintain consistency with the default configuration.

---

## 🚀 Running the Services

### Backend Services
Individual microservices can be started via the .NET CLI or your IDE's runner. Ensure the environment variables defined in the **[Configuration Guide](Configuration.md)** are correctly applied.

```bash
# Example: Starting the Core Gateway
cd backpack/Core.Gateway
dotnet run
```

### Frontend (backpack-gui)
The dashboard can be started using Yarn:

```bash
cd backpack-gui
yarn install
yarn dev
```
