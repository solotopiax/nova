"use strict";

const byId = (id) => document.getElementById(id);
const statusNames = { open: "待处理", closed: "已处理" };
const outcomeNames = {
  executed: "已执行", success: "成功", partial: "部分完成", blocked: "有阻塞",
  failed: "失败", cancelled: "已取消",
};
const completedOutcomes = new Set(["executed", "success", "advanced", "recovered"]);
const usageSources = { codex: "Codex", editor: "Editor", legacy: "未标注" };
const usageEntries = { action: "Nova Action", skill: "Skill 查阅", "unity-cli": "Unity CLI", "nova-cli": "Nova CLI", preload: "Editor 预加载" };
const coverageNames = {
  observed: "已观察", waiting: "待采集", registered: "已注册", not_observed: "未验证",
  connected: "已连接", installed: "已安装", unavailable: "未获得证据", ready: "可用",
  static: "静态证据", interrupted: "已中断", pending: "待分析",
};
const readyCoverageStates = new Set(["observed", "registered", "connected", "installed", "ready"]);
const hookStatuses = {
  complete: ["已加全", "success", "项目钩子和绑定脚本已就绪。授权状态可在 Codex 中查看。"],
  pending: ["待追加", "warning", "工作区中还没有 Nova 自动反馈钩子。"],
  incomplete: ["不完整", "urgent", "工作区的反馈钩子或绑定脚本缺失，或校验未通过。"],
};
const titles = { overview: "总览", projects: "项目", issues: "问题", usage: "使用记录" };
const sortOptions = [["latest", "最新时间"], ["count", "触发次数"]];
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
const filters = {
  search: "", issueProject: "", status: "open", issueSort: "latest",
  project: "", module: "", usageSource: "", usageSort: "count",
};

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

/** 用文字和颜色同时标记已通过、已完成或待核对的状态。 */
function statusPill(text, complete) {
  return pill(text, complete ? "success" : "warning");
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

/** 创建带标签的下拉框，筛选控件可以在选项变化时刷新结果。 */
function control(label, field, options, run) {
  const box = node("label", label);
  const select = node("select");
  for (const [value, name] of options) {
    const option = node("option", name);
    option.value = value;
    select.append(option);
  }
  select.value = String(field);
  if (run) select.addEventListener("change", () => run(select.value));
  box.append(select);
  return box;
}

function filter(name, label, options, repaint) {
  return control(label, filters[name], options, (value) => {
    filters[name] = value;
    repaint();
  });
}

/** 显示上报确认的包标识及版本，缺少归因时由调用处省略。 */
function issuePackage(packageInfo) {
  if (!packageInfo) return null;
  const label = packageInfo.version ? `${packageInfo.id}@${packageInfo.version}` : packageInfo.id;
  return node("code", label, "issue-package");
}

/** 总览和问题列表共用同一张表，点击项目数查看对应名称。 */
function issueRows(issues) {
  return table(["问题", "所属包 / 版本", "状态", "影响项目", "发生次数", "最近发生"], issues.map((item) => {
    const projects = button(item.projects, () => openIssueProjects(item.id), "link issue-project-count");
    projects.setAttribute("aria-label", `查看问题 #${item.id} 影响的 ${item.projects} 个项目`);
    return [button(`#${item.id}  ${item.title}`, () => openIssue(item.id), "link"),
      issuePackage(item.package), statusPill(statusNames[item.status], item.status === "closed"), projects,
      item.occurrences, stamp(item.last_seen)];
  }));
}

/** 读取当前问题的项目名单，关闭弹窗后忽略该次查询结果。 */
async function openIssueProjects(id) {
  const title = `#${id} 受影响的项目`;
  const loading = node("p", "正在读取项目…", "empty");
  openDialog(title, [loading]);
  try {
    const { projects } = await request(`/api/maintainer/issues/${id}/projects`);
    if (!loading.isConnected || !byId("details").open) return;
    const list = node("ul", null, "issue-projects");
    for (const project of projects) list.append(node("li", project.name));
    openDialog(title, [projects.length ? list : node("p", "暂无受影响的项目", "empty")]);
  } catch (failure) {
    if (loading.isConnected && byId("details").open) openDialog(title, [node("p", failure.message, "error")]);
  }
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

/** 读取工作区上报的 Editor 实际载入版本。 */
function loadedFrameworkVersion(item) {
  return item.editor_framework === "unknown" ? null : item.editor_framework;
}

/** 汇总各项目的工作区载入版本、工具配置和最近环境上报。 */
function projectRows(projects, editable = false) {
  return table(["项目", "团队", "Framework版本", "Unity", ...(editable ? ["实施状态"] : []), "最近环境上报"], projects.map((project) => {
    const workspaces = data.workspaces.filter((item) => item.project_id === project.id);
    const implementation = (data.implementation || []).filter((item) => item.project_id === project.id);
    const versions = [...new Set(implementation.map(loadedFrameworkVersion).filter(Boolean))].join("、");
    const unity = [...new Set(workspaces.map((item) => item.unity).filter(Boolean))].join("、");
    return [
      button(project.name, () => openProject(project.id), "link"), editable ? teamControl(project) : project.team || "—",
      versions || "待上报", unity || "暂无",
      ...(editable ? [implementationSummary(implementation.length, data.configured_tools?.[project.id] || [])] : []),
      stamp(Math.max(0, ...workspaces.map((item) => item.received_at))),
    ];
  }));
}

/** 显示项目已确认的三工具配置进度。 */
function implementationSummary(hasReport, configuredTools) {
  if (!hasReport) return statusPill("待上报", false);
  const configured = configuredTools.length;
  return statusPill(`三工具已配置 ${configured}/3`, configured === 3);
}

/** 汇总两种处理状态的数量，并显示最近的待处理问题和项目。 */
function overview() {
  const activeIssues = data.issues.filter((item) => item.status === "open");
  const grid = node("div", null, "metrics");
  for (const [label, value] of [
    ["已登记项目", data.projects.length],
    ["过去 24 小时有上报", data.projects.filter(activeProject).length],
    ["待处理问题", data.issue_counts.active],
    ["已处理问题", data.issue_counts.handled],
  ]) {
    const entry = node("div", null, "metric");
    entry.append(node("strong", value), node("p", label));
    grid.append(entry);
  }
  return [grid, panel("待处理问题", issueRows(activeIssues.slice(0, 8))),
    panel("项目上报", projectRows(data.projects.slice(0, 8)))];
}

/** 将筛选条件和排序一起交给服务端查询问题。 */
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
    const params = new URLSearchParams({ sort: filters.issueSort });
    for (const [key, value] of Object.entries({
      q: filters.search.trim(), project: filters.issueProject, status: filters.status,
    })) {
      if (value) params.set(key, value);
    }
    if (showLoading) results.replaceChildren(node("p", "正在读取问题…", "empty"));
    timer = setTimeout(async () => {
      if (!results.isConnected) return;
      try {
        const { issues } = await request(`/api/maintainer/issues?${params}`);
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
    filter("issueSort", "排序", sortOptions, paint));
  paintIssues = paint;
  paint();
  return panel(null, toolbar, results);
}

/** 按实际触发时间或次数展示使用汇总，筛选和取前 500 项由服务端完成。 */
function usagePage() {
  const toolbar = node("div", null, "toolbar usage-toolbar");
  const results = node("div");
  let latest = 0;
  /** 展示当前筛选结果，并按同一排序选择排列 Codex 的模块汇总。 */
  function show(usage) {
    const details = table(["项目", "模块", "操作", "来源", "入口", "Framework", "结果", "次数", "最近使用"],
      usage.map((item) => {
        const operation = node("code", item.operation, "usage-operation");
        operation.title = item.description || "暂无该操作的说明。";
        return [projectName(item.project_id), item.module, operation,
          usageSources[item.source] || "未标注", usageEntries[item.entry] || "—", item.framework,
          statusPill(outcomeNames[item.outcome] || item.outcome, completedOutcomes.has(item.outcome)),
          item.count, stamp(item.last_used)];
      }));
    results.replaceChildren();
    if (filters.usageSource === "codex") {
      const modules = data.codex_modules.filter((item) => (!filters.project || item.project_id === filters.project) &&
        (!filters.module || item.module === filters.module));
      if (filters.usageSort === "latest") modules.sort((a, b) => b.last_used - a.last_used);
      results.append(node("h2", "项目模块调用"),
        table(["项目", "模块", "调用次数", "Skill 查阅次数（按轮）", "失败或阻塞", "最近使用"],
          modules.map((item) => [projectName(item.project_id), item.module, item.calls,
            item.skill_turns, item.problems, stamp(item.last_used)])), node("h2", "操作明细"));
    }
    results.append(details);
  }
  /** 复用总览的默认汇总，其他筛选和排序读取完整范围内的匹配记录。 */
  async function paint(showLoading = true) {
    const run = ++latest;
    const { project, module, usageSource: source, usageSort: sort } = filters;
    if (!project && !module && !source && sort === "count") {
      show(data.usage);
      return;
    }
    if (showLoading) results.replaceChildren(node("p", "正在读取使用记录…", "empty"));
    const params = new URLSearchParams({ days: data.days, sort, project, module, source });
    try {
      const { usage } = await request(`/api/maintainer/usage?${params}`);
      if (run === latest && results.isConnected) show(usage);
    } catch (error) {
      if (run === latest && results.isConnected) results.replaceChildren(node("p", error.message, "error"));
    }
  }
  toolbar.append(filter("usageSource", "来源", [["", "全部"], ...Object.entries(usageSources)], paint),
    filter("project", "项目", [["", "全部"], ...data.projects.map((item) => [item.id, item.name])], paint),
    filter("module", "模块", [["", "全部"], ...data.usage_modules.map((name) => [name, name])], paint),
    control("最近", days, [[1, "1 天"], [7, "7 天"], [14, "14 天"], [30, "30 天"], [90, "90 天"]], (value) => {
      days = Number(value);
      refresh(true);
    }), filter("usageSort", "排序", sortOptions, paint));
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
    else if (view === "usage") paintUsage?.(false);
    void refreshProjectDetail();
  } catch (error) {
    if (session && run === loadCounter) showError(byId("page-error"), error.message);
  }
}

/** 打开共用弹窗，项目详情可启用点击遮罩关闭。 */
function openDialog(title, children, { closeOnBackdrop = false } = {}) {
  projectLive = null;
  const dialog = byId("details");
  dialog.toggleAttribute("data-close-on-backdrop", closeOnBackdrop);
  byId("detail-title").textContent = title;
  byId("detail-body").replaceChildren(...children);
  if (!dialog.open) dialog.showModal();
}

/** 原生 dialog 会把遮罩点击交给自身，按弹窗边界识别外部空位。 */
function isDialogBackdrop(event) {
  const dialog = event.currentTarget;
  if (event.target !== dialog || !dialog.hasAttribute("data-close-on-backdrop")) return false;
  const bounds = dialog.getBoundingClientRect();
  return event.clientX < bounds.left || event.clientX >= bounds.right ||
    event.clientY < bounds.top || event.clientY >= bounds.bottom;
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

/** 按包名汇总已上报的平台，标记缺少上报或未配置的状态。 */
function gamePackageNames(identifiers) {
  if (!identifiers) return statusPill("待上报", false);
  const platforms = new Map();
  for (const [platform, identifier] of Object.entries(identifiers)) {
    if (!platforms.has(identifier)) platforms.set(identifier, []);
    platforms.get(identifier).push(platform);
  }
  return [...platforms].map(([identifier, targets]) => `${identifier}（${targets.join(" / ")}）`).join("、") ||
    statusPill("未配置", false);
}

/** 显示钩子配置的上报状态和工作区最近运行。 */
function hookStatus(snapshot, run) {
  const status = hookStatuses[snapshot?.codex_hooks];
  let current = statusPill("待上报", false);
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

// 同一份 SDK 对应关系用于核对包的启用状态和展示模块名称。
const sdkPackageConfigs = new Map(Object.entries({
  ad: { name: "Ad", type: "AdPlugin.Runtime.AdPluginConfig" },
  aihelp: { name: "AIHelp", type: "AIHelp.Runtime.AIHelpPluginConfig" },
  applesignin: { name: "AppleSignIn", type: "AppleSignIn.AppleSignInPluginConfig" },
  appsflyer: { name: "AppsFlyer", type: "AppsFlyerPlugin.Runtime.AppsFlyerPluginConfig" },
  "datamaster.abtest": { name: "DataMaster", type: "StarlusDataMaster.ABTest.Runtime.DataMasterPluginConfig" },
  facebook: { name: "Facebook", type: "Facebook.FacebookPluginConfig" },
  firebase: { name: "Firebase", type: "FirebasePlugin.Runtime.FirebasePluginConfig" },
  googlesignin: { name: "GoogleSignIn", type: "GoogleSignIn.GoogleSignInPluginConfig" },
  iap: { name: "IAP", type: "IAP.Runtime.IAPPluginConfig" },
  tga: { name: "TGA", type: "TGAPlugin.Runtime.TGAPluginConfig" },
  "wechat.minigame": { name: "WeChatMiniGame", type: "WeChatMiniGame.Runtime.WeChatMiniGamePluginConfig" },
}));
const enabledModuleNames = new Map([
  ...Array.from(sdkPackageConfigs.values(), ({ name, type }) => [`NovaFramework.SDK.${type}`, name]),
  ["NovaFramework.Kit.Network.GameLogin.Runtime.LoginKitConfig", "GameLogin"],
  ["NovaFramework.Kit.Network.GameSave.Runtime.SaveKitConfig", "GameSave"],
  ["NovaFramework.Kit.Network.GameBind.Runtime.BindKitConfig", "GameBind"],
]);
const sdkAdapterConfigOwners = new Map(Object.entries({
  admob: "广告聚合配置", max: "广告聚合配置",
  "iap.mobile": "IAP 配置", "iap.thirdpay": "IAP 配置", "iap.voucher": "IAP 配置",
}));

/** 汇总 SDK 清单，启用状态来自该包和配置一起上报的记录。 */
function sdkPackageTable(workspaces) {
  const prefix = "com.solotopia.nova.framework.sdk.";
  const packages = new Map();
  for (const workspace of workspaces) {
    const configs = new Set(workspace.sdk_configs);
    for (const item of workspace.sdk_packages || []) {
      if (!packages.has(item.id)) {
        const name = item.id.slice(prefix.length);
        packages.set(item.id, { ...item, name, config: sdkPackageConfigs.get(name)?.type, enabled: false });
      }
      const sdk = packages.get(item.id);
      if (sdk.config && configs.has(`NovaFramework.SDK.${sdk.config}`)) sdk.enabled = true;
    }
  }
  if (!packages.size) return node("p", "暂无已安装 SDK 包", "empty");
  const installed = [...packages.values()].sort((left, right) => left.id.localeCompare(right.id));
  return table(["已安装 SDK 包", "启用 SDK 配置"], installed.map(({ name, id, version, config, enabled }) => {
    const status = statusPill(enabled ? "是" : "否", enabled);
    const owner = sdkAdapterConfigOwners.get(name);
    if (owner) status.title = `此适配包的配置在${owner}中管理。`;
    else if (!config) status.title = "暂无此包独立配置的对应信息。";
    return [`${id}@${version}`, status];
  }));
}

/** 在快照中按上报顺序列出 SDK 和 Kit，旧类型或自定义类型取其简短类名。 */
function enabledModuleList(types = []) {
  if (!types.length) return "—";
  const list = node("ul", null, "enabled-modules");
  for (const type of new Set(types)) {
    const className = type.split(".").filter(Boolean).at(-1);
    const name = enabledModuleNames.get(type) || className.replace(/(?:PluginConfig|KitConfig|Config)$/, "") || className;
    list.append(node("li", name));
  }
  return list;
}

/** 按实际执行结果展示状态，可附上来源已记录的原因。 */
function implementationResult(fact, reason = "") {
  const item = fact.data;
  const submitted = item.stage === "framework-upgrade" && item.step === "request";
  const preloaded = item.stage === "sdk-preload" && item.outcome === "success";
  const label = submitted ? "已提交" : preloaded ? "已进入后续预加载" :
    observedOutcomes[item.outcome] || outcomeNames[item.outcome] || item.outcome;
  const status = statusPill(label, submitted || completedOutcomes.has(item.outcome));
  status.title = `Framework ${item.framework} · Unity ${item.unity} · 本地工程 ${fact.workspace.slice(0, 8)}`;
  if (!reason) return status;
  const value = node("span");
  value.append(status, ` · ${reason}`);
  return value;
}

function implementationVerification(success) {
  const status = statusPill(success ? "已验证" : "待验证", Boolean(success));
  if (success) {
    const { stage, framework, unity } = success.data;
    status.title = `${implementationStages[stage]} · ${stamp(success.observed_at)} · Framework ${framework} · Unity ${unity} · 本地工程 ${success.workspace.slice(0, 8)}`;
  }
  return status;
}

function timedImplementationResult(row, label = "") {
  const value = node("span", label ? `${label} · ` : null);
  value.append(implementationResult(row), ` · ${stamp(row.observed_at)}`);
  return value;
}

function mobileBuildResults(outcomes) {
  const builds = new Map();
  for (const row of outcomes) {
    const item = row.data;
    if (item.stage !== "player-build" || !["Android", "iOS"].includes(item.platform) || item.build_kind === "scripts") continue;
    const key = `${item.platform}:${item.build_kind}:${item.development}`;
    if (!builds.has(key)) builds.set(key, { latest: row, success: null });
    if (item.outcome === "success") builds.get(key).success = row;
  }
  if (!builds.size) return node("p", "暂无 Android／iOS 项目构建记录", "empty");
  return table(["Android／iOS 项目构建", "验证", "最近结果"], [...builds.values()].map(({ latest, success }) => {
    const { platform, build_kind, development } = latest.data;
    return [`${platform} ${buildKinds[build_kind]} · ${development ? "开发构建" : "普通构建"}`,
      implementationVerification(success), timedImplementationResult(latest)];
  }));
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

function playStartup(rows) {
  const latest = rows.find((row) => row.data.stage === "startup-stage");
  if (!latest) return null;
  const phases = rows.filter((row) => row.data.stage === "startup-stage" &&
    row.workspace === latest.workspace && row.data.attempt === latest.data.attempt);
  const order = Object.keys(startupStages);
  phases.sort((left, right) => order.indexOf(left.data.step) - order.indexOf(right.data.step));
  return evidenceSection("startup", "最近一次 Editor 启动",
    table(["阶段", "最近结果", "经过时间", "发生时间"], phases.map((row) =>
      [startupStages[row.data.step], implementationResult(row), elapsed(row.data), stamp(row.observed_at)])));
}

/** 展示每次 Framework 升级的阶段结果与原因。 */
function frameworkUpgrades(rows) {
  const attempts = new Map();
  for (const row of rows.filter((entry) => entry.data.stage === "framework-upgrade")) {
    const key = `${row.workspace}:${row.data.attempt}`;
    if (!attempts.has(key)) attempts.set(key, []);
    attempts.get(key).push(row);
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
        phases[row.data.step], implementationResult(row, reasons[row.data.reason]),
        elapsed(row.data), stamp(row.observed_at),
      ])));
  }
  return evidenceSection("upgrade", "Framework 升级过程", detail);
}

/** 汇总项目已取得的实施证据，并保留各次执行的真实结果。 */
function implementationPanel(result) {
  const facts = result.implementation || [];
  const workspaces = facts.filter((row) => row.data.stage === "workspace").map((row) => row.data);
  const events = facts.filter((row) => Object.hasOwn(implementationStages, row.data.stage));
  const outcomes = result.implementation_outcomes || events;
  const issue = result.upgrade_issues?.[0];
  const usage = new Map((result.project_module_evidence || []).map((row) => [row.module, row]));
  if (!facts.length && !usage.size && !issue) return panel("工程实施", node("p", "等待项目首次上报实施记录。", "empty"));
  const configuredTools = new Set(result.configured_tools || []);
  const moduleIds = new Set([...Object.keys(implementationModules), ...usage.keys()]);
  const observedStages = { config: ["config-load", "config-export"], pipify: ["pipify-batch"],
    upm: ["plugpals-query"], asset: ["bundle-build"], sdk: ["sdk-preload"] };
  const scopeName = (item) => [item.platform, item.channel, item.mode].filter(Boolean).join(" · ") || "工程级";
  const latestAt = Math.max(0, ...facts.map((item) => item.observed_at), issue?.observed_at || 0);
  const sdk = evidenceSection("sdk", "SDK 接入与构建", sdkPackageTable(workspaces));
  sdk.append(mobileBuildResults(outcomes));
  const history = [playStartup(facts), frameworkUpgrades(facts)].filter(Boolean);
  const frameworks = [...new Set(workspaces.map(loadedFrameworkVersion).filter(Boolean))].join("、");
  const unity = [...new Set(workspaces.map((workspace) => workspace.unity).filter(Boolean))].join("、");
  return panel("工程实施",
    fields([["Framework版本", frameworks || "待上报"], ["Unity", unity || "待上报"],
      ["最近上报坐标", workspaces.length ? scopeName(workspaces[0]) : "待上报"],
      ["最近实施记录", stamp(latestAt)],
      ["最近编译或导入异常", issue ? button(`#${issue.issue_id} · ${stamp(issue.observed_at)}`,
        () => openIssue(issue.issue_id), "link") : "暂无记录"]]),
    table(["工具", "配置", "验证", "最近执行结果"], [["config", "Config", ["config-load", "config-export"]],
      ["pipify", "Pipify", ["pipify-batch"]], ["plugpals", "PlugPals", ["plugpals-query"]]].map(([id, label, stages]) => {
      const recent = outcomes.find((row) => stages.includes(row.data.stage));
      const success = outcomes.find((row) => stages.includes(row.data.stage) && row.data.outcome === "success");
      const configured = configuredTools.has(id);
      return [label, statusPill(configured ? "已配置" : "待检查", configured), implementationVerification(success),
        recent ? timedImplementationResult(recent, implementationStages[recent.data.stage]) : "暂无记录"];
    })),
    sdk,
    ...history,
    panel("最近执行结果", events.length ? table(["操作", "目标 / 坐标", "结果", "耗时", "发生时间"],
      events.map((row) => {
        const item = row.data;
        const context = [scopeName(item), buildKinds[item.build_kind],
          item.build_kind ? (item.development ? "开发构建" : "普通构建") : null].filter(Boolean).join(" · ");
        const step = item.stage === "plugpals-query" ? ({ internal: "内网", external: "外网" }[item.step]) : item.step;
        return [[implementationStages[item.stage], step].filter(Boolean).join(" · "), context, implementationResult(row),
          elapsed(item), stamp(row.observed_at)];
      })) : node("p", "等待工具执行结果。", "empty")),
    panel("模块使用证据", table(["模块", "状态", "近 30 天工具调用", "近 30 天 Editor Play", "近 30 天阻塞或失败", "最近成功"],
      [...moduleIds].map((id) => {
        const evidence = usage.get(id);
        const actual = outcomes.find((row) => observedStages[id]?.includes(row.data.stage));
        const successful = outcomes.find((row) => observedStages[id]?.includes(row.data.stage) && row.data.outcome === "success");
        const lastSuccess = Math.max(evidence?.last_success || 0, successful?.observed_at || 0);
        const observed = Boolean(evidence || actual);
        const status = lastSuccess ? statusPill("已验证", true) :
          statusPill(observed ? "已观测使用" : "未观测", observed);
        return [implementationModules[id] || id, status,
          evidence?.count ?? "—", evidence?.play_sessions || "—", evidence?.problems ?? "—",
          stamp(lastSuccess)];
      }))));
}

function implementationEvidence(result) {
  return JSON.stringify([result.implementation, result.configured_tools, result.implementation_outcomes,
    result.project_module_evidence, result.upgrade_issues]);
}

/** 展示环境信息、采集状态及按需读取的包清单。 */
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
  section.append(node("summary", `Unity ${snap.unity} · ${stamp(snapshot.received_at)}`),
    fields([["上报人", snap.git_user_name || "暂无 Git 名称"],
      ["游戏包名", gamePackageNames(snap.application_identifiers)], ["钩子追加", feedback],
      ["已启用", enabledModuleList(snap.enabled)],
      ...Object.entries(snap.coverage).map(([name, state]) => [`${name} 采集`,
        statusPill(coverageNames[state] || state, readyCoverageStates.has(state))]),
      ...Object.entries(snap.tools).filter(([name]) => name !== "mcp" && name !== "pipeline")
        .map(([name, item]) => {
          const status = node("span", item.version ? `${item.version} ` : "");
          status.append(statusPill(coverageNames[item.state] || item.state, readyCoverageStates.has(item.state)));
          return [name, status];
        })]),
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
    const evidence = implementationEvidence(result);
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

/** 打开项目详情，并允许点击弹窗外部关闭。 */
async function openProject(id) {
  const loading = node("p", "正在读取项目…", "empty");
  const options = { closeOnBackdrop: true };
  openDialog("读取项目", [loading], options);
  const path = `/api/maintainer/projects/${encodeURIComponent(id)}`;
  try {
    const result = await request(path);
    if (!session || !loading.isConnected || !byId("details").open) return;
    const current = {
      id, path, lastSeen: result.project.last_seen, team: result.project.team, refreshedAt: data?.server_time || 0,
      implementation: implementationPanel(result), snapshots: node("div"),
      teamField: node("span"),
      evidence: implementationEvidence(result),
    };
    current.teamField.append(teamControl(result.project, true));
    updateProjectSnapshots(current, result);
    openDialog(result.project.name, [
      fields([["标识", id], ["团队", current.teamField]]),
      current.implementation,
      panel("环境快照", current.snapshots),
    ], options);
    projectLive = current;
  } catch (failure) {
    if (session && loading.isConnected && byId("details").open) {
      openDialog("读取项目失败", [node("p", failure.message, "error")], options);
    }
  }
}

/** 按维护台现有账号分派负责人，保存时追加处理记录。 */
function issueForm(issue, assignees) {
  const form = node("form", null, "form-grid");
  const row = node("div", null, "fields");
  const status = control("状态", issue.status, Object.entries(statusNames));
  status.querySelector("select").name = "status";
  const assignee = control("负责人", assignees.includes(issue.assignee) ? issue.assignee : "",
    [["", "未分配"], ...assignees.map((name) => [name, name])]);
  assignee.querySelector("select").name = "assignee";
  const noteBox = node("label", "补充处理记录");
  const note = node("textarea");
  Object.assign(note, { name: "note", maxLength: 1000 });
  noteBox.append(note);
  row.append(status, assignee);
  const actions = node("div", null, "actions");
  const save = node("button", "保存", "primary");
  actions.append(save);
  const error = node("p", "", "error");
  error.hidden = true;
  error.setAttribute("role", "alert");
  form.append(row, noteBox, error, actions);
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const payload = Object.fromEntries(new FormData(form));
    payload.revision = issue.revision;
    save.disabled = true;
    try {
      await request(`/api/maintainer/issues/${issue.id}`, payload);
      refresh(true);
      if (form.isConnected && byId("details").open) openIssue(issue.id);
    } catch (failure) {
      showError(error, failure.message);
    } finally {
      save.disabled = false;
    }
  });
  return form;
}

/** 展示单次发生的包版本和已上报的诊断，方便复制错误内容。 */
function occurrence(item) {
  const box = node("section", null, "occurrence");
  const report = item.data;
  const title = node("h3", item.name);
  const packageInfo = issuePackage(item.package);
  if (packageInfo) title.append(" · ", packageInfo);
  box.append(title, node("p", stamp(item.observed_at), "muted"));
  const details = [["预期", report.expected], ["触发步骤", report.steps],
    ["绕行", report.workaround], ["来源", report.source]];
  if (item.error_log) {
    box.append(node("p", "错误日志"), node("pre", item.error_log.text, "issue-error-log"));
  } else {
    box.append(node("p", report.summary));
    details.splice(1, 0, ["实际", report.actual]);
  }
  box.append(fields(details));
  const evidence = report.evidence.filter((entry) => entry.source !== item.error_log?.source);
  if (evidence.length) {
    const list = node("ul");
    for (const entry of evidence) list.append(node("li", `${entry.source} · ${entry.detail}`));
    box.append(list);
  }
  return box;
}

/** 打开快速处理，历史备注与新记录的输入放在一起。 */
async function openIssue(id) {
  const loading = node("p", "正在读取问题…", "empty");
  openDialog(`#${id}`, [loading]);
  try {
    const result = await request(`/api/maintainer/issues/${id}`);
    if (!loading.isConnected || !byId("details").open) return;
    const issue = result.issue;
    const management = issue.duplicate_of ? button(`打开合并目标 #${issue.duplicate_of}`,
      () => openIssue(issue.duplicate_of)) : issueForm(issue, result.assignees);
    const history = result.history.length ? [table(["时间", "维护者", "状态", "处理记录"], result.history.map((entry) =>
      [stamp(entry.created_at), entry.actor, statusPill(statusNames[entry.status], entry.status === "closed"),
        node("span", entry.note, "issue-note")]))] : [];
    openDialog(`#${id}  ${issue.title}`, [
      fields([["所属包 / 版本", issuePackage(issue.package)], ["模块", issue.area], ["首次发现", stamp(issue.first_seen)],
        ["最近发生", stamp(issue.last_seen)]]),
      panel("快速处理", management, ...history),
      panel("发生记录", ...(result.occurrences.length ? result.occurrences.map(occurrence)
        : [node("p", "暂无发生记录", "empty")])),
    ]);
  } catch (failure) {
    if (loading.isConnected && byId("details").open) {
      openDialog("读取问题失败", [node("p", failure.message, "error")]);
    }
  }
}

// 按下和点击都落在遮罩上时关闭，避免拖选弹窗内文本时误关。
let pointerDownOnBackdrop = false;
byId("details").addEventListener("pointerdown", (event) => {
  pointerDownOnBackdrop = event.isPrimary && event.button === 0 && isDialogBackdrop(event);
});
byId("details").addEventListener("click", (event) => {
  if (pointerDownOnBackdrop && isDialogBackdrop(event)) event.currentTarget.close();
  pointerDownOnBackdrop = false;
});

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
