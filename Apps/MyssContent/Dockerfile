# Multi-stage build.
#
# The pipeline (buildah) builds the FINAL stage — keep `production` last.
# Local development uses `target: dev` from compose.yaml, which mounts the
# source over /opt/app and runs the watch-mode server.

FROM node:22-alpine AS dev
WORKDIR /opt/app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ENV NODE_ENV=development
EXPOSE 1337
CMD ["npm", "run", "develop"]

FROM node:22-alpine AS build
WORKDIR /opt/app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ENV NODE_ENV=production
# The Strapi admin build needs more heap than Node allows itself by default
# in a memory-limited build pod.
ENV NODE_OPTIONS=--max-old-space-size=2048
RUN npm run build && npm prune --omit=dev

FROM node:22-alpine AS production
WORKDIR /opt/app
ENV NODE_ENV=production
COPY --from=build /opt/app ./
# OpenShift runs containers with an arbitrary UID in the root group.
RUN chgrp -R 0 /opt/app && chmod -R g=u /opt/app
EXPOSE 1337
CMD ["npm", "run", "start"]
