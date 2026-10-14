#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TEST_DIR="$(mktemp -d "${TMPDIR:-/tmp}/lifeos-swift.XXXXXX")"
trap 'rm -f "$TEST_DIR/contract"; rmdir "$TEST_DIR"' EXIT
xcrun swiftc \
  "$ROOT/src/frontend-angular/ios/App/App/CookingSessionContract.swift" \
  "$ROOT/src/frontend-angular/ios/App/App/SharedAppGroup.swift" \
  "$ROOT/src/frontend-angular/ios/tests/main.swift" \
  -o "$TEST_DIR/contract"
cd "$ROOT/src/frontend-angular"
LIFEOS_SWIFT_CONTRACT_EXECUTABLE="$TEST_DIR/contract" node --test src/app/core/native/cooking-contract.spec.ts
