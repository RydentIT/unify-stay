import { defineConfig, devices } from "@playwright/test";

/**
 * End-to-end suite for the full stack.
 *
 * These specs live outside the individual app folders because they exercise both portals
 * against a running backend and database - they belong to the system, not to either app.
 *
 * The config does NOT start any servers. The stack is expected to be up already, which is how
 * e2e-ci.yml runs it (docker compose up, run, tear down). Locally: `docker compose up -d`
 * then `npm --prefix src/frontend run e2e`.
 */
const userPortalUrl = process.env.E2E_USER_PORTAL_URL ?? "http://localhost:3000";
const adminPortalUrl = process.env.E2E_ADMIN_PORTAL_URL ?? "http://localhost:3001";

export default defineConfig({
  testDir: ".",
  // Generous: the first request to a cold Next.js container compiles the route.
  timeout: 60_000,
  expect: { timeout: 10_000 },

  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,

  reporter: process.env.CI
    ? [["github"], ["html", { outputFolder: "../../../../playwright-report", open: "never" }]]
    : [["list"]],

  use: {
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
  },

  // One project per portal so each gets its own baseURL and they can be run separately with
  // `--project=user-portal` / `--project=admin-portal`.
  projects: [
    {
      name: "user-portal",
      testDir: "./user-portal",
      use: { ...devices["Desktop Chrome"], baseURL: userPortalUrl },
    },
    {
      name: "admin-portal",
      testDir: "./admin-portal",
      use: { ...devices["Desktop Chrome"], baseURL: adminPortalUrl },
    },
  ],
});
