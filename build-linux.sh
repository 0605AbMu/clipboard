#!/usr/bin/env bash
set -e

ARCH="${1:-linux-x64}"
VERSION="${2:-1.0.0}"
APP_NAME="MacDesktopApp"
PACKAGE_NAME="Clipboard-${VERSION}-${ARCH}"
OUTPUT_DIR="dist-${ARCH}"
ARCHIVE_NAME="${PACKAGE_NAME}.tar.gz"

echo "==> Building Clipboard for Linux (${ARCH}, v${VERSION})..."

# 1. Publish Native AOT with fallback
if dotnet publish -c Release -r "${ARCH}" -p:PublishAot=true -o "${OUTPUT_DIR}/${PACKAGE_NAME}"; then
    echo "==> Native AOT publish succeeded for ${ARCH}"
else
    echo "==> Native AOT failed, falling back to trimmed self-contained publish..."
    dotnet publish -c Release -r "${ARCH}" --self-contained true -p:PublishTrimmed=true -o "${OUTPUT_DIR}/${PACKAGE_NAME}"
fi

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

# Copy application icon
cp Assets/clipboard.png "${OUTPUT_DIR}/${PACKAGE_NAME}/clipboard.png"

# 3. Create .tar.gz archive
echo "==> Creating ${ARCHIVE_NAME}..."
tar -czf "${ARCHIVE_NAME}" -C "${OUTPUT_DIR}" "${PACKAGE_NAME}"
echo "==> Successfully created: ${ARCHIVE_NAME}"

# 4. Create Debian (.deb) Package for Ubuntu / Debian Desktop
case "${ARCH}" in
    linux-x64|x64|amd64)
        DEB_ARCH="amd64"
        ;;
    linux-arm64|arm64)
        DEB_ARCH="arm64"
        ;;
    linux-arm|armhf)
        DEB_ARCH="armhf"
        ;;
    *)
        DEB_ARCH="amd64"
        ;;
esac

DEB_VERSION="${VERSION#v}"
DEB_NAME="clipboard_${DEB_VERSION}_${DEB_ARCH}.deb"
DEB_STAGE="${OUTPUT_DIR}/deb-staging"

echo "==> Packaging Debian installer: ${DEB_NAME} (${DEB_ARCH})..."
rm -rf "${DEB_STAGE}" "${DEB_NAME}"

# Create Debian directory hierarchy
mkdir -p "${DEB_STAGE}/DEBIAN"
mkdir -p "${DEB_STAGE}/opt/clipboard"
mkdir -p "${DEB_STAGE}/usr/bin"
mkdir -p "${DEB_STAGE}/usr/share/applications"
mkdir -p "${DEB_STAGE}/usr/share/pixmaps"
mkdir -p "${DEB_STAGE}/usr/share/icons/hicolor/512x512/apps"

# Copy published application files to /opt/clipboard
cp -r "${OUTPUT_DIR}/${PACKAGE_NAME}/"* "${DEB_STAGE}/opt/clipboard/"
# Remove redundant portable desktop/png from /opt/clipboard if present
rm -f "${DEB_STAGE}/opt/clipboard/clipboard.desktop"
rm -f "${DEB_STAGE}/opt/clipboard/clipboard.png"

# Symlink executable to /usr/bin/clipboard
ln -s /opt/clipboard/${APP_NAME} "${DEB_STAGE}/usr/bin/clipboard"

# Install application icon
cp Assets/clipboard.png "${DEB_STAGE}/usr/share/pixmaps/clipboard.png"
cp Assets/clipboard.png "${DEB_STAGE}/usr/share/icons/hicolor/512x512/apps/clipboard.png"

# Install system desktop entry
cat << EOF > "${DEB_STAGE}/usr/share/applications/clipboard.desktop"
[Desktop Entry]
Type=Application
Version=1.0
Name=Clipboard
GenericName=Clipboard Manager
Comment=Minimalist Plain-Text Clipboard Manager
Exec=/usr/bin/clipboard
Icon=clipboard
Terminal=false
Categories=Utility;Application;
StartupWMClass=${APP_NAME}
Keywords=clipboard;manager;paste;copy;history;
StartupNotify=false
EOF

# Calculate Installed-Size in KiB
INSTALLED_SIZE=$(du -sk "${DEB_STAGE}" | cut -f1)

# Create DEBIAN/control
cat << EOF > "${DEB_STAGE}/DEBIAN/control"
Package: clipboard
Version: ${DEB_VERSION}
Section: utils
Priority: optional
Architecture: ${DEB_ARCH}
Maintainer: Abdumannon <info@clipboard.app>
Installed-Size: ${INSTALLED_SIZE}
Depends: libc6 (>= 2.34), libfontconfig1, libx11-6, libice6, libsm6
Recommends: wl-clipboard, xclip, xdotool
Homepage: https://github.com/0605AbMu/clipboard
Description: Minimalist Plain-Text Clipboard Manager
 High-performance, minimalist plain-text clipboard manager with auto-paste,
 pinning, and system integration for Ubuntu / Linux desktop.
EOF

# Create DEBIAN/postinst
cat << 'EOF' > "${DEB_STAGE}/DEBIAN/postinst"
#!/bin/sh
set -e

if [ "$1" = "configure" ]; then
    if which update-desktop-database >/dev/null 2>&1; then
        update-desktop-database -q /usr/share/applications || true
    fi
    if which gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
    fi
fi

exit 0
EOF
chmod 755 "${DEB_STAGE}/DEBIAN/postinst"

# Create DEBIAN/prerm (stops app cleanly on upgrade or uninstall)
cat << 'EOF' > "${DEB_STAGE}/DEBIAN/prerm"
#!/bin/sh
set -e

if [ "$1" = "remove" ] || [ "$1" = "upgrade" ]; then
    pkill -f "/opt/clipboard/MacDesktopApp" 2>/dev/null || true
    pkill -x "MacDesktopApp" 2>/dev/null || true
    pkill -x "clipboard" 2>/dev/null || true
fi

exit 0
EOF
chmod 755 "${DEB_STAGE}/DEBIAN/prerm"

# Create DEBIAN/postrm (refreshes caches on remove/purge)
cat << 'EOF' > "${DEB_STAGE}/DEBIAN/postrm"
#!/bin/sh
set -e

if [ "$1" = "remove" ] || [ "$1" = "purge" ]; then
    if which update-desktop-database >/dev/null 2>&1; then
        update-desktop-database -q /usr/share/applications || true
    fi
    if which gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
    fi
fi

exit 0
EOF
chmod 755 "${DEB_STAGE}/DEBIAN/postrm"

# Fix permissions
find "${DEB_STAGE}" -type d -exec chmod 755 {} +
find "${DEB_STAGE}/opt/clipboard" -type f -exec chmod 644 {} +
chmod 755 "${DEB_STAGE}/opt/clipboard/${APP_NAME}"
chmod 755 "${DEB_STAGE}/opt/clipboard/createdump" 2>/dev/null || true
find "${DEB_STAGE}/opt/clipboard" -name "*.so" -exec chmod 755 {} + 2>/dev/null || true
chmod 644 "${DEB_STAGE}/usr/share/applications/clipboard.desktop"
chmod 644 "${DEB_STAGE}/usr/share/pixmaps/clipboard.png"
chmod 644 "${DEB_STAGE}/usr/share/icons/hicolor/512x512/apps/clipboard.png"
chmod 644 "${DEB_STAGE}/DEBIAN/control"

# Build .deb archive
dpkg-deb --build --root-owner-group "${DEB_STAGE}" "${DEB_NAME}"
rm -rf "${DEB_STAGE}"

echo "==> Successfully created: ${DEB_NAME}"
