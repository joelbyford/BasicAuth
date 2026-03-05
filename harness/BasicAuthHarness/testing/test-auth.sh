#!/usr/bin/env bash

set -u

BASE_URL="${1:-http://localhost:5057}"
USER_NAME="${2:-demoUser}"
PASSWORD="${3:-demoPass!123}"

AUTH_VALUE="$(printf '%s:%s' "$USER_NAME" "$PASSWORD" | base64 | tr -d '\n')"
BAD_AUTH_VALUE="$(printf '%s:%s' "$USER_NAME" "wrongPassword" | base64 | tr -d '\n')"

all_passed=true

assert_status() {
  local description="$1"
  local expected_status="$2"
  shift 2

  local actual_status
  actual_status="$(curl -s -o /dev/null -w "%{http_code}" "$@")"

  if [[ "$actual_status" == "$expected_status" ]]; then
    echo "PASS: ${description} (expected ${expected_status}, got ${actual_status})"
  else
    echo "FAIL: ${description} (expected ${expected_status}, got ${actual_status})"
    all_passed=false
  fi
}

assert_status \
  "Missing auth header" \
  "401" \
  -X POST "${BASE_URL}/bogus" \
  -H "X-Forwarded-Proto: https"

assert_status \
  "Bad credentials" \
  "401" \
  -X POST "${BASE_URL}/bogus" \
  -H "X-Forwarded-Proto: https" \
  -H "Authorization: Basic ${BAD_AUTH_VALUE}"

assert_status \
  "Valid credentials + bogus endpoint" \
  "404" \
  -X POST "${BASE_URL}/bogus" \
  -H "X-Forwarded-Proto: https" \
  -H "Authorization: Basic ${AUTH_VALUE}"

assert_status \
  "Valid credentials + valid endpoint" \
  "200" \
  "${BASE_URL}/health" \
  -H "X-Forwarded-Proto: https" \
  -H "Authorization: Basic ${AUTH_VALUE}"

if [[ "$all_passed" == true ]]; then
  echo "true"
  exit 0
else
  echo "false"
  exit 1
fi
