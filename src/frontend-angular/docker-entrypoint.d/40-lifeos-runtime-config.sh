#!/bin/sh
set -eu

encode_base64() {
  printf '%s' "$1" | base64 | tr -d '\n'
}

config_file=/usr/share/nginx/html/config.js
config_temp="${config_file}.tmp"
umask 022

cat > "$config_temp" <<EOF
window.LIFEOS_CONFIG = {
  supabaseUrl: atob('$(encode_base64 "${VITE_SUPABASE_URL:-}")'),
  supabaseAnonKey: atob('$(encode_base64 "${VITE_SUPABASE_ANON_KEY:-}")'),
  apiBaseUrl: atob('$(encode_base64 "${VITE_LIFEOS_API_URL:-}")')
};
EOF

mv "$config_temp" "$config_file"
