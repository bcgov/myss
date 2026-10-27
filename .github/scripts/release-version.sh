#!/usr/bin/env bash
# Release versions are <major>.<sprint>.<patch>. major and sprint come from
# version.json; patch is the number of first-parent commits on dev since
# version.json was set to the current major and sprint, so each merged PR
# adds one and a sprint bump resets it to 0. An edit to version.json that
# keeps the numbers does not restart the patch. The Tekton git-clone task (tenant repo) computes the same
# number to tag dev builds; keep the two rules identical.
#
#   release-version.sh current [ref]         version of ref (default HEAD)
#   release-version.sh commit <version> [ref] commit on ref (default
#                                            origin/dev) that built <version>
#
# Needs the full history (actions/checkout with fetch-depth: 0).
set -euo pipefail

field() { # field <name> <json text>
  sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\([0-9][0-9]*\).*/\1/p" <<<"$2" | head -n1
}

version_of() { # version_of <commit>
  local json major sprint base="" c j
  json=$(git show "$1:version.json" 2>/dev/null) || {
    echo "no version.json at $1" >&2; return 1; }
  major=$(field major "$json") sprint=$(field sprint "$json")
  [ -n "$major" ] && [ -n "$sprint" ] || {
    echo "version.json at $1 has no numeric major and sprint" >&2; return 1; }
  # Oldest commit of the latest unbroken run of version.json changes that
  # keep this major and sprint.
  for c in $(git log --first-parent --format=%H "$1" -- version.json); do
    j=$(git show "$c:version.json")
    [ "$(field major "$j")" = "$major" ] && [ "$(field sprint "$j")" = "$sprint" ] || break
    base=$c
  done
  echo "$major.$sprint.$(git rev-list --first-parent --count "$base..$1")"
}

case "${1:-}" in
  current)
    version_of "$(git rev-parse "${2:-HEAD}")"
    ;;
  commit)
    version="${2:?version required}"
    ref="${3:-origin/dev}"
    [[ "$version" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]] || {
      echo "version must be <major>.<sprint>.<patch>, got '$version'" >&2; exit 1; }
    major=${BASH_REMATCH[1]} sprint=${BASH_REMATCH[2]} patch=${BASH_REMATCH[3]}
    # The commits on ref where version.json was set to this major.sprint;
    # the release is <patch> first-parent commits after one of them.
    for base in $(git log --first-parent --format=%H "$ref" -- version.json); do
      json=$(git show "$base:version.json")
      [ "$(field major "$json")" = "$major" ] && [ "$(field sprint "$json")" = "$sprint" ] || continue
      if [ "$patch" = 0 ]; then
        candidate=$base
      else
        candidate=$(git rev-list --first-parent --reverse "$base..$ref" | sed -n "${patch}p")
      fi
      if [ -n "$candidate" ] && [ "$(version_of "$candidate")" = "$version" ]; then
        echo "$candidate"
        exit 0
      fi
    done
    echo "no commit on $ref builds version $version" >&2
    exit 1
    ;;
  *)
    sed -n '2,12p' "$0" >&2
    exit 2
    ;;
esac
