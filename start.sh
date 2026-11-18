#!/usr/bin/env bash
# 一键启动 Filee.Web
# 所有配置与密钥集中在 appsettings.local.json（gitignore，不进库/不进数据库）。
# 首次使用：cp appsettings.local.json.example appsettings.local.json，按其中说明填入真实值。
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

CONFIG="${FILEE_CONFIG:-$ROOT/appsettings.local.json}"
if [ ! -f "$CONFIG" ]; then
  echo "缺少配置文件: ${CONFIG}" >&2
  echo "请先执行: cp appsettings.local.json.example appsettings.local.json 并填写真实值。" >&2
  exit 1
fi

DOTNET="/usr/local/share/dotnet/dotnet"
[ -x "$DOTNET" ] || DOTNET="$(command -v dotnet)"

# exFAT 卷会为源码生成 ._ AppleDouble 伴生文件，C# 编译器会误当成源码报错；启动前清掉。
find "$ROOT/src" -name '._*.cs' -delete 2>/dev/null || true

# 从 JSON 导出为环境变量（在 dotnet 启动前，Kestrel 才能读到 TLS 设置）。
# 相对证书路径按项目根解析为绝对路径。
eval "$(python3 - "$CONFIG" "$ROOT" <<'PY'
import sys, json, shlex, os
cfg = json.load(open(sys.argv[1], encoding='utf-8'))
root = sys.argv[2]
def q(v): return shlex.quote('' if v is None else str(v))
s = cfg.get('Server', {}); sso = cfg.get('Sso', {}); lim = cfg.get('Limits', {})
tls = cfg.get('Tls', {}); origins = sso.get('allowedRedirectOrigins') or []
pfx = tls.get('pfxPath') or ''
if pfx and not os.path.isabs(pfx): pfx = os.path.join(root, pfx)
env = {
  'ASPNETCORE_URLS': s.get('urls', 'https://127.0.0.1:18799'),
  'FILEE_CONFIG': sys.argv[1],
  'FILEE_WEB_ISSUER': sso.get('issuer', ''),
  'FILEE_WEB_AUDIENCE': sso.get('audience', '') or sso.get('clientId', ''),
  'FILEE_WEB_CLIENT_ID': sso.get('clientId', ''),
  'FILEE_WEB_CLIENT_SECRET': sso.get('clientSecret', ''),
  'FILEE_WEB_ALLOWED_ORIGINS': ','.join(origins),
  'FILEE_WEB_REDIRECT_URI': s.get('redirectUri', ''),
  'FILEE_WEB_OPS_TOKEN': cfg.get('opsToken', '') or '',
  'FILEE_WEB_TLS_PFX': pfx,
  'FILEE_WEB_TLS_PASSWORD': tls.get('password', ''),
  'FILEE_LO_WORKERS': lim.get('loWorkers', 1),
  'FILEE_WEB_MAX_FILE_BYTES': lim.get('maxFileBytes', 524288000),
  'FILEE_WEB_MAX_CONCURRENCY': lim.get('maxConcurrency', 4),
  'FILEE_WEB_JOB_TIMEOUT_MINUTES': lim.get('jobTimeoutMinutes', 10),
  'FILEE_WEB_SSE_THROTTLE_MS': lim.get('sseThrottleMs', 150),
  'FILEE_WEB_SSE_BUFFER_SIZE': lim.get('sseBufferSize', 200),
  'FILEE_WEB_SSE_HEARTBEAT_SECONDS': lim.get('sseHeartbeatSeconds', 15),
}
for k, v in env.items(): print(f'export {k}={q(v)}')
PY
)"

echo "启动 Filee.Web (config: ${CONFIG})"
exec "$DOTNET" run --project "$ROOT/src/Filee.Web/Filee.Web.csproj" -c Release
