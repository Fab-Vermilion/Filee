const state = {
  csrfToken: "",
  target: "",
  jobs: new Map(),
  reloading: false,
  groups: new Map(),   // category -> [format]
  category: "",
};

const elements = {
  sessionNote: document.querySelector("#session-note"),
  target: document.querySelector("#target"),
  targetLabel: document.querySelector("#target-label"),
  categoryList: document.querySelector("#category-list"),
  donut: document.querySelector("#donut"),
  files: document.querySelector("#files"),
  dropzone: document.querySelector("#dropzone"),
  submit: document.querySelector("#submit"),
  retry: document.querySelector("#retry"),
  message: document.querySelector("#message"),
  jobs: document.querySelector("#job-list"),
};

const CATEGORY_LABELS = {
  Image: "图片", Pdf: "PDF", Document: "文档", Spreadsheet: "表格",
  Presentation: "演示", Hwp: "HWP", Text: "文本", Subtitle: "字幕",
  Video: "视频", Audio: "音频", Archive: "压缩包", Data: "数据",
};
// 常用在前，避免按字母序把 Data 排到第一位
const CATEGORY_ORDER = ["Image", "Pdf", "Document", "Spreadsheet", "Presentation",
  "Hwp", "Text", "Subtitle", "Video", "Audio", "Archive", "Data"];
function categoryRank(category) {
  const i = CATEGORY_ORDER.indexOf(category);
  return i < 0 ? CATEGORY_ORDER.length : i;
}

function setMessage(text) { elements.message.textContent = text; }
function labelOf(category) { return CATEGORY_LABELS[category] || category; }

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
      const timer = setTimeout(() => {
        window.removeEventListener("message", handler);
        resolve(null);
      }, 8000);
      const handler = (event) => {
        if (!isTrustedReloadMessage(event, requestId)) return;
        clearTimeout(timer);
        window.removeEventListener("message", handler);
        resolve(event.data);
      };
      window.addEventListener("message", handler);
      window.parent.postMessage({ type: "wb:reload:request", requestId }, "*");
    });
    setMessage(reply && reply.code === 0 ? "刷新成功，请重试当前操作。" : "刷新未完成，请从菜单重新进入。");
  } finally {
    state.reloading = false;
  }
}

function isTrustedReloadMessage(event, requestId) {
  if (event.data?.type !== "wb:reload:response" || event.data?.requestId !== requestId) return false;
  const trustedOrigins = window.FILEE_TRUSTED_RELOAD_ORIGINS || [];
  const originOk = trustedOrigins.length ? trustedOrigins.includes(event.origin) : event.source === window.parent;
  return originOk && event.data.code === 0;
}

// 按 category 分组：只保留有可写目标格式的分类
async function loadFormats() {
  const response = await fetch("/api/formats", { credentials: "same-origin" });
  if (!response.ok) return;
  const payload = await response.json();
  const grouped = new Map();
  for (const format of payload.formats) {
    if (!format.writable || !format.category) continue;
    const bucket = grouped.get(format.category) || [];
    bucket.push(format);
    grouped.set(format.category, bucket);
  }
  state.groups = grouped;

  elements.categoryList.innerHTML = "";
  const categories = [...grouped.keys()].sort((a, b) => categoryRank(a) - categoryRank(b));
  for (const category of categories) {
    const chip = document.createElement("button");
    chip.type = "button";
    chip.className = "chip";
    chip.textContent = labelOf(category);
    chip.setAttribute("role", "tab");
    chip.dataset.category = category;
    chip.addEventListener("click", () => selectCategory(category));
    elements.categoryList.appendChild(chip);
  }
  if (categories.length) selectCategory(categories[0]);
}

function selectCategory(category) {
  state.category = category;
  for (const chip of elements.categoryList.querySelectorAll(".chip"))
    chip.classList.toggle("on", chip.dataset.category === category);
  const candidates = (state.groups.get(category) || []).map(f => f.id);
  elements.target.innerHTML = "";
  for (const id of candidates) {
    const option = document.createElement("option");
    option.value = id;
    option.textContent = id.toUpperCase();
    elements.target.appendChild(option);
  }
  renderDonut(candidates);
  if (candidates.length) selectTarget(candidates[0]);
  else { state.target = ""; elements.targetLabel.textContent = "该分类暂无可写目标"; elements.submit.disabled = true; }
}

// 甜甜圈：极坐标扇区（移植官方设计稿的 seg 算法），点击扇区选目标格式
function renderDonut(targets) {
  const NS = "http://www.w3.org/2000/svg";
  const C = 120, R2 = 108, R1 = 46;
  const svg = document.createElementNS(NS, "svg");
  svg.setAttribute("viewBox", "0 0 240 240");
  svg.setAttribute("aria-hidden", "true");
  const el = (tag, attrs) => {
    const node = document.createElementNS(NS, tag);
    for (const k in attrs) node.setAttribute(k, attrs[k]);
    return node;
  };
  const pt = (r, a) => [C + r * Math.cos(a), C + r * Math.sin(a)];
  const seg = (i, n) => {
    if (n === 1) return `M${C - R2} ${C}A${R2} ${R2} 0 1 0 ${C + R2} ${C}A${R2} ${R2} 0 1 0 ${C - R2} ${C}M${C - R1} ${C}A${R1} ${R1} 0 1 1 ${C + R1} ${C}A${R1} ${R1} 0 1 1 ${C - R1} ${C}Z`;
    const sp = 2 * Math.PI / n, s = -Math.PI / 2 + i * sp - sp / 2, e = s + sp;
    const [a, b] = pt(R2, s), [c, d] = pt(R2, e), [f, g] = pt(R1, e), [h, k] = pt(R1, s);
    return `M${a} ${b}A${R2} ${R2} 0 0 1 ${c} ${d}L${f} ${g}A${R1} ${R1} 0 0 0 ${h} ${k}Z`;
  };

  if (!targets.length) {
    svg.appendChild(el("circle", { cx: C, cy: C, r: (R1 + R2) / 2, fill: "none", stroke: "var(--surface-1)", "stroke-width": R2 - R1 }));
  } else {
    targets.forEach((id, i) => {
      const group = el("g", { class: "seg", "data-target": id });
      group.appendChild(el("path", { d: seg(i, targets.length) }));
      const angle = -Math.PI / 2 + i * 2 * Math.PI / targets.length;
      // 单目标时文字放在圆环带上方，避免与中心标题/计数重叠
      const [x, y] = targets.length === 1 ? [C, C - (R1 + R2) / 2] : pt((R1 + R2) / 2, angle);
      const text = el("text", { x, y, "text-anchor": "middle", "dominant-baseline": "central" });
      text.textContent = id.toUpperCase();
      group.appendChild(text);
      group.addEventListener("click", () => selectTarget(id));
      svg.appendChild(group);
    });
  }
  const title = el("text", { x: C, y: C - 4, "text-anchor": "middle", class: "center-title" });
  title.textContent = labelOf(state.category);
  const sub = el("text", { x: C, y: C + 14, "text-anchor": "middle", class: "center-sub" });
  sub.textContent = targets.length ? `${targets.length} 种目标` : "暂无";
  svg.append(title, sub);

  elements.donut.innerHTML = "";
  elements.donut.appendChild(svg);
  syncSegSelection();
}

function syncSegSelection() {
  for (const seg of elements.donut.querySelectorAll(".seg"))
    seg.classList.toggle("on", seg.dataset.target === state.target);
}

function selectTarget(id) {
  state.target = id;
  elements.target.value = id;
  elements.targetLabel.textContent = id.toUpperCase();
  elements.submit.disabled = !(elements.files.files.length > 0);
  syncSegSelection();
}

async function submitJob() {
  if (!state.target || elements.files.files.length === 0) {
    setMessage("请选择目标格式和至少一个文件。");
    return;
  }
  const form = new FormData();
  form.append("target", state.target);
  for (const file of elements.files.files) form.append("files", file);
  const response = await fetch("/api/jobs", {
    method: "POST", credentials: "same-origin",
    headers: { "X-CSRF-Token": state.csrfToken }, body: form,
  });
  if (!response.ok) {
    setMessage("提交失败，请刷新页面重试。");
    if (response.status === 401) await requestRelogin();
    return;
  }
  const payload = await response.json();
  setMessage("任务已创建。");
  trackJob(payload.jobId);
}

function trackJob(jobId) {
  state.jobs.set(jobId, { status: "queued", progress: 0 });
  renderJobs();
  const source = new EventSource(`/api/jobs/${jobId}/events`);
  source.addEventListener("progress", (event) => applyProgress(jobId, event));
  source.addEventListener("done", async (event) => {
    applyProgress(jobId, event);
    source.close();
    await refreshJob(jobId);
  });
}

function applyProgress(jobId, event) {
  const payload = JSON.parse(event.data);
  const current = state.jobs.get(jobId) || {};
  state.jobs.set(jobId, { ...current, status: payload.status, progress: payload.progress });
  renderJobs();
}

async function refreshJob(jobId) {
  const response = await fetch(`/api/jobs/${jobId}`, { credentials: "same-origin" });
  if (!response.ok) return;
  const job = await response.json();
  const current = state.jobs.get(jobId) || {};
  state.jobs.set(jobId, { ...current, ...job });
  renderJobs();
}

function renderJobs() {
  elements.jobs.innerHTML = "";
  for (const [jobId, job] of state.jobs) {
    const row = document.createElement("div");
    row.className = "job";
    const title = document.createElement("div");
    title.className = "job-title";
    title.textContent = `${jobId.slice(0, 8)} · ${job.status || "queued"}`;
    const bar = document.createElement("div");
    bar.className = "bar";
    const fill = document.createElement("span");
    fill.style.width = `${Math.round((job.progress || 0) * 100)}%`;
    bar.appendChild(fill);
    row.append(title, bar);
    for (const file of job.files || []) {
      if (!file.downloadUrl) continue;
      const link = document.createElement("a");
      link.href = file.downloadUrl;
      link.textContent = `下载 ${file.name}`;
      row.appendChild(link);
    }
    elements.jobs.appendChild(row);
  }
}

// 拖放
for (const type of ["dragenter", "dragover"]) elements.dropzone.addEventListener(type, (e) => {
  e.preventDefault(); elements.dropzone.classList.add("dragover");
});
for (const type of ["dragleave", "drop"]) elements.dropzone.addEventListener(type, (e) => {
  e.preventDefault(); elements.dropzone.classList.remove("dragover");
});
elements.dropzone.addEventListener("drop", (e) => {
  elements.files.files = e.dataTransfer.files;
  elements.submit.disabled = !state.target || elements.files.files.length === 0;
});
elements.files.addEventListener("change", () => {
  elements.submit.disabled = !state.target || elements.files.files.length === 0;
});
elements.target.addEventListener("change", () => selectTarget(elements.target.value));
elements.submit.addEventListener("click", submitJob);
elements.retry.addEventListener("click", requestRelogin);

(async () => {
  try {
    if (await readSession()) await loadFormats();
  } catch {
    setMessage("初始化失败，请刷新页面重试。");
  }
})();
