#!/usr/bin/env bash
# Checks that the local ClamAV (compose.yaml / MySS.AspireHost `clamav`) is up and
# actually catching malware, the same way MyssApi talks to it: clamd's TCP
# protocol on port 3310, scanning with INSTREAM.
#
# Usage:            ./test-clamav.sh [host] [port]
# Defaults:         localhost 3310 (override also via CLAMAV_HOST / CLAMAV_PORT)
# Exit codes:       0 all checks passed, 1 a check failed or clamd unreachable
#
# The malware sample is EICAR, the industry-standard harmless test string —
# every scanner detects it by agreement, nothing about it is executable. It is
# assembled from two halves here so this script file itself never contains the
# signature and cannot be quarantined by a desktop scanner.

set -u

HOST="${1:-${CLAMAV_HOST:-localhost}}"
PORT="${2:-${CLAMAV_PORT:-3310}}"

EICAR_A='X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR'
EICAR_B='-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*'
EICAR="${EICAR_A}${EICAR_B}"   # 68 bytes

failures=0

say() { printf '%s\n' "$*"; }

check() {
    local label="$1" expected="$2" got="$3"
    if [[ "$got" == *"$expected"* ]]; then
        say "PASS  $label: $got"
    else
        say "FAIL  $label: expected '*${expected}*', got '${got:-<no reply>}'"
        failures=$((failures + 1))
    fi
}

# clamd commands are null-terminated when prefixed with 'z'. INSTREAM sends
# the file as {4-byte big-endian length}{bytes} chunks, ended by a zero chunk.
# The `sleep` holds our side of the connection open after the payload: the
# Aspire app host fronts 3310 with a TCP proxy that tears the whole connection
# down on a client half-close, which would eat the reply (MEASURED; talking to
# the container directly tolerates the half-close fine).
clamd() { { cat; sleep 1; } | nc -w 10 "$HOST" "$PORT" | tr -d '\0'; }

instream() {
    local bytes="$1"
    local len
    len=$(printf '%s' "$bytes" | wc -c | tr -d ' ')
    {
        printf 'zINSTREAM\0'
        # shellcheck disable=SC2059  # the \x escapes are built on purpose
        printf "$(printf '\\x%02x\\x%02x\\x%02x\\x%02x' \
            $(( len >> 24 & 255 )) $(( len >> 16 & 255 )) $(( len >> 8 & 255 )) $(( len & 255 )))"
        printf '%s' "$bytes"
        printf '\0\0\0\0'
    } | clamd
}

say "ClamAV check against ${HOST}:${PORT}"
say ""

# Reachability is judged by the PING reply, not a bare port probe — some nc
# builds report closed ports as open, and a clamd that is still loading its
# databases accepts the connection but says nothing.
ping_reply="$(printf 'zPING\0' | clamd)"
if [[ "$ping_reply" != *PONG* ]]; then
    say "FAIL  no PONG from ${HOST}:${PORT} (got '${ping_reply:-<no reply>}')."
    say ""
    say "Is the stack up? (docker compose up -d clamav, or the Aspire app host.)"
    say "A first-ever start downloads signature databases for several minutes"
    say "before clamd starts answering."
    exit 1
fi

check "PING           " "PONG" "$ping_reply"
check "VERSION        " "ClamAV" "$(printf 'zVERSION\0' | clamd)"
check "EICAR detected " "FOUND" "$(instream "$EICAR")"
check "Clean stream OK" "stream: OK" "$(instream "just a harmless string")"

say ""
if (( failures > 0 )); then
    say "$failures check(s) failed."
    exit 1
fi
say "All checks passed - clamd is up and detecting."
