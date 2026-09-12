#!/bin/sh
# Wraps a published osx-* folder into YURI.app. Usage: tools/make-app.sh <publish-dir> <out-dir>
set -e
src="$1"; out="$2"
app="$out/YURI.app"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$src"/. "$app/Contents/MacOS/"
icns="$(dirname "$0")/../src/YURI/YURI.icns"
[ -f "$icns" ] && cp "$icns" "$app/Contents/Resources/YURI.icns"
chmod +x "$app/Contents/MacOS/YURI"
cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleName</key><string>YURI</string>
  <key>CFBundleDisplayName</key><string>YURI</string>
  <key>CFBundleIdentifier</key><string>com.yurihub.yuri</string>
  <key>CFBundleVersion</key><string>2.0.0</string>
  <key>CFBundleShortVersionString</key><string>2.0.0</string>
  <key>CFBundleExecutable</key><string>YURI</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleIconFile</key><string>YURI</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
</dict></plist>
PLIST
echo "built $app"
