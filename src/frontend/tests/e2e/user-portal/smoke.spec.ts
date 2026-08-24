import { expect, test } from "@playwright/test";

test.describe("user portal smoke", () => {
  test("serves the home page", async ({ page }) => {
    await page.goto("/");

    await expect(page.getByRole("heading", { name: "UnifyStay user portal" })).toBeVisible();
  });

  test("renders the registration form", async ({ page }) => {
    await page.goto("/register");

    await expect(page.getByRole("heading", { name: "Create an account" })).toBeVisible();
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(page.getByLabel("Display name")).toBeVisible();
    await expect(page.getByLabel("Password")).toBeVisible();
  });

  test("reaches the backend through the api client", async ({ page }) => {
    await page.goto("/register");

    await page.getByLabel("Email").fill("e2e@example.com");
    await page.getByLabel("Display name").fill("End To End");
    await page.getByLabel("Password").fill("correct-horse-battery-staple");
    await page.getByRole("checkbox").check();

    const [response] = await Promise.all([
      page.waitForResponse((res) => res.url().includes("/api/v1/auth/register")),
      page.getByRole("button", { name: "Register" }).click(),
    ]);

    // SCAFFOLD: the handler is a stub, so a reachable backend answers 501. Change this to 201
    // when the Register module lands - a 0 or a network error here means the stack is broken.
    expect(response.status()).toBe(501);
    await expect(page.getByRole("alert")).toContainText(/not built yet/i);
  });
});
