#!/usr/bin/env bash
# Renders icons/source.svg into the PWA PNG icons using macOS built-in tools only.
set -euo pipefail

cd "$(dirname "$0")/.."

out_dir="src/MealPlanner.App/wwwroot/icons"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

mkdir -p "$out_dir"
qlmanage -t -s 1024 -o "$tmp" icons/source.svg >/dev/null 2>&1
master="$tmp/source.svg.png"

render() {
  local size="$1" name="$2"
  sips -z "$size" "$size" "$master" --out "$out_dir/$name" >/dev/null
}

render 192 icon-192.png
render 512 icon-512.png
render 512 icon-maskable-512.png
render 180 apple-touch-icon.png
render 32 favicon-32.png

echo "Icons written to $out_dir"
