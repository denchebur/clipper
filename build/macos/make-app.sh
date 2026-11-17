#!/usr/bin/env bash
# Сборка Clipper.app и .dmg для macOS.
#   ./build/macos/make-app.sh osx-arm64 1.0.0   # Apple Silicon (M1/M2/M3…)
#   ./build/macos/make-app.sh osx-x64   1.0.0   # Intel
# Нужен .NET 8 SDK. На macOS приложение подписывается ad-hoc и упаковывается в .dmg;
# на другой ОС вместо .dmg получится .zip (для проверки).
set -euo pipefail

RID="${1:-osx-arm64}"
VERSION="${2:-0.0.0-dev}"
PLIST_VERSION="${VERSION%%-*}"   # macOS ждёт версию вида 1.2.3, без «-dev»

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PUBLISH="$ROOT/publish/$RID"
STAGE="$ROOT/dist/$RID"
APP="$STAGE/Clipper.app"

dotnet publish "$ROOT/src/Clipper.Desktop" -c Release -r "$RID" --self-contained \
  -p:UseAppHost=true -p:Version="$VERSION" -o "$PUBLISH"

rm -rf "$STAGE"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH/." "$APP/Contents/MacOS/"
sed "s/__VERSION__/$PLIST_VERSION/g" "$ROOT/build/macos/Info.plist" > "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/Clipper"

if command -v codesign >/dev/null; then
  # Ad-hoc подпись: без неё приложение на Apple Silicon не запустится.
  codesign --force --deep --sign - "$APP"
fi

OUT="$ROOT/dist/Clipper-$VERSION-$RID"
if command -v hdiutil >/dev/null; then
  ln -s /Applications "$STAGE/Applications"   # чтобы в окне .dmg можно было перетащить в «Программы»
  for attempt in 1 2 3; do
    hdiutil create -volname Clipper -srcfolder "$STAGE" -ov -format UDZO "$OUT.dmg" && break
    sleep 5
  done
  echo "Готово: $OUT.dmg"
else
  (cd "$STAGE" && zip -qry "$OUT.zip" Clipper.app)
  echo "Готово: $OUT.zip"
fi
