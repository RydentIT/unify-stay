# UnifyStay user portal (Next.js standalone).
# Build context is the REPOSITORY ROOT:
#   docker build -f .docker/user-portal.Dockerfile -t unify-user-portal .

# ---------- dependencies ----------
FROM node:22-alpine AS deps
WORKDIR /app

# Only the manifests, so `npm ci` is cached until a dependency actually changes. Every
# workspace manifest is needed for the lockfile to resolve.
COPY src/frontend/package.json src/frontend/package-lock.json ./
COPY src/frontend/apps/user-portal/package.json   apps/user-portal/
COPY src/frontend/apps/admin-portal/package.json  apps/admin-portal/
COPY src/frontend/packages/ui/package.json        packages/ui/
COPY src/frontend/packages/api-client/package.json packages/api-client/
COPY src/frontend/packages/config/package.json    packages/config/

RUN npm ci

# ---------- build ----------
FROM node:22-alpine AS build
WORKDIR /app

# NEXT_PUBLIC_* values are inlined into the client bundle at build time, so the API URL has to
# be known here rather than at container start. Override per environment with --build-arg.
ARG NEXT_PUBLIC_API_URL=http://localhost:5000
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}
ENV NEXT_TELEMETRY_DISABLED=1

COPY --from=deps /app/node_modules ./node_modules
COPY src/frontend/ ./

RUN npm run build --workspace=@unify/user-portal

# ---------- runtime ----------
FROM node:22-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

RUN addgroup --system --gid 1001 nodejs \
 && adduser  --system --uid 1001 nextjs

# Standalone output already contains a pruned node_modules; static assets and public files are
# not part of it and must be copied alongside.
COPY --from=build --chown=nextjs:nodejs /app/apps/user-portal/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/apps/user-portal/.next/static ./apps/user-portal/.next/static
COPY --from=build --chown=nextjs:nodejs /app/apps/user-portal/public ./apps/user-portal/public

USER nextjs
EXPOSE 3000

CMD ["node", "apps/user-portal/server.js"]
