#!/usr/bin/env bash
# Génère Docs/Rendu.pdf depuis rendu.html (Chromium ou Chrome headless ; les schémas Mermaid
# sont chargés depuis jsDelivr, il faut donc un accès réseau). Usage : Docs/rendu/build.sh [chrome]
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
chrome="${1:-$(command -v chromium || command -v google-chrome-stable || command -v google-chrome || ls -d ~/.cache/ms-playwright/chromium-*/chrome-linux*/chrome 2>/dev/null | tail -1)}"
"$chrome" --headless=new --disable-gpu --no-pdf-header-footer --allow-file-access-from-files \
  --virtual-time-budget=20000 --print-to-pdf="$here/../Rendu.pdf" "file://$here/rendu.html" 2>/dev/null
echo "Docs/Rendu.pdf"
