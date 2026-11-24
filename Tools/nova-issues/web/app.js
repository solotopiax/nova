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
const usageEntries = { action: "Nova Action", skill: "Skill 查阅", "unity-cli": "Unity CLI", "nova-cli": "Nova CLI", preload: "Editor 预加载" };
const coverageNames = {
  observed: "已观察", waiting: "待采集", registered: "已注册", not_observed: "未验证",
  connected: "已连接", installed: "已安装", unavailable: "未获得证据", ready: "可用",
  static: "静态证据", interrupted: "已中断", pending: "待分析",
};
const hookStatuses = {
  complete: ["已加全", "success", "项目钩子和绑定脚本已就绪。授权状态可在 Codex 中查看。"],
  pending: ["待追加", "warning", "工作区中还没有 Nova 自动反馈钩子。"],
  incomplete: ["不完整", "urgent", "工作区的反馈钩子或绑定脚本缺失，或校验未通过。"],
};
const titles = { overview: "总览", issues: "问题", projects: "项目与版本", usage: "使用记录" };
const readStoragePrefix = "nova-issues.read.v1:";
const date = new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" });
let session = null;
let data = null;
let readTotals = {};
let readOnLoad = null;
let view = "overview";
let loadCounter = 0;
let days = 14;
let paintIssues = null;
let paintUsage = null;
let projectLive = null;
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

/** 按账号保存各栏已读总量，首次打开维护台时以当前数据为起点。 */
function updateUnread(readView) {
  for (const badge of byId("nav").querySelectorAll(".nav-unread")) {
    const item = badge.parentElement;
    const section = item.dataset.view;
    const total = data?.activity_totals?.[section];
    let count = 0;
    if (session && Number.isSafeInteger(total)) {
      const key = `${readStoragePrefix}${session.username}:${section}`;
      let seen = readTotals[section] ?? -1;
      try {
        const saved = Number(localStorage.getItem(key) ?? -1);
        if (Number.isSafeInteger(saved)) seen = Math.max(seen, saved);
      } catch {
        // 浏览器禁用存储时，在当前页面内保留已读进度。
      }
      if (seen < 0 || (readView === section && total > seen)) {
        seen = total;
        try { localStorage.setItem(key, String(seen)); } catch { /* 继续使用页面内的进度。 */ }
      }
      readTotals[section] = seen;
      count = Math.max(0, total - seen);
    }
    badge.textContent = count || "";
    badge.hidden = count === 0;
    if (count) item.setAttribute("aria-label", `${titles[section]}，${count} 条未读`);
    else item.removeAttribute("aria-label");
  }
}

/** 清除上一会话的页面数据与临时已读进度。 */
function setSession(value) {
  session = value;
  data = null;
  readTotals = {};
  readOnLoad = null;
  loadCounter++;
  byId("login").hidden = Boolean(value);
  byId("workspace").hidden = !value;
  byId("account").textContent = value?.username || "";
  byId("content").replaceChildren();
  updateUnread();
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

function teamControl(project, returnToProject = false) {
  const control = node("div", null, "project-team");
  if (project.team) control.append(node("span", project.team));
  const edit = button(project.team ? "修改" : "设置团队", () => editTeam(project, returnToProject), "link");
  edit.setAttribute("aria-label", `修改 ${project.name} 的团队`);
  control.append(edit);
  return control;
}

function projectRows(projects, editable = false) {
  const implementation = new Map();
  for (const item of data.implementation || []) {
    if (!implementation.has(item.project_id)) implementation.set(item.project_id, item);
  }
  return table(["项目", "团队", "Framework", "Unity", ...(editable ? ["实施状态"] : []), "最近环境上报", "最近上报"], projects.map((project) => {
    const workspaces = data.workspaces.filter((item) => item.project_id === project.id);
    const versions = [...new Set(workspaces.map((item) => item.framework))].join("、");
    const unity = [...new Set(workspaces.map((item) => item.unity).filter(Boolean))].join("、");
    return [
      button(project.name, () => openProject(project.id), "link"), editable ? teamControl(project) : project.team || "—",
      versions || "暂无", unity || "暂无",
      ...(editable ? [implementationSummary(implementation.get(project.id))] : []),
      stamp(Math.max(0, ...workspaces.map((item) => item.received_at))), stamp(project.last_seen),
    ];
  }));
}

function implementationSummary(item) {
  if (!item) return "待上报";
  const configured = Object.values(item.tools).filter((state) => state === "configured").length;
  const upgrade = item.requested_framework && item.requested_framework !== item.framework ? "待解析" :
    item.editor_framework === item.framework ? "Editor 已载入" : "待载入";
  return `三工具已配置 ${configured}/3 · ${upgrade}`;
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
      .map((item) => {
        const operation = node("code", item.operation, "usage-operation");
        operation.title = item.description || "暂无该操作的说明。";
        return [projectName(item.project_id), item.module, operation,
          usageSources[item.source] || "未标注", usageEntries[item.entry] || "—", item.framework,
          outcomeNames[item.outcome] || item.outcome, item.count];
      }));
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
  const pages = { overview, issues: issuesPage, projects: () => panel(null, projectRows(data.projects, true)), usage: usagePage };
  const content = pages[view]();
  byId("content").replaceChildren(...(Array.isArray(content) ? content : [content]));
}

/** 刷新维护台及各栏未读数量，保留正在使用的筛选控件。 */
async function refresh(force = false) {
  if (!session) return;
  const run = ++loadCounter;
  try {
    const latest = await request(`/api/maintainer/overview?days=${days}`);
    if (run !== loadCounter) return;
    data = latest;
    updateUnread(readOnLoad);
    readOnLoad = null;
    showError(byId("page-error"), "");
    const formControl = byId("content").contains(document.activeElement) &&
      ["INPUT", "SELECT", "TEXTAREA"].includes(document.activeElement.tagName);
    if (force || !formControl) render();
    else if (view === "issues") paintIssues?.(0, false);
    else if (view === "usage") paintUsage?.();
    void refreshProjectDetail();
  } catch (error) {
    if (session && run === loadCounter) showError(byId("page-error"), error.message);
  }
}

function openDialog(title, children) {
  projectLive = null;
  byId("detail-title").textContent = title;
  byId("detail-body").replaceChildren(...children);
  if (!byId("details").open) byId("details").showModal();
}

function fields(entries) {
  const list = node("dl");
  for (const [name, value] of entries) {
    const detail = node("dd");
    detail.append(value || "—");
    list.append(node("dt", name), detail);
  }
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

function editTeam(project, returnToProject) {
  const form = node("form", null, "form-grid");
  const [teamField, team] = textField("团队", "team", project.team);
  const save = node("button", "保存", "primary");
  const error = node("p", "", "error");
  error.setAttribute("role", "alert");
  error.hidden = true;
  function finish() {
    if (returnToProject) openProject(project.id);
    else byId("details").close();
  }
  const actions = node("div", null, "actions");
  actions.append(save, button("取消", finish));
  form.append(teamField, error, actions);
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    save.disabled = true;
    try {
      await request(`/api/maintainer/projects/${encodeURIComponent(project.id)}/team`, {
        team: team.value, previous_team: project.team,
      });
      if (form.isConnected && byId("details").open) finish();
      refresh(true);
    } catch (failure) {
      showError(error, failure.message);
    } finally {
      save.disabled = false;
    }
  });
  openDialog(`修改团队 · ${project.name}`, [form]);
  team.focus();
  team.select();
}

function gamePackageNames(identifiers) {
  if (!identifiers) return "待上报";
  const platforms = new Map();
  for (const [platform, identifier] of Object.entries(identifiers)) {
    if (!platforms.has(identifier)) platforms.set(identifier, []);
    platforms.get(identifier).push(platform);
  }
  return [...platforms].map(([identifier, targets]) => `${identifier}（${targets.join(" / ")}）`).join("、") || "未配置";
}

function hookStatus(snapshot, run) {
  const status = hookStatuses[snapshot?.codex_hooks];
  let current = "待上报";
  if (status) {
    const [text, tone, title] = status;
    current = pill(text, tone);
    current.title = title;
  }
  if (!run) return current;
  const value = node("div");
  value.append(current, node("div", `工作区最近运行 ${stamp(run.last_run)} · Framework ${run.framework}`, "muted"));
  return value;
}

function packageTable(packages, versions) {
  function rank(item) {
    if (item.id.startsWith("com.solotopia.")) return 0;
    return versions.get(item.id)?.registry === "internal" ? 1 : 2;
  }
  const ordered = packages.map((item) => {
    const found = versions.get(item.id);
    const latest = found?.latest;
    const canCompare = latest && item.version && item.version !== "unknown";
    const followsUnity = !latest && item.source === "builtin";
    return { item, found, latest, followsUnity, isLatest: canCompare ? item.version === latest : null };
  });
  // 先显示查到最新版本的包，其次是随 Unity 的包，最后是未知版本。
  ordered.sort((a, b) => Number(!a.latest) - Number(!b.latest) ||
    Number(b.followsUnity) - Number(a.followsUnity) ||
    Number(b.isLatest === false) - Number(a.isLatest === false) ||
    rank(a.item) - rank(b.item) || a.item.id.localeCompare(b.item.id));
  const list = table(["已发现包", "当前版本", "最新版本"], ordered.map(({ item, found, latest, followsUnity, isLatest }) => {
    let current = node("span", item.version);
    if (isLatest !== null) {
      const status = found.registry === "third-party"
        ? (isLatest ? "已是当前 Unity 的最新兼容版本" : "有与当前 Unity 兼容的新版")
        : (isLatest ? "已是最新" : "与仓库最新版本不同");
      current = pill(item.version, isLatest ? "success" : "warning");
      current.title = status;
      current.setAttribute("aria-label", `${item.version}，${status}`);
    }
    return [node("code", item.id), current, latest || (followsUnity ? "随 Unity" : "—")];
  }));
  list.classList.add("package-list");
  return list;
}

const implementationStages = {
  "config-export": "Config 导出", "config-load": "Editor 读取 Config",
  "pipify-batch": "Pipify Batch", "plugpals-query": "PlugPals 仓库查询",
  "bundle-build": "Bundle 构建", "player-build": "目标平台构建", "sdk-preload": "Editor SDK 预加载",
};
const implementationModules = {
  config: "Config", pipify: "Pipify", upm: "包管理", procedure: "流程", asset: "资源",
  table: "表", localization: "本地化", ui: "UI", network: "网络", sdk: "SDK",
};
const buildKinds = { player: "Player", project: "平台工程", scripts: "仅脚本" };
const startupStages = {
  splash: "启动画面", "version-check": "版本检查", "app-download": "应用更新", hotfix: "热更新",
  "load-dll": "配置与业务程序集", persist: "存档", localization: "本地化", network: "网络配置",
  sdk: "SDK", table: "数据表", ui: "UI 配置", sound: "声音", vibration: "振动",
};
const observedOutcomes = {
  running: "未收到结束记录", waiting: "仍在等待", advanced: "已推进",
  recovered: "已恢复并载入",
};

function implementationResult(fact) {
  const item = fact.data;
  if (item.stage === "sdk-preload" && item.outcome === "success") return "已进入后续预加载";
  return observedOutcomes[item.outcome] || outcomeNames[item.outcome] || item.outcome;
}

function evidenceSection(key, title, body) {
  const section = node("details");
  section.dataset.evidenceKey = key;
  section.append(node("summary", title), body);
  return section;
}

function elapsed(item) {
  return Number.isFinite(item.milliseconds) ? `${(item.milliseconds / 1000).toFixed(1)} 秒` : "—";
}

function playStartup(report, rows) {
  const latest = rows.find((row) => row.data.stage === "startup-stage");
  if (!latest) return null;
  const phases = rows.filter((row) => row.data.stage === "startup-stage" && row.data.attempt === latest.data.attempt);
  const order = Object.keys(startupStages);
  phases.sort((left, right) => order.indexOf(left.data.step) - order.indexOf(right.data.step));
  return evidenceSection(`${report.workspace}:startup`, "Editor 启动阶段",
    table(["阶段", "最近结果", "经过时间", "发生时间"], phases.map((row) =>
      [startupStages[row.data.step], implementationResult(row), elapsed(row.data), stamp(row.observed_at)])));
}

function frameworkUpgrades(report, rows) {
  const attempts = new Map();
  for (const row of rows.filter((entry) => entry.data.stage === "framework-upgrade")) {
    if (!attempts.has(row.data.attempt)) attempts.set(row.data.attempt, []);
    attempts.get(row.data.attempt).push(row);
  }
  if (!attempts.size) return null;
  const phases = { request: "提交", resolve: "UPM 解析", compile: "编译", load: "Editor 载入" };
  const reasons = { "upm-package-error": "目标包返回错误", "compiler-error": "工程编译错误", "request-changed": "请求版本已变更" };
  const order = Object.keys(phases);
  const detail = node("div");
  for (const attempt of [...attempts.values()].slice(0, 3)) {
    attempt.sort((left, right) => order.indexOf(left.data.step) - order.indexOf(right.data.step));
    const item = attempt[0].data;
    detail.append(node("h4", `${item.from_framework} → ${item.requested_framework}`),
      table(["阶段", "结果", "经过时间", "发生时间"], attempt.map((row) => [
        phases[row.data.step], [row.data.step === "request" ? "已提交" : implementationResult(row),
          reasons[row.data.reason]].filter(Boolean).join(" · "), elapsed(row.data), stamp(row.observed_at),
      ])));
  }
  return evidenceSection(`${report.workspace}:upgrade`, "Framework 升级过程", detail);
}

function workspaceImplementation(report, result) {
  const current = report.data;
  const sameVersion = (row) => row.workspace === report.workspace && row.data.framework === current.framework;
  const workspaceEvents = result.implementation.filter((row) => row.workspace === report.workspace &&
    row.data.stage !== "workspace");
  const events = workspaceEvents.filter((row) => sameVersion(row) && row.data.unity === current.unity &&
    Object.hasOwn(implementationStages, row.data.stage));
  const issue = (result.upgrade_issues || []).find(sameVersion);
  const usage = new Map((result.module_evidence || [])
    .filter((row) => row.workspace === report.workspace && row.framework === current.framework)
    .map((row) => [row.module, row]));
  const moduleIds = new Set([...Object.keys(implementationModules), ...usage.keys()]);
  const observedStages = { config: ["config-load", "config-export"], pipify: ["pipify-batch"],
    upm: ["plugpals-query"], asset: ["bundle-build"], sdk: ["sdk-preload"] };
  const scopeName = (item) => [item.platform, item.channel, item.mode].filter(Boolean).join(" · ") || "工程级";
  const received = Math.max(report.received_at, ...workspaceEvents.map((item) => item.received_at), issue?.received_at || 0);
  const observed = Math.max(report.observed_at, ...workspaceEvents.map((item) => item.observed_at), issue?.observed_at || 0);
  const section = node("details");
  section.dataset.evidenceKey = report.workspace;
  const sdk = node("details");
  sdk.dataset.evidenceKey = `${report.workspace}:sdk`;
  const mobileBuilds = ["Android", "iOS"].map((platform) => events.find((row) =>
    row.data.stage === "player-build" && row.data.platform === platform && row.data.build_kind !== "scripts")).filter(Boolean);
  sdk.append(node("summary", "SDK 接入与构建"),
    fields([["已安装 SDK 包", current.sdk_packages.map((item) => `${item.id}@${item.version}`).join("、") || "暂无"],
      ["已启用 SDK 配置类型", current.sdk_configs.join("、") || "暂无"],
      ["Android／iOS 项目构建", mobileBuilds.map((row) => `${row.data.platform} ${buildKinds[row.data.build_kind]} · ${implementationResult(row)}`).join("、") || "暂无构建记录"]]));
  const history = [playStartup(report, workspaceEvents),
    frameworkUpgrades(report, workspaceEvents)].filter(Boolean);
  section.append(node("summary", `工作区 ${report.workspace.slice(0, 8)} · Framework ${current.framework} · Unity ${current.unity}`),
    fields([["当前坐标", scopeName(current)], ["Framework 请求版本", current.requested_framework],
      ["Framework 已解析", current.framework],
      ["Editor 已载入版本", current.editor_framework],
      ["实施证据最近发生", stamp(observed)], ["实施证据最近收到", stamp(received)],
      ["最近编译或导入异常", issue ? button(`#${issue.issue_id} · ${stamp(issue.observed_at)}`,
        () => openIssue(issue.issue_id), "link") : "暂无记录"]]),
    table(["工具", "配置", "最近执行证据"], [["config", "Config", ["config-load", "config-export"]],
      ["pipify", "Pipify", ["pipify-batch"]], ["plugpals", "PlugPals", ["plugpals-query"]]].map(([id, label, stages]) => {
      const evidence = events.find((row) => stages.includes(row.data.stage));
      return [label, current.tools[id] === "configured" ? "已配置" : "待检查",
        evidence ? `${implementationStages[evidence.data.stage]} · ${implementationResult(evidence)} · ${stamp(evidence.observed_at)}` : "待验证"];
    })),
    sdk,
    ...history,
    panel("最近执行结果", events.length ? table(["操作", "目标 / 坐标", "结果", "耗时", "发生时间", "收到时间"],
      events.map((row) => {
        const item = row.data;
        const context = [scopeName(item), buildKinds[item.build_kind],
          item.build_kind ? (item.development ? "开发构建" : "普通构建") : null].filter(Boolean).join(" · ");
        const step = item.stage === "plugpals-query" ? ({ internal: "内网", external: "外网" }[item.step]) : item.step;
        return [[implementationStages[item.stage], step].filter(Boolean).join(" · "), context, implementationResult(row),
          elapsed(item),
          stamp(row.observed_at), stamp(row.received_at)];
      })) : node("p", "等待工具执行结果。", "empty")),
    panel("模块使用证据（近 30 天）", table(["模块", "状态", "工具调用", "Editor Play 次数", "阻塞或失败", "最近成功"],
      [...moduleIds].map((id) => {
        const evidence = usage.get(id);
        const actual = events.find((row) => observedStages[id]?.includes(row.data.stage) &&
          row.observed_at >= data.server_time - 30 * 86400);
        const successful = events.find((row) => observedStages[id]?.includes(row.data.stage) &&
          row.data.outcome === "success" && row.observed_at >= data.server_time - 30 * 86400);
        return [implementationModules[id] || id, actual ? implementationStages[actual.data.stage] : evidence ? "已观测使用" : "未观测",
          evidence?.count ?? "—", evidence?.play_sessions || "—", evidence?.problems ?? "—",
          stamp(Math.max(evidence?.last_success || 0, successful?.observed_at || 0))];
      }))));
  return section;
}

function implementationPanel(result) {
  const workspaces = (result.implementation || []).filter((item) => item.data.stage === "workspace");
  if (!workspaces.length) return panel("工程实施", node("p", "等待支持实施反馈的工作区首次上报。", "empty"));
  const sections = workspaces.map((item) => workspaceImplementation(item, result));
  sections[0].open = true;
  return panel("工程实施", ...sections);
}

function snapshotPanel(snapshot, path, hookRun) {
  const snap = snapshot.data;
  const section = node("details");
  const feedback = node("div");
  feedback.dataset.hookFeedback = "";
  feedback.append(hookStatus(snap, hookRun));
  const packages = node("div");
  let loading = false;
  let complete = false;
  section.addEventListener("toggle", async () => {
    if (!section.open || loading || complete) return;
    if (!snap.packages.length) {
      packages.replaceChildren(packageTable(snap.packages, new Map()));
      complete = true;
      return;
    }

    loading = true;
    const route = `${path}/package-versions?snapshot=${encodeURIComponent(snapshot.id)}`;
    const checking = node("p", "正在检查更多包版本…", "empty");
    packages.replaceChildren(node("p", "正在读取包版本…", "empty"));
    try {
      const attempts = 6;
      for (let attempt = 0; attempt < attempts; attempt++) {
        if (!section.isConnected || !section.open || !byId("details").open) return;
        const { packages: versions, pending } = await request(route);
        packages.replaceChildren(packageTable(snap.packages, new Map(Object.entries(versions))));
        complete = !pending;
        if (complete || !section.open || attempt === attempts - 1) return;
        packages.append(checking);
        await new Promise((resolve) => setTimeout(resolve, 700));
      }
    } catch {
      if (!packages.querySelector("table")) packages.replaceChildren(packageTable(snap.packages, new Map()));
    } finally {
      checking.remove();
      loading = false;
    }
  });
  section.append(node("summary", `${snap.framework} · ${snap.unity} · ${stamp(snapshot.received_at)}`),
    fields([["上报人", snap.git_user_name || "暂无 Git 名称"],
      ["游戏包名", gamePackageNames(snap.application_identifiers)], ["钩子追加", feedback],
      ["已启用", snap.enabled?.join("、")],
      ...Object.entries(snap.coverage).map(([name, state]) => [`${name} 采集`, coverageNames[state] || state]),
      ...Object.entries(snap.tools).filter(([name]) => name !== "mcp" && name !== "pipeline")
        .map(([name, item]) => [name, `${item.version || ""} ${coverageNames[item.state] || item.state}`.trim()])]),
    packages);
  return section;
}

function updateProjectSnapshots(current, result) {
  const hookRuns = new Map((result.hook_activity || []).map((run) => [run.workspace, run]));
  const existing = new Map([...current.snapshots.children].map((section) => [section.dataset.snapshot, section]));
  result.snapshots.forEach((snapshot, index) => {
    const run = hookRuns.get(snapshot.workspace);
    hookRuns.delete(snapshot.workspace);
    const previous = existing.get(snapshot.id);
    const section = previous || snapshotPanel(snapshot, current.path, run);
    section.dataset.snapshot = snapshot.id;
    const feedback = section.querySelector("[data-hook-feedback]");
    const signature = JSON.stringify([snapshot.data.codex_hooks, run?.last_run, run?.framework]);
    if (feedback.dataset.value !== signature) {
      feedback.replaceChildren(hookStatus(snapshot.data, run));
      feedback.dataset.value = signature;
    }
    if (section !== current.snapshots.children[index]) {
      current.snapshots.insertBefore(section, current.snapshots.children[index] || null);
    }
    existing.delete(snapshot.id);
    if (!previous && index === 0 && !current.snapshots.querySelector("details[open]")) section.open = true;
  });
  for (const section of existing.values()) section.remove();
  if (!result.snapshots.length) current.snapshots.append(node("p", "等待该项目的首次上报。", "empty"));
}

async function refreshProjectDetail() {
  const current = projectLive;
  if (!current || current.pending || !current.implementation.isConnected || !byId("details").open) return;
  const summary = data?.projects.find((project) => project.id === current.id);
  if (!summary || (summary.last_seen === current.lastSeen && summary.team === current.team &&
      data.server_time - current.refreshedAt < 60)) return;
  current.pending = true;
  try {
    const result = await request(current.path);
    if (projectLive !== current || !session || !byId("details").open) return;
    const evidence = JSON.stringify([result.implementation, result.module_evidence, result.upgrade_issues]);
    if (evidence !== current.evidence) {
      const open = new Map([...current.implementation.querySelectorAll("details[data-evidence-key]")]
        .map((section) => [section.dataset.evidenceKey, section.open]));
      const active = current.implementation.contains(document.activeElement)
        ? document.activeElement.closest("details")?.dataset.evidenceKey : null;
      const next = implementationPanel(result);
      for (const section of next.querySelectorAll("details[data-evidence-key]")) {
        if (open.has(section.dataset.evidenceKey)) section.open = open.get(section.dataset.evidenceKey);
      }
      current.implementation.replaceWith(next);
      current.implementation = next;
      for (const section of next.querySelectorAll("details[data-evidence-key]")) {
        if (section.dataset.evidenceKey === active) section.querySelector("summary")?.focus({ preventScroll: true });
      }
      current.evidence = evidence;
    }
    updateProjectSnapshots(current, result);
    current.lastReport.textContent = stamp(result.project.last_seen);
    if (current.team !== result.project.team) current.teamField.replaceChildren(teamControl(result.project, true));
    current.team = result.project.team;
    current.lastSeen = result.project.last_seen;
    current.refreshedAt = data.server_time;
  } catch {
    // 短暂断线时保留当前详情，下一轮刷新继续读取。
  } finally {
    current.pending = false;
  }
}

async function openProject(id) {
  const loading = node("p", "正在读取项目…", "empty");
  openDialog("读取项目", [loading]);
  const path = `/api/maintainer/projects/${encodeURIComponent(id)}`;
  try {
    const result = await request(path);
    if (!session || !loading.isConnected || !byId("details").open) return;
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
    const current = {
      id, path, lastSeen: result.project.last_seen, team: result.project.team, refreshedAt: data?.server_time || 0,
      implementation: implementationPanel(result), snapshots: node("div"),
      lastReport: node("span", stamp(result.project.last_seen)), teamField: node("span"),
      evidence: JSON.stringify([result.implementation, result.module_evidence, result.upgrade_issues]),
    };
    current.teamField.append(teamControl(result.project, true));
    updateProjectSnapshots(current, result);
    openDialog(result.project.name, [
      fields([["标识", id], ["团队", current.teamField], ["最近上报", current.lastReport]]),
      current.implementation,
      panel("环境快照", current.snapshots),
      rotate,
    ]);
    projectLive = current;
  } catch (failure) {
    if (session && loading.isConnected && byId("details").open) {
      openDialog("读取项目失败", [node("p", failure.message, "error")]);
    }
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

// 主动打开栏目时只清除当前已拉取的数据，后续刷新仍会提示新的上报。
byId("nav").addEventListener("click", (event) => {
  const next = event.target.closest("button")?.dataset.view;
  if (next && titles[next]) {
    view = next;
    if (data) updateUnread(next);
    else readOnLoad = next;
    render();
  }
});

window.addEventListener("storage", (event) => {
  if (event.key?.startsWith(readStoragePrefix)) updateUnread();
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
