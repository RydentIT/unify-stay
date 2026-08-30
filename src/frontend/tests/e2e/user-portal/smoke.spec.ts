import { expect, test } from "@playwright/test";

test.describe("user portal smoke", () => {
  test("serves the home page", async ({ page }) => {
    await page.goto("/");

    await expect(page.getByRole("heading", { name: "UnifyStay user portal" })).toBeVisible();
  });

  test("renders the registration form", async ({ page }) => {
    await page.goto("/register");

    await expect(page.getByRole("heading", { name: "Create an account" })).toBeVisible();
    await expect(page.getByLabel("Name")).toBeVisible();
    await expect(page.getByLabel("Email")).toBeVisible();
    await expect(page.getByLabel("Password", { exact: true })).toBeVisible();
  });

  test("validates client-side before reaching the API", async ({ page }) => {
    await page.goto("/register");
    await page.getByRole("button", { name: "Register" }).click();

    // No request is made: the zod resolver blocks submission.
    await expect(page.getByText("Name is required.")).toBeVisible();
    await expect(page.getByText("You must accept the terms of service.")).toBeVisible();
  });

  test("protects the profile route from anonymous visitors", async ({ page }) => {
    await page.goto("/profile");

    await expect(page).toHaveURL(/\/login$/);
  });
});
