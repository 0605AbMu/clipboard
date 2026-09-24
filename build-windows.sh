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

# 2. Create Zip Archive with cross-platform fallbacks
echo "==> Creating ${ZIP_NAME}..."
rm -f "${ZIP_NAME}"

if command -v 7z >/dev/null 2>&1; then
    7z a -tzip "${ZIP_NAME}" "./${OUTPUT_DIR}/${PACKAGE_NAME}/*"
elif command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -Command "Compress-Archive -Path '${OUTPUT_DIR}/${PACKAGE_NAME}/*' -DestinationPath '${ZIP_NAME}' -Force"
elif command -v pwsh >/dev/null 2>&1; then
    pwsh -NoProfile -Command "Compress-Archive -Path '${OUTPUT_DIR}/${PACKAGE_NAME}/*' -DestinationPath '${ZIP_NAME}' -Force"
elif command -v zip >/dev/null 2>&1; then
    (cd "${OUTPUT_DIR}" && zip -rq "../${ZIP_NAME}" "${PACKAGE_NAME}")
else
    tar -a -cf "${ZIP_NAME}" -C "${OUTPUT_DIR}" "${PACKAGE_NAME}"
fi

echo "==> Successfully created: ${ZIP_NAME}"
