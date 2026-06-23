// Runtime configuration consumed by src/constants.ts.
//
// Allows to inject environment variables to the application
//
// Placeholder overwritten at container startup by
// /docker-entrypoint.d/40-write-runtime-config.sh
//
// During local development with `vite dev`, this file is served as-is and
// src/constants.ts falls back to import.meta.env.VITE_MYSS_API_URL
// (loaded from .env).
window.APP_CONFIG = {};
