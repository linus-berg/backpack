# Full Backpack Deployment Example

This directory contains a comprehensive example of how to orchestrate the entire Backpack ecosystem using Docker Compose.

> [!WARNING]
> This configuration is intended for architectural reference and local evaluation. It is **not** hardened for production use.

## 🏗 Overview

The provided `docker-compose.yml` provisions the following:

- **Backpack Services**: Core Gateway, Integration API, Scheduler, and a selection of Processors and Collectors.
- **Messaging**: RabbitMQ.
- **Storage**: MongoDB (Metadata) and MinIO (Object Storage).
- **Security**: Keycloak (OIDC Provider).
- **Observability**: A full OpenTelemetry stack (OTEL Collector, Prometheus, Tempo, Grafana).

## 🚀 Getting Started

1. **Configure Environment**:
   Copy the example environment file and adjust the values as needed:
   ```bash
   cp vars.env .env
   ```

2. **Launch the Stack**:
   ```bash
   docker-compose up -d
   ```

3. **Verify Deployment**:
   - **Backpack GUI**: `http://localhost:3000` (once deployed)
   - **Keycloak**: `http://localhost:8090`
   - **MinIO Console**: `http://localhost:9001`
   - **RabbitMQ Management**: `http://localhost:15672`

## 🔐 Initial Setup

Detailed instructions for initializing specific services can be found in the following guides:
- [Keycloak Initialization](init-keycloak.md)
- [MongoDB Initialization](init-mongodb.md)
