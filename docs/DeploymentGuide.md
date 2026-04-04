# Deployment Guide

This guide provides instructions for deploying a full Backpack solution, ranging from development environments to production-ready Kubernetes clusters.

## 📋 Infrastructure Requirements

A complete Backpack deployment requires the following infrastructure services:

- **Messaging**: RabbitMQ (with Management plugin recommended).
- **Object Storage**: MinIO or AWS S3.
- **Metadata Store**: MongoDB.
- **Cache**: Redis.
- **Identity Provider**: Any OIDC-compatible provider (e.g., Keycloak, Auth0).
- **Observability (Optional)**: OpenTelemetry Collector, Prometheus, Grafana, Tempo.

---

## 🐳 Option 1: Docker Compose (Development / Small Scale)

The quickest way to get a full stack running is using the provided Docker Compose configuration.

### 1. Initialize Infrastructure
Navigate to the `backpack/Compose` directory and start the core services:

```bash
cd backpack/Compose
docker-compose up -d
```

This will spin up:
- MongoDB, Redis, RabbitMQ.
- MinIO (S3 compatible storage).
- Keycloak (OIDC Provider) with a pre-configured `backpack` realm.
- OpenTelemetry stack (OTEL Collector, Prometheus, Tempo, Grafana).

### 2. Configure Environment Variables
Ensure your environment variables are set correctly in `vars.env`. For a comprehensive list of all available environment variables and their functions, refer to the **[Configuration Guide](Configuration.md)**.

Key variables include:
- `BP_RABBIT_MQ_HOST`
- `BP_MONGO_STR`
- `BP_S3_ENDPOINT`
- `OIDC_AUTHORITY`

### 3. Start Backpack Services
Once the infrastructure is healthy, you can start the individual Backpack microservices (Gateway, API, Processors, and Collectors) as per your requirements.

---

## ☸️ Option 2: Kubernetes & Helm (Production Scale)

For large-scale, resilient deployments, use the official Helm charts hosted at the **[linus-berg/helm-charts](https://linus-berg.github.io/helm-charts/)** repository.

### 1. Prerequisites
- A functional Kubernetes cluster.
- Helm 3.x installed.
- Access to the infrastructure services (can be external or deployed within the cluster).

### 2. Add the Helm Repository
```bash
helm repo add backpack https://linus-berg.github.io/helm-charts/
helm repo update
```

### 3. Configure Values
You can retrieve the default values from the chart:

```bash
# Get default values from chart
helm show values backpack/backpack > my-values.yaml
```

Update the following sections in `my-values.yaml`:
- **`oidc`**: Set your `authority` and `audience`.
- **`s3`**: Configure your S3 endpoint, bucket, and credentials.
- **Infrastructure**: Point `rabbitmq`, `mongo`, and `redis` to your cluster-internal or external services.
- **Replicas**: Adjust the `replicas` count for the API, Gateway, and specific Processors/Collectors based on expected load.

### 4. Deploy
Install the chart into your namespace:

```bash
helm install backpack backpack/backpack -f my-values.yaml -n backpack --create-namespace
```

---

## 🔐 Security & OIDC Configuration

Backpack relies on OpenID Connect for securing the API and GUI.

### Keycloak Setup
If using the provided Keycloak configuration:
1. Create a client named `backpack` (Public).
2. Set valid redirect URIs and web origins to your GUI's URL.
3. Ensure the `Administrator` role is mapped to users who require full system access.

### API Configuration
The `Integration.API` requires the following settings:
- `OIDC_AUTHORITY`: The URL of your identity provider's realm.
- `OIDC_AUDIENCE`: The client ID configured in your provider.

---

## 🗄 Storage Preparation

### S3 / MinIO
Ensure the bucket specified in your configuration (default: `backpack`) exists. If using MinIO, create the bucket via the console or CLI before starting the collectors.

### MongoDB
The Gateway will automatically initialize the necessary collections and indexes on first startup if the provided credentials have sufficient permissions.
