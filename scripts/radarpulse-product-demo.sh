#!/usr/bin/env bash
set -euo pipefail

command_name="${1:-help}"
if [[ $# -gt 0 ]]; then
  shift
fi

url="http://127.0.0.1:5129"
history_path=""
run_id="product-demo"
sources=2
batches=2
events_per_batch=2
handlers="counter-checksum"
skip_ui_build=false
as_json=false

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root="$(cd "${script_dir}/.." && pwd -P)"
operator_ui_project="${repo_root}/src/Presentation/OperatorUi"
operator_ui_dist="${operator_ui_project}/dist/OperatorUi/browser"
product_http_project="${repo_root}/src/Presentation/RadarPulse.Http/RadarPulse.Http.csproj"
demo_root="${repo_root}/.tmp/product-demo"

die() {
  echo "$*" >&2
  exit 1
}

require_value() {
  local option_name="$1"
  if [[ $# -lt 2 || -z "${2:-}" ]]; then
    die "Missing value for ${option_name}."
  fi
  printf '%s\n' "$2"
}

require_command() {
  local executable="$1"
  if ! command -v "$executable" >/dev/null 2>&1; then
    die "Required command not found: ${executable}"
  fi
}

resolve_package_path() {
  local value="$1"
  if [[ "$value" = /* ]]; then
    printf '%s\n' "$value"
  else
    printf '%s/%s\n' "$repo_root" "$value"
  fi
}

normalize_absolute_path() {
  local path="$1"
  local old_ifs="$IFS"
  local part
  local normalized=()

  IFS='/'
  read -r -a parts <<< "$path"
  IFS="$old_ifs"

  for part in "${parts[@]}"; do
    case "$part" in
      ''|.)
        ;;
      ..)
        if [[ ${#normalized[@]} -gt 0 ]]; then
          unset 'normalized[${#normalized[@]}-1]'
        fi
        ;;
      *)
        normalized+=("$part")
        ;;
    esac
  done

  if [[ ${#normalized[@]} -eq 0 ]]; then
    printf '/\n'
  else
    local joined
    joined="$(IFS=/; printf '%s' "${normalized[*]}")"
    printf '/%s\n' "$joined"
  fi
}

json_escape() {
  printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'
}

convert_handler_set() {
  case "$1" in
    none) printf '1\n' ;;
    counter-checksum) printf '2\n' ;;
    counter-checksum-heavy) printf '3\n' ;;
    snapshot-counting) printf '4\n' ;;
    *) die "Unsupported handler set '$1'." ;;
  esac
}

parse_options() {
  while [[ $# -gt 0 ]]; do
    case "$1" in
      -Url|--url)
        url="$(require_value "$1" "${2:-}")"
        shift 2
        ;;
      --url=*)
        url="${1#*=}"
        shift
        ;;
      -HistoryPath|--history-path)
        history_path="$(require_value "$1" "${2:-}")"
        shift 2
        ;;
      --history-path=*)
        history_path="${1#*=}"
        shift
        ;;
      -RunId|--run-id)
        run_id="$(require_value "$1" "${2:-}")"
        shift 2
        ;;
      --run-id=*)
        run_id="${1#*=}"
        shift
        ;;
      -Sources|--sources)
        sources="$(require_value "$1" "${2:-}")"
        shift 2
        ;;
      --sources=*)
        sources="${1#*=}"
        shift
        ;;
      -Batches|--batches)
        batches="$(require_value "$1" "${2:-}")"
        shift 2
        ;;
      --batches=*)
        batches="${1#*=}"
        shift
        ;;
      -EventsPerBatch|--events-per-batch)
        events_per_batch="$(require_value "$1" "${2:-}")"
        shift 2
        ;;
      --events-per-batch=*)
        events_per_batch="${1#*=}"
        shift
        ;;
      -Handlers|--handlers)
        handlers="$(require_value "$1" "${2:-}")"
        shift 2
        ;;
      --handlers=*)
        handlers="${1#*=}"
        shift
        ;;
      -SkipUiBuild|--skip-ui-build)
        skip_ui_build=true
        shift
        ;;
      -AsJson|--as-json|--json)
        as_json=true
        shift
        ;;
      *)
        die "Unsupported option '$1'. Run: bash scripts/radarpulse-product-demo.sh help"
        ;;
    esac
  done
}

initialize_paths() {
  if [[ -z "$history_path" ]]; then
    resolved_history_path="${demo_root}/radarpulse-product-history.json"
  else
    resolved_history_path="$(resolve_package_path "$history_path")"
  fi
  url="${url%/}"
}

show_help() {
  cat <<'EOF'
RadarPulse local product demo/readiness package
Default URL: http://127.0.0.1:5129

Entrypoints:
  Windows PowerShell: powershell -ExecutionPolicy Bypass -File scripts\radarpulse-product-demo.ps1 help
  PowerShell 7:       pwsh -File scripts/radarpulse-product-demo.ps1 help
  Linux/macOS/WSL2:   bash scripts/radarpulse-product-demo.sh help

Typical first run on Linux/macOS/WSL2:
  1. bash scripts/radarpulse-product-demo.sh paths
  2. bash scripts/radarpulse-product-demo.sh reset-history
  3. bash scripts/radarpulse-product-demo.sh start
  4. open http://127.0.0.1:5129
  5. bash scripts/radarpulse-product-demo.sh readiness
  6. bash scripts/radarpulse-product-demo.sh demo --run-id product-demo
  7. bash scripts/radarpulse-product-demo.sh history

Commands:
  bash scripts/radarpulse-product-demo.sh help
  bash scripts/radarpulse-product-demo.sh paths
  bash scripts/radarpulse-product-demo.sh start [--skip-ui-build] [--url http://127.0.0.1:5129]
  bash scripts/radarpulse-product-demo.sh readiness [--url http://127.0.0.1:5129]
  bash scripts/radarpulse-product-demo.sh demo [--run-id product-demo]
  bash scripts/radarpulse-product-demo.sh history
  bash scripts/radarpulse-product-demo.sh reset-history
  bash scripts/radarpulse-product-demo.sh verify

Scope:
  Local deterministic demo/archive-shaped workflows only.
  This is not public production deployment, auth/TLS hardening, external adapter certification, or exactly-once delivery.
  Readiness blockers and warning-only scope posture stay visible.
  Verify refreshes .NET restore metadata for the current OS before no-restore gates.

Docs:
  README.md
  docs/product-demo-readiness.md
EOF
}

write_paths() {
  if [[ "$as_json" == true ]]; then
    cat <<EOF
{
  "repositoryRoot": "$(json_escape "$repo_root")",
  "operatorUiProject": "$(json_escape "$operator_ui_project")",
  "operatorUiDist": "$(json_escape "$operator_ui_dist")",
  "productHttpProject": "$(json_escape "$product_http_project")",
  "demoRoot": "$(json_escape "$demo_root")",
  "historyPath": "$(json_escape "$resolved_history_path")",
  "url": "$(json_escape "$url")"
}
EOF
    return
  fi

  printf 'Repository root:        %s\n' "$repo_root"
  printf 'Operator UI project:    %s\n' "$operator_ui_project"
  printf 'Operator UI dist:       %s\n' "$operator_ui_dist"
  printf 'Product HTTP project:   %s\n' "$product_http_project"
  printf 'Demo workspace:         %s\n' "$demo_root"
  printf 'Product history path:   %s\n' "$resolved_history_path"
  printf 'Local product URL:      %s\n' "$url"
}

run_checked() {
  local working_directory="$1"
  shift

  printf 'Running: %s\n' "$*"
  (
    cd "$working_directory"
    "$@"
  )
}

api_get() {
  local path="$1"
  require_command curl
  curl --fail --silent --show-error "${url}${path}"
}

api_post() {
  local path="$1"
  local body="$2"
  require_command curl
  curl --fail --silent --show-error \
    --request POST \
    --header "Content-Type: application/json" \
    --data "$body" \
    "${url}${path}"
}

print_json_or_summary() {
  local title="$1"
  local response="$2"

  if [[ "$as_json" == true ]]; then
    printf '%s\n' "$response"
    return
  fi

  if command -v jq >/dev/null 2>&1; then
    printf '%s\n' "$title"
    printf '%s\n' "$response" | jq -r '
      "  status:  \(.statusCode)",
      "  success: \(.isSuccess)",
      (if .message then "  message: \(.message)" else empty end)'
  else
    printf '%s\n' "$title"
    printf '%s\n' "$response"
  fi
}

start_local_product_host() {
  require_command npm
  require_command dotnet
  mkdir -p "$demo_root"

  if [[ "$skip_ui_build" != true ]]; then
    run_checked "$operator_ui_project" npm run build
  fi

  export RadarPulse__ProductHttp__HistoryPath="$resolved_history_path"
  export RadarPulse__ProductHttp__UseInMemoryHistory="false"
  export RadarPulse__ProductHttp__EnableOperatorUiStaticFiles="true"
  export RadarPulse__ProductHttp__OperatorUiStaticAssetPath="$operator_ui_dist"

  printf 'Starting RadarPulse.Http at %s\n' "$url"
  printf 'History path: %s\n' "$resolved_history_path"
  printf 'Operator UI static asset path: %s\n' "$operator_ui_dist"
  exec dotnet run --project "$product_http_project" --urls "$url"
}

show_demo_readiness() {
  local response
  response="$(api_get "/product/pipeline/host/demo-readiness")"
  print_json_or_summary "Product demo readiness" "$response"

  if ! printf '%s\n' "$response" | grep -Eq '"isReady"[[:space:]]*:[[:space:]]*true'; then
    exit 2
  fi
}

invoke_demo_run() {
  local handler_set
  local escaped_run_id
  local body
  local response

  handler_set="$(convert_handler_set "$handlers")"
  escaped_run_id="$(json_escape "$run_id")"
  body="{\"runId\":\"${escaped_run_id}\",\"sourceCount\":${sources},\"batchCount\":${batches},\"eventsPerBatch\":${events_per_batch},\"handlerSet\":${handler_set}}"
  response="$(api_post "/product/pipeline/runs/demo" "$body")"
  print_json_or_summary "Product demo run" "$response"

  if ! printf '%s\n' "$response" | grep -Eq '"isSuccess"[[:space:]]*:[[:space:]]*true'; then
    exit 1
  fi
}

show_history() {
  local readiness
  local runs

  readiness="$(api_get "/product/pipeline/host/readiness")"
  runs="$(api_get "/product/pipeline/runs")"

  if [[ "$as_json" == true ]]; then
    printf '{"readiness":%s,"runs":%s}\n' "$readiness" "$runs"
    return
  fi

  if command -v jq >/dev/null 2>&1; then
    printf 'Product history\n'
    printf '%s\n' "$readiness" | jq -r '
      "  ready:       \(.body.isReady)",
      "  storage:     \(.body.storageKind)",
      "  identity:    \(.body.storageIdentity)",
      "  loaded runs: \(.body.loadedRunCount)",
      (if .body.firstBlockingReason then "  blocker:     \(.body.firstBlockingReason)" else empty end)'
    printf '%s\n' "$runs" | jq -r '
      "  listed runs: \(.body | length)",
      (.body[:5][]? | "    \(.runId) - \(.runState) - \(.readiness)")'
  else
    printf 'Product history readiness:\n%s\n' "$readiness"
    printf 'Product runs:\n%s\n' "$runs"
  fi
}

reset_history() {
  mkdir -p "$demo_root"

  local demo_root_real
  local history_candidate
  local history_parent
  local history_dir_real
  local history_real

  demo_root_real="$(cd "$demo_root" && pwd -P)"
  history_candidate="$(normalize_absolute_path "$resolved_history_path")"

  case "$history_candidate" in
    "${demo_root_real}"/*) ;;
    *) die "Refusing to reset history outside the demo workspace: ${history_candidate}" ;;
  esac

  history_parent="$(dirname "$history_candidate")"
  mkdir -p "$history_parent"
  history_dir_real="$(cd "$history_parent" && pwd -P)"
  history_real="${history_dir_real}/$(basename "$history_candidate")"

  case "$history_real" in
    "${demo_root_real}"/*) ;;
    *) die "Refusing to reset history outside the demo workspace: ${history_real}" ;;
  esac

  if [[ -d "$history_real" ]]; then
    die "Refusing to reset a directory as product demo history: ${history_real}"
  fi

  if [[ -e "$history_real" ]]; then
    rm -f "$history_real"
    printf 'Removed product demo history: %s\n' "$history_real"
  else
    printf 'Product demo history is already absent: %s\n' "$history_real"
  fi
}

verify_package() {
  local test_project="${repo_root}/tests/RadarPulse.Tests/RadarPulse.Tests.csproj"
  local solution="${repo_root}/RadarPulse.sln"
  local focused_filter="FullyQualifiedName~RadarPulseProductHttpHostTests|FullyQualifiedName~RadarPulseProductHttpControlTests|FullyQualifiedName~RadarPulseProductPipelineApiContractTests"
  local architecture_filter="FullyQualifiedName~RadarPulseArchitectureTests"

  require_command npm
  require_command dotnet

  printf '\n== Angular unit tests ==\n'
  run_checked "$operator_ui_project" npm test -- --watch=false

  printf '\n== Angular production build ==\n'
  run_checked "$operator_ui_project" npm run build

  printf '\n== Operator UI browser smoke ==\n'
  run_checked "$operator_ui_project" npm run smoke

  printf '\n== Hosted same-origin browser smoke ==\n'
  run_checked "$operator_ui_project" npm run smoke:hosted

  printf '\n== .NET dependency restore ==\n'
  run_checked "$repo_root" dotnet restore "$solution" --force

  printf '\n== .NET architecture boundary gate ==\n'
  run_checked "$repo_root" dotnet test "$test_project" -c Release --no-restore --filter "$architecture_filter"

  printf '\n== Focused .NET product HTTP/API/readiness Release gate ==\n'
  run_checked "$repo_root" dotnet test "$test_project" -c Release --no-restore --filter "$focused_filter"

  printf '\n== .NET Release build ==\n'
  run_checked "$repo_root" dotnet build "$solution" -c Release --no-restore

  printf '\nPackaged verification passed.\n'
}

case "$command_name" in
  help|paths|start|readiness|demo|history|reset-history|verify) ;;
  *) die "Unsupported command '$command_name'. Run: bash scripts/radarpulse-product-demo.sh help" ;;
esac

parse_options "$@"
initialize_paths

case "$command_name" in
  help)
    show_help
    ;;
  paths)
    write_paths
    ;;
  start)
    start_local_product_host
    ;;
  readiness)
    show_demo_readiness
    ;;
  demo)
    invoke_demo_run
    ;;
  history)
    show_history
    ;;
  reset-history)
    reset_history
    ;;
  verify)
    verify_package
    ;;
esac
