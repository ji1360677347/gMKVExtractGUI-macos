#!/bin/bash
# 构建 macOS .app bundle
#
# 用法:
#   chmod +x scripts/build_app.sh
#   ./scripts/build_app.sh                  # arm64 (默认)
#   ARCH=x64 ./scripts/build_app.sh         # Intel
#
# 输出: dist/gMKVExtractGUI.app

set -euo pipefail

# ---- 路径与参数 ----
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

ARCH="${ARCH:-arm64}"
RID="osx-${ARCH}"
CONFIG="${CONFIG:-Release}"
APP_NAME="gMKVExtractGUI"
BUNDLE_ID="org.gpower2.gMKVExtractGUI"
DISPLAY_VERSION="2.14.1"
BUILD_VERSION="2.14.1"

DIST="$ROOT/dist"
APP="$DIST/$APP_NAME.app"
PUBLISH_DIR="$ROOT/src/gMKVExtractGUI.Avalonia/bin/$CONFIG/net10.0/$RID/publish"

export DOTNET_ROOT="${DOTNET_ROOT:-/opt/homebrew/opt/dotnet/libexec}"
export PATH="${DOTNET_ROOT%/libexec}/bin:$PATH"

echo "── gMKVExtractGUI macOS 打包 ──"
echo "  RID:      $RID"
echo "  Config:   $CONFIG"
echo "  Output:   $APP"
echo

# ---- 1) 生成图标 ----
if [ ! -f "$ROOT/build/icon.png" ]; then
    echo "[1/5] 生成图标 PNG..."
    python3 "$ROOT/scripts/make_icon.py" "$ROOT/build/icon.png"
else
    echo "[1/5] 图标 PNG 已存在，跳过 (build/icon.png)"
fi

echo "[1/5] 生成 .icns..."
ICONSET="$ROOT/build/AppIcon.iconset"
rm -rf "$ICONSET"
mkdir -p "$ICONSET"

# Apple 推荐尺寸表
declare -a SIZES=(
    "16:16x16"
    "32:16x16@2x"
    "32:32x32"
    "64:32x32@2x"
    "128:128x128"
    "256:128x128@2x"
    "256:256x256"
    "512:256x256@2x"
    "512:512x512"
    "1024:512x512@2x"
)
for entry in "${SIZES[@]}"; do
    size="${entry%%:*}"
    name="${entry##*:}"
    sips -z "$size" "$size" "$ROOT/build/icon.png" \
        --out "$ICONSET/icon_${name}.png" >/dev/null
done

iconutil -c icns -o "$ROOT/build/AppIcon.icns" "$ICONSET"
echo "       → build/AppIcon.icns"

# ---- 2) dotnet publish ----
echo
echo "[2/5] dotnet publish..."
dotnet publish "$ROOT/src/gMKVExtractGUI.Avalonia/gMKVExtractGUI.Avalonia.csproj" \
    -c "$CONFIG" \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -p:UseAppHost=true \
    -o "$PUBLISH_DIR" \
    --nologo --verbosity minimal

# ---- 3) 组装 .app ----
echo
echo "[3/5] 组装 .app bundle..."
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# 复制 publish 产物到 MacOS/
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"

# 复制图标
cp "$ROOT/build/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"

# ---- 4) Info.plist ----
echo "[4/5] 写入 Info.plist..."
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>gMKVExtractGUI</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$BUILD_VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$DISPLAY_VERSION</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>????</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>11.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
    <key>NSSupportsAutomaticGraphicsSwitching</key>
    <true/>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key>
            <string>Matroska Video</string>
            <key>CFBundleTypeRole</key>
            <string>Viewer</string>
            <key>LSItemContentTypes</key>
            <array>
                <string>org.matroska.mkv</string>
                <string>org.matroska.mka</string>
                <string>org.matroska.mks</string>
                <string>org.webmproject.webm</string>
            </array>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>mkv</string>
                <string>mka</string>
                <string>mks</string>
                <string>webm</string>
            </array>
        </dict>
    </array>
</dict>
</plist>
PLIST

# ---- 5) ad-hoc 签名（可选，未签名 macOS 仍可双击启动，但右键打开时友好） ----
echo "[5/5] ad-hoc 签名..."
codesign --force --deep --sign - "$APP" 2>&1 | head -5 || true

echo
echo "✓ 完成: $APP"
echo
echo "下一步:"
echo "  open '$APP'                    # 双击启动测试"
echo "  cp -R '$APP' /Applications/    # 安装到应用程序文件夹"
