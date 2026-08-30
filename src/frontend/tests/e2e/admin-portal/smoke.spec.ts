import { expect, test } from "@playwright/test";

test.describe("admin portal smoke", () => {
  test("redirects the root to the sign in screen", async ({ page }) => {
    await page.goto("/");

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole("heading", { name: "Staff sign in" })).toBeVisible();
  });

  /** LOG-016: staff sign in with a password only, so no Google button is offered. */
  test("offers no Google sign-in and no registration link", async ({ page }) => {
    await page.goto("/login");

    await expect(page.getByRole("button", { name: /google/i })).toHaveCount(0);
    await expect(page.getByRole("link", { name: /register|sign up/i })).toHaveCount(0);
  });

  test("protects the upgrade requests route", async ({ page }) => {
    await page.goto("/upgrade-requests");

    await expect(page).toHaveURL(/\/login$/);
  });

  test("reaches the backend and reports invalid credentials generically", async ({ page }) => {
    await page.goto("/login");

    await page.getByLabel("Email").fill("nobody@example.com");
    await page.getByLabel("Password").fill("definitely-not-the-password");

    const [response] = await Promise.all([
      page.waitForResponse((res) => res.url().includes("/api/auth/login")),
      page.getByRole("button", { name: "Sign in" }).click(),
    ]);

    expect(response.status()).toBe(401);
    await expect(page.getByText("Invalid email or password.")).toBeVisible();
  });
});
