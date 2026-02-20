#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <aws-region> <param-prefix>"
  echo "Example: $0 us-east-1 /stitchlens/prod"
  exit 1
fi

AWS_REGION="$1"
PARAM_PREFIX="$2"
ENV_FILE="/etc/stitchlens/stitchlens.env"

TMP_FILE="$(mktemp)"

echo "ASPNETCORE_ENVIRONMENT=Production" > "$TMP_FILE"
echo "ASPNETCORE_URLS=http://127.0.0.1:5000" >> "$TMP_FILE"

aws ssm get-parameters-by-path \
  --region "$AWS_REGION" \
  --path "$PARAM_PREFIX" \
  --with-decryption \
  --recursive \
  --query 'Parameters[*].[Name,Value]' \
  --output text | while IFS=$'\t' read -r name value; do
    key="${name##*/}"
    printf '%s=%s\n' "$key" "$value" >> "$TMP_FILE"
done

install -d -m 755 /etc/stitchlens
install -m 600 "$TMP_FILE" "$ENV_FILE"
rm -f "$TMP_FILE"

echo "Wrote environment file: $ENV_FILE"
