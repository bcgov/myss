#!/usr/bin/env bash
# Release helpers shared by the release-draft and deploy workflows.
#
# A release is a set of six images, one per app, named v<major>.<sprint>.<n>.
# major and sprint come from version.json; n counts the releases already
# published for that major.sprint (git tags), starting at 0.
#
# Each image is identified by the 7-character commit it was built from: the
# Tekton builds (tenant repo) tag every dev build <sha7> and `dev`, and only
# build an app when a push changes one of its paths below. The latest build of
# an app is therefore the latest first-parent commit on dev that touched its
# paths. Keep APP_PATHS in step with the CEL filters in the tenant repo's
# manifests/deployments/base/triggers/trigger-*.yaml.
#
# The release body carries the chosen image per app in a fenced block:
#
#   ```release
#   myss-api: a1b2c3d   # changed since v0.7.3
#   myss-content: v0.7.2   # pinned
#   ```
#
# A value is a build (<sha7>) or an earlier release (v<major>.<sprint>.<n>),
# which ships that release's image for the app. Text after `#` is a comment;
# a line whose comment contains `pinned` is kept when the draft is refreshed.
#
#   release.sh apps                       app names, one per line
#   release.sh latest [ref]               app: <sha7> of each app's latest build
#   release.sh next-version [ref]         next release version, without the v
#   release.sh parse < body               app: value lines from a release body
#   release.sh resolve <app> <value>      the <sha7> a value stands for
#   release.sh draft                      create or refresh the draft release
#
# Needs the full history and tags (actions/checkout with fetch-depth: 0) and,
# for resolve and draft, GH_TOKEN and GITHUB_REPOSITORY.
set -euo pipefail

# app|paths that rebuild it (prefixes, space separated). Changes to shared
# root files of Apps/ (the npm workspace manifest, .dockerignore) rebuild
# every app that builds from ./Apps.
APP_PATHS=(
  "myss-api|Apps/MyssApi/ Apps/MyssCommon/ Apps/MyssData/ Apps/MyssFormSpec/ Apps/.dockerignore"
  "myss-admin-api|Apps/MyssAdminApi/ Apps/MyssCommon/ Apps/MyssData/ Apps/MyssFormSpec/ Apps/.dockerignore"
  "icm-api|Apps/IcmApi/ Apps/IcmApi.Host/ Apps/.dockerignore"
  "myss-webclient|Apps/MyssWebclient/ Apps/MyssWebCommon/ Apps/MyssFormio/ Apps/package.json Apps/package-lock.json Apps/.dockerignore"
  "myss-admin-webclient|Apps/MyssAdminWebclient/ Apps/MyssWebCommon/ Apps/MyssFormio/ Apps/package.json Apps/package-lock.json Apps/.dockerignore"
  "myss-content|Apps/MyssContent/"
)

apps() {
  local entry
  for entry in "${APP_PATHS[@]}"; do echo "${entry%%|*}"; done
}

paths_of() { # paths_of <app>
  local entry
  for entry in "${APP_PATHS[@]}"; do
    [ "${entry%%|*}" = "$1" ] && { echo "${entry#*|}"; return; }
  done
  echo "unknown app '$1'" >&2
  return 1
}

latest() { # latest [ref]
  local ref="${1:-HEAD}" app sha
  for app in $(apps); do
    # shellcheck disable=SC2046 # the paths are meant to split
    sha=$(git log --first-parent -1 --format=%H "$ref" -- $(paths_of "$app"))
    [ -n "$sha" ] || { echo "no commit touches $app's paths" >&2; return 1; }
    echo "$app: ${sha:0:7}"
  done
}

field() { # field <name> <json text>
  sed -n "s/.*\"$1\"[[:space:]]*:[[:space:]]*\([0-9][0-9]*\).*/\1/p" <<<"$2" | head -n1
}

next_version() { # next_version [ref]
  local json major sprint last
  json=$(git show "${1:-HEAD}:version.json")
  major=$(field major "$json") sprint=$(field sprint "$json")
  [ -n "$major" ] && [ -n "$sprint" ] || {
    echo "version.json has no numeric major and sprint" >&2; return 1; }
  last=$(git tag -l "v$major.$sprint.*" | sed -n "s/^v$major\.$sprint\.\([0-9][0-9]*\)$/\1/p" | sort -n | tail -n1)
  echo "$major.$sprint.$(( ${last:--1} + 1 ))"
}

parse() { # parse < body; prints `app: value` for each line of the block
  tr -d '\r' | awk '
    /^```release[[:space:]]*$/ { inside = 1; next }
    inside && /^```/ { exit }
    inside {
      sub(/#.*/, "")
      gsub(/[[:space:]]/, "")
      if ($0 != "") { sub(/:/, ": "); print }
    }'
}

value_of() { # value_of <app> <parsed lines>
  sed -n "s/^$1: //p" <<<"$2" | head -n1
}

resolve() { # resolve <app> <value>
  local app="$1" value="$2" inner
  if [[ "$value" =~ ^[0-9a-f]{7}$ ]]; then
    git merge-base --is-ancestor "$value" origin/dev 2>/dev/null || {
      echo "$app: $value is not a commit on dev" >&2; return 1; }
    echo "$value"
  elif [[ "$value" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    inner=$(gh release view "$value" --repo "$GITHUB_REPOSITORY" --json body --jq .body | parse)
    inner=$(value_of "$app" "$inner")
    [[ "$inner" =~ ^[0-9a-f]{7}$ ]] || {
      echo "$app: release $value does not list a build for it" >&2; return 1; }
    echo "$inner"
  else
    echo "$app: '$value' is neither a 7-character commit nor a release (v1.2.3)" >&2
    return 1
  fi
}

draft() {
  local repo="$GITHUB_REPOSITORY" head version prev prev_block draft_id old_block
  local block="" app value note latest_lines pinned
  head=$(git rev-parse HEAD)
  version=$(next_version HEAD)
  prev=$(git tag -l 'v*' --sort=-v:refname | head -n1)
  prev_block=""
  [ -z "$prev" ] || prev_block=$(gh release view "$prev" --repo "$repo" --json body --jq .body | parse)

  # At most one draft is kept; it is the next release.
  draft_id=$(gh api "repos/$repo/releases" --paginate --jq '.[] | select(.draft) | .id' | head -n1)
  old_block=""
  [ -z "$draft_id" ] || old_block=$(gh api "repos/$repo/releases/$draft_id" --jq .body | tr -d '\r' |
    awk '/^```release[[:space:]]*$/ { inside = 1; next } inside && /^```/ { exit } inside')

  latest_lines=$(latest HEAD)
  for app in $(apps); do
    pinned=$(grep -E "^[[:space:]]*${app}[[:space:]]*:.*#.*pinned" <<<"$old_block" | head -n1 || true)
    if [ -n "$pinned" ]; then
      block+="$pinned"$'\n'
      continue
    fi
    value=$(value_of "$app" "$latest_lines")
    if [ -z "$prev" ]; then
      note=""
    elif [ "$(value_of "$app" "$prev_block")" = "$value" ]; then
      note="   # unchanged since $prev"
    else
      note="   # changed since $prev"
    fi
    block+="$app: $value$note"$'\n'
  done

  local notes_args=(-f "tag_name=v$version" -f "target_commitish=$head")
  [ -z "$prev" ] || notes_args+=(-f "previous_tag_name=$prev")
  local notes body
  notes=$(gh api "repos/$repo/releases/generate-notes" "${notes_args[@]}" --jq .body)
  body=$(cat <<EOF
<!-- Images in this release. Each line is an app and the build it ships: a
7-character dev commit (its latest build is filled in) or an earlier release
such as v0.7.2. Add "# pinned" to a line to keep it when new builds land.
Deploy with the "Deploy release" workflow (test). -->
\`\`\`release
$block\`\`\`

$notes
EOF
)

  # A test deploy may have published the draft since it was read above.
  if [ -n "$draft_id" ] && [ "$(gh api "repos/$repo/releases/$draft_id" --jq .draft)" != "true" ]; then
    draft_id=""
  fi
  if [ -n "$draft_id" ]; then
    gh api -X PATCH "repos/$repo/releases/$draft_id" \
      -f "tag_name=v$version" -f "name=v$version" -f "target_commitish=$head" \
      -f "body=$body" --jq '"Updated draft " + .name'
  else
    gh api -X POST "repos/$repo/releases" \
      -f "tag_name=v$version" -f "name=v$version" -f "target_commitish=$head" \
      -F draft=true -F prerelease=true -f "body=$body" --jq '"Created draft " + .name'
  fi
}

case "${1:-}" in
  apps) apps ;;
  latest) latest "${2:-HEAD}" ;;
  next-version) next_version "${2:-HEAD}" ;;
  parse) parse ;;
  resolve) resolve "${2:?app required}" "${3:?value required}" ;;
  draft) draft ;;
  *)
    sed -n '2,/^set -euo/p' "$0" | sed '$d; s/^# \{0,1\}//'
    exit 1
    ;;
esac
