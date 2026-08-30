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

const { default: AdminLoginPage } = await import("@/app/login/page");

describe("AdminLoginPage", () => {
  beforeEach(() => {
    loginMock.mockReset();
    replaceMock.mockReset();
    window.localStorage.clear();
  });

  it("renders the staff sign in form", () => {
    render(<AdminLoginPage />);

    expect(screen.getByRole("heading", { name: "Staff sign in" })).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toBeInTheDocument();
    expect(screen.getByLabelText("Password")).toBeInTheDocument();
  });

  /**
   * LOG-016 blocks Admin/Staff from Google entirely, so offering the button would only lead to
   * a confusing rejection. Registration is internal-only, so there is no sign-up link either.
   */
  it("offers no Google sign-in and no self-service registration", () => {
    render(<AdminLoginPage />);

    expect(screen.queryByRole("button", { name: /google/i })).not.toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: /register|sign up|create an account/i }),
    ).not.toBeInTheDocument();
  });

  it("validates before contacting the server", async () => {
    render(<AdminLoginPage />);

    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByText("Email is required.")).toBeInTheDocument();
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("sends a successful sign-in to the dashboard", async () => {
    loginMock.mockResolvedValue({
      accessToken: "token",
      expiresAt: "2026-08-24T13:00:00Z",
      mustChangePassword: false,
      refreshToken: "refresh",
      refreshTokenExpiresAt: "2026-08-31T12:00:00Z",
      roles: ["Admin"],
    });

    render(<AdminLoginPage />);
    await userEvent.type(screen.getByLabelText("Email"), "staff@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "correct-horse-battery");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => expect(replaceMock).toHaveBeenCalledWith("/dashboard"));
  });

  it("reports a failed sign in without revealing which field was wrong", async () => {
    const { ApiError } = await import("@unify/api-client");

    loginMock.mockRejectedValue(
      new ApiError(401, "unauthorized", { title: "Invalid email or password." }),
    );

    render(<AdminLoginPage />);
    await userEvent.type(screen.getByLabelText("Email"), "staff@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "wrong-password-here");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Invalid email or password.");
  });
});
