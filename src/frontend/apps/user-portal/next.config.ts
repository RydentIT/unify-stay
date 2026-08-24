import { fileURLToPath } from "node:url";

import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Emits .next/standalone, which is what the Dockerfile copies for a small final image.
  output: "standalone",

  // The workspace packages ship TypeScript source rather than a build output, so Next
  // compiles them alongside the app. This keeps the packages free of their own build step.
  transpilePackages: ["@unify/api-client", "@unify/ui"],

  // The monorepo root, so Next traces file dependencies from the right place.
  // fileURLToPath, not URL.pathname: the latter yields "/E:/..." on Windows, which Turbopack
  // cannot canonicalize.
  outputFileTracingRoot: fileURLToPath(new URL("../../", import.meta.url)),

  reactStrictMode: true,
};

export default nextConfig;
