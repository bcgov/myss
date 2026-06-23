# Dockerfile

# -----------
# Build stage
# -----------
FROM docker.io/library/node:24-alpine3.23 AS build-stage

# Copy package.json and package-lock.json
WORKDIR /app
COPY package.json package-lock.json ./

# Print npm config
RUN npm config list

# Install dependencies
RUN npm ci

# Copy required files
COPY . .

# Run Vite build script, which places built files in /app/dist.
# Note: Variables are intentionally NOT passed at build time.
# Env is injected at start by entrypoint.sh → /usr/share/nginx/html/config.js → window.<vars> (see src/constants.ts).
RUN npm run build

# -----------
# Serve stage
# -----------
FROM docker.io/nginxinc/nginx-unprivileged:alpine3.22 AS serve-stage

# Copy nginx configuration
COPY ./nginx.conf /etc/nginx/nginx.conf

COPY --from=build-stage /app/dist /usr/share/nginx/html

# Runtime config writer. /docker-entrypoint.d/*.sh runs before nginx starts;
# the 40- prefix sequences it after the base image's own scripts (10–30).
COPY --chmod=755 entrypoint.sh /docker-entrypoint.d/40-write-runtime-config.sh

# OpenShift's restricted SCC (Security Context Constraints — the default
# per-namespace pod security policy that overrides UID/capabilities/host
# access) runs containers as a random UID with GID 0, so group-own the html
# dir to root (0) and grant group write — the standard Red Hat pattern for
# SCC-compatible images.
USER root
RUN chgrp -R 0 /usr/share/nginx/html && \
    chmod -R g=u /usr/share/nginx/html
USER 101

EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
