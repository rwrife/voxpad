#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 || $# -gt 4 ]]; then
  echo "Usage: $0 <publish-directory> <output-directory> <runtime-identifier> [version]" >&2
  exit 64
fi

publish_dir=$(cd "$1" && pwd)
output_dir=$2
rid=$3
version=${4:-0.1.0}

if [[ ! -x "$publish_dir/Voxpad.Desktop" ]]; then
  echo "Published desktop executable was not found or is not executable: $publish_dir/Voxpad.Desktop" >&2
  exit 1
fi

for tool in ditto hdiutil plutil; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "$tool is required to package the macOS application." >&2
    exit 1
  fi
done

mkdir -p "$output_dir"
output_dir=$(cd "$output_dir" && pwd)
staging_dir="$output_dir/dmg-root-$rid"
app="$staging_dir/Voxpad.app"
zip="$output_dir/voxpad-$rid.app.zip"
dmg="$output_dir/voxpad-$rid.dmg"

rm -rf "$staging_dir"
rm -f "$zip" "$dmg"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$publish_dir"/. "$app/Contents/MacOS/"
cp packaging/macos/Info.plist "$app/Contents/Info.plist"
chmod +x "$app/Contents/MacOS/Voxpad.Desktop"

if [[ "$version" =~ ^(0|[1-9][0-9]*)[.](0|[1-9][0-9]*)[.](0|[1-9][0-9]*)$ ]]; then
  bundle_version=$version
  /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $bundle_version" "$app/Contents/Info.plist"
  /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $bundle_version" "$app/Contents/Info.plist"
fi

plutil -lint "$app/Contents/Info.plist"
ditto -c -k --sequesterRsrc --keepParent "$app" "$zip"
hdiutil create -volname Voxpad -srcfolder "$staging_dir" -ov -format UDZO "$dmg"
rm -rf "$staging_dir"

if [[ ! -s "$zip" || ! -s "$dmg" ]]; then
  echo "A packaged macOS artifact is empty: $zip or $dmg" >&2
  exit 1
fi

echo "PACKAGED_MACOS_APP=$zip"
echo "PACKAGED_MACOS_DMG=$dmg"
