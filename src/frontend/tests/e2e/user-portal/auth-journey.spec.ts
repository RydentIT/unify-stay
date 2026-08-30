import { expect, test } from "@playwright/test";

import { fetchLatestLink, uniqueEmail } from "../support/mailhog";

/**
 * The two journeys that span the whole stack: register -> verify -> login, and
 * forgot-password -> reset -> login.
 *
 * These pull the real verification and reset links out of MailHog rather than reaching into
 * the database, so they exercise exactly what a user would receive. MailHog is expected at
 * http://localhost:8025 (override with MAILHOG_URL).
 */
const PASSWORD = "correct-horse-battery-staple";
const NEW_PASSWORD = "a-different-battery-staple";

test.describe("register, verify and sign in", () => {
  test("completes the full journey", async ({ page }) => {
    const email = uniqueEmail("e2e-journey");

    // ---------- Register ----------
    await page.goto("/register");

    await page.getByLabel("Name").fill("E2E Student");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByLabel("Confirm password").fill(PASSWORD);
    await page.getByLabel("I accept the terms of service").check();
    await page.getByRole("button", { name: "Register" }).click();

    await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();

    // ---------- LOG-004: sign-in is blocked until the address is confirmed ----------
    await page.goto("/login");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Log in" }).click();

    await expect(page.getByText(/verify your email address/i)).toBeVisible();

    // ---------- Verify using the emailed link ----------
    const verifyUrl = await fetchLatestLink(email, "/verify-email/");
    expect(verifyUrl, "no verification email arrived").not.toBeNull();

    await page.goto(verifyUrl!);
    await expect(page.getByRole("heading", { name: "Email verified" })).toBeVisible();

    // ---------- EVR-010: the same link cannot be spent twice ----------
    await page.goto(verifyUrl!);
    await expect(page.getByRole("heading", { name: "Already verified" })).toBeVisible();

    // ---------- Sign in for real ----------
    await page.goto("/login");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Log in" }).click();

    await expect(page).toHaveURL(/\/profile$/);
    await expect(page.getByRole("heading", { name: "Profile" })).toBeVisible();
    await expect(page.getByText(email)).toBeVisible();
  });
});

test.describe("forgot password and reset", () => {
  test("resets the password and signs in with the new one", async ({ page }) => {
    const email = uniqueEmail("e2e-reset");

    // Arrange: a verified account.
    await page.goto("/register");
    await page.getByLabel("Name").fill("E2E Reset");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByLabel("Confirm password").fill(PASSWORD);
    await page.getByLabel("I accept the terms of service").check();
    await page.getByRole("button", { name: "Register" }).click();
    await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();

    const verifyUrl = await fetchLatestLink(email, "/verify-email/");
    expect(verifyUrl).not.toBeNull();
    await page.goto(verifyUrl!);
    await expect(page.getByRole("heading", { name: "Email verified" })).toBeVisible();

    // ---------- Request a reset ----------
    await page.goto("/forgot-password");
    await page.getByLabel("Email").fill(email);
    await page.getByRole("button", { name: "Send reset link" }).click();
    await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();

    const resetUrl = await fetchLatestLink(email, "/reset-password/");
    expect(resetUrl, "no reset email arrived").not.toBeNull();

    // ---------- Set a new password ----------
    await page.goto(resetUrl!);
    await page.getByLabel("New password", { exact: true }).fill(NEW_PASSWORD);
    await page.getByLabel("Confirm new password").fill(NEW_PASSWORD);
    await page.getByRole("button", { name: "Set new password" }).click();

    await expect(page.getByRole("heading", { name: "Password updated" })).toBeVisible();

    // ---------- FPW-005: the link is spent ----------
    await page.goto(resetUrl!);
    await page.getByLabel("New password", { exact: true }).fill(NEW_PASSWORD);
    await page.getByLabel("Confirm new password").fill(NEW_PASSWORD);
    await page.getByRole("button", { name: "Set new password" }).click();
    await expect(page.getByRole("alert").first()).toBeVisible();

    // ---------- The old password no longer works ----------
    await page.goto("/login");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "Log in" }).click();
    await expect(page.getByText("Invalid email or password.")).toBeVisible();

    // ---------- The new one does ----------
    await page.goto("/login");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Password", { exact: true }).fill(NEW_PASSWORD);
    await page.getByRole("button", { name: "Log in" }).click();

    await expect(page).toHaveURL(/\/profile$/);
  });
});

test.describe("account enumeration", () => {
  /** FPW-003 / BR-FPW-002: an unknown address must be indistinguishable from a known one. */
  test("forgot-password answers identically for an unknown address", async ({ page }) => {
    await page.goto("/forgot-password");
    await page.getByLabel("Email").fill(uniqueEmail("never-registered"));
    await page.getByRole("button", { name: "Send reset link" }).click();

    await expect(page.getByRole("heading", { name: "Check your email" })).toBeVisible();
    await expect(page.getByText(/if that address has an account/i)).toBeVisible();
  });
});
