import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const loginMock = vi.fn();
const replaceMock = vi.fn();

vi.mock("@/lib/api", () => ({
  api: { auth: { login: (...args: unknown[]) => loginMock(...args) } },
  apiBaseUrl: "http://api.test",
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock, push: vi.fn() }),
}));

const { default: LoginPage } = await import("@/app/login/page");

describe("LoginPage", () => {
  beforeEach(() => {
    loginMock.mockReset();
    replaceMock.mockReset();
    window.localStorage.clear();
  });

  async function signIn(password = "correct-horse-battery") {
    await userEvent.type(screen.getByLabelText("Email"), "student@example.com");
    await userEvent.type(screen.getByLabelText("Password"), password);
    await userEvent.click(screen.getByRole("button", { name: "Log in" }));
  }

  it("requires both fields before contacting the server", async () => {
    render(<LoginPage />);

    await userEvent.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByText("Email is required.")).toBeInTheDocument();
    expect(screen.getByText("Password is required.")).toBeInTheDocument();
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("sends the typed credentials through the api client", async () => {
    loginMock.mockResolvedValue({
      accessToken: "token",
      expiresAt: "2026-08-24T13:00:00Z",
      mustChangePassword: false,
      refreshToken: "refresh",
      refreshTokenExpiresAt: "2026-08-31T12:00:00Z",
      roles: ["Student"],
    });

    render(<LoginPage />);
    await signIn();

    await waitFor(() =>
      expect(loginMock).toHaveBeenCalledWith({
        email: "student@example.com",
        password: "correct-horse-battery",
        rememberMe: false,
      }),
    );
  });

  /** BR-LOG-005: the flag reaches the API so it can lengthen the refresh window. */
  it("passes Remember Me through when ticked", async () => {
    loginMock.mockResolvedValue({
      accessToken: "token",
      expiresAt: "2026-08-24T13:00:00Z",
      mustChangePassword: false,
      refreshToken: "refresh",
      refreshTokenExpiresAt: "2026-09-23T12:00:00Z",
      roles: ["Student"],
    });

    render(<LoginPage />);
    await userEvent.type(screen.getByLabelText("Email"), "student@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "correct-horse-battery");
    await userEvent.click(screen.getByLabelText("Remember me"));
    await userEvent.click(screen.getByRole("button", { name: "Log in" }));

    await waitFor(() =>
      expect(loginMock).toHaveBeenCalledWith(expect.objectContaining({ rememberMe: true })),
    );
  });

  it("sends a successful sign-in to the profile", async () => {
    loginMock.mockResolvedValue({
      accessToken: "token",
      expiresAt: "2026-08-24T13:00:00Z",
      mustChangePassword: false,
      refreshToken: "refresh",
      refreshTokenExpiresAt: "2026-08-31T12:00:00Z",
      roles: ["Student"],
    });

    render(<LoginPage />);
    await signIn();

    await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/profile"));
  });

  /** LOG-013 / BR-LOG-008: the forced-reset redirect, client side. */
  it("redirects to the mandatory change-password screen when the token is limited scope", async () => {
    loginMock.mockResolvedValue({
      accessToken: "limited-token",
      expiresAt: "2026-08-24T12:10:00Z",
      mustChangePassword: true,
      refreshToken: null,
      refreshTokenExpiresAt: null,
      roles: [],
    });

    render(<LoginPage />);
    await signIn();

    await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/change-password-required"));
    expect(replaceMock).not.toHaveBeenCalledWith("/profile");
  });

  /** BR-LOG-001: the UI must not embellish the server's deliberately vague message. */
  it("shows the generic failure message verbatim", async () => {
    const { ApiError } = await import("@unify/api-client");

    loginMock.mockRejectedValue(
      new ApiError(401, "unauthorized", {
        title: "Invalid email or password.",
        code: "auth.login.invalid_credentials",
      }),
    );

    render(<LoginPage />);
    await signIn("wrong-password-here");

    expect(await screen.findByText("Invalid email or password.")).toBeInTheDocument();
  });

  /**
   * Part 4: a suspended account gets a distinct message rather than the generic
   * invalid-credentials text - no resend/appeal link, since there is nothing actionable here in
   * this pass (appeal flow is a separate, later module).
   */
  it("shows the distinct suspended message rather than the generic failure text", async () => {
    const { ApiError } = await import("@unify/api-client");

    loginMock.mockRejectedValue(
      new ApiError(403, "forbidden", {
        title: "This account has been suspended. Contact support for more information.",
        code: "auth.login.account_suspended",
      }),
    );

    render(<LoginPage />);
    await signIn();

    expect(await screen.findByText(/this account has been suspended/i)).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /resend the verification email/i })).not.toBeInTheDocument();
  });

  /** LOG-004 is actionable, so the UI offers the way forward. */
  it("offers a resend link when the account is unverified", async () => {
    const { ApiError } = await import("@unify/api-client");

    loginMock.mockRejectedValue(
      new ApiError(403, "forbidden", {
        title: "Please verify your email address before signing in.",
        code: "auth.login.email_not_verified",
      }),
    );

    render(<LoginPage />);
    await signIn();

    expect(await screen.findByText(/verify your email address/i)).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: /resend the verification email/i }),
    ).toBeInTheDocument();
  });
});
