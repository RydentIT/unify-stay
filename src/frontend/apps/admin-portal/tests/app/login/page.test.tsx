import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

const login = vi.fn();

vi.mock("@/lib/api", () => ({
  api: { auth: { login: (...args: unknown[]) => login(...args) } },
  apiBaseUrl: "http://api.test",
}));

const { default: AdminLoginPage } = await import("../../../app/login/page");

describe("AdminLoginPage", () => {
  beforeEach(() => {
    login.mockReset();
    window.localStorage.clear();
  });

  it("renders the staff sign in form", () => {
    render(<AdminLoginPage />);

    expect(screen.getByRole("heading", { name: "Staff sign in" })).toBeInTheDocument();
    expect(screen.getByLabelText("Email")).toBeInTheDocument();
    expect(screen.getByLabelText("Password")).toBeInTheDocument();
  });

  it("offers no self-service registration or OAuth", () => {
    render(<AdminLoginPage />);

    expect(screen.queryByRole("link", { name: /register/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /google|facebook|apple|sso/i })).not.toBeInTheDocument();
  });

  it("reports a failed sign in without revealing which field was wrong", async () => {
    const { ApiError } = await import("@unify/api-client");
    login.mockRejectedValue(
      new ApiError(401, "unauthorized", { title: "Invalid email or password." }),
    );

    render(<AdminLoginPage />);

    await userEvent.type(screen.getByLabelText("Email"), "staff@example.com");
    await userEvent.type(screen.getByLabelText("Password"), "wrong-password-here");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Invalid email or password.");
  });
});
