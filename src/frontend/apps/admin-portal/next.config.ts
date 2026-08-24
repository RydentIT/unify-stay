import { fileURLToPath } from "node:url";

import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  transpilePackages: ["@unify/api-client", "@unify/ui"],
  // fileURLToPath, not URL.pathname: the latter yields "/E:/..." on Windows, which Turbopack
  // cannot canonicalize.
  outputFileTracingRoot: fileURLToPath(new URL("../../", import.meta.url)),
  reactStrictMode: true,
};

export default nextConfig;
