#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 <release-tar.gz>"
  exit 1
fi

ARCHIVE_PATH="$1"
RELEASES_DIR="/opt/stitchlens/releases"
CURRENT_LINK="/opt/stitchlens/current"

if [[ ! -f "$ARCHIVE_PATH" ]]; then
  echo "Archive not found: $ARCHIVE_PATH"
  exit 1
fi

TIMESTAMP="$(date +%Y%m%d%H%M%S)"
RELEASE_DIR="${RELEASES_DIR}/${TIMESTAMP}"

mkdir -p "$RELEASE_DIR"
tar -xzf "$ARCHIVE_PATH" -C "$RELEASE_DIR"

chown -R stitchlens:stitchlens "$RELEASE_DIR"

ln -sfn "$RELEASE_DIR" "$CURRENT_LINK"

systemctl daemon-reload
systemctl restart stitchlens
systemctl is-active --quiet stitchlens

echo "Deployment complete: $RELEASE_DIR"
