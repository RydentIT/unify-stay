import { type ApiClient } from "../client";
import {
  type ForgotPasswordRequest,
  type LoginRequest,
  type LoginResponse,
  type RegisterRequest,
  type RegisterResponse,
} from "../types";

/**
 * Auth calls, matching the routes under /api/v1/auth.
 *
 * NOTE: every one of these currently resolves to a 501 from the backend - the endpoints exist
 * and validate their input, but the handlers are scaffold stubs. Callers should expect
 * ApiError.isNotImplemented until the auth module lands.
 */
export function createAuthEndpoints(client: ApiClient) {
  return {
    register(request: RegisterRequest, options?: { signal?: AbortSignal }) {
      return client.post<RegisterResponse>("/api/v1/auth/register", request, options);
    },

    login(request: LoginRequest, options?: { signal?: AbortSignal }) {
      return client.post<LoginResponse>("/api/v1/auth/login", request, options);
    },

    forgotPassword(request: ForgotPasswordRequest, options?: { signal?: AbortSignal }) {
      return client.post<void>("/api/v1/auth/forgot-password", request, options);
    },
  };
}

export type AuthEndpoints = ReturnType<typeof createAuthEndpoints>;
