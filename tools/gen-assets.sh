#!/usr/bin/env bash
# Generates placeholder SVG artwork (products + banners) so the layout can be
# previewed without real photography. Delete after running; outputs are static.
set -euo pipefail

OUT_PRODUCTS="wwwroot/img/products"
OUT_BANNERS="wwwroot/img/banners"
mkdir -p "$OUT_PRODUCTS" "$OUT_BANNERS"

motif() {
  case "$1" in
    gold-bar)
      cat <<'SVG'
      <g fill="url(#gold)">
        <path d="M120 250h160l-18 46H138z"/>
        <path d="M140 196h120l-14 40H154z"/>
        <path d="M158 146h84l-11 36h-62z"/>
      </g>
SVG
      ;;
    necklace)
      cat <<'SVG'
      <g fill="none" stroke="url(#gold)" stroke-width="10" stroke-linecap="round">
        <path d="M110 120c0 78 40 128 90 128s90-50 90-128"/>
      </g>
      <path fill="url(#gold)" d="M200 248l-26 34 26 44 26-44z"/>
SVG
      ;;
    ring)
      cat <<'SVG'
      <circle cx="200" cy="240" r="72" fill="none" stroke="url(#gold)" stroke-width="16"/>
      <path fill="url(#gold)" d="M200 128l-30 40h60z"/>
      <path fill="url(#gold)" d="M170 168l30 34 30-34z" opacity=".85"/>
SVG
      ;;
    bracelet)
      cat <<'SVG'
      <circle cx="200" cy="210" r="92" fill="none" stroke="url(#gold)" stroke-width="16" stroke-dasharray="26 16"/>
      <circle cx="200" cy="210" r="58" fill="none" stroke="url(#gold)" stroke-width="6" opacity=".5"/>
SVG
      ;;
    earring)
      cat <<'SVG'
      <g fill="none" stroke="url(#gold)" stroke-width="9" stroke-linecap="round">
        <path d="M140 120a26 26 0 0 0 52 0"/>
        <path d="M208 120a26 26 0 0 0 52 0"/>
      </g>
      <path fill="url(#gold)" d="M166 160l-24 40 24 52 24-52z"/>
      <path fill="url(#gold)" d="M234 160l-24 40 24 52 24-52z" opacity=".85"/>
SVG
      ;;
    diamond)
      cat <<'SVG'
      <path fill="url(#gold)" d="M140 150h120l36 52-96 122-96-122z"/>
      <path fill="#241c12" opacity=".35" d="M104 202h192l-96 122z"/>
      <path fill="none" stroke="#241c12" stroke-width="4" opacity=".4" d="M140 150l-12 52 72 122 72-122-12-52M104 202h192"/>
SVG
      ;;
    gift)
      cat <<'SVG'
      <g fill="url(#gold)">
        <rect x="120" y="170" width="160" height="40" rx="6"/>
        <rect x="136" y="214" width="128" height="86" rx="6"/>
        <rect x="190" y="170" width="20" height="130"/>
      </g>
      <path fill="none" stroke="url(#gold)" stroke-width="10" d="M200 168c-14-34-52-34-52-8s38 8 52 8zm0 0c14-34 52-34 52-8s-38 8-52 8z"/>
SVG
      ;;
  esac
}

label() {
  case "$1" in
    gold-bar) echo "GOLD BAR" ;;
    necklace) echo "NECKLACE" ;;
    ring) echo "RING" ;;
    bracelet) echo "BRACELET" ;;
    earring) echo "EARRING" ;;
    diamond) echo "DIAMOND" ;;
    gift) echo "GIFT" ;;
  esac
}

for kind in gold-bar necklace ring bracelet earring diamond gift; do
  for variant in 1 2 3; do
    case "$variant" in
      1) rot="0" ;;
      2) rot="7" ;;
      3) rot="-7" ;;
    esac
    {
      echo '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 400" width="400" height="400" role="img">'
      echo '  <defs>'
      echo '    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">'
      echo '      <stop offset="0" stop-color="#241c12"/>'
      echo '      <stop offset="1" stop-color="#3a2d1a"/>'
      echo '    </linearGradient>'
      echo '    <linearGradient id="gold" x1="0" y1="0" x2="1" y2="1">'
      echo '      <stop offset="0" stop-color="#f7e7b4"/>'
      echo '      <stop offset="0.5" stop-color="#d4af37"/>'
      echo '      <stop offset="1" stop-color="#a8801f"/>'
      echo '    </linearGradient>'
      echo '  </defs>'
      echo '  <rect width="400" height="400" fill="url(#bg)"/>'
      echo '  <circle cx="200" cy="200" r="152" fill="none" stroke="url(#gold)" stroke-width="2" opacity="0.3"/>'
      echo "  <g transform=\"rotate($rot 200 210)\">"
      motif "$kind"
      echo '  </g>'
      echo "  <text x=\"200\" y=\"372\" text-anchor=\"middle\" font-family=\"Tahoma, sans-serif\" font-size=\"20\" letter-spacing=\"4\" fill=\"#c9a84c\">$(label "$kind") $variant</text>"
      echo '</svg>'
    } > "$OUT_PRODUCTS/$kind-$variant.svg"
  done
done

for i in 1 2 3; do
  {
    echo '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1600 700" width="1600" height="700" role="img">'
    echo '  <defs>'
    echo '    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">'
    echo '      <stop offset="0" stop-color="#171208"/>'
    echo '      <stop offset="1" stop-color="#4a3a1c"/>'
    echo '    </linearGradient>'
    echo '    <radialGradient id="glow" cx="0.78" cy="0.4" r="0.55">'
    echo '      <stop offset="0" stop-color="#f7e7b4" stop-opacity="0.55"/>'
    echo '      <stop offset="1" stop-color="#f7e7b4" stop-opacity="0"/>'
    echo '    </radialGradient>'
    echo '  </defs>'
    echo '  <rect width="1600" height="700" fill="url(#bg)"/>'
    echo '  <rect width="1600" height="700" fill="url(#glow)"/>'
    echo '  <g fill="none" stroke="#d4af37" stroke-width="3" opacity="0.5">'
    echo '    <circle cx="1250" cy="300" r="180"/>'
    echo '    <circle cx="1250" cy="300" r="120"/>'
    echo '    <circle cx="1250" cy="300" r="60"/>'
    echo '  </g>'
    echo '  <g fill="#d4af37" opacity="0.85">'
    echo '    <path d="M1150 380h200l-24 60h-152z"/>'
    echo '    <path d="M1176 316h148l-18 46h-112z"/>'
    echo '  </g>'
    echo "</svg>"
  } > "$OUT_BANNERS/hero-$i.svg"
done

for i in 1 2; do
  {
    echo '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 400" width="800" height="400" role="img">'
    echo '  <defs>'
    echo '    <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">'
    echo '      <stop offset="0" stop-color="#2b2113"/>'
    echo '      <stop offset="1" stop-color="#5a4520"/>'
    echo '    </linearGradient>'
    echo '  </defs>'
    echo '  <rect width="800" height="400" fill="url(#bg)"/>'
    echo '  <g fill="none" stroke="#e8cf8a" stroke-width="3" opacity="0.45">'
    echo '    <circle cx="660" cy="200" r="120"/>'
    echo '    <circle cx="660" cy="200" r="80"/>'
    echo '  </g>'
    echo '  <g fill="#e8cf8a" opacity="0.8">'
    echo '    <path d="M600 250h120l-14 36h-92z"/>'
    echo '  </g>'
    echo '</svg>'
  } > "$OUT_BANNERS/promo-$i.svg"
done

echo "generated:"
ls "$OUT_PRODUCTS" | wc -l
ls "$OUT_BANNERS" | wc -l
