#!/usr/bin/env bash
set -e

ARCH="${1:-linux-x64}"
VERSION="${2:-1.0.0}"
APP_NAME="MacDesktopApp"
PACKAGE_NAME="Clipboard-${VERSION}-${ARCH}"
OUTPUT_DIR="dist-${ARCH}"
ARCHIVE_NAME="${PACKAGE_NAME}.tar.gz"

echo "==> Building Clipboard for Linux (${ARCH}, v${VERSION})..."

# 1. Publish Self-Contained
dotnet publish -c Release -r "${ARCH}" --self-contained true -o "${OUTPUT_DIR}/${PACKAGE_NAME}"

# 2. Add Linux Desktop Integration (.desktop launcher & icon)
cat << EOF > "${OUTPUT_DIR}/${PACKAGE_NAME}/clipboard.desktop"
[Desktop Entry]
Name=Clipboard
Comment=Minimalist Plain-Text Clipboard Manager
Exec=./MacDesktopApp
Icon=clipboard
Terminal=false
Type=Application
Categories=Utility;
StartupNotify=false
EOF

# Convert or copy icon
if [ -f "Assets/AppIcon.icns" ] && which sips >/dev/null 2>&1; then
    sips -s format png Assets/avalonia-logo.ico --out "${OUTPUT_DIR}/${PACKAGE_NAME}/clipboard.png" >/dev/null 2>&1 || true
fi

# 3. Create .tar.gz archive
echo "==> Creating ${ARCHIVE_NAME}..."
tar -czf "${ARCHIVE_NAME}" -C "${OUTPUT_DIR}" "${PACKAGE_NAME}"

echo "==> Successfully created: ${ARCHIVE_NAME}"
