const state = {
  csrfToken: "",
  target: "",
  jobs: new Map(),
  reloading: false,
};

const elements = {
  sessionNote: document.querySelector("#session-note"),
  target: document.querySelector("#target"),
  files: document.querySelector("#files"),
  submit: document.querySelector("#submit"),
  retry: document.querySelector("#retry"),
  message: document.querySelector("#message"),
  jobs: document.querySelector("#job-list"),
};

function setMessage(text) {
  elements.message.textContent = text;
}

async function readSession() {
  const response = await fetch("/api/session", { credentials: "same-origin" });
  if (response.status === 401) {
    await requestRelogin();
    return false;
  }
  if (!response.ok) throw new Error("会话读取失败");
  const session = await response.json();
  state.csrfToken = session.csrfToken;
  elements.sessionNote.textContent = `已连接：${session.sub}`;
  elements.submit.disabled = false;
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

async function loadFormats() {
  const response = await fetch("/api/formats", { credentials: "same-origin" });
  if (!response.ok) return;
  const payload = await response.json();
  elements.target.innerHTML = "";
  for (const format of payload.formats) {
    if (!format.writable) continue;
    const option = document.createElement("option");
    option.value = format.id;
    option.textContent = format.name;
    elements.target.appendChild(option);
  }
  state.target = elements.target.value;
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
    method: "POST",
    credentials: "same-origin",
    headers: { "X-CSRF-Token": state.csrfToken },
    body: form,
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

elements.target.addEventListener("change", () => { state.target = elements.target.value; });
elements.submit.addEventListener("click", submitJob);
elements.retry.addEventListener("click", requestRelogin);

(async () => {
  try {
    if (await readSession()) await loadFormats();
  } catch {
    setMessage("初始化失败，请刷新页面重试。");
  }
})();
