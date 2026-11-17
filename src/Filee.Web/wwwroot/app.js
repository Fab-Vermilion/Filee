const state = {
  csrfToken: "",
  target: "",
  jobs: new Map(),
  sources: new Map(),      // jobId -> EventSource，清空任务列表时一并关闭
  reloading: false,
  formats: [],          // 全部 FormatDescriptor
  byId: new Map(),      // id -> descriptor
  files: [],            // [{name, size, sourceId, targets}]
  commonTargets: [],    // 所有文件 targets 的交集
  category: "",
};

const elements = {
  sessionNote: document.querySelector("#session-note"),
  formatHint: document.querySelector("#format-hint"),
  target: document.querySelector("#target"),
  targetLabel: document.querySelector("#target-label"),
  categoryList: document.querySelector("#category-list"),
  donut: document.querySelector("#donut"),
  files: document.querySelector("#files"),
  dropzone: document.querySelector("#dropzone"),
  fileList: document.querySelector("#file-list"),
  addMore: document.querySelector("#add-more"),
  submit: document.querySelector("#submit"),
  retry: document.querySelector("#retry"),
  message: document.querySelector("#message"),
  jobs: document.querySelector("#job-list"),
};

const CATEGORY_LABELS = {
  Image: "图片", Pdf: "PDF", Document: "文档", Spreadsheet: "表格",
  Presentation: "演示", Hwp: "HWP", Text: "文本", Subtitle: "字幕",
  Video: "视频", Audio: "音频", Archive: "压缩包", Data: "数据",
  Vector: "矢量", Cad: "CAD", Font: "字体", Ebook: "电子书",
  Model3D: "3D 模型", Geo: "地理数据",
};
const MAX_FILES = 20;  // 单批最多文件数（方案约定）
const CATEGORY_ORDER = ["Image", "Pdf", "Document", "Spreadsheet", "Presentation",
  "Hwp", "Text", "Subtitle", "Video", "Audio", "Archive", "Data",
  "Vector", "Cad", "Font", "Ebook", "Model3D", "Geo"];
function categoryRank(c) { const i = CATEGORY_ORDER.indexOf(c); return i < 0 ? CATEGORY_ORDER.length : i; }
function labelOf(c) { return CATEGORY_LABELS[c] || c; }
function setMessage(t) { elements.message.textContent = t; }
function humanSize(n) {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${(n / 1024 / 1024).toFixed(1)} MB`;
}

async function readSession() {
  const response = await fetch("/api/session", { credentials: "same-origin" });
  if (response.status === 401) { await requestRelogin(); return false; }
  if (!response.ok) throw new Error("会话读取失败");
  const session = await response.json();
  state.csrfToken = session.csrfToken;
  elements.sessionNote.textContent = `已连接：${session.sub}`;
  elements.retry.hidden = true;
  return true;
}

async function requestRelogin() {
  if (state.reloading) return;
  state.reloading = true;
  elements.submit.disabled = true;
  elements.retry.hidden = false;
  setMessage("登录态已失效，正在重新进入应用…");
  try {
    if (window.wb && typeof window.wb.reload === "function") {
      await window.wb.reload();
      setMessage("已通知客户端刷新，页面即将重新载入。");
      return;
    }
    const requestId = crypto.randomUUID();
    const reply = await new Promise((resolve) => {
      const timer = setTimeout(() => { window.removeEventListener("message", handler); resolve(null); }, 8000);
      const handler = (event) => {
        if (!isTrustedReloadMessage(event, requestId)) return;
        clearTimeout(timer); window.removeEventListener("message", handler); resolve(event.data);
      };
      window.addEventListener("message", handler);
      window.parent.postMessage({ type: "wb:reload:request", requestId }, "*");
    });
    setMessage(reply && reply.code === 0 ? "刷新成功，请重试当前操作。" : "刷新未完成，请从菜单重新进入。");
  } finally { state.reloading = false; }
}

function isTrustedReloadMessage(event, requestId) {
  if (event.data?.type !== "wb:reload:response" || event.data?.requestId !== requestId) return false;
  const trustedOrigins = window.FILEE_TRUSTED_RELOAD_ORIGINS || [];
  const originOk = trustedOrigins.length ? trustedOrigins.includes(event.origin) : event.source === window.parent;
  return originOk && event.data.code === 0;
}

async function loadFormats() {
  const response = await fetch("/api/formats", { credentials: "same-origin" });
  if (!response.ok) return;
  const payload = await response.json();
  state.formats = payload.formats || [];
  state.byId = new Map(state.formats.map(f => [f.id.toLowerCase(), f]));
  // 若用户已选文件，重新计算可转目标
  if (state.files.length) recomputeTargets();
}

// 按扩展名识别源格式（用 api/formats 的 extensions 字段）
function detectSource(name) {
  const ext = (name.split(".").pop() || "").toLowerCase();
  for (const f of state.formats) {
    if (f.extensions && f.extensions.some(e => e.toLowerCase() === ext)) return f;
  }
  return null;
}

// 核心：所有文件 targets 求交集（批量每个文件都要能转）
function intersectTargets(lists) {
  if (!lists.length) return [];
  const sets = lists.map(l => new Set(l));
  return [...sets[0]].filter(id => sets.every(s => s.has(id)));
}

function onFilesChanged(fileList) {
  let added = 0;
  for (const file of fileList) {
    if (state.files.length >= MAX_FILES) break;
    // 同名同大小视为已添加，避免重复
    if (state.files.some(f => f.name === file.name && f.size === file.size)) continue;
    const source = detectSource(file.name);
    state.files.push({
      file, name: file.name, size: file.size,
      sourceId: source ? source.id : "",
      targets: source ? (source.targets || []) : [],
    });
    added++;
  }
  if (state.files.length >= MAX_FILES && added < fileList.length)
    setMessage(`单批最多 ${MAX_FILES} 个文件，超出部分已忽略。`);
  // 清空 input，让"继续添加"能再次选择同一批文件
  elements.files.value = "";
  recomputeTargets();
}

function removeFile(index) {
  state.files.splice(index, 1);
  recomputeTargets();
}

function recomputeTargets() {
  renderFileList();
  state.commonTargets = intersectTargets(state.files.map(f => f.targets));
  const byCategory = new Map();
  for (const id of state.commonTargets) {
    const f = state.byId.get(id.toLowerCase());
    if (!f) continue;
    const cat = f.category || "Other";
    const bucket = byCategory.get(cat) || [];
    bucket.push(id); byCategory.set(cat, bucket);
  }
  renderCategories(byCategory);

  // 目标可能因文件变化而失效，重置选择
  state.target = "";
  elements.targetLabel.textContent = "待选择";
  elements.target.innerHTML = "";
  elements.submit.disabled = true;
  const categories = [...byCategory.keys()].sort((a, b) => categoryRank(a) - categoryRank(b));
  elements.formatHint.textContent = state.files.length
    ? (categories.length ? `已检测 ${state.files.length} 个文件，以下分类可转换。` : "所选文件没有可转换的目标格式。")
    : "先添加文件，系统会检测格式并列出可转换的目标。";
  if (categories.length) selectCategory(categories[0]);
  else { elements.categoryList.innerHTML = ""; renderDonut([]); }
}

function renderFileList() {
  elements.fileList.innerHTML = "";
  state.files.forEach((file, index) => {
    const row = document.createElement("div");
    row.className = "file-item";
    const name = document.createElement("span");
    name.className = "file-name"; name.textContent = file.name; name.title = file.name;
    const size = document.createElement("span");
    size.className = "file-size"; size.textContent = humanSize(file.size);
    const fmt = document.createElement("span");
    fmt.className = "file-fmt"; fmt.textContent = file.sourceId ? file.sourceId.toUpperCase() : "未知";
    const flag = document.createElement("span");
    if (file.targets.length) { flag.className = "file-flag ok"; flag.textContent = "可转换"; }
    else { flag.className = "file-flag no"; flag.textContent = "不支持"; }
    const remove = document.createElement("button");
    remove.type = "button"; remove.className = "file-remove"; remove.textContent = "×";
    remove.title = "移除此文件"; remove.setAttribute("aria-label", `移除 ${file.name}`);
    remove.addEventListener("click", () => removeFile(index));
    row.append(name, size, fmt, flag, remove);
    elements.fileList.appendChild(row);
  });
  elements.addMore.hidden = state.files.length === 0 || state.files.length >= MAX_FILES;
}

function renderCategories(byCategory) {
  elements.categoryList.innerHTML = "";
  const categories = [...byCategory.keys()].sort((a, b) => categoryRank(a) - categoryRank(b));
  for (const cat of categories) {
    const chip = document.createElement("button");
    chip.type = "button"; chip.className = "chip"; chip.setAttribute("role", "tab");
    chip.textContent = `${labelOf(cat)} (${byCategory.get(cat).length})`;
    chip.dataset.category = cat;
    chip.addEventListener("click", () => selectCategory(cat));
    elements.categoryList.appendChild(chip);
  }
}

function selectCategory(category) {
  state.category = category;
  for (const chip of elements.categoryList.querySelectorAll(".chip"))
    chip.classList.toggle("on", chip.dataset.category === category);
  const targets = state.commonTargets.filter(id => {
    const f = state.byId.get(id.toLowerCase());
    return f && (f.category || "Other") === category;
  }).sort((a, b) => a.localeCompare(b));
  elements.target.innerHTML = "";
  for (const id of targets) {
    const opt = document.createElement("option"); opt.value = id; opt.textContent = id.toUpperCase();
    elements.target.appendChild(opt);
  }
  renderDonut(targets);
  if (targets.length) selectTarget(targets[0]);
}

// 目标格式网格：按容器自适应的按钮网格，无甜甜圈
function renderDonut(targets) {
  const host = elements.donut;
  host.innerHTML = "";
  if (!targets.length) {
    const empty = document.createElement("div");
    empty.className = "target-empty";
    empty.textContent = "该分类暂无可转换的目标格式";
    host.appendChild(empty);
    return;
  }
  const grid = document.createElement("div");
  grid.className = "target-grid";
  grid.setAttribute("role", "group");
  grid.setAttribute("aria-label", "目标格式列表");
  for (const id of targets) {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "target-chip";
    btn.dataset.target = id;
    btn.textContent = id.toUpperCase();
    btn.setAttribute("aria-pressed", String(id === state.target));
    btn.addEventListener("click", () => selectTarget(id));
    grid.appendChild(btn);
  }
  host.appendChild(grid);
  syncSeg();
}

function syncSeg() {
  for (const btn of elements.donut.querySelectorAll(".target-chip"))
    btn.setAttribute("aria-pressed", String(btn.dataset.target === state.target));
}

function selectTarget(id) {
  state.target = id;
  elements.target.value = id;
  elements.targetLabel.textContent = id.toUpperCase();
  elements.submit.disabled = !(state.files.length && state.target);
  syncSeg();
}

async function submitJob() {
  if (!state.target || !state.files.length) { setMessage("请先添加文件并选择目标格式。"); return; }
  const form = new FormData();
  form.append("target", state.target);
  for (const item of state.files) form.append("files", item.file);
  const response = await fetch("/api/jobs", {
    method: "POST", credentials: "same-origin",
    headers: { "X-CSRF-Token": state.csrfToken }, body: form,
  });
  if (!response.ok) { setMessage("提交失败，请刷新页面重试。"); if (response.status === 401) await requestRelogin(); return; }
  const payload = await response.json();
  // 新任务开始：关闭旧任务的进度流并清空列表，避免新文件附加到旧任务后面
  for (const source of state.sources.values()) source.close();
  state.sources.clear();
  state.jobs.clear();
  setMessage("任务已创建。");
  trackJob(payload.jobId);
}

function trackJob(jobId) {
  state.jobs.set(jobId, { status: "queued", progress: 0 });
  renderJobs();
  const source = new EventSource(`/api/jobs/${jobId}/events`);
  state.sources.set(jobId, source);
  source.addEventListener("progress", (e) => applyProgress(jobId, e));
  source.addEventListener("done", async (e) => {
    applyProgress(jobId, e); source.close(); state.sources.delete(jobId);
    await refreshJob(jobId);
  });
}
function applyProgress(jobId, event) {
  const p = JSON.parse(event.data);
  const cur = state.jobs.get(jobId) || {};
  state.jobs.set(jobId, { ...cur, status: p.status, progress: p.progress });
  renderJobs();
}
async function refreshJob(jobId) {
  const response = await fetch(`/api/jobs/${jobId}`, { credentials: "same-origin" });
  if (!response.ok) return;
  const job = await response.json();
  state.jobs.set(jobId, { ...(state.jobs.get(jobId) || {}), ...job });
  renderJobs();
}
const STATUS_LABELS = {
  queued: "排队中", running: "进行中", completed: "已完成",
  completedwitherrors: "已完成（部分失败）", failed: "失败",
  cancelled: "已取消", interrupted: "已中断",
};
function statusLabel(status) {
  return STATUS_LABELS[(status || "queued").toLowerCase()] || status;
}

function renderJobs() {
  elements.jobs.innerHTML = "";
  for (const job of state.jobs.values()) {
    const row = document.createElement("div"); row.className = "job";
    const title = document.createElement("div"); title.className = "job-title";
    const raw = (job.status || "queued").toLowerCase();
    const failed = raw === "failed" || raw === "completedwitherrors";
    title.textContent = statusLabel(raw);
    if (failed) title.classList.add("fail");
    const bar = document.createElement("div"); bar.className = "bar";
    const fill = document.createElement("span"); fill.style.width = `${Math.round((job.progress || 0) * 100)}%`;
    bar.appendChild(fill); row.append(title, bar);

    const links = document.createElement("div"); links.className = "job-links";
    for (const file of job.files || []) {
      if (!file.downloadUrl) continue;
      const link = document.createElement("a"); link.href = file.downloadUrl; link.textContent = `下载 ${file.name}`;
      links.appendChild(link);
    }
    // 全部下载：服务端把该任务所有输出打包成 zip，一次点击一次下载（避开多文件下载被拦截）
    if ((job.files || []).some(f => f.downloadUrl) && job.archiveUrl)
      links.appendChild(Object.assign(document.createElement("a"), {
        href: job.archiveUrl, className: "archive-link", textContent: "全部下载 (zip)",
      }));
    row.appendChild(links);
    elements.jobs.appendChild(row);
  }
}

// 事件（拖放无效：label 包裹的 file input 会把 drop 转成点击，故只保留点击选择）
elements.files.addEventListener("change", () => onFilesChanged(elements.files.files));
elements.addMore.addEventListener("click", () => elements.files.click());
elements.target.addEventListener("change", () => selectTarget(elements.target.value));
elements.submit.addEventListener("click", submitJob);
elements.retry.addEventListener("click", requestRelogin);

(async () => {
  try { if (await readSession()) await loadFormats(); }
  catch { setMessage("初始化失败，请刷新页面重试。"); }
})();
