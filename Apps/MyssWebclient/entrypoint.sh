#!/bin/sh
# Runtime config writer.
#
# Generates /usr/share/nginx/html/config.js from environment variables before
# nginx starts serving. The file is loaded by index.html as a regular <script> tag
# values land on window.APP_CONFIG and are visible to the SPA at runtime.
#
# This script lives in /docker-entrypoint.d/ in the image; the nginx
# base image runs every executable script in that directory before starting the
# server in alphabetical order.
#
# New variables need to be added here, mirror it in public/config.js,
# and reference it from src/constants.ts.

set -eu

cat > /usr/share/nginx/html/config.js <<EOF
window.APP_CONFIG = {
  MYSS_API_URL: "${MYSS_API_URL:-}"
};
EOF
