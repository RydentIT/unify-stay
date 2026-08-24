import { type ApiClient } from "../client";
import { type HealthResponse } from "../types";

export function createHealthEndpoints(client: ApiClient) {
  return {
    /** Readiness. Round trips through every backend layer, including the database. */
    ready(options?: { signal?: AbortSignal }) {
      return client.get<HealthResponse>("/health", options);
    },

    /** Liveness. Touches no dependency. */
    live(options?: { signal?: AbortSignal }) {
      return client.get<{ status: string }>("/health/live", options);
    },
  };
}

export type HealthEndpoints = ReturnType<typeof createHealthEndpoints>;
