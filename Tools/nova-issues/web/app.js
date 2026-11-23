"use strict";

const byId = (id) => document.getElementById(id);
const statusNames = {
  open: "待分派", triaged: "已分派", working: "处理中", awaiting_verification: "待项目复验",
  closed: "已关闭", duplicate: "已合并",
};
const outcomeNames = {
  executed: "已执行", success: "成功", partial: "部分完成", blocked: "有阻塞",
  failed: "失败", cancelled: "已取消",
};
const usageSources = { codex: "Codex", editor: "Editor", legacy: "未标注" };
const usageEntries = { action: "Nova Action", skill: "Skill 查阅", "unity-cli": "Unity CLI", "nova-cli": "Nova CLI" };
const coverageNames = {
  observed: "已观察", waiting: "待采集", registered: "已注册", not_observed: "未验证",
  connected: "已连接", installed: "已安装", unavailable: "未获得证据", ready: "可用",
  static: "静态证据", interrupted: "已中断", pending: "待分析",
};
const titles = { overview: "总览", issues: "问题", projects: "项目与版本", usage: "使用记录" };
const date = new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" });
let session = null;
let data = null;
let view = "overview";
let loadCounter = 0;
let days = 14;
let paintIssues = null;
let paintUsage = null;
const filters = { search: "", issueProject: "", status: "", priority: "", project: "", module: "", usageSource: "codex" };

function node(tag, text, className) {
  const value = document.createElement(tag);
  if (text !== undefined && text !== null) value.textContent = String(text);
  if (className) value.className = className;
  return value;
}

function button(text, action, className = "secondary") {
  const element = node("button", text, className);
  element.type = "button";
  element.addEventListener("click", action);
  return element;
}

function showError(element, message) {
  element.textContent = message || "";
  element.hidden = !message;
}

function stamp(value) {
  return value ? date.format(new Date(value * 1000)) : "暂无";
}

function projectName(id) {
  return data.projects.find((project) => project.id === id)?.name || id;
}

async function request(path, body) {
  const post = body !== undefined;
  const response = await fetch(path, {
    method: post ? "POST" : "GET",
    cache: "no-store",
    headers: post ? { "Content-Type": "application/json", "X-Nova-CSRF": session?.csrf || "" } : {},
    body: post ? JSON.stringify(body) : undefined,
  });
  const result = await response.json();
  if (response.status === 401 && session) setSession(null);
  if (!response.ok) throw new Error(result.error || `请求失败（${response.status}）`);
  return result;
}

function setSession(value) {
  session = value;
  data = null;
  loadCounter++;
  byId("login").hidden = Boolean(value);
  byId("workspace").hidden = !value;
  byId("account").textContent = value?.username || "";
  byId("content").replaceChildren();
  if (!value && byId("details").open) byId("details").close();
}

function pill(text, tone = "") {
  return node("span", text, `pill ${tone}`.trim());
}

function table(headers, rows) {
  if (!rows.length) return node("p", "暂无符合条件的记录", "empty");
  const wrapper = node("div", null, "scroll");
  const element = node("table");
  const head = element.createTHead().insertRow();
  headers.forEach((title) => head.append(node("th", title)));
  const body = element.createTBody();
  for (const row of rows) {
    const tr = body.insertRow();
    for (const cell of row) {
      const td = tr.insertCell();
      if (cell instanceof Node) td.append(cell);
      else td.textContent = cell ?? "—";
    }
  }
  wrapper.append(element);
  return wrapper;
}

function panel(title, ...content) {
  const box = node("section", null, "panel");
  if (title) box.append(node("h2", title));
  box.append(...content);
  return box;
}

function control(label, field, options, run) {
  const box = node("label", label);
  const select = node("select");
  for (const [value, name] of options) {
    const option = node("option", name);
    option.value = value;
    select.append(option);
  }
  select.value = String(field);
  select.addEventListener("change", () => run(select.value));
  box.append(select);
  return box;
}

function filter(name, label, options, repaint) {
  return control(label, filters[name], options, (value) => {
    filters[name] = value;
    repaint();
  });
}

function issueRows(issues) {
  return table(["问题", "优先级", "状态", "影响项目", "发生次数", "最近发生"], issues.map((item) => [
    button(`#${item.id}  ${item.title}`, () => openIssue(item.id), "link"),
    pill(item.priority, item.priority === "P0" ? "urgent" : item.priority === "P1" ? "warning" : ""),
    statusNames[item.status], item.projects, item.occurrences, stamp(item.last_seen),
  ]));
}

function activeProject(project) {
  return project.last_seen && data.server_time - project.last_seen <= 86400;
}

function projectRows(projects) {
  return table(["项目", "团队", "Framework", "Unity", "最近环境上报", "最近上报"], projects.map((project) => {
    const workspaces = data.workspaces.filter((item) => item.project_id === project.id);
    const versions = [...new Set(workspaces.map((item) => item.framework))].join("、");
    const unity = [...new Set(workspaces.map((item) => item.unity).filter(Boolean))].join("、");
    return [
      button(project.name, () => openProject(project.id), "link"), project.team || "—", versions || "暂无", unity || "暂无",
      stamp(Math.max(0, ...workspaces.map((item) => item.received_at))), stamp(project.last_seen),
    ];
  }));
}

function overview() {
  const activeIssues = data.issues.filter((item) => !["closed", "duplicate"].includes(item.status));
  const grid = node("div", null, "metrics");
  for (const [label, value] of [
    ["已登记项目", data.projects.length],
    ["过去 24 小时有上报", data.projects.filter(activeProject).length],
    ["待处理问题", data.issue_counts.active],
    ["P0 阻塞", data.issue_counts.p0],
  ]) {
    const entry = node("div", null, "metric");
    entry.append(node("strong", value), node("p", label));
    grid.append(entry);
  }
  return [grid, panel("待处理问题", issueRows(activeIssues.slice(0, 8))),
    panel("项目上报", projectRows(data.projects.slice(0, 8)))];
}

function issuesPage() {
  const toolbar = node("div", null, "toolbar issues-toolbar");
  const results = node("div");
  const nameCounts = new Map();
  for (const { name } of data.projects) nameCounts.set(name, (nameCounts.get(name) || 0) + 1);
  const projects = data.projects.map(({ id, name }) => [id, nameCounts.get(name) > 1 ? `${name}（${id}）` : name]);
  if (filters.issueProject && !projects.some(([id]) => id === filters.issueProject)) filters.issueProject = "";
  const search = node("label", "搜索问题", "search");
  const input = node("input");
  input.type = "search";
  input.placeholder = "编号、模块或标题";
  input.value = filters.search;
  let timer;
  let latest = 0;
  function paint(delay = 0, showLoading = true) {
    clearTimeout(timer);
    const run = ++latest;
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries({
      q: filters.search.trim(), project: filters.issueProject, status: filters.status, priority: filters.priority,
    })) {
      if (value) params.set(key, value);
    }
    const query = params.toString();
    if (!query) {
      results.replaceChildren(issueRows(data.issues));
      return;
    }
    if (showLoading) results.replaceChildren(node("p", "正在读取问题…", "empty"));
    timer = setTimeout(async () => {
      if (!results.isConnected) return;
      try {
        const { issues } = await request(`/api/maintainer/issues?${query}`);
        if (run === latest && results.isConnected) results.replaceChildren(issueRows(issues));
      } catch (error) {
        if (run === latest && results.isConnected) results.replaceChildren(node("p", error.message, "error"));
      }
    }, delay);
  }
  input.addEventListener("input", () => {
    filters.search = input.value;
    paint(180);
  });
  search.append(input);
  toolbar.append(search, filter("issueProject", "项目", [["", "全部项目"], ...projects], paint),
    filter("status", "状态", [["", "全部"], ...Object.entries(statusNames)], paint),
    filter("priority", "优先级", [["", "全部"], ...["P0", "P1", "P2", "P3"].map((item) => [item, item])], paint));
  paintIssues = paint;
  paint();
  return panel(null, toolbar, results);
}

function usagePage() {
  const toolbar = node("div", null, "toolbar");
  const results = node("div");
  const modules = [...new Set([...data.usage, ...(data.codex_modules || [])].map((row) => row.module))].sort();
  const selected = (item) => (!filters.project || item.project_id === filters.project) &&
    (!filters.module || item.module === filters.module);
  function paint() {
    const details = table(["项目", "模块", "操作", "来源", "入口", "Framework", "结果", "次数"], data.usage
      .filter((item) => selected(item) && (!filters.usageSource || item.source === filters.usageSource))
      .map((item) => [projectName(item.project_id), item.module, node("code", item.operation),
        usageSources[item.source] || "未标注", usageEntries[item.entry] || "—", item.framework,
        outcomeNames[item.outcome] || item.outcome, item.count]));
    results.replaceChildren();
    if (filters.usageSource === "codex") {
      results.append(node("h2", "项目模块调用"),
        table(["项目", "模块", "调用次数", "Skill 查阅次数（按轮）", "失败或阻塞", "最近使用"], (data.codex_modules || [])
          .filter(selected).map((item) => [projectName(item.project_id), item.module, item.calls,
            item.skill_turns, item.problems, stamp(item.last_used)])), node("h2", "操作明细"));
    }
    results.append(details);
  }
  toolbar.append(filter("usageSource", "来源", [["", "全部"], ...Object.entries(usageSources)], paint),
    filter("project", "项目", [["", "全部"], ...data.projects.map((item) => [item.id, item.name])], paint),
    filter("module", "模块", [["", "全部"], ...modules.map((name) => [name, name])], paint),
    control("最近", days, [[1, "1 天"], [7, "7 天"], [14, "14 天"], [30, "30 天"], [90, "90 天"]], (value) => {
      days = Number(value);
      refresh(true);
    }));
  paintUsage = paint;
  paint();
  return panel(null, toolbar, results);
}

function render() {
  if (!session || !data) return;
  byId("updated").textContent = `最近刷新 ${stamp(data.server_time)}`;
  byId("page-title").textContent = titles[view];
  for (const item of byId("nav").querySelectorAll("button")) {
    if (item.dataset.view === view) item.setAttribute("aria-current", "page");
    else item.removeAttribute("aria-current");
  }
  const actions = byId("page-actions");
  actions.replaceChildren(button("刷新", () => refresh(true)));
  if (view === "projects") actions.append(button("添加项目", registerProject, "primary"));
  const pages = { overview, issues: issuesPage, projects: () => panel(null, projectRows(data.projects)), usage: usagePage };
  const content = pages[view]();
  byId("content").replaceChildren(...(Array.isArray(content) ? content : [content]));
}

async function refresh(force = false) {
  if (!session) return;
  const run = ++loadCounter;
  try {
    const latest = await request(`/api/maintainer/overview?days=${days}`);
    if (run !== loadCounter) return;
    data = latest;
    showError(byId("page-error"), "");
    const formControl = byId("content").contains(document.activeElement) &&
      ["INPUT", "SELECT", "TEXTAREA"].includes(document.activeElement.tagName);
    if (force || !formControl) render();
    else if (view === "issues") paintIssues?.(0, false);
    else if (view === "usage") paintUsage?.();
  } catch (error) {
    if (session && run === loadCounter) showError(byId("page-error"), error.message);
  }
}

function openDialog(title, children) {
  byId("detail-title").textContent = title;
  byId("detail-body").replaceChildren(...children);
  if (!byId("details").open) byId("details").showModal();
}

function fields(entries) {
  const list = node("dl");
  for (const [name, value] of entries) list.append(node("dt", name), node("dd", value || "—"));
  return list;
}

function textField(label, name, value = "", limit = 100) {
  const box = node("label", label);
  const input = node("input");
  Object.assign(input, { name, value, maxLength: limit });
  box.append(input);
  return [box, input];
}

function tokenDialog(id, token) {
  const [field, input] = textField("项目上报凭据", "token", token);
  input.readOnly = true;
  input.className = "token";
  input.addEventListener("focus", () => input.select());
  openDialog(`接入 ${id}`, [node("p", "请将此凭据保存在维护端的项目部署配置中。凭据只在生成时显示。"), field]);
  input.focus();
}

function registerProject() {
  const group = node("form", null, "form-grid");
  const [idField, id] = textField("项目标识", "id", "", 96);
  id.pattern = "[A-Za-z0-9][A-Za-z0-9_.\\-]{0,95}";
  id.required = true;
  const [nameField, name] = textField("项目名称", "name");
  name.required = true;
  const [teamField] = textField("项目组", "team");
  const error = node("p", "", "error");
  error.setAttribute("role", "alert");
  error.hidden = true;
  const save = node("button", "创建项目和凭据", "primary");
  group.append(idField, nameField, teamField, error, save);
  group.addEventListener("submit", async (event) => {
    event.preventDefault();
    save.disabled = true;
    try {
      const result = await request("/api/maintainer/projects", Object.fromEntries(new FormData(group)));
      tokenDialog(result.id, result.token);
      refresh(true);
    } catch (failure) {
      showError(error, failure.message);
    } finally {
      save.disabled = false;
    }
  });
  openDialog("添加项目", [group]);
}

function snapshotPanel(snapshot) {
  const snap = snapshot.data;
  const section = node("details");
  section.append(node("summary", `${snap.framework} · ${snap.unity} · ${stamp(snapshot.received_at)}`),
    fields([["工作区", snapshot.workspace], ["平台 / 渠道 / 模式", [snap.platform, snap.channel, snap.mode].filter(Boolean).join(" / ")],
      ["配置证据", snap.coordinate_source], ["已启用", snap.enabled?.join("、")],
      ...Object.entries(snap.coverage).map(([name, state]) => [`${name} 采集`, coverageNames[state] || state]),
      ...Object.entries(snap.tools).map(([name, item]) => [name, `${item.version || ""} ${coverageNames[item.state] || item.state}`.trim()])]),
    table(["已发现包", "版本", "来源", "证据"], snap.packages.map((item) =>
      [node("code", item.id), item.version, item.source, item.evidence])));
  return section;
}

async function openProject(id) {
  openDialog("读取项目", []);
  try {
    const result = await request(`/api/maintainer/projects/${encodeURIComponent(id)}`);
    if (!session || !byId("details").open) return;
    const rotate = button("轮换上报凭据", () => {
      const actions = node("div", null, "actions");
      const error = node("p", "", "error");
      error.hidden = true;
      const save = button("确认轮换", async () => {
        save.disabled = true;
        try {
          const token = await request(`/api/maintainer/projects/${encodeURIComponent(id)}/rotate`, {});
          tokenDialog(id, token.token);
        } catch (failure) {
          showError(error, failure.message);
        } finally {
          save.disabled = false;
        }
      }, "primary");
      actions.append(save, button("返回", () => openProject(id)));
      openDialog("轮换上报凭据", [node("p", "轮换后旧凭据立即失效。请在维护端更新该项目的接入配置。"), error, actions]);
    });
    const snapshots = result.snapshots.map(snapshotPanel);
    if (snapshots.length) snapshots[0].open = true;
    openDialog(result.project.name, [
      fields([["标识", id], ["项目组", result.project.team], ["最近上报", stamp(result.project.last_seen)]]),
      panel("环境快照", ...(snapshots.length ? snapshots : [node("p", "等待该项目的首次上报。", "empty")])),
      rotate,
    ]);
  } catch (failure) {
    if (session && byId("details").open) openDialog("读取项目失败", [node("p", failure.message, "error")]);
  }
}

function issueForm(issue) {
  const form = node("form", null, "form-grid");
  const row = node("div", null, "fields");
  const status = control("状态", issue.status, Object.entries(statusNames), (value) => {
    duplicateBox.hidden = value !== "duplicate";
    duplicate.required = value === "duplicate";
    fixed.required = value === "awaiting_verification";
    note.required = value === "closed";
  });
  status.querySelector("select").name = "status";
  const priority = control("优先级", issue.priority, ["P0", "P1", "P2", "P3"].map((value) => [value, value]), () => {});
  priority.querySelector("select").name = "priority";
  const [assigneeBox] = textField("负责人", "assignee", issue.assignee, 80);
  const [versionBox, fixed] = textField("修复版本", "fixed_version", issue.fixed_version, 80);
  fixed.required = issue.status === "awaiting_verification";
  const [duplicateBox, duplicate] = textField("合并到 Issue 编号", "duplicate_of", "", 15);
  duplicateBox.hidden = true;
  duplicate.inputMode = "numeric";
  duplicate.pattern = "[0-9]+";
  const noteBox = node("label", "处理记录");
  const note = node("textarea");
  Object.assign(note, { name: "note", maxLength: 1000, required: issue.status === "closed" });
  noteBox.append(note);
  row.append(status, priority, assigneeBox, versionBox);
  const save = node("button", "保存处理结果", "primary");
  const error = node("p", "", "error");
  error.hidden = true;
  error.setAttribute("role", "alert");
  form.append(row, duplicateBox, noteBox, error, save);
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const payload = Object.fromEntries(new FormData(form));
    payload.revision = issue.revision;
    if (payload.status === "duplicate") payload.duplicate_of = Number(payload.duplicate_of);
    else delete payload.duplicate_of;
    save.disabled = true;
    try {
      await request(`/api/maintainer/issues/${issue.id}`, payload);
      refresh(true);
      openIssue(issue.id);
    } catch (failure) {
      showError(error, failure.message);
    } finally {
      save.disabled = false;
    }
  });
  return form;
}

function occurrence(item) {
  const box = node("section", null, "occurrence");
  const report = item.data;
  box.append(node("h3", `${item.name} · Framework ${report.framework}`),
    node("p", stamp(item.observed_at), "muted"), node("p", report.summary),
    fields([["预期", report.expected], ["实际", report.actual], ["触发步骤", report.steps],
      ["绕行", report.workaround], ["来源", report.source]]));
  const list = node("ul");
  for (const entry of report.evidence) list.append(node("li", `${entry.source} · ${entry.detail}`));
  box.append(list);
  return box;
}

async function openIssue(id) {
  openDialog(`读取 Issue #${id}`, []);
  try {
    const result = await request(`/api/maintainer/issues/${id}`);
    if (!session || !byId("details").open) return;
    const issue = result.issue;
    const management = issue.duplicate_of ? button(`打开合并目标 #${issue.duplicate_of}`,
      () => openIssue(issue.duplicate_of)) : issueForm(issue);
    openDialog(`#${id}  ${issue.title}`, [
      fields([["状态", statusNames[issue.status]], ["模块", issue.area], ["首次发现", stamp(issue.first_seen)],
        ["最近发生", stamp(issue.last_seen)]]),
      panel("处理", management),
      panel("发生记录", ...(result.occurrences.length ? result.occurrences.map(occurrence)
        : [node("p", "暂无发生记录", "empty")])),
      panel("处理历史", table(["时间", "维护者", "状态", "记录"], result.history.map((entry) =>
        [stamp(entry.created_at), entry.actor, statusNames[entry.status], entry.note]))),
    ]);
  } catch (failure) {
    if (session && byId("details").open) openDialog("读取问题失败", [node("p", failure.message, "error")]);
  }
}

byId("nav").addEventListener("click", (event) => {
  const next = event.target.closest("button")?.dataset.view;
  if (next && titles[next]) {
    view = next;
    render();
  }
});

byId("login-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  const submit = form.querySelector("button");
  submit.disabled = true;
  showError(byId("login-error"), "");
  try {
    const user = await request("/api/session", Object.fromEntries(new FormData(form)));
    form.reset();
    setSession(user);
    await refresh(true);
  } catch (error) {
    showError(byId("login-error"), error.message);
  } finally {
    form.elements.password.value = "";
    submit.disabled = false;
  }
});

byId("logout").addEventListener("click", async () => {
  try {
    await request("/api/session/logout", {});
    setSession(null);
  } catch (error) {
    if (session) showError(byId("page-error"), error.message);
  }
});

request("/api/session").then((user) => {
  setSession(user);
  return refresh(true);
}).catch(() => setSession(null));
setInterval(() => refresh(), 15000);
