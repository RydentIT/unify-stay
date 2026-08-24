import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const login = vi.fn();

// The page must talk to the backend only through @unify/api-client, so that is the seam the
// test replaces. A page that called fetch directly would not be testable this way.
vi.mock("@/lib/api", () => ({
  api: { auth: { login: (...args: unknown[]) => login(...args) } },
  apiBaseUrl: "http://api.test",
}));

const { default: LoginPage } = await import("../../../app/login/page");

describe("LoginPage", () => {
  beforeEach(() => {
    login.mockReset();
    window.localStorage.clear();
  });

  it("submits the typed credentials through the api client", async () => {
    login.mockResolvedValue({
      accessToken: "token-123",
      expiresAtUtc: "2026-08-24T01:00:00Z",
      mustChangePassword: false,
    });

    render(<LoginPage />);

    await userEvent.type(screen.getByLabelText("Email"), "someone@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "correct-horse-battery");
    await userEvent.click(screen.getByRole("button", { name: "Log in" }));

    await waitFor(() =>
      expect(login).toHaveBeenCalledWith({
        email: "someone@example.com",
        password: "correct-horse-battery",
      }),
    );
  });

  it("tells the user when the backend endpoint is still a stub", async () => {
    const { ApiError } = await import("@unify/api-client");
    login.mockRejectedValue(new ApiError(501, "not implemented", { title: "Nope" }));

    render(<LoginPage />);

    await userEvent.type(screen.getByLabelText("Email"), "someone@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "correct-horse-battery");
    await userEvent.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/not built yet/i);
  });

  it("surfaces the forced password change when the token is limited scope", async () => {
    login.mockResolvedValue({
      accessToken: "token-123",
      expiresAtUtc: "2026-08-24T01:00:00Z",
      mustChangePassword: true,
    });

    render(<LoginPage />);

    await userEvent.type(screen.getByLabelText("Email"), "someone@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "correct-horse-battery");
    await userEvent.click(screen.getByRole("button", { name: "Log in" }));

    expect(await screen.findByText(/must change your password/i)).toBeInTheDocument();
  });
});
