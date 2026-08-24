import { describe, expect, it, vi } from "vitest";

import { ApiError, ApiNetworkError, createUnifyApi } from "../src/index";

function jsonResponse(body: unknown, init: ResponseInit = {}): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "content-type": "application/json" },
    ...init,
  });
}

describe("ApiClient", () => {
  it("sends the request to the configured base URL and parses the body", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({ status: "Healthy", checkedAtUtc: "2026-08-24T00:00:00Z", components: {} }),
    );

    const api = createUnifyApi({ baseUrl: "http://api.test", fetch: fetchMock });
    const health = await api.health.ready();

    expect(health.status).toBe("Healthy");
    expect(fetchMock).toHaveBeenCalledOnce();
    expect(fetchMock.mock.calls[0]?.[0]).toBe("http://api.test/health");
  });

  it("strips a trailing slash from the base URL so paths do not double up", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ status: "alive" }));

    const api = createUnifyApi({ baseUrl: "http://api.test/", fetch: fetchMock });
    await api.health.live();

    expect(fetchMock.mock.calls[0]?.[0]).toBe("http://api.test/health/live");
  });

  it("attaches a bearer token when one is available", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ status: "alive" }));

    const api = createUnifyApi({
      baseUrl: "http://api.test",
      fetch: fetchMock,
      getAccessToken: () => "token-123",
    });

    await api.health.live();

    const init = fetchMock.mock.calls[0]?.[1] as RequestInit;
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer token-123");
  });

  it("sends no Authorization header when there is no token", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ status: "alive" }));

    const api = createUnifyApi({ baseUrl: "http://api.test", fetch: fetchMock });
    await api.health.live();

    const init = fetchMock.mock.calls[0]?.[1] as RequestInit;
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it("surfaces field-level validation errors from a problem document", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(
        { title: "One or more validation errors occurred.", errors: { Email: ["Not a valid address."] } },
        { status: 400, headers: { "content-type": "application/problem+json" } },
      ),
    );

    const api = createUnifyApi({ baseUrl: "http://api.test", fetch: fetchMock });

    const error = await api.auth
      .register({ email: "nope", password: "short", displayName: "", acceptedTerms: false })
      .catch((caught: unknown) => caught);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).fieldErrors.Email).toEqual(["Not a valid address."]);
  });

  it("flags the scaffold stubs so callers can tell 501 from a real failure", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(
        { title: "Login is not implemented yet.", code: "auth.login.not_implemented" },
        { status: 501, headers: { "content-type": "application/problem+json" } },
      ),
    );

    const api = createUnifyApi({ baseUrl: "http://api.test", fetch: fetchMock });

    const error = (await api.auth
      .login({ email: "someone@example.com", password: "correct-horse-battery" })
      .catch((caught: unknown) => caught)) as ApiError;

    expect(error.isNotImplemented).toBe(true);
    expect(error.code).toBe("auth.login.not_implemented");
  });

  it("wraps a transport failure as ApiNetworkError rather than leaking fetch internals", async () => {
    const fetchMock = vi.fn().mockRejectedValue(new TypeError("Failed to fetch"));

    const api = createUnifyApi({ baseUrl: "http://api.test", fetch: fetchMock });

    await expect(api.health.live()).rejects.toBeInstanceOf(ApiNetworkError);
  });

  it("refuses to construct without a base URL", () => {
    expect(() => createUnifyApi({ baseUrl: "" })).toThrow(/NEXT_PUBLIC_API_URL/);
  });
});
