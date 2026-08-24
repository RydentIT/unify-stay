import { expect, test } from "@playwright/test";

test.describe("admin portal smoke", () => {
  test("redirects the root to the sign in screen", async ({ page }) => {
    await page.goto("/");

    await expect(page).toHaveURL(/\/login$/);
    await expect(page.getByRole("heading", { name: "Staff sign in" })).toBeVisible();
  });

  test("offers no self-service registration", async ({ page }) => {
    await page.goto("/login");

    await expect(page.getByRole("link", { name: /register/i })).toHaveCount(0);
  });

  test("reaches the backend through the api client", async ({ page }) => {
    await page.goto("/login");

    await page.getByLabel("Email").fill("staff@example.com");
    await page.getByLabel("Password").fill("correct-horse-battery-staple");

    const [response] = await Promise.all([
      page.waitForResponse((res) => res.url().includes("/api/v1/auth/login")),
      page.getByRole("button", { name: "Sign in" }).click(),
    ]);

    // SCAFFOLD: stub handler. Change to 200 when the Login module lands.
    expect(response.status()).toBe(501);
  });
});
