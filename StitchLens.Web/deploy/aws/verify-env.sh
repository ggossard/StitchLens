#!/usr/bin/env bash
set -euo pipefail

ENV_FILE="/etc/stitchlens/stitchlens.env"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Environment file not found: $ENV_FILE"
  exit 1
fi

echo "Environment file present: $ENV_FILE"
echo "Keys loaded (values redacted):"

grep -E '^[A-Za-z_][A-Za-z0-9_]*=' "$ENV_FILE" | cut -d'=' -f1 | sort
