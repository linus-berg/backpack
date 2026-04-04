# Data Type & Interface Reference

This document defines the core schemas used for communication between Backpack services. These types are sent as JSON over RabbitMQ.

## 1. Artifact
The central entity in the system.

| Field | Type | Description |
| :--- | :--- | :--- |
| `id` | `string` | Unique identifier (e.g., `lodash`). |
| `processor` | `string` | Name of the ecosystem handler. |
| `filter` | `string` | Regex or SemVer range for version selection. |
| `config` | `map<string, string>` | Ecosystem-specific configuration. |
| `versions` | `map<string, ArtifactVersion>` | Dictionary of discovered versions. |
| `dependencies` | `Array<ArtifactDependency>` | List of transitive dependencies. |

## 2. ArtifactVersion
Represents a specific release of a package.

| Field | Type | Description |
| :--- | :--- | :--- |
| `version` | `string` | The version string. |
| `status` | `int` | Internal status code (usually `0` for new). |
| `files` | `map<string, ArtifactFile>` | Dictionary of files associated with this version. |

## 3. ArtifactFile
Represents a specific binary or metadata file to be collected.

| Field | Type | Description |
| :--- | :--- | :--- |
| `uri` | `string` | The remote URL to download the file from. |
| `folder` | `string` | Optional relative path override in S3. |
| `collected` | `bool` | Whether the file has been successfully mirrored. |
| `error` | `string` | Detailed error message if collection failed. |

## 4. ArtifactDependency
A reference to another artifact required by the parent.

| Field | Type | Description |
| :--- | :--- | :--- |
| `id` | `string` | The ID of the dependency. |
| `processor` | `string` | The processor capable of handling this dependency. |
| `config` | `map<string, string>` | Configuration passed to the child processor. |

## 5. Processor Expectations

Any processor (standard or raw) is expected to handle the following responsibilities:

1.  **Metadata Discovery**: Convert a single Artifact ID into a complete list of versions.
2.  **Exhaustive Discovery**: Identify **direct** dependencies. The Gateway handles the recursion; the processor only needs to look one level deep.
3.  **URI Mapping**: Provide direct download links for every file in every version.
4.  **Filter Application**: While the Router filters versions later, the processor should return all metadata found; the system will decide what to collect based on the user's regex.
5.  **State Ignorance**: Processors should be stateless. Do not check if a file already exists in S3 or if a version is in the DB; the Gateway and Router handle those checks.
