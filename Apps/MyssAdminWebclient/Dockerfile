# Dockerfile

# -----------
# Build stage
# -----------
FROM docker.io/library/node:24-alpine3.23 AS build-stage

# The build context is Apps/, the npm workspace root: this app imports the
# Form.io components from the @myss/formio workspace (Apps/MyssFormio) and the
# sign-in plumbing and shared pieces from @myss/web-common (Apps/MyssWebCommon),
# so it cannot build from its own directory alone. The pipeline passes
# context ./Apps and Dockerfile MyssAdminWebclient/Dockerfile.
WORKDIR /app

# Manifests first, so the install layer is reused until a dependency changes.
# npm needs every workspace's package.json to read the lockfile, even the ones
# this image does not build.
COPY package.json package-lock.json ./
COPY MyssFormio/package.json MyssFormio/
COPY MyssWebCommon/package.json MyssWebCommon/
COPY MyssWebclient/package.json MyssWebclient/
COPY MyssAdminWebclient/package.json MyssAdminWebclient/

# Print npm config
RUN npm config list

# Install this app's dependencies (and its workspace dependencies @myss/formio
# and @myss/web-common)
RUN npm ci --workspace myss-admin-webclient

# Copy required files: the shared workspaces and this app
COPY MyssFormio/ MyssFormio/
COPY MyssWebCommon/ MyssWebCommon/
COPY MyssAdminWebclient/ MyssAdminWebclient/

# Run Vite build script, which places built files in /app/MyssAdminWebclient/dist.
# Note: Variables are intentionally NOT passed at build time.
# Env is injected at start by entrypoint.sh → /usr/share/nginx/html/config.js → window.<vars> (see src/constants.ts).
RUN npm run build --workspace myss-admin-webclient

# -----------
# Serve stage
# -----------
FROM docker.io/nginxinc/nginx-unprivileged:alpine3.22 AS serve-stage

# Copy nginx configuration
COPY MyssAdminWebclient/nginx.conf /etc/nginx/nginx.conf

COPY --from=build-stage /app/MyssAdminWebclient/dist /usr/share/nginx/html

# Runtime config writer. /docker-entrypoint.d/*.sh runs before nginx starts;
# the 40- prefix sequences it after the base image's own scripts (10–30).
COPY --chmod=755 MyssAdminWebclient/entrypoint.sh /docker-entrypoint.d/40-write-runtime-config.sh

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

# For container runtimes that read it (docker, podman). OpenShift ignores it and
# uses the Deployment's probes instead. BusyBox wget ships in the base image.
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://127.0.0.1:8080/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
