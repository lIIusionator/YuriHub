#!/bin/sh
# Ad-hoc signs a YURI.app. Usage: tools/sign-mac.sh <path/to/YURI.app>
#
# The apphost is built by stamping the app's name into a template, which
# invalidates whatever signature the template carried - so it ships unsigned
# while every dylib beside it is already signed by Microsoft. On Apple Silicon
# the kernel REFUSES to exec an unsigned arm64 binary, so the build does not
# launch at all without this step: it is not a Gatekeeper warning, it is a hard
# failure. Ad-hoc is enough to satisfy that; notarisation is a separate thing.
set -e
app="$1"
if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep --sign - "$app"
elif command -v rcodesign >/dev/null 2>&1; then
  rcodesign sign "$app" >/dev/null 2>&1 || rcodesign sign "$app"
else
  echo "no codesign or rcodesign found - the arm64 build will not launch unsigned" >&2
  exit 1
fi
echo "signed $app"
