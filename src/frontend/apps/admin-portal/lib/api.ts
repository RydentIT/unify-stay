import { createUnifyApi } from "@unify/api-client";

import { readAccessToken } from "./session";

/**
 * The single API instance for this app. Pages and components import this rather than calling
 * fetch directly, so the base URL, auth header and error shape are decided in one place.
 */
export const apiBaseUrl = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

export const api = createUnifyApi({
  baseUrl: apiBaseUrl,
  getAccessToken: () => readAccessToken(),
});
