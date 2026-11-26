"use strict";

const byId = (id) => document.getElementById(id);
const statusNames = { open: "待处理", closed: "已处理" };
const outcomeNames = {
  executed: "已执行", success: "成功", partial: "部分完成", blocked: "有阻塞",
  failed: "失败", cancelled: "已取消",
};
const completedOutcomes = new Set(["executed", "success", "advanced", "recovered"]);
const usageSources = { codex: "Codex", editor: "Editor", legacy: "未标注" };
const usageEntries = {
  action: "Nova Action", skill: "Skill 查阅", "unity-cli": "Unity CLI", "nova-cli": "Nova CLI",
  inspector: "Unity 检视面板", "unity-mcp": "Unity MCP",
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
const legacyReadStoragePrefix = "nova-issues.read.v1:";
const readStoragePrefix = "nova-issues.read.v2:";
const date = new Intl.DateTimeFormat("zh-CN", { dateStyle: "medium", timeStyle: "short" });
const usageNumber = new Intl.NumberFormat("zh-CN");
const usagePercent = new Intl.NumberFormat("zh-CN", { style: "percent", maximumFractionDigits: 1 });
const usageDimensions = [["module", "模块"], ["operation", "操作"], ["source", "来源"], ["entry", "入口"]];
const usageChart = { dimension: "module", search: "", historyOnly: false, scrollTop: 0 };
let session = null;
let data = null;
let readProgress = {};
let unreadRows = {};
let view = "overview";
let loadCounter = 0;
let days = 14;
let paintLists = null;
let projectLive = null;
let dialogHasEdits = null;
const filters = {
  projectActivity: "",
  search: "", issueProject: "", status: "open", issueSort: "latest",
  project: "", module: "", usageSource: "", usageSort: "latest",
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

/** 用同一批未读上报刷新侧栏、条目红点和次数增量。 */
function updateUnread() {
  const activity = data?.activity;
  for (const section of unreadSections) {
    const rows = new Map();
    let count = 0;
    if (session && activity) {
      const { after, items } = activity.sections[section];
      const progress = currentReadProgress(section) || { after, viewed: new Set() };
      let nextAfter = activity.latest;
      for (const [id, key] of items) {
        if (id <= progress.after || progress.viewed.has(id)) continue;
        if (!rows.has(key)) rows.set(key, []);
        rows.get(key).push(id);
        count++;
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
    const item = byId("nav").querySelector(`[data-view="${section}"]`);
    const badge = item.querySelector(".nav-unread");
    badge.textContent = count || "";
    badge.hidden = count === 0;
    if (count) item.setAttribute("aria-label", `${titles[section]}，${count} 条未读`);
    else item.removeAttribute("aria-label");
  }
  for (const item of document.querySelectorAll("button[data-unread-section]")) updateUnreadButton(item);
  for (const item of document.querySelectorAll(".unread-increment")) updateUnreadIncrement(item);
  const markAll = byId("mark-all-read");
  if (markAll) markAll.disabled = !unreadRows[view]?.size;
}

/** 将本栏截至最近一次刷新收到的上报全部标为已读。 */
function markSectionRead(section) {
  if (!unreadRows[section]?.size) return;
  const progress = readProgress[section];
  progress.after = Math.max(progress.after, data.activity.latest);
  updateUnread();
}

function unreadItemCount(item) {
  return unreadRows[item.dataset.unreadSection]?.get(item.dataset.unreadKey)?.length || 0;
}

/** 显示条目红点，并向读屏软件提供该条目的未读数量。 */
function updateUnreadButton(item) {
  const indicator = item.querySelector(".row-unread");
  const count = unreadItemCount(item);
  indicator.hidden = count === 0;
  indicator.setAttribute("aria-label", `${count} 条未读`);
}

function updateUnreadIncrement(item) {
  const count = unreadItemCount(item);
  item.textContent = count ? `+${count}` : "";
  item.hidden = count === 0;
}

/** 在同一条目的总次数后显示当前浏览器尚未查看的上报数。 */
function countWithUnread(section, key, total) {
  const count = node("span", String(total), "count-with-unread");
  const increment = node("span", null, "unread-increment");
  increment.dataset.unreadSection = section;
  increment.dataset.unreadKey = key;
  increment.setAttribute("aria-hidden", "true");
  count.append(increment);
  updateUnreadIncrement(increment);
  return count;
}

/** 打开条目成功后，只标记点击时已出现在该条目上的上报。 */
function unreadButton(section, key, label, open) {
  const item = button(null, async () => {
    const account = session;
    const ids = unreadRows[section]?.get(key) || [];
    if (!await open() || account !== session || !ids.length) return;
    const progress = readProgress[section];
    for (const id of ids) progress.viewed.add(id);
    updateUnread();
  }, "link");
  item.dataset.unreadSection = section;
  item.dataset.unreadKey = key;
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
  listPages.clear();
  Object.assign(usageChart, { dimension: "module", search: "", historyOnly: false, scrollTop: 0 });
  paintLists = null;
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

/** 筛选范围改变时回到第一页，保留该列表选定的每页条数。 */
function resetListPage(id) {
  const state = listPages.get(id);
  if (state) state.page = 1;
}

/** 在浏览器中对已有的完整项目列表或模块汇总取页。 */
function localListPage(items, page, size) {
  const total = items.length;
  const number = Math.min(page, Math.max(1, Math.ceil(total / size)));
  return { items: items.slice((number - 1) * size, number * size), page: number, total };
}

/** 从维护台接口读取筛选和排序后的指定页及总数。 */
async function remoteListPage(resource, filters, page, size) {
  const query = new URLSearchParams({ ...filters, page, page_size: size });
  const result = await request(`/api/maintainer/${resource}?${query}`);
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

  /** 读取当前页，前台筛选时清除旧结果，自动刷新时继续显示现有表格。 */
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
        results.replaceChildren(renderRows(items));
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
    const projects = filters.projectActivity === "24h" ? data.projects.filter(activeProject) : data.projects;
    return localListPage(projects, page, size);
  }, (projects) => projectRows(projects, true));
  toolbar.append(filter("projectActivity", "上报时间", [["", "全部项目"], ["24h", "过去 24 小时有上报"]],
    () => list.paint({ reset: true })));
  paintLists = list.paint;
  return panel(null, toolbar, list.element);
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
  }, page, size), issueRows);
  input.addEventListener("input", () => {
    filters.search = input.value;
    list.paint({ reset: true, delay: 180 });
  });
  search.append(input);
  const applyFilters = () => list.paint({ reset: true });
  toolbar.append(search, filter("issueProject", "项目", [["", "全部项目"], ...projects], applyFilters),
    filter("status", "状态", [["", "全部"], ...Object.entries(statusNames)], applyFilters),
    filter("issueSort", "排序", sortOptions, applyFilters));
  paintLists = list.paint;
  return panel(null, toolbar, list.element);
}

/** 显示当前页的操作汇总，点击名称可查阅记录。 */
function usageRows(items) {
  return table(["项目", "模块", "操作", "来源", "入口", "Framework", "结果", "次数", "最近使用"],
    items.map((item) => {
      const operation = node("code", item.operation, "usage-operation");
      operation.title = item.description || "暂无该操作的说明。";
      const entry = unreadButton("usage", item.activity_key, operation, () => openUsage(item));
      return [projectName(item.project_id), usageModuleName(item.module), entry,
        usageSources[item.source] || "未标注", usageEntries[item.entry] || "—", item.framework,
        statusPill(outcomeNames[item.outcome] || item.outcome, completedOutcomes.has(item.outcome)),
        countWithUnread("usage", item.activity_key, item.count), stamp(item.last_used)];
    }));
}

/** 显示 Unity MCP 模块的产品名称，筛选查询仍使用原标识。 */
function usageModuleName(id) {
  return id === "unity-mcp" ? usageEntries[id] : id;
}

/** 使用记录的两张表共享筛选条件，各自保留页大小。 */
function resetUsagePages() {
  resetListPage("usageModules");
  resetListPage("usageDetails");
}

/** 明细与图表共用时间、项目、模块和来源条件。 */
function usageScope() {
  return { days: data.days, project: filters.project, module: filters.module, source: filters.usageSource };
}

/** 维度名称沿用使用记录与模块清单，保留历史未知类型。 */
function usageCategoryLabel(dimension, item) {
  if (dimension === "source") return usageSources[item.id] || item.id || "未标注";
  if (dimension === "entry") return usageEntries[item.id] || item.id || "未标注";
  if (dimension === "module" && Object.hasOwn(implementationModules, item.id)) return implementationModules[item.id];
  if (dimension === "module") return item.label || item.id;
  return item.id;
}

/** 有采集入口且没有任何已保留调用证据的模块可归入历史无上报。 */
function unreportedUsageModule(item) {
  return item.tracked && !item.history && !item.tool_history && !item.play_history;
}

/** 以整段筛选范围的次数为分母绘制占比条，未采集的模块保留在清单中。 */
function usageChartRow(dimension, item, title, total) {
  const row = node("li", null, "usage-chart-row");
  const label = node("span", null, "usage-chart-label");
  const name = node(dimension === "operation" ? "code" : "span", title);
  name.title = item.description || item.id;
  label.append(name);
  const untracked = dimension === "module" && !item.tracked;
  if (!item.count) {
    const evidence = [item.history && "有历史记录", item.tool_history && "有专项工具上报",
      item.play_history && "有 Editor Play 上报"].filter(Boolean);
    label.append(node("small", evidence.join(" · ") || (untracked ? "暂无开发调用采集" : "历史无上报"),
      evidence.length || untracked ? "muted" : "usage-history-empty"));
  }

  const share = total ? item.count / total : 0;
  const bar = node("span", null, "usage-chart-track");
  bar.setAttribute("aria-hidden", "true");
  if (share) {
    const fill = node("span");
    fill.style.width = `${share * 100}%`;
    bar.append(fill);
  }
  const percent = share > 0 && share < .001 ? "<0.1%" : usagePercent.format(share);
  row.append(label, bar, node("span", untracked ? "—" : usageNumber.format(item.count), "usage-chart-value"),
    node("span", untracked ? "—" : percent, "usage-chart-value"));
  return row;
}

/** 在使用记录顶部显示四个维度的完整占比图，切换维度和搜索无需重新取数。 */
function usageAnalysis() {
  const element = node("section", null, "usage-analysis");
  const totals = node("div", null, "usage-analysis-totals");
  const metrics = ["使用记录", "开发调用", "Skill 查阅（按轮）", "历史无调用上报的模块"].map((label) => {
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
    usageChart.scrollTop = 0;
    showChart();
  });
  const choices = usageDimensions.map(([id, label]) => button(label, () => {
    if (usageChart.dimension === id) return;
    Object.assign(usageChart, { dimension: id, search: "", scrollTop: 0 });
    search.value = "";
    showChart();
  }, "secondary"));
  tabs.append(...choices);
  const refine = node("div", null, "usage-analysis-refine");
  const historyControl = node("label");
  const history = node("input");
  history.type = "checkbox";
  history.checked = usageChart.historyOnly;
  history.addEventListener("change", () => {
    usageChart.historyOnly = history.checked;
    usageChart.scrollTop = 0;
    showChart();
  });
  historyControl.append(history, "只看历史无上报");
  refine.append(search, historyControl);
  controls.append(tabs, refine);

  const note = node("p", null, "usage-analysis-note muted");
  const graph = node("div", null, "usage-chart");
  graph.tabIndex = 0;
  graph.setAttribute("role", "region");
  graph.setAttribute("aria-label", "使用次数与占比");
  graph.addEventListener("scroll", () => { usageChart.scrollTop = graph.scrollTop; });
  const status = node("p", "正在读取使用分析…", "empty");
  status.setAttribute("role", "status");
  const header = node("div", null, "usage-chart-row usage-chart-header");
  header.append(node("span", "名称"), node("span"), node("span", "次数"), node("span", "占比"));
  element.append(node("h2", "使用分析"), totals, controls, note, header, graph, status);

  let payload = null;
  let scope = "";
  let latest = 0;

  /** 更新当前维度的按次数排行，搜索只改变可见项。 */
  function showChart() {
    const dimension = usageChart.dimension;
    choices.forEach((choice, index) => choice.setAttribute("aria-pressed", String(usageDimensions[index][0] === dimension)));
    search.hidden = dimension === "entry" || dimension === "source";
    const modules = dimension === "module";
    historyControl.hidden = !modules;
    note.textContent = modules
      ? "占比按使用记录中的开发调用计算。历史查看所选项目全部时间，含专项工具和 Editor Play 上报。"
      : "占比按所选范围的使用记录计算，含 Skill 查阅。历史状态查看所选项目的全部上报时间。";
    header.hidden = graph.hidden = !payload;
    if (!payload) return;
    const query = usageChart.search.trim().toLowerCase();
    const rows = payload.dimensions[dimension].map((item) => ({ item, title: usageCategoryLabel(dimension, item) }))
      .filter(({ item }) => !modules || !usageChart.historyOnly || unreportedUsageModule(item))
      .filter(({ item, title }) => !query || `${title} ${item.id} ${item.description || ""}`.toLowerCase().includes(query))
      .sort((a, b) => b.item.count - a.item.count || a.title.localeCompare(b.title, "zh-CN"));
    const list = node("ul", null, "usage-chart-list");
    const total = modules ? payload.calls : payload.total;
    for (const { item, title } of rows) list.append(usageChartRow(dimension, item, title, total));
    graph.replaceChildren(rows.length ? list : node("p", "没有匹配的类型", "empty"));
    graph.scrollTop = usageChart.scrollTop;
  }

  /** 按当前筛选更新图表，旧请求返回时不覆盖新范围。 */
  async function paint() {
    const query = new URLSearchParams(usageScope()).toString();
    const run = ++latest;
    if (query !== scope) {
      if (scope) usageChart.scrollTop = 0;
      scope = query;
      payload = null;
      for (const metric of metrics) metric.textContent = "—";
      status.textContent = "正在读取使用分析…";
      status.hidden = false;
      showChart();
    }
    try {
      const result = await request(`/api/maintainer/usage-analysis?${query}`);
      if (run !== latest || !element.isConnected) return;
      payload = result;
      const neverReported = result.dimensions.module.filter(unreportedUsageModule).length;
      [result.total, result.calls, result.skill_turns, neverReported].forEach((value, index) => {
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
    { ...usageScope(), sort: filters.usageSort }, page, size), usageRows);
  const analysis = usageAnalysis();
  const moduleSection = node("div", null, "usage-module-summary");
  moduleSection.append(node("h2", "项目模块调用"), modules.element);
  const detailsTitle = node("h2", "操作明细");
  const paintTables = (options) => {
    moduleSection.hidden = detailsTitle.hidden = filters.usageSource !== "codex";
    if (!moduleSection.hidden) modules.paint(options);
    details.paint(options);
  };
  paintLists = (options) => {
    void analysis.paint();
    paintTables(options);
  };
  const applyFilters = () => {
    resetUsagePages();
    paintLists({ reset: true });
  };
  toolbar.append(filter("usageSource", "来源", [["", "全部"], ...Object.entries(usageSources)], applyFilters),
    filter("project", "项目", [["", "全部"], ...data.projects.map((item) => [item.id, item.name])], applyFilters),
    filter("module", "模块", [["", "全部"], ...data.usage_modules.map((name) => [name, usageModuleName(name)])], applyFilters),
    control("最近", days, [[1, "1 天"], [7, "7 天"], [14, "14 天"], [30, "30 天"], [90, "90 天"]], (value) => {
      days = Number(value);
      usageChart.scrollTop = 0;
      refresh(true);
    }), filter("usageSort", "排序", sortOptions, () => {
      resetUsagePages();
      paintTables({ reset: true });
    }));
  return panel(null, toolbar, analysis.element, moduleSection, detailsTitle, details.element);
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
  const content = pages[view]();
  byId("content").replaceChildren(...(Array.isArray(content) ? content : [content]));
  paintLists();
}

/** 刷新维护台及各栏未读数量，保留正在使用的筛选控件。 */
async function refresh(force = false) {
  if (!session) return;
  const run = ++loadCounter;
  try {
    const latest = await request(`/api/maintainer/overview?${overviewQuery()}`);
    if (run !== loadCounter) return;
    if (data && data.days !== latest.days) resetUsagePages();
    data = latest;
    updateUnread();
    showError(byId("page-error"), "");
    const active = document.activeElement;
    const keepFocus = byId("content").contains(active) &&
      (active.matches("input, select, textarea") || active.closest(".pagination, .usage-analysis"));
    if (force || !keepFocus) render();
    else paintLists?.({ background: true });
    void refreshProjectDetail();
  } catch (error) {
    if (session && run === loadCounter) showError(byId("page-error"), error.message);
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

/** 打开使用记录所属的操作，展示其用途和列表汇总。 */
function openUsage(item) {
  const details = fields([
    ["项目", projectName(item.project_id)], ["模块", usageModuleName(item.module)],
    ["来源", usageSources[item.source] || "未标注"], ["入口", usageEntries[item.entry] || "—"],
    ["Framework", item.framework], ["结果", outcomeNames[item.outcome] || item.outcome],
    ["次数", String(item.count)], ["最近使用", stamp(item.last_used)],
  ]);
  openDialog(item.operation, item.description ? [node("p", item.description), details] : [details],
    { closeOnBackdrop: true });
  return true;
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
  "bundle-build": "Bundle 构建", "player-build": "目标平台构建",
};
const implementationModules = {
  config: "Config", pipify: "Pipify", upm: "包管理", procedure: "流程", asset: "资源",
  table: "表", localization: "本地化", ui: "UI", network: "网络", sdk: "SDK",
};
const buildKinds = { player: "Player", project: "平台工程" };

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
  if (!workspaces.length) return node("p", "暂无 SDK 清单上报", "empty");
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
  }
  return status;
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

/** 显示各模块的配置和两类调用次数，最近时间在末列统一显示。 */
function moduleUsagePanel(usage, hasWorkspaceReport, configuredTools, outcomes, sdk, unityCli, unityMcp) {
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
    let configuration = "—";
    if (tool) {
      configuration = toolConfigurationStatus(hasWorkspaceReport, configuredTools.has(tool));
    } else if (isUnityCli) {
      configuration = unityCli ? snapshotToolStatus(unityCli.tool) : node("span", "暂无状态上报", "muted");
      if (unityCli) configuration.title = `环境快照 · ${stamp(unityCli.observedAt)}`;
    } else if (isUnityMcp) {
      configuration = unityMcpConfiguration(unityMcp);
    } else if (id === "sdk") {
      configuration = button("查看 SDK 清单", () => {
        sdk.open = true;
        sdk.scrollIntoView({ block: "nearest" });
      }, "link");
    }

    const latest = Math.max(evidence?.last_use || 0, id === "table" ? tableReadyAt : 0,
      isUnityCli ? unityCli?.observedAt || 0 : 0, isUnityMcp ? unityMcp[0]?.observedAt || 0 : 0);
    return [implementationModules[id] || usageModuleName(id), configuration,
      evidence?.development_calls || 0, evidence?.play_calls || 0, stamp(latest)];
  });
  const summary = table(["模块", "配置 / 启用", "开发调用次数", "Play 调用次数", "最近记录"], rows);
  const headers = summary.querySelectorAll("th");
  headers[2].title = "执行开发工具、导出或命令的次数。";
  headers[3].title = "Unity Editor Play 中成功调用模块入口的次数。旧版按场次上报的记录不计入。";
  summary.classList.add("module-usage");
  return panel("模块接入与使用", node("p", "次数按项目统计近 30 天。", "muted"), summary);
}

/** 汇总项目的工具验证、SDK 构建和模块使用。 */
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
  const latestAt = Math.max(0, ...[...workspaceFacts, ...outcomes].map((item) => item.observed_at), issue?.observed_at || 0);
  const sdk = evidenceSection("sdk", "SDK 接入与构建", sdkPackageTable(workspaces));
  sdk.append(mobileBuildResults(outcomes));
  const frameworks = [...new Set(workspaces.map(loadedFrameworkVersion).filter(Boolean))].join("、");
  const unity = [...new Set(workspaces.map((workspace) => workspace.unity).filter(Boolean))].join("、");
  return panel("工程实施",
    fields([["Framework版本", frameworks || "待上报"], ["Unity", unity || "待上报"],
      ["最近上报坐标", workspaces.length ? scopeName(workspaces[0]) : "待上报"],
      ["最近实施记录", stamp(latestAt)],
      ["最近编译或导入异常", issue ? button(`#${issue.issue_id} · ${stamp(issue.observed_at)}`,
        () => openIssue(issue.issue_id), "link") : "暂无记录"]]),
    table(["工具", "配置", "验证", "最近结果"], [["config", "Config", ["config-load", "config-export"]],
      ["pipify", "Pipify", ["pipify-batch"]], ["plugpals", "PlugPals", ["plugpals-query"]]].map(([id, label, stages]) => {
      const recent = outcomes.find((row) => stages.includes(row.data.stage));
      const success = outcomes.find((row) => stages.includes(row.data.stage) && row.data.outcome === "success");
      const configured = configuredTools.has(id);
      return [label, toolConfigurationStatus(hasWorkspaceReport, configured), implementationVerification(success),
        recent ? timedImplementationResult(recent, implementationLabel(recent)) : "暂无记录"];
    })),
    sdk,
    moduleUsagePanel(usage, hasWorkspaceReport, configuredTools, outcomes, sdk, unityCli, unityMcp));
}

/** 提取会影响工程实施面板的上报，工具状态变化时同样刷新。 */
function implementationEvidence(result) {
  return JSON.stringify([result.implementation, result.configured_tools, result.implementation_outcomes,
    result.project_module_evidence, result.upgrade_issues, latestUnityCliStatus(result.snapshots), unityMcpConnections(result)]);
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
      ["操作系统", { macos: "macOS", windows: "Windows", linux: "Linux" }[snap.operating_system] || "待上报"],
      ["游戏包名", gamePackageNames(snap.application_identifiers)], ["钩子追加", feedback],
      ["已启用", enabledModuleList(snap.enabled)],
      ...Object.entries(snap.coverage).map(([name, state]) => [`${name} 采集`,
        statusPill(coverageNames[state] || state, readyCoverageStates.has(state))]),
      ...Object.entries(snap.tools).filter(([name]) => name !== "mcp" && name !== "pipeline")
        .map(([name, item]) => [name, snapshotToolStatus(item)])]),
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
      data.server_time - current.refreshedAt < 15)) return;
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
      deleteAction(path, `确定删除项目「${result.project.name}」吗？项目的上报和仅由该项目上报的问题会一并删除。此操作不可撤销。今后的自动上报仍可重新登记该项目。`),
    ], options);
    projectLive = current;
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
  if (session && unreadSections.some((section) => event.key === readKey(section))) updateUnread();
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
