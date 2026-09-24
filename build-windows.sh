#!/usr/bin/env bash
set -e

ARCH="${1:-win-x64}"
VERSION="${2:-1.0.0}"
APP_NAME="MacDesktopApp"
PACKAGE_NAME="Clipboard-${VERSION}-${ARCH}"
OUTPUT_DIR="dist-${ARCH}"
ZIP_NAME="${PACKAGE_NAME}.zip"

echo "==> Building Clipboard for Windows (${ARCH}, v${VERSION})..."

# 1. Publish Self-Contained
dotnet publish -c Release -r "${ARCH}" --self-contained true -o "${OUTPUT_DIR}/${PACKAGE_NAME}"

# 2. Create Zip Archive
echo "==> Creating ${ZIP_NAME}..."
rm -f "${ZIP_NAME}"
(cd "${OUTPUT_DIR}" && zip -rq "../${ZIP_NAME}" "${PACKAGE_NAME}")

echo "==> Successfully created: ${ZIP_NAME}"
