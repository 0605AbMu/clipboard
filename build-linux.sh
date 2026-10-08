#!/usr/bin/env bash
set -e

ARCH="${1:-linux-x64}"
VERSION="${2:-2.0.1}"
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
        DEB_ARCH="all"
        ;;
esac

DEB_VERSION="${VERSION#v}"
DEB_NAME="clipboard_${DEB_VERSION}_${DEB_ARCH}.deb"
DEB_STAGE="${OUTPUT_DIR}/deb-staging"

echo "==> Packaging Debian installer: ${DEB_NAME} (${DEB_ARCH})..."
rm -rf "${DEB_STAGE}" "${DEB_NAME}"

# Create Debian directory hierarchy
mkdir -p "${DEB_STAGE}/DEBIAN"
mkdir -p "${DEB_STAGE}/usr/share/gnome-shell/extensions/clipboard-manager@0605AbMu"
mkdir -p "${DEB_STAGE}/usr/share/glib-2.0/schemas"
mkdir -p "${DEB_STAGE}/usr/share/icons/hicolor/512x512/apps"
mkdir -p "${DEB_STAGE}/usr/share/pixmaps"

# Compile extension schema before packaging
if which glib-compile-schemas >/dev/null 2>&1; then
    glib-compile-schemas extensions/clipboard-manager@0605AbMu/schemas/
fi

# Copy Native GNOME Shell Extension files
cp -r extensions/clipboard-manager@0605AbMu/* "${DEB_STAGE}/usr/share/gnome-shell/extensions/clipboard-manager@0605AbMu/"

# Copy system-wide GSettings schema
cp extensions/clipboard-manager@0605AbMu/schemas/*.xml "${DEB_STAGE}/usr/share/glib-2.0/schemas/"

# Install application icon
cp Assets/clipboard.png "${DEB_STAGE}/usr/share/pixmaps/clipboard.png"
cp Assets/clipboard.png "${DEB_STAGE}/usr/share/icons/hicolor/512x512/apps/clipboard.png"

# Calculate Installed-Size in KiB
INSTALLED_SIZE=$(du -sk "${DEB_STAGE}" | cut -f1)

# Create DEBIAN/control
cat << EOF > "${DEB_STAGE}/DEBIAN/control"
Package: clipboard
Version: ${DEB_VERSION}
Section: gnome
Priority: optional
Architecture: ${DEB_ARCH}
Maintainer: Abdumannon <info@clipboard.app>
Installed-Size: ${INSTALLED_SIZE}
Depends: gnome-shell (>= 45)
Replaces: clipboard (<< ${DEB_VERSION})
Provides: clipboard
Homepage: https://github.com/0605AbMu/clipboard
Description: Native Plain-Text Clipboard Manager GNOME Shell Extension
 High-performance, minimalist plain-text clipboard manager extension for GNOME.
 Runs natively inside GNOME Shell with 0ms virtual paste, history search,
 and custom hotkey (Super+V / Win+V).
EOF

# Create DEBIAN/preinst (Completely wipes legacy standalone app and processes)
cat << 'EOF' > "${DEB_STAGE}/DEBIAN/preinst"
#!/bin/sh
set -e

echo "==> Cleaning up any legacy clipboard versions..."

# 1. Kill any running legacy standalone app processes
pkill -9 -f "/opt/clipboard" 2>/dev/null || true
pkill -9 -f "/usr/bin/clipboard" 2>/dev/null || true
pkill -x "MacDesktopApp" 2>/dev/null || true
pkill -x "clipboard" 2>/dev/null || true

# 2. Remove legacy app binaries and desktop files
rm -rf /opt/clipboard
rm -f /usr/bin/clipboard
rm -f /usr/share/applications/clipboard.desktop
rm -rf /usr/share/gnome-shell/extensions/clipboard-bridge@0605AbMu

# 3. Clean user-level legacy autostart and old bridge/prototype extensions for all human users
for udir in /home/*; do
    if [ -d "$udir" ]; then
        rm -f "$udir/.config/autostart/clipboard.desktop"
        rm -rf "$udir/.local/share/gnome-shell/extensions/clipboard-bridge@0605AbMu"
        rm -rf "$udir/.local/share/gnome-shell/extensions/clipboard@0605AbMu"
    fi
done

exit 0
EOF
chmod 755 "${DEB_STAGE}/DEBIAN/preinst"

# Create DEBIAN/postinst (Activates native extension and updates caches)
cat << 'EOF' > "${DEB_STAGE}/DEBIAN/postinst"
#!/bin/sh
set -e

if [ "$1" = "configure" ]; then
    # 1. Recompile GSettings schemas
    if which glib-compile-schemas >/dev/null 2>&1; then
        glib-compile-schemas /usr/share/glib-2.0/schemas || true
    fi

    # 2. Update icon caches
    if which gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
    fi

    # 3. Clean up desktop cache in case legacy clipboard.desktop was registered
    if which update-desktop-database >/dev/null 2>&1; then
        update-desktop-database -q /usr/share/applications || true
    fi

    # 4. Sync extension to all existing user profiles and activate in GNOME Shell
    for udir in /home/*; do
        uname=$(basename "$udir")
        if id -u "$uname" >/dev/null 2>&1; then
            # Sync atomically to user extensions directory for instant detection
            user_ext="$udir/.local/share/gnome-shell/extensions/clipboard-manager@0605AbMu"
            tmp_ext="${user_ext}.tmp.$$"
            rm -rf "$tmp_ext"
            mkdir -p "$tmp_ext"
            cp -rf /usr/share/gnome-shell/extensions/clipboard-manager@0605AbMu/* "$tmp_ext/"

            if which glib-compile-schemas >/dev/null 2>&1; then
                glib-compile-schemas "$tmp_ext/schemas" 2>/dev/null || true
            fi
            chown -R "$uname:$uname" "$tmp_ext" 2>/dev/null || true
            rm -rf "$user_ext"
            mv -f "$tmp_ext" "$user_ext"

            # Clean up old custom keybindings and add extension to enabled-extensions
            uid=$(id -u "$uname" 2>/dev/null || true)
            if [ -n "$uid" ] && [ -S "/run/user/$uid/bus" ]; then
                su - "$uname" -c "
                    export DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$uid/bus
                    # Clear legacy media-keys
                    gsettings reset-recursively org.gnome.settings-daemon.plugins.media-keys.custom-keybinding:/org/gnome/settings-daemon/plugins/media-keys/custom-keybindings/clipboard-toggle/ 2>/dev/null || true
                    gsettings reset-recursively org.gnome.settings-daemon.plugins.media-keys.custom-keybinding:/org/gnome/settings-daemon/plugins/media-keys/custom-keybindings/clipboard-toggle-alt/ 2>/dev/null || true

                    # Free up Super+V from GNOME Shell built-in message tray shortcut
                    current_tray=\$(gsettings get org.gnome.shell.keybindings toggle-message-tray 2>/dev/null || echo '')
                    if echo \"\$current_tray\" | grep -q \"'<Super>v'\"; then
                        gsettings set org.gnome.shell.keybindings toggle-message-tray \"['<Super>m']\" 2>/dev/null || true
                    fi

                    # Safely enable clipboard-manager@0605AbMu and remove deprecated clipboard@0605AbMu
                    python3 -c \"
import subprocess, ast

def run_cmd(cmd):
    return subprocess.run(cmd, capture_output=True, text=True).stdout.strip()

try:
    raw = run_cmd(['gsettings', 'get', 'org.gnome.shell', 'enabled-extensions'])
    val = raw.replace('@as', '').strip()
    exts = ast.literal_eval(val) if val else []
    if not isinstance(exts, list):
        exts = []
except Exception:
    exts = []

exts = [e for e in exts if e not in ('clipboard@0605AbMu', 'clipboard-bridge@0605AbMu')]
if 'clipboard-manager@0605AbMu' not in exts:
    exts.append('clipboard-manager@0605AbMu')

formatted = str(exts)
subprocess.run(['gsettings', 'set', 'org.gnome.shell', 'enabled-extensions', formatted])
\" 2>/dev/null || true

                    if which gnome-extensions >/dev/null 2>&1; then
                        gnome-extensions enable clipboard-manager@0605AbMu 2>/dev/null || true
                    fi
                " 2>/dev/null || true
            fi
        fi
    done

    echo \"==> Clipboard Manager GNOME Extension installed and activated successfully!\"
fi

exit 0
EOF
chmod 755 "${DEB_STAGE}/DEBIAN/postinst"

# Create DEBIAN/prerm (Stops any running instances)
cat << 'EOF' > "${DEB_STAGE}/DEBIAN/prerm"
#!/bin/sh
set -e

pkill -9 -f "/opt/clipboard" 2>/dev/null || true
pkill -9 -f "/usr/bin/clipboard" 2>/dev/null || true
pkill -x "MacDesktopApp" 2>/dev/null || true
pkill -x "clipboard" 2>/dev/null || true

exit 0
EOF
chmod 755 "${DEB_STAGE}/DEBIAN/prerm"

# Create DEBIAN/postrm (Clean up on uninstall/purge)
cat << 'EOF' > "${DEB_STAGE}/DEBIAN/postrm"
#!/bin/sh
set -e

if [ "$1" = "remove" ] || [ "$1" = "purge" ]; then
    rm -rf /usr/share/gnome-shell/extensions/clipboard-manager@0605AbMu
    rm -f /usr/share/glib-2.0/schemas/org.gnome.shell.extensions.clipboard-manager.gschema.xml

    if which glib-compile-schemas >/dev/null 2>&1; then
        glib-compile-schemas /usr/share/glib-2.0/schemas || true
    fi

    if which gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
    fi

    # Restore default message tray keybinding and clean user extensions
    for udir in /home/*; do
        uname=$(basename "$udir")
        uid=$(id -u "$uname" 2>/dev/null || true)
        if [ "$1" = "purge" ]; then
            rm -rf "$udir/.local/share/gnome-shell/extensions/clipboard-manager@0605AbMu"
            rm -rf "$udir/.local/share/gnome-shell/extensions/clipboard@0605AbMu"
        fi
        if [ -n "$uid" ] && [ -S "/run/user/$uid/bus" ]; then
            su - "$uname" -c "
                export DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$uid/bus
                gsettings reset org.gnome.shell.keybindings toggle-message-tray 2>/dev/null || true
                python3 -c \"
import subprocess, ast
try:
    raw = subprocess.run(['gsettings', 'get', 'org.gnome.shell', 'enabled-extensions'], capture_output=True, text=True).stdout.strip()
    val = raw.replace('@as', '').strip()
    exts = ast.literal_eval(val) if val else []
    if isinstance(exts, list):
        exts = [e for e in exts if e not in ('clipboard-manager@0605AbMu', 'clipboard@0605AbMu', 'clipboard-bridge@0605AbMu')]
        subprocess.run(['gsettings', 'set', 'org.gnome.shell', 'enabled-extensions', str(exts)])
except Exception:
    pass
\" 2>/dev/null || true
            " 2>/dev/null || true
        fi
    done
fi

exit 0
EOF
chmod 755 "${DEB_STAGE}/DEBIAN/postrm"

# Fix permissions
find "${DEB_STAGE}" -type d -exec chmod 755 {} +
find "${DEB_STAGE}/usr/share/gnome-shell/extensions" -type f -exec chmod 644 {} +
find "${DEB_STAGE}/usr/share/glib-2.0/schemas" -type f -exec chmod 644 {} +
chmod 644 "${DEB_STAGE}/usr/share/pixmaps/clipboard.png"
chmod 644 "${DEB_STAGE}/usr/share/icons/hicolor/512x512/apps/clipboard.png"
chmod 644 "${DEB_STAGE}/DEBIAN/control"

# Build .deb archive
dpkg-deb --build --root-owner-group "${DEB_STAGE}" "${DEB_NAME}"
rm -rf "${DEB_STAGE}"

echo "==> Successfully created: ${DEB_NAME}"
