<h1 align="center"><img src="images/backpack-256.png" alt="backpack"/></h1>

:warning: Warning: This project is still under heavy development, to be considered a beta, at best.

Backpack is used to collect and store a varied and large amounts of artifacts, for example npm, nuget...

Primary use-case is transferring to airgapped / high security networks for offline consumption via an artifact repository.

The complex keeps track of already collected artifacts, dependencies, and versions.

The Backpack's collection of artifacts is greedy, it will by default collect every version and every dependency version, recursively, 
until every item is collected, artifact filters can be applied to reduce the amount collected.

It builds daily deltas to reduce the day-to-day load on the transfer device.

The design is microservice oriented and intended to minimize the amount of work needed to build a new "Processor", for example maven.

Processors can safely rely on the core infrastructure of Backpack, the core infrastructure handles the memorization of metadata, 
and the collection via standard protocols, for example http, git, or docker.
## Deploy
An early stage helm chart is available at
https://github.com/linus-berg/helm-charts/tree/main/charts/backpack

https://linus-berg.github.io/helm-charts/

See the `values.examples.yaml` file for configuration.
The auxiliary services such as mongodb still needs to be provided by the administrator.

## Recommended minimum requirements (single node)
| Resource | Req                                  |
|----------|--------------------------------------|
| RAM      | 16GB                                 |
| CPU      | 8 cores                              |
| Storage  | 2TB * Collector (depending on usage) |
| Network  | >= 1 Gbps                            |

Backpack is designed to be horizontally scalable and suitable for kubernetes, however, kubernetes deployments have not been tested thoroughly.

The following services are required to run the complete suite of Backpack capabilities.
A batteries included starter pack is included under /examples and /Compose, however, these are not production ready configurations, and are only intended as an development/test environment.


| Service                 | Requirement                             |
|-------------------------|-----------------------------------------|
| OIDC Provider           | Required (only for API and GUI)         |
| RabbitMq                | Required                                |
| Minio                   | Required                                |
| MongoDb                 | Required                                |
| Redis                   | Required                                |
| Container Registry      | Required (only for Collector.Container) |
| OpenTelemetry Collector | Recommended but required for Telemetry  | 
| Grafana                 | Recommended                             | 
| Prometheus              | Recommended                             | 
| Tempo                   | Recommended                             | 


## Architecture

### Message Propagation Diagram
The following diagram illustrates how messages flow between the different services in the Backpack ecosystem.

```mermaid
graph TD
    API[Integration.API] -- ArtifactIngestRequest --> GW(Core.Gateway)
    SCHED[Tracker.Scheduler] -- ArtifactIngestRequest --> GW
    GW -- ArtifactProcessRequest --> PROC{Processors}
    PROC -- ArtifactProcessedRequest --> GW
    GW -- ArtifactRouteRequest --> ROUTER[Collector.Router]
    ROUTER -- ArtifactCollectRequest --> COLL{Collectors}
    COLL -- Download --> STORAGE[(S3 Storage)]

    subgraph Processors
        PROC_NPM[Processor.Npm]
        PROC_PYPI[Processor.Pypi]
        PROC_...[...]
    end

    subgraph Collectors
        COLL_HTTP[Collector.Http]
        COLL_GIT[Collector.Git]
        COLL_...[...]
    end
```

## Modules
| Tag       | Name                        | Description                                                           |
|-----------|-----------------------------|-----------------------------------------------------------------------|
| Tracker   | Artifact Tracking Module    | Handles the tracking of artifacts to request an update by the APC.    |
| Processor | Artifact Processing Module  | Handles the processing of a defined artifact-type.                    |
| Collector | Artifact Collector Module   | Handles the collection of artifact-types based on standard protocols. |

## Services

### Core Services
| Service             | Description                                                                                                                              |
|---------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| `Core.Gateway`      | The central message bus for the Backpack system. It uses MassTransit to route messages between services.                                   |
| `Tracker.Scheduler` | Schedules and triggers artifact tracking jobs using Quartz.NET. It reads a schedule configuration to define when to check for new artifacts. |
| `Integration.API`   | A RESTful API that provides an external interface to the Backpack system. It uses generic OIDC for authentication and authorization.           |

### Collectors
Collectors are responsible for retrieving artifacts from their sources.

| Service                   | Description                                                                                             |
|---------------------------|---------------------------------------------------------------------------------------------------------|
| `Collector.Git`           | Collects artifacts from Git repositories.                                                               |
| `Collector.Http`          | Collects artifacts over HTTP/HTTPS.                                                                     |
| `Collector.Wget`          | A collector that uses `wget` to download artifacts.                                                     |
| `Collector.Rsync`         | A collector that uses `rsync` to synchronize artifacts.                                                 |
| `Collector.Router`        | Routes collection requests to the appropriate collector based on the artifact type.                     |
| `Collector.Container`     | Collects container images from a registry.                                                              |
| `Collector.DockerArchive` | Collects Docker images from a local Docker archive (`.tar` file).                                       |

### Processors
Processors are responsible for extracting metadata and dependencies from artifacts.

| Service                       | Description                                                                                             |
|-------------------------------|---------------------------------------------------------------------------------------------------------|
| `Processor.Npm`               | Processes Node.js packages from the npm registry.                                                       |
| `Processor.Php`               | Processes PHP packages.                                                                                 |
| `Processor.Helm`              | Processes Helm charts.                                                                                  |
| `Processor.Pypi`              | Processes Python packages from PyPI.                                                                    |
| `Processor.Maven`             | Processes Java packages from a Maven repository.                                                        |
| `Processor.Nuget`             | Processes .NET packages from a NuGet repository.                                                        |
| `Processor.Rancher`           | Processes Rancher artifacts.                                                                            |
| `Processor.Container`         | Processes container images to extract metadata.                                                         |
| `Processor.Terraform`         | Processes Terraform modules.                                                                            |
| `Processor.OperatorHub`       | Processes operators from OperatorHub.io.                                                                |
| `Processor.Jetbrains.IDE`     | Processes JetBrains IDEs.                                                                               |
| `Processor.Github.Releases`   | Processes releases from GitHub.                                                                         |
| `Processor.Jetbrains.Plugins` | Processes plugins for JetBrains IDEs.                                                                   |

### Other Services
| Service                | Description                                                                                             |
|------------------------|---------------------------------------------------------------------------------------------------------|
| `Backpack.Tester`      | A service for testing Backpack functionality.                                                           |
| `Backpack.Toolbox`     | A service containing various tools for interacting with the Backpack system.                              |
| `Backpack.GitUnpack`   | A service for unpacking Git repositories.                                                               |

## Development

### Scaffolding New Processors
Backpack provides a official .NET template to quickly scaffold new artifact processors.

#### Installation
Run the following command from the root of the repository:
```bash
dotnet new install ./Templates/ProcessorTemplate
```

#### Usage
To create a new processor (e.g., for Go Modules):
```bash
mkdir Processor.Go
cd Processor.Go
dotnet new backpack-processor -n Go --ENDPOINT go --BASE_URL https://proxy.golang.org
```

This will generate a pre-configured project including:
- A standardized MassTransit consumer.
- Automatic integration with the system-wide activity feed.
- Interface-driven logic classes for metadata fetching.

## Environment Variables
| Name                            | Default   | Modules                            |
|---------------------------------|-----------|------------------------------------|
| BP_OTEL_HOST                    | -         | Gateway, API, Processor, Collector |
| BP_RABBIT_MQ_HOST               | localhost | Gateway, API, Processor, Collector |
| BP_RABBIT_MQ_USER               | guest     | Gateway, API, Processor, Collector |
| BP_RABBIT_MQ_PASS               | guest     | Gateway, API, Processor, Collector |
| BP_REDIS_HOST                   | localhost | Gateway, API                       |
| BP_REDIS_USER                   | -         | Gateway, API                       |
| BP_REDIS_PASS                   | -         | Gateway, API                       |
| BP_MONGO_STR                    | -         | Gateway, API                       |
| BP_API_HOST                     | localhost | GUI, Tracker, API                  |
| BP_API_PORT                     | 4000      | GUI, Tracker, API                  |
| BP_S3_ACCESS_KEY                | -         | Collector                          |
| BP_S3_SECRET_KEY                | -         | Collector                          |
| BP_S3_REGION                    | -         | Collector                          |
| BP_S3_ENDPOINT                  | -         | Collector                          |
| BP_S3_BUCKET                    | -         | Collector                          |
| BP_COLLECTOR_DIRECTORY          | /data/    | Collector.Git                      |
| BP_COLLECTOR_HTTP_DELTA         | true      | Collector.Http                     |
| BP_COLLECTOR_HTTP_MODE          | lake      | Collector.Http                     |
| BP_COLLECTOR_CONTAINER_REGISTRY | -         | Collector.Container                |
