import { ApiError, ApiNetworkError } from "./errors";
import { type ProblemDetails } from "./types";

export interface ApiClientOptions {
  /** Base URL of the backend, e.g. http://localhost:5000. Trailing slashes are tolerated. */
  baseUrl: string;
  /**
   * Supplies the bearer token for authenticated calls. Async so a caller can refresh before
   * returning. Return undefined to send the request unauthenticated.
   */
  getAccessToken?: () => string | undefined | Promise<string | undefined>;
  /** Injectable for tests and for server components that need a custom fetch. */
  fetch?: typeof globalThis.fetch;
  /** Applied to every request unless the caller passes its own signal. */
  defaultTimeoutMs?: number;
}

export interface RequestOptions {
  signal?: AbortSignal;
  headers?: Record<string, string>;
}

const DEFAULT_TIMEOUT_MS = 15_000;

export class ApiClient {
  readonly #baseUrl: string;
  readonly #getAccessToken: ApiClientOptions["getAccessToken"];
  readonly #fetch: typeof globalThis.fetch;
  readonly #timeoutMs: number;

  constructor(options: ApiClientOptions) {
    if (!options.baseUrl) {
      throw new Error("ApiClient requires a baseUrl. Set NEXT_PUBLIC_API_URL.");
    }

    this.#baseUrl = options.baseUrl.replace(/\/+$/, "");
    this.#getAccessToken = options.getAccessToken;
    this.#fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
    this.#timeoutMs = options.defaultTimeoutMs ?? DEFAULT_TIMEOUT_MS;
  }

  get<TResponse>(path: string, options?: RequestOptions): Promise<TResponse> {
    return this.#send<TResponse>("GET", path, undefined, options);
  }

  post<TResponse>(path: string, body?: unknown, options?: RequestOptions): Promise<TResponse> {
    return this.#send<TResponse>("POST", path, body, options);
  }

  async #send<TResponse>(
    method: string,
    path: string,
    body: unknown,
    options?: RequestOptions,
  ): Promise<TResponse> {
    const headers: Record<string, string> = {
      Accept: "application/json",
      ...options?.headers,
    };

    if (body !== undefined) {
      headers["Content-Type"] = "application/json";
    }

    const token = await this.#getAccessToken?.();
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    // Every request gets a deadline. Without one a hung backend leaves the UI spinning
    // forever with no way for the user to recover.
    const timeout = AbortSignal.timeout(this.#timeoutMs);
    const signal = options?.signal
      ? AbortSignal.any([options.signal, timeout])
      : timeout;

    let response: Response;

    try {
      response = await this.#fetch(`${this.#baseUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal,
      });
    } catch (cause) {
      throw new ApiNetworkError(`Request to ${method} ${path} failed.`, { cause });
    }

    if (!response.ok) {
      throw new ApiError(response.status, `${method} ${path} failed with ${response.status}.`, await readProblem(response));
    }

    if (response.status === 204) {
      return undefined as TResponse;
    }

    return (await response.json()) as TResponse;
  }
}

/** Best-effort parse; a server that returned HTML or nothing must not mask the real status. */
async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    const contentType = response.headers.get("content-type") ?? "";

    if (!contentType.includes("json")) {
      return undefined;
    }

    return (await response.json()) as ProblemDetails;
  } catch {
    return undefined;
  }
}
