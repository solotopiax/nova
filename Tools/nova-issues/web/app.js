"use strict";

const byId = (id) => document.getElementById(id);
const statusNames = { open: "待处理", closed: "已处理" };
const outcomeNames = {
  executed: "已执行", success: "成功", partial: "部分完成", blocked: "有阻塞",
  failed: "失败", cancelled: "已取消", observed: "已观察",
};
const completedOutcomes = new Set(["executed", "success", "advanced", "recovered"]);
const usageSources = { codex: "Codex", editor: "Editor", legacy: "未标注" };
const usageEntries = {
  action: "Nova Action", skill: "Skill 查阅", "unity-cli": "Unity CLI", "nova-cli": "Nova CLI",
  inspector: "手动操作", "unity-mcp": "Unity MCP", "editor-tool": "Editor 工具", play: "Editor Play",
};
const coverageNames = {
  observed: "已观察", waiting: "待采集", registered: "已注册", not_observed: "未验证",
  connected: "已连接", installed: "已安装", unavailable: "未获得证据", ready: "可用",
  static: "静态证据", interrupted: "已中断", pending: "待分析",
};
const readyCoverageStates = new Set(["observed", "registered", "connected", "installed", "ready"]);
const unityMcpStates = {
  connected: "已连接", listening: "已监听", disconnected: "未连接", unknown: "未验证", stale: "近期未上报",
};
const hookStatuses = {
  complete: ["已加全", "success", "项目钩子和绑定脚本已就绪。授权状态可在 Codex 中查看。"],
  pending: ["待追加", "warning", "工作区中还没有 Nova 自动反馈钩子。"],
  incomplete: ["不完整", "urgent", "工作区的反馈钩子或绑定脚本缺失，或校验未通过。"],
};
const titles = { overview: "总览", projects: "项目", issues: "问题", usage: "使用记录" };
const sortOptions = [["latest", "最新时间"], ["count", "触发次数"]];
const pageSizes = [10, 50, 100, 200];
const listPages = new Map();
const unreadSections = ["projects", "issues", "usage"];
const unreadUnits = { projects: "个项目", issues: "个问题", usage: "组操作" };
const unreadOnly = new Set();
const legacyReadStoragePrefix = "nova-issues.read.v1:";
const readStoragePrefix = "nova-issues.read.v2:";
const date = new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" });
const usageNumber = new Intl.NumberFormat("zh-CN");
const usagePercent = new Intl.NumberFormat("zh-CN", { style: "percent", maximumFractionDigits: 1 });
const usageDimensions = [["module", "模块"], ["operation", "操作"], ["source", "来源"], ["entry", "操作类型"]];
const usageChart = { dimension: "module", search: "", historyOnly: false };
let session = null;
let data = null;
let readProgress = {};
let unreadRows = {};
let view = "overview";
let loadCounter = 0;
let refreshing = false;
let days = 14;
let paintLists = null;
let paintUnreadList = null;
let unreadListNeedsPaint = false;
let projectLive = null;
let dialogHasEdits = null;
const filters = {
  projectActivity: "",
  search: "", issueProject: "", status: "open", issueSort: "latest",
  project: "", module: "", usageSource: "", usageEntry: "", usageSort: "latest",
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

async function request(path, body, method = body === undefined ? "GET" : "POST") {
  const headers = method === "GET" ? {} : { "X-Nova-CSRF": session?.csrf || "" };
  if (body !== undefined) headers["Content-Type"] = "application/json";
  const response = await fetch(path, {
    method,
    cache: "no-store",
    headers,
    body: JSON.stringify(body),
  });
  const result = await response.json();
  if (response.status === 401 && session) setSession(null);
  if (!response.ok) throw new Error(result.error || `请求失败（${response.status}）`);
  return result;
}

/** 同一浏览器按账号和栏目保存连续已读位置及单独查阅的上报。 */
function readKey(section, prefix = readStoragePrefix) {
  return `${prefix}${session.username}:${section}`;
}

/** 合并当前页面和其它标签页记录的已读进度。 */
function currentReadProgress(section) {
  const current = readProgress[section];
  try {
    const saved = JSON.parse(localStorage.getItem(readKey(section)));
    if (Number.isSafeInteger(saved?.after) && saved.after >= 0 && Array.isArray(saved.viewed)) {
      return {
        after: Math.max(current?.after ?? 0, saved.after),
        viewed: new Set([...(current?.viewed ?? []), ...saved.viewed.filter(Number.isSafeInteger)]),
      };
    }
  } catch {
    // 浏览器存储不可用时使用当前页面的进度。
  }
  return current;
}

/** 请求本机已读位置之后的上报，首次使用时沿用旧版进度。 */
function overviewQuery() {
  const query = new URLSearchParams({ days, activity: "1", lists: "0" });
  for (const section of unreadSections) {
    const progress = currentReadProgress(section);
    if (progress) query.set(`${section}_after`, progress.after);
    else {
      try {
        const count = Number(localStorage.getItem(readKey(section, legacyReadStoragePrefix)) ?? -1);
        if (Number.isSafeInteger(count) && count >= 0) query.set(`${section}_seen`, count);
      } catch {
        // 没有已保存的进度时，从服务器当前数据开始计数。
      }
    }
  }
  return query;
}

/** 更新侧栏的未读条目数，按需同步已展示的条目红点和次数增量。 */
function updateUnread({ sidebarOnly = false } = {}) {
  const activity = data?.activity;
  for (const section of unreadSections) {
    const rows = new Map();
    if (session && activity) {
      const { after, items } = activity.sections[section];
      const progress = currentReadProgress(section) || { after, viewed: new Set() };
      let nextAfter = activity.latest;
      for (const [id, key] of items) {
        if (id <= progress.after || progress.viewed.has(id)) continue;
        if (!rows.has(key)) rows.set(key, []);
        rows.get(key).push(id);
        nextAfter = Math.min(nextAfter, id - 1);
      }
      nextAfter = Math.max(progress.after, nextAfter);
      const viewed = [...progress.viewed].filter((id) => id > nextAfter).sort((a, b) => a - b);
      readProgress[section] = { after: nextAfter, viewed: new Set(viewed) };
      try {
        const key = readKey(section);
        const value = JSON.stringify({ after: nextAfter, viewed });
        if (localStorage.getItem(key) !== value) localStorage.setItem(key, value);
      } catch {
        // 继续使用本页已读进度。
      }
    }
    unreadRows[section] = rows;
    let count = rows.size;
    if (section === "usage") {
      const operations = new Set();
      for (const key of rows.keys()) {
        const [project, module, operation] = JSON.parse(key);
        operations.add(JSON.stringify([project, moduleAliases.get(module) || module, operation]));
      }
      count = operations.size;
    }
    const item = byId("nav").querySelector(`[data-view="${section}"]`);
    const badge = item.querySelector(".nav-unread");
    badge.textContent = count || "";
    badge.hidden = count === 0;
    if (count) {
      const label = `${count} ${unreadUnits[section]}有未读上报`;
      item.title = label;
      item.setAttribute("aria-label", `${titles[section]}，${label}`);
    } else {
      item.removeAttribute("title");
      item.removeAttribute("aria-label");
    }
  }
  const markAll = byId("mark-all-read");
  if (markAll) markAll.disabled = !unreadRows[view]?.size;
  if (sidebarOnly) return;
  for (const item of document.querySelectorAll("button[data-unread-section]")) updateUnreadButton(item);
  for (const item of document.querySelectorAll(".unread-increment")) updateUnreadIncrement(item);
}

/** 将本栏截至最近一次刷新收到的上报全部标为已读。 */
function markSectionRead(section) {
  if (!unreadRows[section]?.size) return;
  const progress = readProgress[section];
  progress.after = Math.max(progress.after, data.activity.latest);
  updateUnread();
  refreshUnreadList(section);
}

/** 已读后更新当前筛选结果，详情打开期间保留原有列表。 */
function refreshUnreadList(section) {
  if (view !== section || !unreadOnly.has(section)) return;
  if (byId("details").open) unreadListNeedsPaint = true;
  else paintUnreadList?.({ background: true });
}

/** 只取当前条目渲染时已到达的上报，避免把后台新收到的上报计入旧列表。 */
function unreadItemIds(item) {
  const through = Number(item.dataset.unreadThrough);
  return unreadRows[item.dataset.unreadSection]?.get(item.dataset.unreadKey)?.filter((id) => id <= through) || [];
}

/** 显示条目红点，并向读屏软件提供该条目的未读数量。 */
function updateUnreadButton(item) {
  const indicator = item.querySelector(".row-unread");
  const count = unreadItemIds(item).length;
  indicator.hidden = count === 0;
  indicator.setAttribute("aria-label", `${count} 条未读`);
}

/** 在当前条目显示渲染时已收到且尚未查阅的上报数量。 */
function updateUnreadIncrement(item) {
  const count = unreadItemIds(item).length;
  item.textContent = count ? `+${count}` : "";
  item.hidden = count === 0;
}

/** 在同一条目的总次数后显示当前浏览器尚未查看的上报数。 */
function countWithUnread(section, key, total) {
  const count = node("span", String(total), "count-with-unread");
  const increment = node("span", null, "unread-increment");
  increment.dataset.unreadSection = section;
  increment.dataset.unreadKey = key;
  increment.dataset.unreadThrough = data.activity.latest;
  increment.setAttribute("aria-hidden", "true");
  count.append(increment);
  updateUnreadIncrement(increment);
  return count;
}

/** 打开条目成功后，只标记点击时已出现在该条目上的上报。 */
function unreadButton(section, key, label, open) {
  const item = button(null, async () => {
    const account = session;
    const ids = unreadItemIds(item);
    if (!await open() || account !== session || !ids.length) return;
    const progress = readProgress[section];
    for (const id of ids) progress.viewed.add(id);
    updateUnread();
    refreshUnreadList(section);
  }, "link");
  item.dataset.unreadSection = section;
  item.dataset.unreadKey = key;
  item.dataset.unreadThrough = data.activity.latest;
  const indicator = node("span", null, "row-unread");
  indicator.setAttribute("role", "img");
  item.append(indicator, label);
  updateUnreadButton(item);
  return item;
}

/** 清除上一会话的页面数据与临时已读进度。 */
function setSession(value) {
  session = value;
  data = null;
  readProgress = {};
  unreadRows = {};
  unreadOnly.clear();
  listPages.clear();
  Object.assign(usageChart, { dimension: "module", search: "", historyOnly: false });
  paintLists = null;
  paintUnreadList = null;
  unreadListNeedsPaint = false;
  refreshing = false;
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

/** 按环境快照的上报值显示工具版本和状态。 */
function snapshotToolStatus(item) {
  const status = node("span", item.version ? `${item.version} ` : "");
  status.append(statusPill(coverageNames[item.state] || item.state, readyCoverageStates.has(item.state)));
  return status;
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

/** 各栏目独立开关未读筛选，并从筛选结果的第一页开始查看。 */
function unreadFilter(section, repaint) {
  const label = node("label", null, "unread-filter");
  const input = node("input");
  input.type = "checkbox";
  input.checked = unreadOnly.has(section);
  input.addEventListener("change", () => {
    if (input.checked) unreadOnly.add(section);
    else unreadOnly.delete(section);
    repaint({ reset: true, background: true });
  });
  label.append(input, "仅未读");
  return label;
}

/** 筛选范围改变时回到第一页，保留该列表选定的每页条数。 */
function resetListPage(id) {
  const state = listPages.get(id);
  if (state) state.page = 1;
}

/** 在浏览器中对已加载的结果取页，筛选后页码最多到新的末页。 */
function localListPage(items, page, size) {
  const total = items.length;
  const number = Math.min(page, Math.max(1, Math.ceil(total / size)));
  return { items: items.slice((number - 1) * size, number * size), page: number, total };
}

/** 分析和列表沿用当前浏览器收到的上报及其已读进度。 */
function unreadSelection(section) {
  const through = data.activity.latest;
  const { after, viewed } = currentReadProgress(section) || { after: through, viewed: [] };
  return { after, through, viewed: [...viewed].filter((id) => id > after && id <= through) };
}

/** 让服务端在分页前应用筛选。 */
async function remoteListPage(resource, filters, page, size, onlyUnread = false) {
  const query = new URLSearchParams({ ...filters, page, page_size: size });
  if (onlyUnread && !unreadRows[resource]?.size) return localListPage([], page, size);
  const selection = onlyUnread ? unreadSelection(resource) : undefined;
  const result = await request(`/api/maintainer/${resource}?${query}`, selection);
  return { items: result[resource], ...result.pagination };
}

/** 每张列表独立保存页码和条数，过期请求不能覆盖新的翻页或筛选结果。 */
function paginatedList(id, title, defaultSize, readPage, renderRows) {
  const state = listPages.get(id) || { page: 1, size: defaultSize };
  listPages.set(id, state);
  const element = node("div");
  const results = node("div");
  const footer = node("div", null, "pagination");
  footer.setAttribute("role", "group");
  footer.setAttribute("aria-label", `${title}分页`);
  const summary = node("span", null, "pagination-summary");
  summary.setAttribute("role", "status");
  const sizes = pageSizes.filter((size) => size >= defaultSize).map((size) => [size, `${size} 条`]);
  const sizeControl = control("每页", state.size, sizes, (value) => {
    state.size = Number(value);
    paint({ reset: true });
  });
  sizeControl.querySelector("select").setAttribute("aria-label", `${title}每页条数`);
  const previous = button("上一页", () => move(-1));
  const next = button("下一页", () => move(1));
  previous.disabled = next.disabled = true;
  const navigation = node("div", null, "pagination-buttons");
  navigation.append(previous, next);
  footer.append(summary, sizeControl, navigation);
  element.append(results, footer);

  let timer;
  let latest = 0;
  let lastPage = 1;

  /** 翻页时保留页脚和键盘焦点，待新一页返回后替换表格。 */
  function move(delta) {
    state.page = Math.max(1, Math.min(lastPage, state.page + delta));
    paint({ background: true });
  }

  /** 读取当前页，筛选时清除旧结果，翻页时继续显示现有表格。 */
  function paint({ reset = false, delay = 0, background = false } = {}) {
    if (reset) state.page = 1;
    clearTimeout(timer);
    const run = ++latest;
    if (!background) {
      results.replaceChildren(node("p", "正在读取…", "empty"));
      summary.textContent = "";
      previous.disabled = next.disabled = true;
    }
    results.setAttribute("aria-busy", "true");
    timer = setTimeout(async () => {
      if (!element.isConnected) return;
      try {
        const { items, page, total } = await readPage(state.page, state.size);
        if (run !== latest || !element.isConnected) return;
        state.page = page;
        lastPage = Math.max(1, Math.ceil(total / state.size));
        const content = renderRows(items, page);
        if (results.firstChild !== content) results.replaceChildren(content);
        summary.textContent = total ? `第 ${page} / ${lastPage} 页 · 共 ${total} 条` : "共 0 条";
        previous.disabled = page <= 1;
        next.disabled = page >= lastPage;
      } catch (error) {
        if (run === latest && element.isConnected) {
          results.replaceChildren(node("p", error.message, "error"));
          summary.textContent = "";
          previous.disabled = next.disabled = true;
        }
      } finally {
        if (run === latest) results.removeAttribute("aria-busy");
      }
    }, delay);
  }
  return { element, paint };
}

/** 显示上报确认的包标识及版本，缺少归因时由调用处省略。 */
function issuePackage(packageInfo) {
  if (!packageInfo) return null;
  const label = packageInfo.version ? `${packageInfo.id}@${packageInfo.version}` : packageInfo.id;
  return node("code", label, "issue-package");
}

/** 总览和问题列表共用同一张表，并标记有新发生记录的问题。 */
function issueRows(issues) {
  return table(["问题", "所属包 / 版本", "状态", "影响项目", "发生次数", "最近发生"], issues.map((item) => {
    const projects = button(item.projects, () => openIssueProjects(item.id), "link issue-project-count");
    projects.setAttribute("aria-label", `查看问题 #${item.id} 影响的 ${item.projects} 个项目`);
    const title = unreadButton("issues", String(item.id), `#${item.id}  ${item.title}`, () => openIssue(item.id));
    return [title,
      issuePackage(item.package), statusPill(statusNames[item.status], item.status === "closed"), projects,
      countWithUnread("issues", String(item.id), item.occurrences), stamp(item.last_seen)];
  }));
}

/** 读取当前问题的项目名单，关闭弹窗后忽略该次查询结果。 */
async function openIssueProjects(id) {
  const title = `#${id} 受影响的项目`;
  const loading = node("p", "正在读取项目…", "empty");
  const options = { closeOnBackdrop: true };
  openDialog(title, [loading], options);
  try {
    const { projects } = await request(`/api/maintainer/issues/${id}/projects`);
    if (!loading.isConnected || !byId("details").open) return;
    const list = node("ul", null, "issue-projects");
    for (const project of projects) {
      const item = node("li");
      item.append(unreadButton("projects", project.id, project.name, () => openProject(project.id)));
      list.append(item);
    }
    openDialog(title, [projects.length ? list : node("p", "暂无受影响的项目", "empty")], options);
  } catch (failure) {
    if (loading.isConnected && byId("details").open) openDialog(title, [node("p", failure.message, "error")], options);
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

/** 汇总项目环境和实施状态，并标记新登记或收到新上报的项目。 */
function projectRows(projects, editable = false) {
  return table(["项目", "团队", "Framework版本", "Unity", ...(editable ? ["实施状态"] : []), "最近环境上报"], projects.map((project) => {
    const workspaces = data.workspaces.filter((item) => item.project_id === project.id);
    const implementation = (data.implementation || []).filter((item) => item.project_id === project.id);
    const versions = [...new Set(implementation.map(loadedFrameworkVersion).filter(Boolean))].join("、");
    const unity = [...new Set(workspaces.map((item) => item.unity).filter(Boolean))].join("、");
    const name = unreadButton("projects", project.id, project.name, () => openProject(project.id));
    return [
      name, editable ? teamControl(project) : project.team || "—",
      versions || "待上报", unity || "暂无",
      ...(editable ? [implementationSummary(implementation.length, data.configured_tools?.[project.id] || [])] : []),
      stamp(Math.max(0, ...workspaces.map((item) => item.observed_at))),
    ];
  }));
}

/** 显示项目已确认的三工具配置进度。 */
function implementationSummary(hasReport, configuredTools) {
  if (!hasReport) return statusPill("待上报", false);
  const configured = configuredTools.length;
  return statusPill(`三工具已配置 ${configured}/3`, configured === 3);
}

/** 展示总览数量，点击卡片打开对应栏目并套用筛选。 */
function overview() {
  const grid = node("div", null, "metrics");
  const issueScope = { issueProject: "", search: "" };
  for (const [label, value, section, preset] of [
    ["已登记项目", data.projects.length, "projects", { projectActivity: "" }],
    ["过去 24 小时有上报", data.projects.filter(activeProject).length, "projects", { projectActivity: "24h" }],
    ["待处理问题", data.issue_counts.active, "issues", { ...issueScope, status: "open" }],
    ["已处理问题", data.issue_counts.handled, "issues", { ...issueScope, status: "closed" }],
  ]) {
    const entry = button(null, () => {
      Object.assign(filters, preset);
      unreadOnly.delete(section);
      resetListPage(section);
      openView(section);
      byId("page-title").focus();
    }, "metric");
    entry.append(node("strong", value), node("span", label));
    grid.append(entry);
  }
  const issues = paginatedList("overviewIssues", "待处理问题", 10,
    (page, size) => remoteListPage("issues", { status: "open", sort: "latest" }, page, size), issueRows);
  const projects = paginatedList("overviewProjects", "项目上报", 10,
    (page, size) => localListPage(data.projects, page, size), projectRows);
  paintLists = (options) => {
    issues.paint(options);
    projects.paint(options);
  };
  return [grid, panel("待处理问题", issues.element), panel("项目上报", projects.element)];
}

/** 项目列表与总览共用 24 小时上报口径，切换筛选时只刷新表格。 */
function projectsPage() {
  const toolbar = node("div", null, "toolbar");
  const list = paginatedList("projects", "项目", 10, (page, size) => {
    let projects = data.projects;
    if (filters.projectActivity === "24h") projects = projects.filter(activeProject);
    if (unreadOnly.has("projects")) projects = projects.filter((project) => unreadRows.projects?.has(project.id));
    return localListPage(projects, page, size);
  }, (projects) => projectRows(projects, true));
  toolbar.append(filter("projectActivity", "上报时间", [["", "全部项目"], ["24h", "过去 24 小时有上报"]],
    () => list.paint({ reset: true })), unreadFilter("projects", list.paint));
  paintLists = paintUnreadList = list.paint;
  return panel(null, toolbar, node("p", `近 24 小时上报环境快照 ${usageNumber.format(data.snapshot_count_24h)} 份`, "muted"), list.element);
}

/** 将筛选条件和排序一起交给服务端查询问题。 */
function issuesPage() {
  const toolbar = node("div", null, "toolbar issues-toolbar");
  const nameCounts = new Map();
  for (const { name } of data.projects) nameCounts.set(name, (nameCounts.get(name) || 0) + 1);
  const projects = data.projects.map(({ id, name }) => [id, nameCounts.get(name) > 1 ? `${name}（${id}）` : name]);
  if (filters.issueProject && !projects.some(([id]) => id === filters.issueProject)) {
    filters.issueProject = "";
    resetListPage("issues");
  }
  const search = node("label", "搜索问题", "search");
  const input = node("input");
  input.type = "search";
  input.placeholder = "编号、模块或标题";
  input.value = filters.search;
  const list = paginatedList("issues", "问题", 50, (page, size) => remoteListPage("issues", {
    sort: filters.issueSort, q: filters.search.trim(), project: filters.issueProject, status: filters.status,
  }, page, size, unreadOnly.has("issues")), issueRows);
  input.addEventListener("input", () => {
    filters.search = input.value;
    list.paint({ reset: true, delay: 180 });
  });
  search.append(input);
  const applyFilters = () => list.paint({ reset: true });
  toolbar.append(search, filter("issueProject", "项目", [["", "全部项目"], ...projects], applyFilters),
    filter("status", "状态", [["", "全部"], ...Object.entries(statusNames)], applyFilters),
    filter("issueSort", "排序", sortOptions, applyFilters), unreadFilter("issues", list.paint));
  paintLists = paintUnreadList = list.paint;
  return panel(null, toolbar, list.element);
}

/** 显示当前页的操作汇总，点击名称可查阅记录。 */
function usageRows(items) {
  const details = table(["项目", "模块", "操作", "来源", "操作类型", "Framework", "结果", "记录数", "调用次数", "最近使用"],
    items.map((item) => {
      const name = usageOperationName(item.operation);
      const operation = node(name === item.operation ? "code" : "span", name, "usage-operation");
      const description = item.description || "暂无该操作的说明。";
      operation.title = description;
      const entry = unreadButton("usage", item.activity_key, operation, () => openUsage(description));
      const count = button(null, () => openUsageOccurrences(item, data.days), "link");
      count.append(countWithUnread("usage", item.activity_key, item.count));
      count.setAttribute("aria-label", `查看 ${item.count} 条发生记录`);
      const calls = item.play_calls ?? (item.development_calls || null);
      return [projectName(item.project_id), usageCategoryLabel("module", { id: item.module }), entry,
        usageSources[item.source] || "未标注", usageCategoryLabel("entry", { id: item.entry }), item.framework,
        statusPill(outcomeNames[item.outcome] || item.outcome, completedOutcomes.has(item.outcome)),
        count, calls === null ? "—" : usageNumber.format(calls), stamp(item.last_used)];
    }));
  const headers = details.querySelectorAll("th");
  if (headers.length) {
    headers[7].title = "收到几条记录。点数字可查看每条记录的时间和结果。";
    headers[8].title = "实际调用了几次。一次 Play 上报可以包含多次调用。";
  }
  return details;
}

function usageOperationName(id) {
  return Object.hasOwn(implementationStages, id) ? implementationStages[id] : id;
}

/** 显示工具的产品名称，筛选查询仍使用原标识。 */
function usageModuleName(id) {
  if (id === "upm") return implementationModules.upm;
  return id === "unity-mcp" ? usageEntries[id] : id;
}

/** 使用记录的两张表共享筛选条件，各自保留页大小。 */
function resetUsagePages() {
  resetListPage("usageModules");
  resetListPage("usageDetails");
}

function resetUsageRankings() {
  for (const id of ["usageAnalysis", "usageDevelopment", "usagePlay"]) resetListPage(id);
}

/** 明细与图表共用时间、项目、模块、来源和操作类型条件。 */
function usageScope() {
  return { days: data.days, project: filters.project, module: filters.module, source: filters.usageSource, entry: filters.usageEntry };
}

/** 维度名称沿用使用记录与模块清单，保留历史未知类型。 */
function usageCategoryLabel(dimension, item) {
  if (dimension === "operation") return usageOperationName(item.id);
  if (dimension === "source") return usageSources[item.id] || item.id || "未标注";
  if (dimension === "entry") return usageEntries[item.id] || item.id || "未标注";
  if (dimension === "module") {
    const id = moduleAliases.get(item.id) || item.id;
    if (Object.hasOwn(implementationModules, id)) return implementationModules[id];
    if (id === "bundle") return implementationStages["bundle-build"];
    return item.label || usageModuleName(id);
  }
  return item.id;
}

/** 有采集入口且没有任何已保留调用证据的模块可归入历史无上报。 */
function unreportedUsageModule(item) {
  return item.tracked && !item.history && !item.tool_history && !item.play_history;
}

/** 以当前排行的总次数为分母，保留未采集次数的条目。 */
function usageChartRow(dimension, { item, title, count }, total) {
  const row = node("li", null, "usage-chart-row");
  const label = node("span", null, "usage-chart-label");
  const name = node(dimension === "operation" && title === item.id ? "code" : "span", title);
  name.title = item.description || item.id;
  label.append(name);
  if (dimension !== "module" && !count) {
    label.append(node("small", item.history ? "有历史记录" : "历史无上报",
      item.history ? "muted" : "usage-history-empty"));
  }

  const share = total && count !== null ? count / total : 0;
  const bar = node("span", null, "usage-chart-track");
  bar.setAttribute("aria-hidden", "true");
  if (share) {
    const fill = node("span");
    fill.style.width = `${share * 100}%`;
    bar.append(fill);
  }
  const percent = share > 0 && share < .001 ? "<0.1%" : usagePercent.format(share);
  row.append(label, bar, node("span", count === null ? "—" : usageNumber.format(count), "usage-chart-value"),
    node("span", count === null ? "—" : percent, "usage-chart-value"));
  return row;
}

/** 各排行独立排序和翻页，占比使用搜索前的总次数。 */
function usageRanking(id, title, emptyMessage = "没有匹配的类型") {
  let rows = [];
  let total = 0;
  let dimension = "module";
  const chart = paginatedList(id, title, 10, (page, size) => localListPage(rows, page, size), (visible) => {
    if (!visible.length) return node("p", emptyMessage, "empty");
    const header = node("div", null, "usage-chart-row usage-chart-header");
    header.append(node("span", "名称"), node("span"), node("span", "次数"), node("span", "占比"));
    const list = node("ul", null, "usage-chart-list");
    for (const row of visible) list.append(usageChartRow(dimension, row, total));
    const content = document.createDocumentFragment();
    content.append(header, list);
    return content;
  });
  chart.element.classList.add("usage-ranking-list");
  chart.element.setAttribute("role", "region");
  chart.element.setAttribute("aria-label", title);
  return {
    element: chart.element,
    show(items, chartDimension, chartTotal, countFor) {
      dimension = chartDimension;
      total = chartTotal;
      rows = items.map(({ item, title }) => ({ item, title, count: countFor(item) }))
        .sort((a, b) => (b.count ?? -1) - (a.count ?? -1) || a.title.localeCompare(b.title, "zh-CN"));
      chart.paint({ background: true });
    },
  };
}

/** 按页显示使用分析，切换维度、搜索和翻页复用已加载的数据。 */
function usageAnalysis() {
  const element = node("section", null, "usage-analysis");
  const totals = node("div", null, "usage-analysis-totals");
  const metrics = ["记录数", "开发调用", "Skill 查阅（按轮）", "历史无调用上报的模块"].map((label) => {
    const tile = node("div", null, "usage-analysis-metric");
    const number = node("strong", "—");
    tile.append(node("span", label), number);
    totals.append(tile);
    return number;
  });
  const controls = node("div", null, "usage-analysis-controls");
  const tabs = node("div", null, "usage-analysis-dimensions");
  tabs.setAttribute("role", "group");
  tabs.setAttribute("aria-label", "使用分析维度");
  const search = node("input");
  search.type = "search";
  search.placeholder = "搜索名称";
  search.setAttribute("aria-label", "搜索当前维度");
  search.value = usageChart.search;
  search.addEventListener("input", () => {
    usageChart.search = search.value;
    showChart({ reset: true });
  });
  const choices = usageDimensions.map(([id, label]) => button(label, () => {
    if (usageChart.dimension === id) return;
    Object.assign(usageChart, { dimension: id, search: "" });
    search.value = "";
    showChart({ reset: true });
  }, "secondary"));
  tabs.append(...choices);
  controls.append(tabs, search);
  const historyControl = node("label");
  const history = node("input");
  history.type = "checkbox";
  history.checked = usageChart.historyOnly;
  history.addEventListener("change", () => {
    usageChart.historyOnly = history.checked;
    resetListPage("usageDevelopment");
    showChart();
  });
  historyControl.append(history, "只看历史无上报");

  const note = node("p", null, "usage-analysis-note muted");
  const status = node("p", "正在读取使用分析…", "empty");
  status.setAttribute("role", "status");
  const chart = usageRanking("usageAnalysis", "使用分析");
  chart.element.setAttribute("aria-label", "使用次数与占比");
  const development = usageRanking("usageDevelopment", "开发次数");
  const play = usageRanking("usagePlay", "Play 次数", "暂无匹配的 Play 上报");
  const rankings = node("div", null, "usage-module-rankings");
  const developmentHeading = node("div", null, "usage-ranking-heading");
  developmentHeading.append(node("h3", "开发次数"), historyControl);
  const playHeading = node("div", null, "usage-ranking-heading");
  playHeading.append(node("h3", "Play 次数"));
  const developmentSection = node("section");
  developmentSection.append(developmentHeading, development.element);
  const playSection = node("section");
  playSection.append(playHeading, play.element);
  rankings.append(developmentSection, playSection);
  element.append(node("h2", "使用分析"), totals, controls, note, rankings, chart.element, status);

  let payload = null;
  let scope = "";
  let latest = 0;

  /** 更新当前维度的完整排行，再用分页展示符合条件的条目。 */
  function showChart({ reset = false } = {}) {
    if (reset) resetUsageRankings();
    const dimension = usageChart.dimension;
    choices.forEach((choice, index) => choice.setAttribute("aria-pressed", String(usageDimensions[index][0] === dimension)));
    search.hidden = dimension === "entry" || dimension === "source";
    const modules = dimension === "module";
    note.textContent = modules
      ? "开发次数包含手动操作。一次 Play 上报可包含多次调用，旧版只记录场次时显示「—」。"
      : "占比按操作明细的记录数计算，含 Skill 查阅。历史状态查看所选项目的全部上报时间。";
    rankings.hidden = !payload || !modules;
    chart.element.hidden = !payload || modules;
    if (!payload) return;
    const query = usageChart.search.trim().toLowerCase();
    const rows = payload.dimensions[dimension].map((item) => ({ item, title: usageCategoryLabel(dimension, item) }))
      .filter(({ item, title }) => !query || `${title} ${item.id} ${item.description || ""}`.toLowerCase().includes(query));
    if (!modules) {
      chart.show(rows, dimension, payload.total, (item) => item.count);
      return;
    }
    const matching = rows.filter(({ item }) => !payload.selected_module || payload.selected_module === item.id);
    const developmentRows = usageChart.historyOnly ? matching.filter(({ item }) => unreportedUsageModule(item)) : matching;
    const playRows = !filters.usageSource || filters.usageSource === "editor"
      ? matching.filter(({ item }) => item.play_history) : [];
    development.show(developmentRows, dimension, payload.development_calls,
      (item) => item.tracked ? item.development_calls : null);
    play.show(playRows, dimension, payload.play_calls, (item) => item.play_calls);
  }

  /** 按当前筛选更新图表，旧请求返回时不覆盖新范围。 */
  async function paint() {
    const query = new URLSearchParams(usageScope()).toString();
    const selection = unreadOnly.has("usage") ? unreadSelection("usage") : undefined;
    const scopeKey = JSON.stringify([query, selection]);
    const run = ++latest;
    if (scopeKey !== scope) {
      const reset = Boolean(scope);
      scope = scopeKey;
      payload = null;
      for (const metric of metrics) metric.textContent = "—";
      status.textContent = "正在读取使用分析…";
      status.hidden = false;
      showChart({ reset });
    }
    try {
      const result = await request(`/api/maintainer/usage-analysis?${query}`, selection);
      if (run !== latest || !element.isConnected) return;
      payload = result;
      const neverReported = result.dimensions.module.filter(unreportedUsageModule).length;
      [result.total, result.development_calls, result.skill_turns, neverReported].forEach((value, index) => {
        metrics[index].textContent = usageNumber.format(value);
      });
      status.hidden = true;
      showChart();
    } catch (error) {
      if (run === latest && element.isConnected) {
        status.textContent = error.message;
        status.hidden = false;
      }
    }
  }
  return { element, paint };
}

/** 按实际触发时间或次数展示使用汇总和操作明细。 */
function usagePage() {
  const toolbar = node("div", null, "toolbar usage-toolbar");
  if (filters.project && !data.projects.some((item) => item.id === filters.project)) {
    filters.project = "";
    resetUsagePages();
  }
  if (filters.module && !data.usage_modules.includes(filters.module)) {
    filters.module = "";
    resetUsagePages();
  }
  const modules = paginatedList("usageModules", "项目模块调用", 50, (page, size) => {
    const items = data.codex_modules.filter((item) => (!filters.project || item.project_id === filters.project) &&
      (!filters.module || item.module === filters.module));
    if (filters.usageSort === "latest") items.sort((a, b) => b.last_used - a.last_used);
    return localListPage(items, page, size);
  }, (items) => table(["项目", "模块", "调用次数", "Skill 查阅次数（按轮）", "失败或阻塞", "最近使用"],
    items.map((item) => [projectName(item.project_id), usageModuleName(item.module), item.calls,
      item.skill_turns, item.problems, stamp(item.last_used)])));
  const details = paginatedList("usageDetails", "操作明细", 50, (page, size) => remoteListPage("usage",
    { ...usageScope(), sort: filters.usageSort }, page, size, unreadOnly.has("usage")), usageRows);
  const analysis = usageAnalysis();
  const moduleSection = node("div", null, "usage-module-summary");
  moduleSection.append(node("h2", "项目模块调用"), modules.element);
  const detailsToolbar = node("div", null, "toolbar usage-details-toolbar");
  const paintDetails = (options) => {
    void analysis.paint();
    details.paint(options);
  };
  detailsToolbar.append(node("h2", "操作明细"), unreadFilter("usage", paintDetails));
  const paintModules = (options) => {
    // 该汇总按模块累计所有类型，选定类型时由使用分析和明细展示结果。
    moduleSection.hidden = filters.usageSource !== "codex" || Boolean(filters.usageEntry);
    if (!moduleSection.hidden) modules.paint(options);
  };
  paintLists = (options) => {
    paintDetails(options);
    paintModules(options);
  };
  paintUnreadList = paintDetails;
  const applyFilters = () => {
    resetUsagePages();
    paintLists({ reset: true });
  };
  toolbar.append(filter("usageSource", "来源", [["", "全部"], ...Object.entries(usageSources)], applyFilters),
    filter("usageEntry", "操作类型", [["", "全部"], ...Object.entries(usageEntries), ["legacy", "未标注"]], applyFilters),
    filter("project", "项目", [["", "全部"], ...data.projects.map((item) => [item.id, item.name])], applyFilters),
    filter("module", "模块", [["", "全部"], ...data.usage_modules.map((id) => [id, usageCategoryLabel("module", { id })])], applyFilters),
    control("最近", days, [[1, "1 天"], [7, "7 天"], [14, "14 天"], [30, "30 天"]], (value) => {
      days = Number(value);
      resetUsageRankings();
      refresh(true);
    }), filter("usageSort", "排序", [["latest", "最新时间"], ["count", "记录数"]], () => {
      resetUsagePages();
      paintModules({ reset: true });
      details.paint({ reset: true });
    }));
  return panel(null, toolbar, analysis.element, moduleSection, detailsToolbar, details.element);
}

/** 根据当前栏目更新标题、侧栏、操作按钮和内容。 */
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
  if (unreadSections.includes(view)) {
    const section = view;
    const markAll = button("全部已读", () => markSectionRead(section));
    markAll.id = "mark-all-read";
    markAll.title = "标记本栏全部未读上报，包括筛选隐藏的条目";
    markAll.disabled = !unreadRows[section]?.size;
    actions.append(markAll);
  }
  if (view === "projects") actions.append(button("添加项目", registerProject, "primary"));
  const pages = { overview, issues: issuesPage, projects: projectsPage, usage: usagePage };
  paintUnreadList = null;
  unreadListNeedsPaint = false;
  const content = pages[view]();
  byId("content").replaceChildren(...(Array.isArray(content) ? content : [content]));
  paintLists();
}

/** 后台只检查新上报，完整刷新由打开页面或明确操作触发。 */
async function refresh(refreshPage = false) {
  if (!session || (!refreshPage && refreshing)) return;
  const run = ++loadCounter;
  refreshing = true;
  try {
    const latest = await request(`/api/maintainer/overview?${overviewQuery()}`);
    if (run !== loadCounter) return;
    if (data && data.days !== latest.days) resetUsagePages();
    data = latest;
    const renderPage = refreshPage || !paintLists;
    updateUnread({ sidebarOnly: !renderPage });
    if (renderPage) {
      showError(byId("page-error"), "");
      render();
    }
    void refreshProjectDetail();
  } catch (error) {
    if (refreshPage && session && run === loadCounter) showError(byId("page-error"), error.message);
  } finally {
    if (run === loadCounter) refreshing = false;
  }
}

/** 打开共用弹窗，按当前内容设置遮罩关闭和草稿检查。 */
function openDialog(title, children, { closeOnBackdrop = false, hasEdits = null } = {}) {
  projectLive = null;
  dialogHasEdits = hasEdits;
  const dialog = byId("details");
  dialog.toggleAttribute("data-close-on-backdrop", closeOnBackdrop);
  byId("detail-title").textContent = title;
  byId("detail-body").replaceChildren(...children);
  if (!dialog.open) dialog.showModal();
}

/** 打开操作说明，供使用记录登记已读状态。 */
function openUsage(description) {
  openDialog("操作说明", [node("p", description, "usage-description")], { closeOnBackdrop: true });
  return true;
}

/** 已知的 Play 次数按原上报列出，每条操作保留实际结果和时间。 */
function openUsageOccurrences(item, days) {
  const isPlay = item.entry === "play";
  const columns = ["发生时间", "结果"];
  if (isPlay) columns.push("调用次数");
  const list = paginatedList("usageOccurrences", "发生记录", 10,
    (page, size) => remoteListPage("usage-occurrences", { key: item.activity_key, days }, page, size),
    (items) => table(columns, items.map((record) => {
      const cells = [preciseStamp(record.observed_at), outcomeNames[item.outcome] || item.outcome];
      if (isPlay) cells.push(record.play_calls == null ? "—" : usageNumber.format(record.play_calls));
      return cells;
    })));
  openDialog("发生记录", [list.element], { closeOnBackdrop: true });
  list.paint({ reset: true });
}

function closeDialog() {
  if (dialogHasEdits?.() && !window.confirm("有未保存的修改。确定放弃修改并关闭吗？")) return;
  byId("details").close();
}

/** 先确认，再删除服务端记录。失败时保留详情和错误提示。 */
function deleteAction(path, message) {
  const footer = node("div", null, "dialog-delete");
  const error = node("p", "", "error");
  error.hidden = true;
  error.setAttribute("role", "alert");
  const remove = button("删除", async () => {
    if (!window.confirm(message)) return;
    remove.disabled = true;
    showError(error, "");
    try {
      await request(path, undefined, "DELETE");
      if (footer.isConnected && byId("details").open) byId("details").close();
      await refresh(true);
    } catch (failure) {
      showError(error, failure.message);
      remove.disabled = false;
    }
  }, "danger");
  footer.append(remove, error);
  return footer;
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

/** 显示该份快照上报的钩子状态和实测数量。 */
function hookStatus(snapshot) {
  const status = hookStatuses[snapshot.codex_hooks];
  if (!status) return statusPill("待上报", false);
  const [text, tone, title] = status;
  const counts = snapshot.codex_hook_counts;
  const value = pill(counts ? `${text}（${counts.added}/${counts.expected}）` : text, tone);
  value.title = counts ? title : `${title} 这份快照未记录钩子数量。`;
  return value;
}

/** 按当前可核实的版本分组，查询更新时保留各组的展开状态。 */
function renderPackageGroups(element, packages, versions) {
  function rank(item) {
    if (item.id.startsWith("com.solotopia.")) return 0;
    return versions.get(item.id)?.registry === "internal" ? 1 : 2;
  }

  const groups = {
    different: { label: "非最新版本", rows: [] },
    latest: { label: "最新版本", rows: [] },
    builtin: { label: "随 Unity 版本", rows: [] },
    other: { label: "其他版本", rows: [] },
  };
  const ordered = [...packages].sort((a, b) => rank(a) - rank(b) || a.id.localeCompare(b.id));
  for (const item of ordered) {
    const found = versions.get(item.id);
    const followsUnity = item.source === "builtin";
    const latest = followsUnity ? null : found?.latest;
    const canCompare = latest && item.version && item.version !== "unknown";
    let current = node("span", item.version);
    let group = followsUnity ? groups.builtin : groups.other;
    if (canCompare) {
      const isLatest = item.version === latest;
      group = isLatest ? groups.latest : groups.different;
      const status = found.registry === "third-party"
        ? (isLatest ? "已是当前 Unity 的最新兼容版本" : "与当前 Unity 的最新兼容版本不同")
        : (isLatest ? "已是最新" : "与仓库最新版本不同");
      current = pill(item.version, isLatest ? "success" : "warning");
      current.title = status;
      current.setAttribute("aria-label", `${item.version}，${status}`);
    }
    group.rows.push([node("code", item.id), current, latest || (followsUnity ? "随 Unity" : "—")]);
  }
  Object.values(groups).forEach(({ label, rows }, index) => {
    let section = element.children[index];
    if (!section) {
      section = node("details");
      section.append(node("summary"), node("div"));
      element.append(section);
    }
    section.firstElementChild.textContent = `${label}（${rows.length}）`;
    const list = table(["已发现包", "当前版本", "最新版本"], rows);
    list.classList.add("package-list");
    section.lastElementChild.replaceChildren(list);
  });
}

const implementationStages = {
  "config-export": "Config 导出", "config-load": "Editor 读取 Config",
  "pipify-batch": "Pipify Batch", "plugpals-query": "PlugPals 仓库查询",
  "bundle-build": "Bundle 构建", "player-build": "目标平台构建", "editor-module": "Play 模块调用",
};
const moduleAliases = new Map([["persist", "persistence"], ["vibrate", "vibration"]]);
const implementationModules = {
  config: "Config", pipify: "Pipify", upm: "PlugPals", procedure: "流程", asset: "资源",
  table: "表", localization: "本地化", ui: "UI", network: "网络", sdk: "SDK",
  persistence: "存档", event: "事件", sound: "声音", vibration: "振动",
};
const buildKinds = { player: "Player", project: "平台工程" };
const workflowStages = {
  started: "任务开始", entry: "使用入口", lookup: "查阅资料", check: "执行检查",
  plan: "计划", execute: "执行", verify: "验证", wait: "等待回执", finished: "本轮结束",
  interrupted: "中断", startup: "Editor 启动", upgrade: "Framework 升级",
  queue: "队列恢复", sign: "打包签名", registry: "仓库发布", github: "GitHub 同步",
};
const workflowOperations = {
  "nova-task": "Nova 任务", "project-rules": "工程协作规则", "nova-project-action": "Nova Action 发现",
  "nova-constraints": "Nova 开发约束", "nova-docs": "Nova 文档", "nova-minds": "Nova 知识库",
  "editor-ready": "Editor 就绪", "editor-compilation": "Editor 编译",
  "framework-loaded": "Framework 已载入", "framework-compilation": "Framework 编译",
  "reports-resumed": "待发上报恢复", "public-sync": "公开仓同步",
};
const preciseDate = new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "medium" });

function preciseStamp(value) {
  return value ? preciseDate.format(value * 1000) : "—";
}

function openWorkflow(project, trace = "") {
  const list = paginatedList("projectWorkflow", "协作记录", 10, async (page, size) => {
    const query = new URLSearchParams({ trace, page, page_size: size });
    const result = await request(`/api/maintainer/projects/${encodeURIComponent(project.id)}/workflow?${query}`);
    return { items: result.workflow, ...result.pagination };
  }, (items) => table(["发生时间", "工作区", "来源", "阶段", "记录", "状态", "编号"], items.map((row) => {
    const item = row.data;
    const outcome = node("span", outcomeNames[item.outcome] || item.outcome);
    if (item.reason) outcome.append(` · ${item.reason}`);
    if (item.stage === "queue") outcome.append(` · 待发 ${item.pending} · 重试 ${item.retries}`);
    const scope = node("div");
    for (const [id, label] of [[item.trace, "任务"], [item.receipt, "操作"]]) {
      if (!id || (label === "操作" && id === item.trace)) continue;
      const entry = button(`${label} ${id.slice(0, 8)}`, () => openWorkflow(project, id), "link");
      entry.title = id;
      scope.append(entry, " ");
    }
    const operation = workflowOperations[item.operation] || item.operation;
    const versions = [item.previous_version, item.target_version].filter(Boolean);
    const source = item.stage === "queue" ? "本地队列" : usageSources[item.source] || "发布工具";
    return [preciseStamp(row.observed_at), row.workspace.slice(0, 8), source,
      workflowStages[item.stage] || item.stage, [operation, versions.join(" → ")].filter(Boolean).join(" · "), outcome, scope];
  })));
  const controls = node("div", null, "toolbar");
  controls.append(button("返回项目", () => openProject(project.id)));
  if (trace) controls.append(button("查看全部记录", () => openWorkflow(project)));
  openDialog(`协作记录 · ${project.name}${trace ? ` · ${trace.slice(0, 8)}` : ""}`, [controls, list.element],
    { closeOnBackdrop: true });
  list.paint({ reset: true });
}

// 环境快照用短名展示已启用的 SDK 和 Kit。
const enabledModuleNames = new Map([
  ["NovaFramework.SDK.AdPlugin.Runtime.AdPluginConfig", "Ad"],
  ["NovaFramework.SDK.AIHelp.Runtime.AIHelpPluginConfig", "AIHelp"],
  ["NovaFramework.SDK.AppleSignIn.AppleSignInPluginConfig", "AppleSignIn"],
  ["NovaFramework.SDK.AppsFlyerPlugin.Runtime.AppsFlyerPluginConfig", "AppsFlyer"],
  ["NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime.DataMasterPluginConfig", "DataMaster"],
  ["NovaFramework.SDK.Facebook.FacebookPluginConfig", "Facebook"],
  ["NovaFramework.SDK.FirebasePlugin.Runtime.FirebasePluginConfig", "Firebase"],
  ["NovaFramework.SDK.GoogleSignIn.GoogleSignInPluginConfig", "GoogleSignIn"],
  ["NovaFramework.SDK.IAP.Runtime.IAPPluginConfig", "IAP"],
  ["NovaFramework.SDK.TGAPlugin.Runtime.TGAPluginConfig", "TGA"],
  ["NovaFramework.SDK.WeChatMiniGame.Runtime.WeChatMiniGamePluginConfig", "WeChatMiniGame"],
  ["NovaFramework.Kit.Network.GameLogin.Runtime.LoginKitConfig", "GameLogin"],
  ["NovaFramework.Kit.Network.GameSave.Runtime.SaveKitConfig", "GameSave"],
  ["NovaFramework.Kit.Network.GameBind.Runtime.BindKitConfig", "GameBind"],
]);

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

/** 显示实际执行结果及其版本和工作区。 */
function implementationResult(fact) {
  const item = fact.data;
  const label = outcomeNames[item.outcome] || item.outcome;
  const status = statusPill(label, completedOutcomes.has(item.outcome));
  status.title = `Framework ${item.framework} · Unity ${item.unity} · 本地工程 ${fact.workspace.slice(0, 8)}`;
  return status;
}

function implementationVerification(success) {
  const status = statusPill(success ? "已验证" : "待验证", Boolean(success));
  if (success) {
    const { stage, framework, unity } = success.data;
    status.title = `${implementationStages[stage]} · ${stamp(success.observed_at)} · Framework ${framework} · Unity ${unity} · 本地工程 ${success.workspace.slice(0, 8)}`;
    status.setAttribute("aria-label", `已验证 · ${status.title}`);
  }
  return status;
}

function sameVerificationScope(first, second) {
  return first.workspace === second.workspace && first.data.framework === second.data.framework && first.data.unity === second.data.unity;
}

function timedImplementationResult(row, label = "") {
  const value = node("span", label ? `${label} · ` : null);
  value.append(implementationResult(row), ` · ${stamp(row.observed_at)}`);
  return value;
}

function implementationLabel({ data }) {
  const stage = implementationStages[data.stage];
  return data.step && data.stage === "pipify-batch" ? `${stage} · ${data.step}` : stage;
}

/** 按构建类型汇总最近结果，每个项目独立翻页。 */
function projectBuildPanel(projectId, outcomes) {
  const builds = new Map();
  for (const row of outcomes) {
    const item = row.data;
    if (item.stage !== "player-build" || !["Android", "iOS", "WebGL"].includes(item.platform) || item.build_kind === "scripts") continue;
    const key = `${item.platform}:${item.build_kind}:${item.development}:${Boolean(item.batch_mode)}`;
    if (!builds.has(key)) builds.set(key, { latest: row, success: null });
    if (item.outcome === "success" && sameVerificationScope(row, builds.get(key).latest)) builds.get(key).success = row;
  }
  const title = "项目构建记录";
  if (!builds.size) return panel(title, node("p", "暂无 Android / iOS / WebGL 构建记录", "empty"));
  const items = [...builds.values()];
  const list = paginatedList(`projectBuilds:${projectId}`, title, 10,
    (page, size) => localListPage(items, page, size),
    (rows) => table(["构建", "入口", "验证", "最近结果"], rows.map(({ latest, success }) => {
      const { platform, build_kind, development, batch_mode } = latest.data;
      return [`${platform} ${buildKinds[build_kind]} · ${development ? "开发构建" : "普通构建"}`, batch_mode ? "批处理" : "Editor",
        implementationVerification(success), timedImplementationResult(latest)];
    })));
  list.paint();
  return panel(title, list.element);
}

/** 从按采集时间倒序排列的环境快照中取最近一次 Unity CLI 状态。 */
function latestUnityCliStatus(snapshots = []) {
  const snapshot = snapshots.find(({ data }) => data.tools?.["unity-cli"]);
  if (!snapshot) return null;
  return { tool: snapshot.data.tools["unity-cli"], observedAt: snapshot.observed_at };
}

/** 按各工作区的最近一次观察展示状态，静默工作区在十分钟后标为近期未上报。 */
function unityMcpConnections(result) {
  const serverTime = data?.server_time || 0;
  return (result.unity_mcp_connections || []).map((row) => ({
    observedAt: row.observed_at,
    state: serverTime - row.observed_at > 600 ? "stale" : row.data.state,
  }));
}

/** 汇总同项目的工作区状态，让连接和未连接可同时显示。 */
function unityMcpConfiguration(connections) {
  if (!connections.length) return node("span", "暂无状态上报", "muted");
  const counts = new Map();
  for (const { state } of connections) counts.set(state, (counts.get(state) || 0) + 1);
  const value = node("span", null, "status-line");
  for (const [state, count] of counts) {
    const text = unityMcpStates[state];
    value.append(statusPill(connections.length > 1 ? `${text} ${count}` : text,
      state === "connected" || state === "listening"));
  }
  value.title = "各工作区最近上报的 Editor 桥接状态。「已监听」表示 stdio 端口已启动。";
  return value;
}

/** 工具配置只按工作区的独立上报显示。 */
function toolConfigurationStatus(hasWorkspaceReport, configured) {
  return hasWorkspaceReport ? statusPill(configured ? "已配置" : "待检查", configured)
    : node("span", "暂无配置上报", "muted");
}

/** 普通模块按实际上报显示使用状态。 */
function moduleUseStatus(evidence, registeredAt) {
  if (evidence?.last_success) {
    const status = statusPill("有成功记录", true);
    status.title = `最近成功 · ${stamp(evidence.last_success)}`;
    return status;
  }
  if (registeredAt) return statusPill("已注册", true);
  return evidence?.last_use ? statusPill("待验证", false) : node("span", "暂无上报", "muted");
}

/** 显示各模块的状态和两类调用次数，最近时间在末列统一显示。 */
function moduleUsagePanel(usage, hasWorkspaceReport, configuredTools, outcomes, unityCli, unityMcp) {
  const modules = new Set([...Object.keys(implementationModules), ...usage.keys()]);
  if (unityCli) modules.add("unity-cli");
  if (unityMcp.length) modules.add("unity-mcp");
  const toolKeys = { config: "config", pipify: "pipify", upm: "plugpals" };
  const tableReadyAt = outcomes.find(({ data }) => data.stage === "table-ready")?.observed_at || 0;
  const rows = [...modules].map((id) => {
    const evidence = usage.get(id);
    const isUnityCli = id === "unity-cli";
    const isUnityMcp = id === "unity-mcp";
    const tool = toolKeys[id];
    const registeredAt = id === "table" ? tableReadyAt : 0;
    let configuration;
    if (tool) {
      configuration = toolConfigurationStatus(hasWorkspaceReport, configuredTools.has(tool));
    } else if (isUnityCli) {
      configuration = unityCli ? snapshotToolStatus(unityCli.tool) : node("span", "暂无状态上报", "muted");
      if (unityCli) configuration.title = `环境快照 · ${stamp(unityCli.observedAt)}`;
    } else if (isUnityMcp) {
      configuration = unityMcpConfiguration(unityMcp);
    } else {
      configuration = moduleUseStatus(evidence, registeredAt);
    }

    const latest = Math.max(evidence?.last_use || 0, registeredAt,
      isUnityCli ? unityCli?.observedAt || 0 : 0, isUnityMcp ? unityMcp[0]?.observedAt || 0 : 0);
    return [implementationModules[id] || usageModuleName(id), configuration,
      evidence?.development_calls || 0, evidence?.play_calls || 0, stamp(latest)];
  });
  const summary = table(["模块", "状态", "开发调用次数", "Play 调用次数", "最近记录"], rows);
  const headers = summary.querySelectorAll("th");
  headers[2].title = "执行开发工具、导出或命令的次数。";
  headers[3].title = "Unity Editor Play 中成功调用模块入口的次数。旧版按场次上报的记录不计入。";
  summary.classList.add("module-usage");
  return panel("模块接入与使用", node("p", "次数按项目统计近 30 天。", "muted"), summary);
}

/** 汇总项目的工具验证、模块使用和构建记录。 */
function implementationPanel(result) {
  const workspaceFacts = (result.implementation || []).filter((row) => row.data.stage === "workspace");
  const workspaces = workspaceFacts.map((row) => row.data);
  const outcomes = result.implementation_outcomes || [];
  const issue = result.upgrade_issues?.[0];
  const usage = new Map((result.project_module_evidence || []).map((row) => [row.module, row]));
  const unityCli = latestUnityCliStatus(result.snapshots);
  const unityMcp = unityMcpConnections(result);
  const hasWorkspaceReport = workspaces.length > 0;
  if (!hasWorkspaceReport && !outcomes.length && !usage.size && !issue && !unityCli && !unityMcp.length) {
    return panel("工程实施", node("p", "等待项目首次上报实施记录。", "empty"));
  }
  const configuredTools = new Set(result.configured_tools || []);
  const scopeName = (item) => [item.platform, item.channel, item.mode].filter(Boolean).join(" · ") || "工程级";
  const latestAt = Math.max(result.archived_play_at || 0, ...[...workspaceFacts, ...outcomes].map((item) => item.observed_at),
    issue?.observed_at || 0);
  return panel("工程实施",
    fields([["最近上报坐标", workspaces.length ? scopeName(workspaces[0]) : "待上报"],
      ["最近实施记录", stamp(latestAt)],
      ["最近编译或导入异常", issue ? button(`#${issue.issue_id} · ${stamp(issue.observed_at)}`,
        () => openIssue(issue.issue_id), "link") : "暂无记录"]]),
    table(["工具", "配置", "验证", "最近结果"], [["config", "Config", ["config-load", "config-export"]],
      ["pipify", "Pipify", ["pipify-batch"]], ["plugpals", "PlugPals", ["plugpals-query"]]].map(([id, label, stages]) => {
      const recent = outcomes.find((row) => stages.includes(row.data.stage));
      const success = recent && outcomes.find((row) => stages.includes(row.data.stage) &&
        row.data.outcome === "success" && sameVerificationScope(row, recent));
      const configured = configuredTools.has(id);
      return [label, toolConfigurationStatus(hasWorkspaceReport, configured), implementationVerification(success),
        recent ? timedImplementationResult(recent, implementationLabel(recent)) : "暂无记录"];
    })),
    moduleUsagePanel(usage, hasWorkspaceReport, configuredTools, outcomes, unityCli, unityMcp),
    projectBuildPanel(result.project.id, outcomes));
}

/** 提取会影响工程实施面板的上报，工具状态变化时同样刷新。 */
function implementationEvidence(result) {
  return JSON.stringify([result.implementation, result.configured_tools, result.implementation_outcomes,
    result.project_module_evidence, result.archived_play_at, result.upgrade_issues, latestUnityCliStatus(result.snapshots),
    unityMcpConnections(result)]);
}

/** 展示环境信息、采集状态及按需读取的包清单。 */
function snapshotPanel(snapshot, path) {
  const snap = snapshot.data;
  const frameworkVersion = snap.editor_framework ?? snap.framework;
  const nova = frameworkVersion && frameworkVersion !== "unknown" ? frameworkVersion : "待上报";
  const unity = snap.unity && snap.unity !== "unknown" ? snap.unity : "待上报";
  const reporter = snap.git_user_name || "暂无 Git 名称";
  const section = node("details");
  const packageSection = node("section", null, "snapshot-packages");
  const packages = node("div", null, "snapshot-package-groups");
  const packageStatus = node("p", null, "empty");
  packageStatus.hidden = true;
  packageSection.append(node("h3", "包清单"), packages, packageStatus);
  let loading = false;
  let complete = false;
  section.addEventListener("toggle", async () => {
    if (!section.open || loading || complete) return;
    if (!snap.packages.length) {
      packageStatus.textContent = "暂无已发现包";
      packageStatus.hidden = false;
      complete = true;
      return;
    }

    loading = true;
    const route = `${path}/package-versions?snapshot=${encodeURIComponent(snapshot.id)}`;
    packageStatus.textContent = "正在读取包版本…";
    packageStatus.hidden = false;
    try {
      const attempts = 6;
      for (let attempt = 0; attempt < attempts; attempt++) {
        if (!section.isConnected || !section.open || !byId("details").open) return;
        const { packages: versions, pending } = await request(route);
        if (!section.isConnected || !byId("details").open) return;
        renderPackageGroups(packages, snap.packages, new Map(Object.entries(versions)));
        complete = !pending;
        if (complete || !section.open || attempt === attempts - 1) return;
        packageStatus.textContent = "正在检查更多包版本…";
        await new Promise((resolve) => setTimeout(resolve, 700));
      }
    } catch {
      if (!packages.childElementCount) renderPackageGroups(packages, snap.packages, new Map());
    } finally {
      packageStatus.hidden = true;
      loading = false;
    }
  });
  section.append(node("summary", `Unity ${unity} · Nova ${nova} · ${reporter} · ${stamp(snapshot.observed_at)}`),
    fields([["Framework版本", nova], ["Unity", unity], ["上报人", reporter],
      ["操作系统", { macos: "macOS", windows: "Windows", linux: "Linux" }[snap.operating_system] || "待上报"],
      ["游戏包名", gamePackageNames(snap.application_identifiers)], ["钩子追加", hookStatus(snap)],
      ["已启用", enabledModuleList(snap.enabled)],
      ...Object.entries(snap.coverage).map(([name, state]) => [`${name} 采集`,
        statusPill(coverageNames[state] || state, readyCoverageStates.has(state))]),
      ...Object.entries(snap.tools).filter(([name]) => name !== "mcp" && name !== "pipeline")
        .map(([name, item]) => [name, snapshotToolStatus(item)])]),
    packageSection);
  return section;
}

/** 分页读取快照，后台刷新时复用当前页已展开的内容。 */
function projectSnapshotList(id, path) {
  const sections = node("div");
  return paginatedList(`projectSnapshots:${id}`, "环境快照", 10, async (page, size) => {
    const query = new URLSearchParams({ page, page_size: size });
    const { snapshots, pagination } = await request(`${path}/snapshots?${query}`);
    return { items: snapshots, ...pagination };
  }, (snapshots, page) => {
    const existing = new Map([...sections.children].map((section) => [section.dataset.snapshot, section]));
    snapshots.forEach((snapshot, index) => {
      const previous = existing.get(snapshot.id);
      const section = previous || snapshotPanel(snapshot, path);
      section.dataset.snapshot = snapshot.id;
      const atIndex = sections.children[index];
      if (section !== atIndex) sections.insertBefore(section, atIndex || null);
      existing.delete(snapshot.id);
      if (!previous && page === 1 && index === 0 && !sections.querySelector(":scope > details[open]")) section.open = true;
    });
    for (const section of existing.values()) section.remove();
    if (!snapshots.length) sections.append(node("p", "等待该项目的首次上报。", "empty"));
    return sections;
  });
}

/** 在弹窗顶部更新项目详情，滚动阅读时将更新留到下一轮。 */
async function refreshProjectDetail() {
  const current = projectLive;
  const dialog = byId("details");
  if (!current || current.pending || !current.implementation.isConnected || !dialog.open || dialog.scrollTop) return;
  const summary = data?.projects.find((project) => project.id === current.id);
  if (!summary || (summary.last_seen === current.lastSeen && summary.team === current.team &&
      data.server_time - current.refreshedAt < 15)) return;
  current.pending = true;
  try {
    const result = await request(current.path);
    if (projectLive !== current || !session || !dialog.open || dialog.scrollTop) return;
    const evidence = implementationEvidence(result);
    if (evidence !== current.evidence) {
      const next = implementationPanel(result);
      current.implementation.replaceWith(next);
      current.implementation = next;
      current.evidence = evidence;
    }
    current.snapshots.paint({ background: true });
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

/** 打开项目详情，详情成功显示后返回查阅结果。 */
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
      implementation: implementationPanel(result), snapshots: projectSnapshotList(id, path),
      teamField: node("span"),
      evidence: implementationEvidence(result),
    };
    current.teamField.append(teamControl(result.project, true));
    openDialog(result.project.name, [
      fields([["标识", id], ["团队", current.teamField]]),
      button("协作记录", () => openWorkflow(result.project)),
      current.implementation,
      panel("环境快照", current.snapshots.element),
      deleteAction(path, `确定删除项目「${result.project.name}」吗？项目的上报和仅由该项目上报的问题会一并删除。此操作不可撤销。今后的自动上报仍可重新登记该项目。`),
    ], options);
    projectLive = current;
    current.snapshots.paint();
    return true;
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
  const saved = Object.fromEntries(new FormData(form));
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
  return { form, hasEdits: () => Object.entries(saved).some(([name, value]) => form.elements[name].value !== value) };
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

/** 打开问题详情和快速处理，详情成功显示后返回查阅结果。 */
async function openIssue(id) {
  const loading = node("p", "正在读取问题…", "empty");
  const options = { closeOnBackdrop: true };
  const path = `/api/maintainer/issues/${id}`;
  openDialog(`#${id}`, [loading], options);
  try {
    const result = await request(path);
    if (!loading.isConnected || !byId("details").open) return;
    const issue = result.issue;
    const editor = issue.duplicate_of ? null : issueForm(issue, result.assignees);
    const management = editor?.form || button(`打开合并目标 #${issue.duplicate_of}`, () => openIssue(issue.duplicate_of));
    const history = result.history.length ? [table(["时间", "维护者", "状态", "处理记录"], result.history.map((entry) =>
      [stamp(entry.created_at), entry.actor, statusPill(statusNames[entry.status], entry.status === "closed"),
        node("span", entry.note, "issue-note")]))] : [];
    openDialog(`#${id}  ${issue.title}`, [
      fields([["所属包 / 版本", issuePackage(issue.package)], ["模块", issue.area], ["首次发现", stamp(issue.first_seen)],
        ["最近发生", stamp(issue.last_seen)]]),
      panel("快速处理", management, ...history),
      panel("发生记录", ...(result.occurrences.length ? result.occurrences.map(occurrence)
        : [node("p", "暂无发生记录", "empty")])),
      deleteAction(path, `确定删除问题 #${id}「${issue.title}」吗？发生记录和处理记录会一并删除。此操作不可撤销。`),
    ], { ...options, hasEdits: editor?.hasEdits });
    return true;
  } catch (failure) {
    if (loading.isConnected && byId("details").open) {
      openDialog("读取问题失败", [node("p", failure.message, "error")], options);
    }
  }
}

// 按下和点击都落在遮罩上时关闭，避免拖选弹窗内文本时误关。
let pointerDownOnBackdrop = false;
const detailDialog = byId("details");
byId("detail-close").addEventListener("click", closeDialog);
detailDialog.addEventListener("cancel", (event) => {
  event.preventDefault();
  closeDialog();
});
detailDialog.addEventListener("close", () => {
  if (!detailDialog.open) {
    projectLive = null;
    dialogHasEdits = null;
    if (unreadListNeedsPaint) {
      unreadListNeedsPaint = false;
      refreshUnreadList(view);
    }
  }
});
detailDialog.addEventListener("pointerdown", (event) => {
  pointerDownOnBackdrop = event.isPrimary && event.button === 0 && isDialogBackdrop(event);
});
detailDialog.addEventListener("click", (event) => {
  if (pointerDownOnBackdrop && isDialogBackdrop(event)) closeDialog();
  pointerDownOnBackdrop = false;
});

/** 从侧栏或总览打开栏目，保留各条目的未读状态。 */
function openView(next) {
  view = next;
  render();
}

byId("nav").addEventListener("click", (event) => {
  const next = event.target.closest("button")?.dataset.view;
  if (next && titles[next]) openView(next);
});

window.addEventListener("storage", (event) => {
  if (!session || !unreadSections.some((section) => event.key === readKey(section))) return;
  const previous = unreadRows[view];
  updateUnread();
  if (unreadOnly.has(view) && event.key === readKey(view) &&
      [...previous.keys()].some((key) => !unreadRows[view].has(key))) refreshUnreadList(view);
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
