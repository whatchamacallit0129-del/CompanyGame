#!/bin/bash
# Official Cline GitHub Issue RCA script, adapted to the official GitHub Actions integration.

set -euo pipefail

if [ -z "${1:-}" ]; then
  echo "Usage: $0 <github-issue-url> [prompt] [address]"
  exit 1
fi

ISSUE_URL="$1"
PROMPT="${2:-What is the root cause of this issue?}"

if [ -n "${3:-}" ]; then
  ADDRESS="--address $3"
else
  ADDRESS=""
fi

cline -y "$PROMPT: $ISSUE_URL" --mode act $ADDRESS -F json \
  | sed -n '/^{/,$p' \
  | jq -r 'select(.say == "completion_result") | .text' \
  | sed 's/\\n/\n/g'
