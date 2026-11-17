#!/usr/bin/env bash
# 显式创建空数据库（幂等，可重复运行；已存在的库不会被清空）。
# 表结构由 JobStore 定义，脚本只触发建库，避免 schema 漂移。
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

DOTNET="/usr/local/share/dotnet/dotnet"
[ -x "$DOTNET" ] || DOTNET="$(command -v dotnet)"

# exFAT 卷的 ._ AppleDouble 伴生文件会干扰 C# 编译，清除后再建库。
find "$ROOT/src" -name '._*.cs' -delete 2>/dev/null || true

echo "初始化 Filee 数据库（仅建表，不清空已有数据）"
"$DOTNET" run --project "$ROOT/src/Filee.Web/Filee.Web.csproj" -c Debug -- --init-db
