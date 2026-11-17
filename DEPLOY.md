# Filee.Web 部署与配置说明

## 1. 配置：单一 JSON 文件

所有配置与密钥集中在 **`appsettings.local.json`**（项目根目录），该文件已被 `.gitignore` 忽略，**不会进代码库、不会写进数据库、不会随部署包分发**。

### 首次配置

```bash
cp appsettings.local.json.example appsettings.local.json
# 按文件中的 _ 注释逐项填写真实值
```

各字段含义与取值来源都写在 `appsettings.local.json.example` 的注释里，重点是：

| 配置段 | 内容 | 去哪取 |
|---|---|---|
| `Server.urls` | 监听地址（含端口） | 与 WorkBuddy 控制台登记的回调地址同端口 |
| `Server.redirectUri` | 免登回调地址 | 必须与控制台登记值**完全一致** |
| `Sso.issuer` | 令牌签发方 | 访问 Client_profile 里的 `.well-known/openid-configuration`，取响应的 `issuer` |
| `Sso.clientId` / `clientSecret` | 应用标识与密钥 | Client_profile 的 Client ID / Client Secret |
| `Sso.audience` | 令牌受众 | 一般等于 clientId |
| `Tls.pfxPath` / `password` | 本地 HTTPS 证书与密码 | 自签证书仅本机联调用 |
| `opsToken` | 运维接口令牌 | 自行生成；留空则关闭 `/api/ops/health` |
| `Limits.*` | 文件大小、并发、超时、SSE、LibreOffice 并行度 | 按需调整，均有默认值 |

> **安全说明**：JSON 是明文存储。若需真正保密，请改用 Docker secret 或密钥管理服务注入。环境变量优先级高于 JSON（便于容器临时覆盖），不要把密钥写进命令行参数或日志。

### 一键启动

```bash
./start.sh
```

`start.sh` 检查配置文件存在后启动服务，无需手动 export 一堆环境变量。也可用 `FILEE_CONFIG=/path/to/config.json` 指定配置路径。

## 2. 数据库：不带库、首次运行构建

- **部署包不含数据库**：`Filee.Web.csproj` 已排除 `**/*.db` 与 `appsettings.local.json`。
- **数据库只存用户数据**：仅 `jobs` 一张表（任务摘要、状态、进度、输出文件名），**不含任何 SSO 配置或密钥**。
- **自动构建兜底**：`JobStore` 构造时执行 `CREATE TABLE IF NOT EXISTS`，首次运行自动建空库，旧库缺列会自动补（如 `output_files`）。
- **显式建库脚本**（可选，便于部署预置）：

  ```bash
  ./init-db.sh        # 幂等；已存在的库不会被清空
  ```

  脚本通过程序的 `--init-db` 入口建库，表结构始终由 `JobStore` 定义，不会漂移。

## 3. 部署步骤小结

1. 部署源码/发布产物（**不带** `.db`、不带 `appsettings.local.json`）。
2. 在部署目录 `cp appsettings.local.json.example appsettings.local.json` 并填值。
3. （可选）`./init-db.sh` 预建空库；不跑也行，首次启动会自动建。
4. `./start.sh` 启动。
5. 确认 WorkBuddy 控制台登记的免登回调地址与配置里的 `Server.redirectUri` 一致。
