import { type ProblemDetails } from "./types";

/**
 * Thrown for any non-2xx response. Carries the parsed problem document when the server sent
 * one, so callers can render field-level validation messages without re-parsing the body.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  /** Field name -> messages, empty when the failure was not a validation failure. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }

  get code(): string | undefined {
    return this.problem?.code;
  }

  /** True while the backend endpoint is still a scaffold stub. */
  get isNotImplemented(): boolean {
    return this.status === 501;
  }

  get isRateLimited(): boolean {
    return this.status === 429;
  }
}

/** Raised when the request never reached the server (offline, DNS, CORS, abort). */
export class ApiNetworkError extends Error {
  constructor(message: string, options?: { cause?: unknown }) {
    super(message, options);
    this.name = "ApiNetworkError";
  }
}
