import { ApiClient, type ApiClientOptions } from "./client";
import { createAdminUserEndpoints, type AdminUserEndpoints } from "./endpoints/admin-users";
import { createAuthEndpoints, type AuthEndpoints } from "./endpoints/auth";
import { createHealthEndpoints, type HealthEndpoints } from "./endpoints/health";
import { createProfileEndpoints, type ProfileEndpoints } from "./endpoints/profile";
import { createSettingsEndpoints, type SettingsEndpoints } from "./endpoints/settings";

export { ApiClient, type ApiClientOptions, type RequestOptions } from "./client";
export { ApiError, ApiNetworkError } from "./errors";
export * from "./types";
export { type AdminUserEndpoints, type UserDirectoryQuery } from "./endpoints/admin-users";
export { type AuthEndpoints } from "./endpoints/auth";
export { type HealthEndpoints } from "./endpoints/health";
export { type ProfileEndpoints } from "./endpoints/profile";
export { type SettingsEndpoints } from "./endpoints/settings";

export interface UnifyApi {
  auth: AuthEndpoints;
  profile: ProfileEndpoints;
  settings: SettingsEndpoints;
  adminUsers: AdminUserEndpoints;
  health: HealthEndpoints;
  raw: ApiClient;
}

/**
 * Builds the API surface used by both portals. Grouping the endpoints behind one object keeps
 * call sites stable if these move to generated code later.
 */
export function createUnifyApi(options: ApiClientOptions): UnifyApi {
  const client = new ApiClient(options);

  return {
    auth: createAuthEndpoints(client),
    profile: createProfileEndpoints(client),
    settings: createSettingsEndpoints(client),
    adminUsers: createAdminUserEndpoints(client),
    health: createHealthEndpoints(client),
    raw: client,
  };
}
