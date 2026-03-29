# Development environment
### Docker compose
Run the external services in the Compose folder with
`docker compose up`.

This will setup the required external services required to run the APC framework.

### OIDC Provider (required for GUI & API)
You can use any OIDC-compatible provider (e.g., Keycloak, Auth0, Okta).

1.  **Configure Authority and Audience**:
    *   Set the `OIDC_AUTHORITY` environment variable for the API (e.g., `http://localhost:8080/realms/backpack`).
    *   Set the `OIDC_AUDIENCE` environment variable for the API (e.g., `backpack-gui`).
    *   For the GUI, update the `VITE_OIDC_AUTHORITY` and `VITE_OIDC_CLIENT_ID` in your `.env` or `oidc.ts`.

2.  **Client Configuration**:
    *   Create a public client.
    *   Set Valid Redirect URIs to `*` (for development).
    *   Set Web Origins to `*`.

3.  **Roles**:
    *   The application expects an `Administrator` role.
    *   By default, the API looks for roles in the `resource_access` claim (Keycloak style) or a flat `roles` claim.

### Minio (required for ACM development)
Create a bucket named `apc`.

Create an access key and secret key with the credentials `minio-apc` in both fields.
