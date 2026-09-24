#!/usr/bin/env bash
set -e

ARCH="${1:-osx-arm64}"
VERSION="${2:-1.0.0}"
APP_NAME="MacDesktopApp"
DISPLAY_NAME="Clipboard"
OUTPUT_DIR="dist-${ARCH}"
BUNDLE_DIR="${OUTPUT_DIR}/${DISPLAY_NAME}.app"
DMG_NAME="Clipboard-${VERSION}-${ARCH#osx-}.dmg"

echo "==> Building ${DISPLAY_NAME} for ${ARCH} (v${VERSION})..."

# 1. Publish Self-Contained .NET 10
dotnet publish -c Release -r "${ARCH}" --self-contained true -o "${OUTPUT_DIR}/publish"

# 2. Prepare .app Bundle Structure
rm -rf "${BUNDLE_DIR}"
mkdir -p "${BUNDLE_DIR}/Contents/MacOS"
mkdir -p "${BUNDLE_DIR}/Contents/Resources"

cp -r "${OUTPUT_DIR}/publish/"* "${BUNDLE_DIR}/Contents/MacOS/"
cp "Assets/AppIcon.icns" "${BUNDLE_DIR}/Contents/Resources/AppIcon.icns"

# 3. Create Info.plist
cat << EOF > "${BUNDLE_DIR}/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>${APP_NAME}</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>CFBundleIdentifier</key>
    <string>com.antigravity.macdesktopapp</string>
    <key>CFBundleName</key>
    <string>${DISPLAY_NAME}</string>
    <key>CFBundleDisplayName</key>
    <string>${DISPLAY_NAME}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSUIElement</key>
    <true/>
    <key>NSAppleEventsUsageDescription</key>
    <string>Clipboard requires accessibility permission to paste text at cursor position.</string>
</dict>
</plist>
EOF

chmod +x "${BUNDLE_DIR}/Contents/MacOS/${APP_NAME}"

# 4. Ad-hoc Code Sign
echo "==> Signing ${DISPLAY_NAME}.app..."
codesign --force --deep -s - "${BUNDLE_DIR}"

# 5. Create DMG Installer
echo "==> Packaging ${DMG_NAME}..."
DMG_TEMP="/tmp/dmg-temp-${ARCH}"
rm -rf "${DMG_TEMP}" "${DMG_NAME}"
mkdir -p "${DMG_TEMP}"
cp -r "${BUNDLE_DIR}" "${DMG_TEMP}/"
ln -s /Applications "${DMG_TEMP}/Applications"

hdiutil create -volname "${DISPLAY_NAME}" -srcfolder "${DMG_TEMP}" -ov -format UDZO "${DMG_NAME}"
rm -rf "${DMG_TEMP}"

echo "==> Successfully created: ${DMG_NAME}"
