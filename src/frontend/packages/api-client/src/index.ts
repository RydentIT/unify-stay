import { ApiClient, type ApiClientOptions } from "./client";
import { createAuthEndpoints, type AuthEndpoints } from "./endpoints/auth";
import { createHealthEndpoints, type HealthEndpoints } from "./endpoints/health";

export { ApiClient, type ApiClientOptions, type RequestOptions } from "./client";
export { ApiError, ApiNetworkError } from "./errors";
export * from "./types";
export { type AuthEndpoints } from "./endpoints/auth";
export { type HealthEndpoints } from "./endpoints/health";

export interface UnifyApi {
  auth: AuthEndpoints;
  health: HealthEndpoints;
  raw: ApiClient;
}

/**
 * Builds the API surface used by both portals. Grouping endpoints behind one object keeps the
 * call sites stable if these move to generated code later.
 */
export function createUnifyApi(options: ApiClientOptions): UnifyApi {
  const client = new ApiClient(options);

  return {
    auth: createAuthEndpoints(client),
    health: createHealthEndpoints(client),
    raw: client,
  };
}
