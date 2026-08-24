/**
 * Request and response shapes for the backend API.
 *
 * HAND-WRITTEN FOR NOW. The API exposes an OpenAPI document at /openapi/v1.json (Development),
 * and the intent is to replace this file with generated output once the auth module settles.
 * That is why every type here mirrors the server contract exactly rather than being reshaped
 * for convenience - the swap should be a delete-and-regenerate, not a rewrite of call sites.
 */

export type HealthStatus = "Healthy" | "Degraded" | "Unhealthy";

export interface HealthResponse {
  status: HealthStatus;
  checkedAtUtc: string;
  components: Record<string, HealthStatus>;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
  acceptedTerms: boolean;
}

export interface RegisterResponse {
  userId: string;
  email: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  /**
   * When true the token is a limited-scope credential that authorises nothing except the
   * change-password call. The UI must route straight to the forced-reset screen.
   */
  mustChangePassword: boolean;
}

export interface ForgotPasswordRequest {
  email: string;
}

/** RFC 7807 problem document, as produced by the API's exception middleware. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Present on validation failures: field name -> messages. */
  errors?: Record<string, string[]>;
  /** Stable machine-readable error code, e.g. "auth.login.invalid_credentials". */
  code?: string;
  traceId?: string;
}
