// PmTracker.Web/wwwroot/js/modules/eventBus.js
(function installPmTrackerGovClickAdapter() {
  const dispatched = new WeakSet();
  document.addEventListener("gov-click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) return;
    if (dispatched.has(event)) return;
    dispatched.add(event);
    const native = new MouseEvent("click", { bubbles: true, cancelable: true, composed: true, detail: 1 });
    target.dispatchEvent(native);
  });
})();

// PmTracker.Web/wwwroot/js/modules/navigationRuntime.js
var navigationRuntime = {
  initRecordFormEnhancements: null,
  prepareRecordEditorFormNavigation: null
};
function configureNavigationRuntime(runtime = {}) {
  if (typeof runtime.initRecordFormEnhancements === "function") {
    navigationRuntime.initRecordFormEnhancements = runtime.initRecordFormEnhancements;
  }
  if (typeof runtime.prepareRecordEditorFormNavigation === "function") {
    navigationRuntime.prepareRecordEditorFormNavigation = runtime.prepareRecordEditorFormNavigation;
  }
}

// PmTracker.Web/wwwroot/js/modules/navigationShared.js
function appendCurrentAsUser(url) {
  const currentUrl = new URL(window.location.href);
  const asUser = currentUrl.searchParams.get("asUser");
  if (!asUser) {
    return url;
  }
  const resolvedUrl = new URL(url, window.location.origin);
  if (resolvedUrl.origin !== window.location.origin || resolvedUrl.searchParams.has("asUser")) {
    return url;
  }
  resolvedUrl.searchParams.set("asUser", asUser);
  return `${resolvedUrl.pathname}${resolvedUrl.search}${resolvedUrl.hash}`;
}
async function fetchHtmlDocument(url) {
  const response = await fetch(appendCurrentAsUser(url), {
    headers: { "X-Requested-With": "XMLHttpRequest" },
    credentials: "same-origin",
    cache: "no-store"
  });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }
  const html = await response.text();
  return new DOMParser().parseFromString(html, "text/html");
}
async function fetchHtmlFragment(url) {
  const response = await fetch(appendCurrentAsUser(url), {
    headers: { "X-Requested-With": "XMLHttpRequest" },
    credentials: "same-origin",
    cache: "no-store"
  });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }
  return response.text();
}
function parseHtmlFragment(html) {
  return new DOMParser().parseFromString(html, "text/html");
}
function resolveProjectTabPanel(tabNameOrPanel) {
  if (tabNameOrPanel instanceof HTMLElement && tabNameOrPanel.matches("[data-tab-panel]")) {
    return tabNameOrPanel;
  }
  if (typeof tabNameOrPanel !== "string" || !tabNameOrPanel) {
    return null;
  }
  const selector = `[data-tab-panel="${CSS.escape(tabNameOrPanel)}"]`;
  const panel = document.querySelector(selector);
  return panel instanceof HTMLElement ? panel : null;
}
function resolveRecordCardElement(cardOrChild) {
  if (cardOrChild instanceof HTMLElement && cardOrChild.classList.contains("record-card")) {
    return cardOrChild;
  }
  if (!(cardOrChild instanceof Element)) {
    return null;
  }
  const card = cardOrChild.closest(".record-card[data-record-id]");
  return card instanceof HTMLElement ? card : null;
}
function resolveOrCreateErrorContainer(container, attributeName) {
  let error = container.querySelector(`[${attributeName}]`);
  if (error instanceof HTMLElement) {
    return error;
  }
  error = document.createElement("div");
  error.className = "record-loading-error";
  error.setAttribute(attributeName, "");
  error.hidden = true;
  container.appendChild(error);
  return error;
}
function renderLazyLoadError(errorContainer, message, retryAttributeName) {
  if (!(errorContainer instanceof HTMLElement)) {
    return;
  }
  errorContainer.innerHTML = "";
  const text = document.createElement("span");
  text.textContent = message;
  errorContainer.appendChild(text);
  const retryButton = document.createElement("button");
  retryButton.type = "button";
  retryButton.className = "btn small ghost";
  retryButton.setAttribute(retryAttributeName, "");
  retryButton.textContent = "Zkusit znovu";
  errorContainer.appendChild(retryButton);
  errorContainer.hidden = false;
}
function setLazyLoadingState(container, placeholder, errorContainer, isLoading) {
  if (container instanceof HTMLElement) {
    if (isLoading) {
      container.setAttribute("aria-busy", "true");
    } else {
      container.removeAttribute("aria-busy");
    }
  }
  if (placeholder instanceof HTMLElement) {
    placeholder.hidden = !isLoading;
  }
  if (errorContainer instanceof HTMLElement && isLoading) {
    errorContainer.hidden = true;
    errorContainer.textContent = "";
  }
}
function isElementInHiddenTree(element) {
  let current = element;
  while (current instanceof HTMLElement) {
    if (current.hidden) {
      return true;
    }
    current = current.parentElement;
  }
  return false;
}

// PmTracker.Web/wwwroot/js/modules/comments.js
var commentSortDirectionStorageKey = "pmtracker.comments.sortDirection";
var defaultRecordCommentsLoadStep = 5;
function normalizeCommentSortDirection(direction) {
  return direction === "desc" ? "desc" : "asc";
}
function getStoredCommentSortDirection() {
  const value = localStorage.getItem(commentSortDirectionStorageKey);
  return normalizeCommentSortDirection(value);
}
function setStoredCommentSortDirection(direction) {
  localStorage.setItem(commentSortDirectionStorageKey, normalizeCommentSortDirection(direction));
}
function setCommentSortButtonLabel(button, direction) {
  if (!(button instanceof HTMLButtonElement)) {
    return;
  }
  button.textContent = direction === "desc" ? "Řazení: jednání sestupně" : "Řazení: jednání vzestupně";
  button.setAttribute("aria-pressed", direction === "desc" ? "true" : "false");
}
function getCommentSortDirection(scope) {
  const section = scope instanceof HTMLElement && scope.matches("[data-comment-sort-section]") ? scope : scope instanceof Element ? scope.closest("[data-comment-sort-section]") : null;
  if (!(section instanceof HTMLElement)) {
    return getStoredCommentSortDirection();
  }
  return normalizeCommentSortDirection(section.getAttribute("data-comment-sort-direction"));
}
function applyCommentSort(section, direction) {
  if (!(section instanceof HTMLElement)) {
    return;
  }
  const normalizedDirection = normalizeCommentSortDirection(direction);
  const list = section.querySelector("[data-comment-list]");
  const paginationActions = section.querySelector(".comment-pagination-actions");
  if (list instanceof HTMLElement) {
    const items = Array.from(list.querySelectorAll("[data-comment-item]")).filter((item) => item instanceof HTMLElement);
    if (items.length > 1) {
      items.sort((aNode, bNode) => {
        const aMeeting = Number(aNode.getAttribute("data-comment-meeting") || "0");
        const bMeeting = Number(bNode.getAttribute("data-comment-meeting") || "0");
        const aDate = Date.parse(aNode.getAttribute("data-comment-date") || "");
        const bDate = Date.parse(bNode.getAttribute("data-comment-date") || "");
        const aId = Number(aNode.getAttribute("data-comment-id") || "0");
        const bId = Number(bNode.getAttribute("data-comment-id") || "0");
        if (normalizedDirection === "desc") {
          return bMeeting - aMeeting || (Number.isFinite(bDate) ? bDate : 0) - (Number.isFinite(aDate) ? aDate : 0) || bId - aId;
        }
        return aMeeting - bMeeting || (Number.isFinite(aDate) ? aDate : 0) - (Number.isFinite(bDate) ? bDate : 0) || aId - bId;
      });
      items.forEach((item) => list.appendChild(item));
    }
  }
  const header = section.querySelector(".record-comments-header");
  if (header instanceof HTMLElement) {
    header.classList.toggle("record-comments-header--stacked-right", normalizedDirection === "asc");
  }
  if (paginationActions instanceof HTMLElement) {
    paginationActions.classList.toggle("comment-pagination-actions--in-header", normalizedDirection === "asc");
    if (normalizedDirection === "asc" && header instanceof HTMLElement) {
      if (paginationActions.parentElement !== header) {
        header.appendChild(paginationActions);
      }
    } else if (normalizedDirection === "desc" && list instanceof HTMLElement) {
      if (paginationActions.previousElementSibling !== list) {
        list.after(paginationActions);
      }
    }
    paginationActions.querySelectorAll("button[data-label-asc][data-label-desc]").forEach((btn) => {
      if (!(btn instanceof HTMLElement)) {
        return;
      }
      const label = normalizedDirection === "asc" ? btn.getAttribute("data-label-asc") : btn.getAttribute("data-label-desc");
      if (typeof label === "string" && label.length > 0) {
        btn.textContent = label;
      }
    });
  }
  section.setAttribute("data-comment-sort-direction", normalizedDirection);
}
function resolveRecordCommentsShell(source) {
  if (source instanceof HTMLElement && source.matches("[data-record-comments-shell]")) {
    return source;
  }
  if (!(source instanceof Element)) {
    return null;
  }
  const shell = source.closest("[data-record-comments-shell]");
  return shell instanceof HTMLElement ? shell : null;
}
function normalizeCount(value) {
  const parsed = Number.parseInt(String(value || ""), 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
}
function normalizeLoadStep(value) {
  const parsed = Number.parseInt(String(value || ""), 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : defaultRecordCommentsLoadStep;
}
function readRecordCommentsPanelState(source) {
  const panel = source instanceof HTMLElement && source.matches("[data-record-comments-panel]") ? source : source instanceof Element ? source.closest("[data-record-comments-panel]") : null;
  if (!(panel instanceof HTMLElement)) {
    return null;
  }
  const loadedCount = normalizeCount(panel.dataset.recordCommentsLoadedCount);
  const totalCount = normalizeCount(panel.dataset.recordCommentsTotalCount);
  const loadStep = normalizeLoadStep(panel.dataset.recordCommentsLoadStep);
  const isFullyLoaded = panel.dataset.recordCommentsIsFullyLoaded === "true" || totalCount > 0 && loadedCount >= totalCount;
  return {
    panel,
    loadedCount,
    totalCount,
    loadStep,
    isFullyLoaded
  };
}
function buildRecordCommentsRequestUrl(baseUrl, options = {}) {
  if (typeof baseUrl !== "string" || !baseUrl.trim()) {
    return "";
  }
  const url = new URL(baseUrl, window.location.origin);
  if (options.loadAll === true) {
    url.searchParams.set("loadAll", "true");
    url.searchParams.delete("limit");
  } else {
    url.searchParams.delete("loadAll");
    const limit = Number(options.limit);
    if (Number.isFinite(limit) && limit > 0) {
      url.searchParams.set("limit", String(Math.trunc(limit)));
    } else {
      url.searchParams.delete("limit");
    }
  }
  return `${url.pathname}${url.search}${url.hash}`;
}
function resolveRecordCommentsBaseUrl(shell) {
  if (!(shell instanceof HTMLElement)) {
    return "";
  }
  const shellUrl = (shell.dataset.recordCommentsBaseUrl || shell.dataset.recordCommentsUrl || "").trim();
  if (shellUrl) {
    return shellUrl;
  }
  const card = shell.closest(".record-card[data-record-id]");
  if (!(card instanceof HTMLElement)) {
    return "";
  }
  return (card.dataset.recordCommentsBaseUrl || card.dataset.recordCommentsUrl || "").trim();
}
function resolveInlineCommentsError(shell) {
  const existing = shell.querySelector("[data-record-comments-inline-error]");
  if (existing instanceof HTMLElement) {
    return existing;
  }
  const error = document.createElement("div");
  error.className = "record-loading-error";
  error.setAttribute("data-record-comments-inline-error", "");
  error.hidden = true;
  shell.appendChild(error);
  return error;
}
function setInlineCommentsError(shell, message) {
  const error = resolveInlineCommentsError(shell);
  error.textContent = message;
  error.hidden = !message;
}
async function reloadProjectRecordCommentsPanel(source, options = {}) {
  const shell = resolveRecordCommentsShell(source);
  if (!(shell instanceof HTMLElement)) {
    return false;
  }
  const baseUrl = resolveRecordCommentsBaseUrl(shell);
  if (!baseUrl) {
    return false;
  }
  const requestUrl = buildRecordCommentsRequestUrl(baseUrl, {
    limit: options.limit,
    loadAll: options.loadAll === true
  }) || baseUrl;
  const sortDirection = options.sortDirection === "desc" || options.sortDirection === "asc" ? options.sortDirection : getCommentSortDirection(source);
  const card = shell.closest(".record-card[data-record-id]");
  setInlineCommentsError(shell, "");
  shell.setAttribute("aria-busy", "true");
  try {
    shell.innerHTML = await fetchHtmlFragment(requestUrl);
    shell.dataset.recordCommentsLoaded = "true";
    shell.dataset.recordCommentsUrl = requestUrl;
    shell.dataset.recordCommentsBaseUrl = baseUrl;
    if (card instanceof HTMLElement) {
      card.dataset.recordCommentsLoaded = "true";
      card.dataset.recordCommentsBaseUrl = baseUrl;
    }
    navigationRuntime.initRecordFormEnhancements?.(shell);
    initCommentSortUi(shell);
    const section = shell.querySelector("[data-comment-sort-section]");
    if (section instanceof HTMLElement) {
      applyCommentSort(section, sortDirection);
      const toggle = section.querySelector("[data-comment-sort-toggle]");
      if (toggle instanceof HTMLButtonElement) {
        setCommentSortButtonLabel(toggle, sortDirection);
      }
    }
    return true;
  } catch {
    setInlineCommentsError(shell, "Nepodařilo se načíst další vyjádření.");
    return false;
  } finally {
    shell.removeAttribute("aria-busy");
  }
}
function applyCommentSortToAllSections(direction, scope = document) {
  const root = scope instanceof Element ? scope : document;
  const normalizedDirection = normalizeCommentSortDirection(direction);
  root.querySelectorAll("[data-comment-sort-section]").forEach((section) => {
    if (!(section instanceof HTMLElement)) {
      return;
    }
    applyCommentSort(section, normalizedDirection);
    const toggle = section.querySelector("[data-comment-sort-toggle]");
    if (toggle instanceof HTMLButtonElement) {
      setCommentSortButtonLabel(toggle, normalizedDirection);
    }
  });
}
function initCommentSortUi(scope = document) {
  const root = scope instanceof Element ? scope : document;
  const sections = root.querySelectorAll("[data-comment-sort-section]");
  if (sections.length === 0) {
    return;
  }
  sections.forEach((section) => {
    if (!(section instanceof HTMLElement)) {
      return;
    }
    const defaultDirection = getStoredCommentSortDirection();
    applyCommentSort(section, defaultDirection);
    const toggle = section.querySelector("[data-comment-sort-toggle]");
    if (!(toggle instanceof HTMLButtonElement)) {
      return;
    }
    setCommentSortButtonLabel(toggle, defaultDirection);
    if (toggle.dataset.commentSortReady === "true") {
      const loadMoreButtonReady = section.querySelector("[data-record-comments-load-more]");
      const loadAllButtonReady = section.querySelector("[data-record-comments-load-all]");
      if (loadMoreButtonReady instanceof HTMLButtonElement && loadMoreButtonReady.dataset.commentLoadMoreReady !== "true") {
        loadMoreButtonReady.dataset.commentLoadMoreReady = "true";
        loadMoreButtonReady.addEventListener("click", async () => {
          const state = readRecordCommentsPanelState(section);
          if (!state) {
            return;
          }
          loadMoreButtonReady.disabled = true;
          try {
            await reloadProjectRecordCommentsPanel(loadMoreButtonReady, {
              limit: state.loadedCount + state.loadStep,
              sortDirection: getCommentSortDirection(section)
            });
          } finally {
            loadMoreButtonReady.disabled = false;
          }
        });
      }
      if (loadAllButtonReady instanceof HTMLButtonElement && loadAllButtonReady.dataset.commentLoadAllReady !== "true") {
        loadAllButtonReady.dataset.commentLoadAllReady = "true";
        loadAllButtonReady.addEventListener("click", async () => {
          loadAllButtonReady.disabled = true;
          try {
            await reloadProjectRecordCommentsPanel(loadAllButtonReady, {
              loadAll: true,
              sortDirection: getCommentSortDirection(section)
            });
          } finally {
            loadAllButtonReady.disabled = false;
          }
        });
      }
      return;
    }
    toggle.dataset.commentSortReady = "true";
    toggle.addEventListener("click", () => {
      const current = section.getAttribute("data-comment-sort-direction") === "desc" ? "desc" : "asc";
      const next = current === "asc" ? "desc" : "asc";
      setStoredCommentSortDirection(next);
      applyCommentSortToAllSections(next, document);
    });
    const loadMoreButton = section.querySelector("[data-record-comments-load-more]");
    if (loadMoreButton instanceof HTMLButtonElement && loadMoreButton.dataset.commentLoadMoreReady !== "true") {
      loadMoreButton.dataset.commentLoadMoreReady = "true";
      loadMoreButton.addEventListener("click", async () => {
        const state = readRecordCommentsPanelState(section);
        if (!state) {
          return;
        }
        loadMoreButton.disabled = true;
        try {
          await reloadProjectRecordCommentsPanel(loadMoreButton, {
            limit: state.loadedCount + state.loadStep,
            sortDirection: getCommentSortDirection(section)
          });
        } finally {
          loadMoreButton.disabled = false;
        }
      });
    }
    const loadAllButton = section.querySelector("[data-record-comments-load-all]");
    if (loadAllButton instanceof HTMLButtonElement && loadAllButton.dataset.commentLoadAllReady !== "true") {
      loadAllButton.dataset.commentLoadAllReady = "true";
      loadAllButton.addEventListener("click", async () => {
        loadAllButton.disabled = true;
        try {
          await reloadProjectRecordCommentsPanel(loadAllButton, {
            loadAll: true,
            sortDirection: getCommentSortDirection(section)
          });
        } finally {
          loadAllButton.disabled = false;
        }
      });
    }
  });
}
// PmTracker.Web/wwwroot/js/modules/utils.js
var msPerDay = 24 * 60 * 60 * 1000;
var dateMonths = [
  "Leden",
  "Únor",
  "Březen",
  "Duben",
  "Květen",
  "Červen",
  "Červenec",
  "Srpen",
  "Září",
  "Říjen",
  "Listopad",
  "Prosinec"
];
function normalizeFilterText(value) {
  if (!value) {
    return "";
  }
  return value.toString().trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "");
}
function normalizeFilterToken(value) {
  if (value === null || value === undefined) {
    return "";
  }
  return String(value).trim().toUpperCase();
}
function normalizeSearchText2(value) {
  return normalizeFilterText(value);
}
function containsWordPrefix(text, token) {
  if (!text || !token) {
    return false;
  }
  const parts = text.split(/[\s@._,;:/\\-]+/g).filter(Boolean);
  return parts.some((part) => part.startsWith(token));
}
function scoreSearchCandidate(query, haystack) {
  const normalizedQuery = normalizeSearchText2(query);
  const normalizedHaystack = normalizeSearchText2(haystack);
  if (!normalizedQuery) {
    return 1;
  }
  if (!normalizedHaystack) {
    return 0;
  }
  const tokens = normalizedQuery.split(/\s+/g).filter(Boolean);
  let score = 0;
  if (normalizedHaystack === normalizedQuery) {
    score += 1600;
  }
  if (normalizedHaystack.startsWith(normalizedQuery)) {
    score += 1100;
  }
  if (normalizedHaystack.includes(normalizedQuery)) {
    score += 700;
  }
  tokens.forEach((token) => {
    if (containsWordPrefix(normalizedHaystack, token)) {
      score += 180;
    } else if (normalizedHaystack.includes(token)) {
      score += 85;
    }
  });
  return score;
}
function debounce(callback, waitMs) {
  let timeoutId = 0;
  return (...args) => {
    window.clearTimeout(timeoutId);
    timeoutId = window.setTimeout(() => callback(...args), waitMs);
  };
}
var rainbowMeasureCanvas = null;
function measureTextWidth(text, fontSpec) {
  const normalized = String(text || "").trim();
  if (!normalized) {
    return 0;
  }
  if (!(rainbowMeasureCanvas instanceof HTMLCanvasElement)) {
    rainbowMeasureCanvas = document.createElement("canvas");
  }
  const context = rainbowMeasureCanvas.getContext("2d");
  if (!context) {
    return normalized.length * 7;
  }
  context.font = fontSpec || "600 11px sans-serif";
  return context.measureText(normalized).width;
}
function parseColorChannels(value) {
  const normalized = String(value || "").trim();
  if (!normalized) {
    return null;
  }
  if (normalized.startsWith("#")) {
    const hex = normalized.slice(1);
    if (hex.length === 6) {
      const r2 = Number.parseInt(hex.slice(0, 2), 16);
      const g2 = Number.parseInt(hex.slice(2, 4), 16);
      const b2 = Number.parseInt(hex.slice(4, 6), 16);
      if (Number.isFinite(r2) && Number.isFinite(g2) && Number.isFinite(b2)) {
        return { r: r2, g: g2, b: b2 };
      }
    }
  }
  const match = normalized.match(/^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)/i);
  if (!match) {
    return null;
  }
  const r = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[1]))));
  const g = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[2]))));
  const b = Math.max(0, Math.min(255, Math.round(Number.parseFloat(match[3]))));
  if (!Number.isFinite(r) || !Number.isFinite(g) || !Number.isFinite(b)) {
    return null;
  }
  return { r, g, b };
}
function getContrastTextColor(backgroundColor) {
  const channels = parseColorChannels(backgroundColor);
  if (!channels) {
    return "#0F172A";
  }
  const toLinear = (channel) => {
    const normalized = channel / 255;
    if (normalized <= 0.03928) {
      return normalized / 12.92;
    }
    return ((normalized + 0.055) / 1.055) ** 2.4;
  };
  const luminance = 0.2126 * toLinear(channels.r) + 0.7152 * toLinear(channels.g) + 0.0722 * toLinear(channels.b);
  return luminance >= 0.45 ? "#0F172A" : "#F8FAFC";
}
function pickSegmentLabel(fullLabel, shortLabel, availableWidthPx, fontSpec) {
  const available = Math.max(0, Number(availableWidthPx) || 0);
  if (available < 14) {
    return "";
  }
  const normalizedFull = String(fullLabel || "").trim();
  const normalizedShort = String(shortLabel || "").trim();
  const candidates = [normalizedFull, normalizedShort].filter(Boolean);
  const horizontalPaddingPx = 8;
  for (const candidate of candidates) {
    if (measureTextWidth(candidate, fontSpec) + horizontalPaddingPx <= available) {
      return candidate;
    }
  }
  return "";
}
function parseIsoDate(value) {
  if (!value) {
    return null;
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value.trim());
  if (!match) {
    return null;
  }
  const year = Number.parseInt(match[1], 10);
  const month = Number.parseInt(match[2], 10);
  const day = Number.parseInt(match[3], 10);
  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
    return null;
  }
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }
  return date;
}
function parseDisplayDate(value) {
  if (!value) {
    return null;
  }
  const match = /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/.exec(value.trim());
  if (!match) {
    return null;
  }
  const day = Number.parseInt(match[1], 10);
  const month = Number.parseInt(match[2], 10);
  const year = Number.parseInt(match[3], 10);
  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day)) {
    return null;
  }
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }
  return date;
}
function formatIsoDate(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}
function formatDisplayDate(date) {
  const day = String(date.getDate()).padStart(2, "0");
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const year = date.getFullYear();
  return `${day}.${month}.${year}`;
}
function toUtcDayStamp(date) {
  return Date.UTC(date.getFullYear(), date.getMonth(), date.getDate());
}
function diffCalendarDays(a, b) {
  return Math.round((toUtcDayStamp(a) - toUtcDayStamp(b)) / msPerDay);
}
function addCalendarDays(baseDate, dayCount) {
  const days = Number.isFinite(dayCount) ? Math.trunc(dayCount) : 0;
  const next = new Date(baseDate.getFullYear(), baseDate.getMonth(), baseDate.getDate());
  next.setDate(next.getDate() + days);
  return next;
}
function formatAxisDayMonth(date) {
  const day = String(date.getDate()).padStart(2, "0");
  const month = String(date.getMonth() + 1).padStart(2, "0");
  return `${day}.${month}.`;
}
function formatAxisMonthYear(date) {
  const month = String(date.getMonth() + 1).padStart(2, "0");
  return `${month}/${date.getFullYear()}`;
}
function parseIsoDateTime(value) {
  if (!value) {
    return null;
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::\d{2})?$/.exec(value.trim());
  if (!match) {
    return null;
  }
  const year = Number.parseInt(match[1], 10);
  const month = Number.parseInt(match[2], 10);
  const day = Number.parseInt(match[3], 10);
  const hours = Number.parseInt(match[4], 10);
  const minutes = Number.parseInt(match[5], 10);
  if (!Number.isFinite(year) || !Number.isFinite(month) || !Number.isFinite(day) || !Number.isFinite(hours) || !Number.isFinite(minutes) || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
    return null;
  }
  const date = new Date(year, month - 1, day, hours, minutes, 0, 0);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day || date.getHours() !== hours || date.getMinutes() !== minutes) {
    return null;
  }
  return date;
}
function parseTimeValue(value) {
  if (!value) {
    return null;
  }
  const match = /^(\d{1,2}):(\d{2})$/.exec(value.trim());
  if (!match) {
    return null;
  }
  const hours = Number.parseInt(match[1], 10);
  const minutes = Number.parseInt(match[2], 10);
  if (!Number.isFinite(hours) || !Number.isFinite(minutes) || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
    return null;
  }
  return { hours, minutes };
}
function formatTime(hours, minutes) {
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
}
function isSameCalendarDate(a, b) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}
function parseJsonPayload(rawText) {
  const text = String(rawText || "").trim();
  if (!text) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch (error) {
    return null;
  }
}
function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}
function resolveAjaxResponseTraceId(response) {
  if (!(response instanceof Response) || !(response.headers instanceof Headers)) {
    return "";
  }
  return response.headers.get("x-trace-id") || response.headers.get("trace-id") || response.headers.get("request-id") || "";
}
function truncateDiagnosticBody(value, maxLength) {
  const text = String(value || "");
  const limit = Number.isFinite(maxLength) ? Math.max(256, Number(maxLength)) : 12000;
  if (text.length <= limit) {
    return text;
  }
  return `${text.slice(0, limit)}
...[truncated ${text.length - limit} chars]`;
}
function reportClientDiagnostic(type, detail = {}) {
  const name = String(type || "").trim();
  if (!name) {
    return;
  }
  window.dispatchEvent(new CustomEvent("pmtracker:client-diagnostic", {
    detail: {
      type: name,
      ...detail
    }
  }));
}
function buildResponseHeadersSnapshot(response, maxHeaders) {
  if (!(response instanceof Response) || !(response.headers instanceof Headers)) {
    return "<none>";
  }
  const limit = Number.isFinite(maxHeaders) ? Math.max(5, Number(maxHeaders)) : 80;
  const entries = Array.from(response.headers.entries());
  if (entries.length === 0) {
    return "<none>";
  }
  return entries.slice(0, limit).map(([key, value]) => `${key}: ${value}`).join(`
`);
}
function buildFormDataSnapshot(formData, maxFields) {
  if (!(formData instanceof FormData)) {
    return "<unavailable>";
  }
  const limit = Number.isFinite(maxFields) ? Math.max(5, Number(maxFields)) : 120;
  const lines = [];
  let count = 0;
  for (const [key, rawValue] of formData.entries()) {
    count += 1;
    if (count > limit) {
      break;
    }
    if (rawValue instanceof File) {
      lines.push(`${key}=<file:${rawValue.name};size=${rawValue.size}>`);
      continue;
    }
    const value = truncateDiagnosticBody(String(rawValue || ""), 300);
    lines.push(`${key}=${value}`);
  }
  if (count === 0) {
    return "<empty>";
  }
  if (count > limit) {
    lines.push(`...[truncated ${count - limit} fields]`);
  }
  return lines.join(`
`);
}
async function copyTextToClipboard(text) {
  const value = String(text || "");
  if (!value) {
    return false;
  }
  if (navigator.clipboard && typeof navigator.clipboard.writeText === "function") {
    try {
      await navigator.clipboard.writeText(value);
      return true;
    } catch (error) {}
  }
  const helper = document.createElement("textarea");
  helper.value = value;
  helper.setAttribute("readonly", "readonly");
  helper.style.position = "fixed";
  helper.style.opacity = "0";
  document.body.appendChild(helper);
  helper.select();
  const copied = document.execCommand("copy");
  document.body.removeChild(helper);
  return copied;
}

// PmTracker.Web/wwwroot/js/modules/filters.js
var projectFilterStoragePrefix = "pmtracker.projectFilters.v1.project.";
var projectRecordFilterPanelStorageKey = "pmtracker.filters.open";
var legacyProjectFilterPrefixes = [
  "pmtracker.filter.",
  "pmtracker.schedule.filter.",
  "pmtracker.gantt.filter."
];
var legacyProjectFilterKeys = [
  "pmtracker.records.view",
  "pmtracker.gantt.filters.open"
];
var legacyGanttStoragePrefixes = [
  "pmtracker.gantt.pinned.",
  "pmtracker.gantt.expanded."
];
var recordMeetingCommentStateCache = new Map;
var recordMeetingCommentStateRequests = new Map;
var recordMeetingCommentStateLoadingMessage = "Načítání dat pro filtr jednání-vyjádření...";
var recordMeetingCommentStateErrorMessage = "Nepodařilo se načíst data pro filtr jednání-vyjádření.";
var projectFilterConfigs = {
  records: {
    rootSelector: '[data-project-filter-scope="records"]',
    inputSelector: "[data-filter-key]",
    keyAttribute: "data-filter-key",
    chipRowSelector: '[data-filter-chip-row="records"]',
    statusSelector: '[data-filter-save-status="records"]',
    fields: [
      { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
      { inputKey: "sortBy", stateKey: "sortBy", type: "select", skipChip: true },
      { inputKey: "kategorie", stateKey: "kategorie", type: "select", chipLabel: "Kategorie" },
      { inputKey: "stav", stateKey: "stav", type: "select", chipLabel: "Stav úkolu" },
      { inputKey: "typ", stateKey: "typ", type: "select", chipLabel: "Typ úkolu" },
      { inputKey: "vlastnik", stateKey: "vlastnik", type: "select", chipLabel: "Vlastník" },
      { inputKey: "aktivni", stateKey: "aktivni", type: "checkbox", chipLabel: "Pouze aktivní úkoly" },
      { inputKey: "mine", stateKey: "mine", type: "checkbox", chipLabel: "Jen mé záznamy" },
      { inputKey: "jednani-vyjadreni-stav", stateKey: "jednaniVyjadreniStav", type: "select", chipLabel: "Jednání-vyjádření" },
      { inputKey: "groupBySubsystem", stateKey: "groupBySubsystem", type: "checkbox", skipChip: true }
    ]
  },
  schedule: {
    rootSelector: '[data-project-filter-scope="schedule"]',
    inputSelector: "[data-schedule-filter-key]",
    keyAttribute: "data-schedule-filter-key",
    chipRowSelector: '[data-filter-chip-row="schedule"]',
    statusSelector: '[data-filter-save-status="schedule"]',
    fields: [
      { inputKey: "subsystem", stateKey: "subsystem", type: "select", chipLabel: "Subsystém" },
      { inputKey: "sortBy", stateKey: "sortBy", type: "select", skipChip: true }
    ]
  }
};
var projectPrintRelevantRecordStateKeys = [
  "subsystem",
  "kategorie",
  "stav",
  "typ",
  "vlastnik",
  "aktivni",
  "mine",
  "jednaniVyjadreniStav"
];
function normalizeFilterText2(value) {
  if (!value) {
    return "";
  }
  return value.toString().trim().toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "");
}
function normalizeFilterToken2(value) {
  if (value === null || value === undefined) {
    return "";
  }
  return String(value).trim().toUpperCase();
}
function normalizeSubsystemSortMode(value) {
  const candidate = String(value || "").trim().toLowerCase();
  if (candidate === "alpha-asc" || candidate === "alpha-desc" || candidate === "project-desc") {
    return candidate;
  }
  return "project-asc";
}
function buildSubsystemSortMeta(source = {}) {
  return {
    name: String(source.name || "").trim() || "-",
    code: String(source.code || "").trim(),
    order: Number.isInteger(source.order) ? source.order : Number.parseInt(source.order || "0", 10) || 0,
    hasProjectOrder: source.hasProjectOrder === true || source.hasProjectOrder === "true"
  };
}
function readSubsystemGroupSortMeta(element) {
  if (!(element instanceof Element)) {
    return buildSubsystemSortMeta();
  }
  return buildSubsystemSortMeta({
    name: element.getAttribute("data-subsystem-name") || "",
    code: element.getAttribute("data-subsystem-kod") || "",
    order: element.getAttribute("data-subsystem-order") || "0",
    hasProjectOrder: element.getAttribute("data-subsystem-order-active") === "true"
  });
}
function compareSubsystemAlpha(left, right) {
  const byName = left.name.localeCompare(right.name, "cs");
  if (byName !== 0) {
    return byName;
  }
  return left.code.localeCompare(right.code, "cs");
}
function compareSubsystemSortMeta(leftSource, rightSource, sortMode) {
  const left = buildSubsystemSortMeta(leftSource);
  const right = buildSubsystemSortMeta(rightSource);
  const resolvedMode = normalizeSubsystemSortMode(sortMode);
  const alpha = compareSubsystemAlpha(left, right);
  if (resolvedMode === "alpha-asc") {
    return alpha;
  }
  if (resolvedMode === "alpha-desc") {
    return alpha * -1;
  }
  if (left.hasProjectOrder && right.hasProjectOrder) {
    if (left.order !== right.order) {
      return resolvedMode === "project-desc" ? right.order - left.order : left.order - right.order;
    }
    return alpha;
  }
  if (left.hasProjectOrder !== right.hasProjectOrder) {
    return left.hasProjectOrder ? -1 : 1;
  }
  return resolvedMode === "project-desc" ? alpha * -1 : alpha;
}
function sortSubsystemGroupsInContainer(container, sortMode) {
  if (!(container instanceof Element)) {
    return [];
  }
  const groups = Array.from(container.querySelectorAll("[data-subsystem-group]")).filter((group) => group instanceof HTMLElement);
  groups.sort((left, right) => compareSubsystemSortMeta(readSubsystemGroupSortMeta(left), readSubsystemGroupSortMeta(right), sortMode)).forEach((group) => container.appendChild(group));
  return groups;
}
function buildProjectPrintFilterSnapshot() {
  const state = buildProjectFilterStateFromInputs("records");
  const snapshot = {
    subsystem: typeof state.subsystem === "string" ? state.subsystem.trim() : "",
    kategorie: typeof state.kategorie === "string" ? state.kategorie.trim() : "",
    stav: typeof state.stav === "string" ? state.stav.trim() : "",
    typ: typeof state.typ === "string" ? state.typ.trim() : "",
    vlastnik: typeof state.vlastnik === "string" ? state.vlastnik.trim() : "",
    aktivni: Boolean(state.aktivni),
    mine: Boolean(state.mine),
    jednaniVyjadreniStav: typeof state.jednaniVyjadreniStav === "string" ? state.jednaniVyjadreniStav.trim() : ""
  };
  return {
    ...snapshot,
    hasRelevantFilters: projectPrintRelevantRecordStateKeys.some((key) => Boolean(snapshot[key]))
  };
}
function buildProjectPrintFilterQueryParams(useCurrentFilters) {
  const snapshot = buildProjectPrintFilterSnapshot();
  const params = new URLSearchParams;
  if (!useCurrentFilters) {
    params.set("useCurrentFilters", "false");
    return { snapshot, params };
  }
  params.set("useCurrentFilters", "true");
  if (snapshot.subsystem) {
    params.set("subsystem", snapshot.subsystem);
  }
  if (snapshot.kategorie) {
    params.set("kategorie", snapshot.kategorie);
  }
  if (snapshot.stav) {
    params.set("stav", snapshot.stav);
  }
  if (snapshot.typ) {
    params.set("typ", snapshot.typ);
  }
  if (snapshot.vlastnik) {
    params.set("vlastnik", snapshot.vlastnik);
  }
  if (snapshot.aktivni) {
    params.set("aktivni", "true");
  }
  if (snapshot.mine) {
    params.set("mine", "true");
  }
  if (snapshot.jednaniVyjadreniStav) {
    params.set("jednaniVyjadreniStav", snapshot.jednaniVyjadreniStav);
  }
  return { snapshot, params };
}
function getProjectFilterRoot(scope) {
  const config = getProjectFilterConfig(scope);
  if (!config) {
    return null;
  }
  const root = document.querySelector(config.rootSelector);
  return root instanceof HTMLElement ? root : null;
}
function getProjectFilterProjectId(scope) {
  const root = getProjectFilterRoot(scope);
  const projectId = (root?.dataset.projectId || "").trim();
  return projectId || "0";
}
function getProjectDetailRoot() {
  const root = document.querySelector("[data-project-detail-root]");
  return root instanceof HTMLElement ? root : null;
}
function getProjectRecordsPanel() {
  const panel = document.querySelector('[data-tab-panel="zaznamy"]');
  return panel instanceof HTMLElement ? panel : null;
}
function normalizeRecordMeetingCommentStatesPayload(payload) {
  if (!payload || typeof payload !== "object") {
    return {};
  }
  const rawStates = payload.statesByRecordId && typeof payload.statesByRecordId === "object" ? payload.statesByRecordId : payload;
  const normalized = {};
  Object.entries(rawStates).forEach(([recordId, values]) => {
    const normalizedRecordId = String(recordId || "").trim();
    if (!normalizedRecordId) {
      return;
    }
    const normalizedValues = Array.isArray(values) ? values.map((value) => normalizeFilterToken2(value)).filter(Boolean) : [];
    normalized[normalizedRecordId] = Array.from(new Set(normalizedValues));
  });
  return normalized;
}
function applyCachedRecordMeetingCommentStates(projectId) {
  const normalizedProjectId = String(projectId || "").trim();
  if (!normalizedProjectId || !recordMeetingCommentStateCache.has(normalizedProjectId)) {
    return false;
  }
  const statesByRecordId = recordMeetingCommentStateCache.get(normalizedProjectId) || {};
  const recordsPanel = getProjectRecordsPanel();
  if (!(recordsPanel instanceof HTMLElement)) {
    return false;
  }
  recordsPanel.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
    if (!(card instanceof HTMLElement)) {
      return;
    }
    const recordId = (card.dataset.recordId || "").trim();
    const values = Array.isArray(statesByRecordId[recordId]) ? statesByRecordId[recordId] : [];
    card.dataset.filterVyjadreniJednaniStavy = values.join("|");
  });
  return true;
}
async function ensureRecordMeetingCommentStatesLoaded() {
  const projectRoot = getProjectDetailRoot();
  const projectId = getProjectFilterProjectId("records");
  const loadUrl = (projectRoot?.dataset.recordMeetingCommentStatesUrl || "").trim();
  if (!projectId || projectId === "0" || !loadUrl) {
    return false;
  }
  if (applyCachedRecordMeetingCommentStates(projectId)) {
    return true;
  }
  const existingRequest = recordMeetingCommentStateRequests.get(projectId);
  if (existingRequest instanceof Promise) {
    return existingRequest;
  }
  setProjectFilterSaveStatus("records", recordMeetingCommentStateLoadingMessage);
  const request = (async () => {
    try {
      const response = await fetch(loadUrl, {
        headers: {
          Accept: "application/json",
          "X-Requested-With": "XMLHttpRequest"
        },
        credentials: "same-origin"
      });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }
      const payload = await response.json();
      recordMeetingCommentStateCache.set(projectId, normalizeRecordMeetingCommentStatesPayload(payload));
      applyCachedRecordMeetingCommentStates(projectId);
      setProjectFilterSaveStatus("records", "");
      applyProjectRecordFilters();
      return true;
    } catch (error) {
      setProjectFilterSaveStatus("records", recordMeetingCommentStateErrorMessage);
      return false;
    } finally {
      recordMeetingCommentStateRequests.delete(projectId);
    }
  })();
  recordMeetingCommentStateRequests.set(projectId, request);
  return request;
}
function readJsonStorage(storage, key) {
  if (!key) {
    return null;
  }
  try {
    const raw = storage.getItem(key);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw);
    return parsed && typeof parsed === "object" ? parsed : null;
  } catch (error) {
    return null;
  }
}
function writeJsonStorage(storage, key, value) {
  if (!key) {
    return;
  }
  storage.setItem(key, JSON.stringify(value));
}
function hasSelectOptionValue(input, value) {
  if (!(input instanceof HTMLSelectElement)) {
    return false;
  }
  return Array.from(input.options).some((option) => option.value === value);
}
function readProjectFilterInputValue(input, field) {
  if (input instanceof HTMLInputElement && field.type === "checkbox") {
    return input.checked;
  }
  if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
    return input.value;
  }
  return field.type === "checkbox" ? false : "";
}
function applyProjectFilterStateToInputs(scope, state) {
  const config = getProjectFilterConfig(scope);
  if (!config) {
    return;
  }
  config.fields.forEach((field) => {
    const input = getProjectFilterInput(scope, field.inputKey);
    if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement)) {
      return;
    }
    const value = state[field.stateKey];
    if (field.type === "checkbox" && input instanceof HTMLInputElement) {
      input.checked = Boolean(value);
      return;
    }
    input.value = typeof value === "string" ? value : "";
  });
}
function readStoredProjectFilterState(scope, kind, fallbackState) {
  const storage = kind === "state" ? sessionStorage : localStorage;
  const key = getProjectFilterStorageKey(scope, kind);
  const rawState = readJsonStorage(storage, key);
  if (!rawState) {
    return null;
  }
  return normalizeProjectFilterState(scope, rawState, fallbackState);
}
function persistProjectFilterSessionState(scope) {
  const currentState = buildProjectFilterStateFromInputs(scope);
  const normalizedState = normalizeProjectFilterState(scope, currentState, currentState);
  const key = getProjectFilterStorageKey(scope, "state");
  writeJsonStorage(sessionStorage, key, normalizedState);
  return normalizedState;
}
function buildProjectFilterChipLabel(field, input) {
  if (field.type === "checkbox") {
    return field.chipLabel || "";
  }
  if (!(input instanceof HTMLSelectElement)) {
    return "";
  }
  const option = input.selectedOptions[0];
  const optionText = option?.textContent?.trim() || "";
  if (!optionText) {
    return "";
  }
  return `${field.chipLabel}: ${optionText}`;
}
function applyProjectFilterScope(scope, options = {}) {
  if (scope === "records") {
    const state = buildProjectFilterStateFromInputs(scope);
    applyRecordsView(Boolean(state.groupBySubsystem) ? "subsystem" : "flat");
    return;
  }
  if (typeof options.applyScope === "function") {
    options.applyScope(scope, buildProjectFilterStateFromInputs(scope));
  }
}
function removeMatchingStorageKeys(storage, predicate) {
  const keys = [];
  for (let i = 0;i < storage.length; i += 1) {
    const key = storage.key(i);
    if (key && predicate(key)) {
      keys.push(key);
    }
  }
  keys.forEach((key) => storage.removeItem(key));
}
function getProjectFilterConfig(scope) {
  return projectFilterConfigs[scope] || null;
}
function getProjectFilterInput(scope, inputKey) {
  const config = getProjectFilterConfig(scope);
  const root = getProjectFilterRoot(scope);
  if (!config || !(root instanceof HTMLElement)) {
    return null;
  }
  const input = root.querySelector(`${config.inputSelector}[${config.keyAttribute}="${inputKey}"]`);
  return input instanceof HTMLInputElement || input instanceof HTMLSelectElement ? input : null;
}
function getProjectFilterCurrentUserId(scope) {
  return normalizeFilterToken2(getProjectFilterRoot(scope)?.dataset.currentUserId || "");
}
function getProjectFilterStorageKey(scope, kind) {
  const projectId = getProjectFilterProjectId(scope);
  if (!projectId || projectId === "0") {
    return "";
  }
  return `${projectFilterStoragePrefix}${projectId}.${scope}.${kind}`;
}
function normalizeProjectFilterState(scope, rawState, fallbackState) {
  const config = getProjectFilterConfig(scope);
  if (!config) {
    return {};
  }
  const normalized = {};
  const source = rawState && typeof rawState === "object" ? rawState : {};
  const fallback = fallbackState && typeof fallbackState === "object" ? fallbackState : {};
  config.fields.forEach((field) => {
    const input = getProjectFilterInput(scope, field.inputKey);
    const fallbackValue = fallback[field.stateKey];
    const sourceValue = source[field.stateKey];
    if (field.type === "checkbox") {
      if (typeof sourceValue === "boolean") {
        normalized[field.stateKey] = sourceValue;
        return;
      }
      if (sourceValue === "true" || sourceValue === "false") {
        normalized[field.stateKey] = sourceValue === "true";
        return;
      }
      normalized[field.stateKey] = Boolean(fallbackValue);
      return;
    }
    const fallbackText = typeof fallbackValue === "string" ? fallbackValue : "";
    const candidate = typeof sourceValue === "string" ? sourceValue : fallbackText;
    if (candidate && input instanceof HTMLSelectElement && !hasSelectOptionValue(input, candidate)) {
      normalized[field.stateKey] = fallbackText && hasSelectOptionValue(input, fallbackText) ? fallbackText : "";
      return;
    }
    normalized[field.stateKey] = candidate;
  });
  return normalized;
}
function buildProjectFilterStateFromInputs(scope) {
  const config = getProjectFilterConfig(scope);
  if (!config) {
    return {};
  }
  const state = {};
  config.fields.forEach((field) => {
    const input = getProjectFilterInput(scope, field.inputKey);
    state[field.stateKey] = readProjectFilterInputValue(input, field);
  });
  return state;
}
function setProjectFilterSaveStatus(scope, message) {
  const config = getProjectFilterConfig(scope);
  const root = getProjectFilterRoot(scope);
  if (!config || !(root instanceof HTMLElement)) {
    return;
  }
  const status = root.querySelector(config.statusSelector);
  if (status instanceof HTMLElement) {
    status.textContent = message || "";
  }
}
function renderProjectFilterChips(scope) {
  const config = getProjectFilterConfig(scope);
  const root = getProjectFilterRoot(scope);
  if (!config || !(root instanceof HTMLElement)) {
    return;
  }
  const chipRow = root.querySelector(config.chipRowSelector);
  if (!(chipRow instanceof HTMLElement)) {
    return;
  }
  chipRow.innerHTML = "";
  const state = buildProjectFilterStateFromInputs(scope);
  const chips = [];
  config.fields.forEach((field) => {
    if (field.skipChip) {
      return;
    }
    const value = state[field.stateKey];
    const isActive = field.type === "checkbox" ? Boolean(value) : Boolean(value);
    if (!isActive) {
      return;
    }
    const input = getProjectFilterInput(scope, field.inputKey);
    const label = buildProjectFilterChipLabel(field, input);
    if (!label) {
      return;
    }
    const chip = document.createElement("span");
    chip.className = "active-filter-chip";
    const text = document.createElement("span");
    text.className = "active-filter-chip-label";
    text.textContent = label;
    const remove = document.createElement("button");
    remove.type = "button";
    remove.className = "active-filter-chip-remove";
    remove.setAttribute("data-filter-chip-remove", scope);
    remove.setAttribute("data-filter-chip-key", field.inputKey);
    remove.setAttribute("aria-label", `Odebrat filtr ${label}`);
    remove.textContent = "×";
    chip.append(text, remove);
    chips.push(chip);
  });
  chipRow.hidden = chips.length === 0;
  chips.forEach((chip) => chipRow.appendChild(chip));
}
function handleProjectFilterInputChange(scope, options = {}) {
  persistProjectFilterSessionState(scope);
  renderProjectFilterChips(scope);
  setProjectFilterSaveStatus(scope, "");
  applyProjectFilterScope(scope, options);
}
function restoreProjectFilterScope(scope) {
  const fallbackState = buildProjectFilterStateFromInputs(scope);
  const restoredState = readStoredProjectFilterState(scope, "state", fallbackState) || readStoredProjectFilterState(scope, "defaults", fallbackState) || fallbackState;
  applyProjectFilterStateToInputs(scope, restoredState);
  persistProjectFilterSessionState(scope);
  renderProjectFilterChips(scope);
  return restoredState;
}
function saveProjectFilterDefaults(scope) {
  const state = persistProjectFilterSessionState(scope);
  const key = getProjectFilterStorageKey(scope, "defaults");
  writeJsonStorage(localStorage, key, state);
  setProjectFilterSaveStatus(scope, "Výchozí filtry uloženy v tomto prohlížeči.");
}
function clearProjectFilterInput(scope, inputKey) {
  const input = getProjectFilterInput(scope, inputKey);
  if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement)) {
    return;
  }
  if (input instanceof HTMLInputElement && input.type === "checkbox") {
    input.checked = false;
  } else {
    input.value = "";
  }
}
function clearProjectFilterPreferenceStorage() {
  removeMatchingStorageKeys(localStorage, (key) => key.startsWith(projectFilterStoragePrefix) || legacyProjectFilterKeys.includes(key) || legacyProjectFilterPrefixes.some((prefix) => key.startsWith(prefix)) || legacyGanttStoragePrefixes.some((prefix) => key.startsWith(prefix)));
  removeMatchingStorageKeys(sessionStorage, (key) => key.startsWith(projectFilterStoragePrefix) || legacyProjectFilterKeys.includes(key) || legacyProjectFilterPrefixes.some((prefix) => key.startsWith(prefix)) || legacyGanttStoragePrefixes.some((prefix) => key.startsWith(prefix)));
}
function setFilterPanelOpen(open) {
  const filterPanel = document.querySelector("[data-filter-panel]");
  const filterToggle = document.querySelector("[data-filter-toggle]");
  if (!(filterPanel instanceof HTMLElement)) {
    return;
  }
  filterPanel.classList.toggle("collapsed", !open);
  if (filterToggle instanceof HTMLElement) {
    filterToggle.setAttribute("aria-expanded", String(open));
  }
  localStorage.setItem(projectRecordFilterPanelStorageKey, String(open));
}
function setRecordFilterVisibility(element, isVisible) {
  if (!(element instanceof HTMLElement)) {
    return;
  }
  element.classList.toggle("is-filter-hidden", !isVisible);
  element.hidden = !isVisible;
}
function applyProjectRecordFilters() {
  const recordsPanel = getProjectRecordsPanel();
  if (!(recordsPanel instanceof HTMLElement)) {
    return;
  }
  const cards = recordsPanel.querySelectorAll(".record-card[data-record-id]");
  if (cards.length === 0) {
    return;
  }
  const state = buildProjectFilterStateFromInputs("records");
  const currentUserId = getProjectFilterCurrentUserId("records");
  const hasCurrentUser = currentUserId && currentUserId !== "0";
  const filters = {
    subsystem: normalizeFilterToken2(state.subsystem),
    kategorie: normalizeFilterToken2(state.kategorie),
    stav: normalizeFilterToken2(state.stav),
    typ: normalizeFilterToken2(state.typ),
    vlastnik: normalizeFilterToken2(state.vlastnik),
    onlyActive: Boolean(state.aktivni),
    mine: Boolean(state.mine),
    meetingCommentState: normalizeFilterToken2(state.jednaniVyjadreniStav)
  };
  const projectId = getProjectFilterProjectId("records");
  const hasMeetingCommentStateCache = applyCachedRecordMeetingCommentStates(projectId);
  if (filters.meetingCommentState && !hasMeetingCommentStateCache) {
    ensureRecordMeetingCommentStatesLoaded();
  }
  cards.forEach((item) => {
    if (!(item instanceof HTMLElement)) {
      return;
    }
    const subsystem = normalizeFilterToken2(item.dataset.filterSubsystemKod || item.dataset.filterSubsystem);
    const kategorie = normalizeFilterToken2(item.dataset.filterKategorieKod || item.dataset.filterKategorie);
    const stav = normalizeFilterToken2(item.dataset.filterStavKod || item.dataset.filterStav);
    const typ = normalizeFilterToken2(item.dataset.filterTypKod || item.dataset.filterTyp);
    const vlastnik = normalizeFilterToken2(item.dataset.filterVlastnikId || item.dataset.filterVlastnik);
    const isActive = item.dataset.filterAktivni === "true";
    const isTask = item.dataset.filterJeUkol === "true";
    const commentMeetingStates = (item.dataset.filterVyjadreniJednaniStavy || "").split(/[|,]/g).map((value) => normalizeFilterToken2(value)).filter(Boolean);
    const matchesMeetingCommentState = !filters.meetingCommentState || !hasMeetingCommentStateCache || isTask && commentMeetingStates.includes(filters.meetingCommentState);
    const matchesMine = !filters.mine || hasCurrentUser && vlastnik === currentUserId;
    const matches = (!filters.subsystem || subsystem === filters.subsystem) && (!filters.kategorie || kategorie === filters.kategorie) && (!filters.stav || stav === filters.stav) && (!filters.typ || typ === filters.typ) && (!filters.vlastnik || vlastnik === filters.vlastnik) && (!filters.onlyActive || isActive) && matchesMine && matchesMeetingCommentState;
    setRecordFilterVisibility(item, matches);
  });
  recordsPanel.querySelectorAll(".subsystem-group").forEach((group) => {
    if (!(group instanceof HTMLElement)) {
      return;
    }
    const hasVisibleCards = Array.from(group.querySelectorAll(".record-card")).some((card) => card instanceof HTMLElement && !card.hidden);
    setRecordFilterVisibility(group, hasVisibleCards);
  });
  scheduleSubsystemIndicatorSync();
}
function resolveCurrentSubsystemGroup(groups, anchorY) {
  if (!Array.isArray(groups) || groups.length === 0) {
    return null;
  }
  let current = groups[0];
  for (const group of groups) {
    if (!(group instanceof HTMLElement)) {
      continue;
    }
    const rect = group.getBoundingClientRect();
    if (rect.bottom <= anchorY) {
      current = group;
      continue;
    }
    if (rect.top <= anchorY) {
      current = group;
    }
    break;
  }
  return current;
}
function resolveActiveSubsystemIndicatorShell() {
  const activePanel = document.querySelector(".tab-panel.active");
  if (!(activePanel instanceof HTMLElement)) {
    return null;
  }
  const groupedShell = activePanel.querySelector("[data-subsystem-grouped-shell]");
  if (!(groupedShell instanceof HTMLElement) || groupedShell.hidden) {
    return null;
  }
  return groupedShell;
}
function updateSubsystemScrollIndicator() {
  const indicator = document.querySelector("[data-subsystem-scroll-indicator]");
  const bubble = document.querySelector("[data-subsystem-scroll-indicator-bubble]");
  const label = document.querySelector("[data-subsystem-scroll-indicator-label]");
  if (!(indicator instanceof HTMLElement) || !(bubble instanceof HTMLElement) || !(label instanceof HTMLElement)) {
    return;
  }
  if (window.scrollY <= 0) {
    indicator.hidden = true;
    return;
  }
  const groupedShell = resolveActiveSubsystemIndicatorShell();
  if (!(groupedShell instanceof HTMLElement)) {
    indicator.hidden = true;
    return;
  }
  const visibleGroups = Array.from(groupedShell.querySelectorAll("[data-subsystem-group]")).filter((group) => group instanceof HTMLElement && !group.hidden);
  if (visibleGroups.length === 0) {
    indicator.hidden = true;
    return;
  }
  const shellRect = groupedShell.getBoundingClientRect();
  if (shellRect.bottom <= 120 || shellRect.top >= window.innerHeight) {
    indicator.hidden = true;
    return;
  }
  const anchorY = Math.max(132, Math.min(window.innerHeight * 0.35, 220));
  const currentGroup = resolveCurrentSubsystemGroup(visibleGroups, anchorY);
  const subsystemName = currentGroup instanceof HTMLElement ? (currentGroup.getAttribute("data-subsystem-name") || "").trim() : "";
  if (!subsystemName) {
    indicator.hidden = true;
    return;
  }
  const bubbleTravel = Math.max(0, indicator.clientHeight - bubble.offsetHeight);
  const currentRect = currentGroup.getBoundingClientRect();
  const currentCenter = currentRect.top + currentRect.height / 2;
  const progress = Math.max(0, Math.min(1, (currentCenter - shellRect.top) / Math.max(shellRect.height, 1)));
  bubble.style.transform = `translateY(${Math.round(progress * bubbleTravel)}px)`;
  label.textContent = subsystemName;
  indicator.hidden = false;
}
function scheduleSubsystemIndicatorSync() {
  if (!(document.body instanceof HTMLElement)) {
    return;
  }
  const currentFrame = Number.parseInt(document.body.dataset.subsystemIndicatorFrame || "0", 10);
  if (Number.isInteger(currentFrame) && currentFrame > 0) {
    window.cancelAnimationFrame(currentFrame);
  }
  const nextFrame = window.requestAnimationFrame(() => {
    document.body.dataset.subsystemIndicatorFrame = "0";
    updateSubsystemScrollIndicator();
  });
  document.body.dataset.subsystemIndicatorFrame = String(nextFrame);
}
function initSubsystemScrollIndicator() {
  const indicator = document.querySelector("[data-subsystem-scroll-indicator]");
  if (!(indicator instanceof HTMLElement) || !(document.body instanceof HTMLElement)) {
    return;
  }
  if (document.body.dataset.subsystemIndicatorReady !== "true") {
    document.body.dataset.subsystemIndicatorReady = "true";
    window.addEventListener("scroll", scheduleSubsystemIndicatorSync, { passive: true });
    window.addEventListener("resize", scheduleSubsystemIndicatorSync);
  }
  scheduleSubsystemIndicatorSync();
}
function applyRecordsView(view) {
  const recordsPanel = getProjectRecordsPanel();
  if (!(recordsPanel instanceof HTMLElement)) {
    return;
  }
  const groupBySubsystemInput = getProjectFilterInput("records", "groupBySubsystem");
  const resolvedView = groupBySubsystemInput instanceof HTMLInputElement ? groupBySubsystemInput.checked ? "subsystem" : "flat" : view;
  const shells = recordsPanel.querySelectorAll("[data-records-view]");
  if (shells.length === 0) {
    return;
  }
  const groupedList = recordsPanel.querySelector("[data-record-grouped-list]");
  const flatList = recordsPanel.querySelector("[data-record-flat-list]");
  const cards = Array.from(recordsPanel.querySelectorAll(".record-card[data-record-id]")).filter((card) => card instanceof HTMLElement);
  const sortMode = normalizeSubsystemSortMode(buildProjectFilterStateFromInputs("records").sortBy);
  if (groupedList instanceof HTMLElement && flatList instanceof HTMLElement && cards.length > 0) {
    const groupsByKey = new Map;
    const orderedGroups = [];
    cards.forEach((card) => {
      const subsystemName = (card.getAttribute("data-filter-subsystem") || "").trim() || "-";
      const subsystemCode = (card.getAttribute("data-filter-subsystem-kod") || "").trim();
      const subsystemOrder = Number.parseInt(card.getAttribute("data-filter-subsystem-order") || "0", 10) || 0;
      const subsystemHasProjectOrder = card.getAttribute("data-filter-subsystem-order-active") === "true";
      const groupKey = `${subsystemCode}\x00${subsystemName}`;
      let group = groupsByKey.get(groupKey);
      if (!group) {
        group = {
          meta: {
            name: subsystemName,
            code: subsystemCode,
            order: subsystemOrder,
            hasProjectOrder: subsystemHasProjectOrder
          },
          cards: []
        };
        groupsByKey.set(groupKey, group);
        orderedGroups.push(group);
      }
      group.cards.push(card);
    });
    orderedGroups.sort((left, right) => compareSubsystemSortMeta(left.meta, right.meta, sortMode));
    if (resolvedView === "subsystem") {
      groupedList.innerHTML = "";
      orderedGroups.forEach((group) => {
        const currentGroup = document.createElement("div");
        currentGroup.className = "subsystem-group";
        currentGroup.setAttribute("data-subsystem-group", "");
        currentGroup.setAttribute("data-subsystem-name", group.meta.name);
        currentGroup.setAttribute("data-subsystem-kod", group.meta.code);
        currentGroup.setAttribute("data-subsystem-order", String(group.meta.order));
        currentGroup.setAttribute("data-subsystem-order-active", String(group.meta.hasProjectOrder));
        const heading = document.createElement("h3");
        heading.textContent = group.meta.name;
        currentGroup.appendChild(heading);
        const currentGroupCards = document.createElement("div");
        currentGroupCards.className = "card-list";
        group.cards.forEach((card) => currentGroupCards.appendChild(card));
        currentGroup.appendChild(currentGroupCards);
        groupedList.appendChild(currentGroup);
      });
    } else {
      orderedGroups.forEach((group) => {
        group.cards.forEach((card) => flatList.appendChild(card));
      });
      groupedList.innerHTML = "";
    }
  }
  shells.forEach((shell) => {
    const mode = shell.getAttribute("data-records-view");
    shell.toggleAttribute("hidden", mode !== resolvedView);
  });
  applyProjectRecordFilters();
  scheduleSubsystemIndicatorSync();
}
function restoreFilterState() {
  return restoreProjectFilterScope("records");
}
function persistFilterState(input, options = {}) {
  handleProjectFilterInputChange("records", options);
}
function invalidateRecordMeetingCommentStates(projectId) {
  const normalizedProjectId = String(projectId || "").trim();
  if (!normalizedProjectId) {
    return;
  }
  recordMeetingCommentStateCache.delete(normalizedProjectId);
  recordMeetingCommentStateRequests.delete(normalizedProjectId);
}

// PmTracker.Web/wwwroot/js/modules/ui.js
var printFormatStorageKey = "pmtracker.print.preferredFormat";
var printState = {
  popover: null,
  trigger: null,
  hoverTimerId: 0
};
var floatingPanelRegistry = new Set;
var globalFloatingRoot = null;
function getStoredPrintFormat() {
  const value = localStorage.getItem(printFormatStorageKey);
  if (value === "pdf" || value === "word") {
    return value;
  }
  return null;
}
function setStoredPrintFormat(format) {
  if (format !== "pdf" && format !== "word") {
    return;
  }
  localStorage.setItem(printFormatStorageKey, format);
  refreshPrintPreferenceUi();
}
function clearStoredPrintFormat() {
  localStorage.removeItem(printFormatStorageKey);
  refreshPrintPreferenceUi();
}
function getPrintFormatLabel(format) {
  if (format === "pdf") {
    return "PDF";
  }
  if (format === "word") {
    return "WORD";
  }
  return "není nastaven";
}
function refreshPrintPreferenceUi() {
  const preferred = getStoredPrintFormat();
  document.querySelectorAll("[data-print-preference-current]").forEach((element) => {
    element.textContent = getPrintFormatLabel(preferred);
  });
  document.querySelectorAll("[data-print-preference-reset]").forEach((element) => {
    if (element instanceof HTMLButtonElement) {
      element.disabled = preferred === null;
    }
  });
}
function clearPrintHoverTimer() {
  if (printState.hoverTimerId) {
    window.clearTimeout(printState.hoverTimerId);
    printState.hoverTimerId = 0;
  }
}
function closePrintChooser(options) {
  const settings = options || {};
  const restoreFocus = Boolean(settings.restoreFocus);
  const trigger = printState.trigger;
  if (printState.popover instanceof HTMLElement) {
    printState.popover.remove();
  }
  printState.popover = null;
  printState.trigger = null;
  clearPrintHoverTimer();
  if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
    trigger.focus({ preventScroll: true });
  }
}
function resolvePrintUrl(trigger, format, options) {
  const settings = options || {};
  if (!(trigger instanceof Element)) {
    return "";
  }
  if (format === "word") {
    if (typeof settings.wordUrlOverride === "string" && settings.wordUrlOverride) {
      return settings.wordUrlOverride;
    }
    return trigger.getAttribute("data-print-word-url") || "";
  }
  if (typeof settings.pdfUrlOverride === "string" && settings.pdfUrlOverride) {
    return settings.pdfUrlOverride;
  }
  return trigger.getAttribute("data-print-pdf-url") || trigger.getAttribute("href") || "";
}
function isProjectPrintTrigger(trigger) {
  return trigger instanceof HTMLElement && trigger.dataset.projectPrintTrigger === "true";
}
function shouldPromptForProjectPrintFilters(trigger) {
  return isProjectPrintTrigger(trigger) && buildProjectPrintFilterSnapshot().hasRelevantFilters;
}
function buildProjectPrintUrl(trigger, format, useCurrentFilters) {
  const baseUrl = resolvePrintUrl(trigger, format);
  if (!baseUrl) {
    return "";
  }
  const url = new URL(baseUrl, window.location.origin);
  const { params } = buildProjectPrintFilterQueryParams(useCurrentFilters);
  params.forEach((value, key) => {
    url.searchParams.set(key, value);
  });
  return url.toString();
}
function openPrintUrl(url) {
  if (!url) {
    return;
  }
  const link = document.createElement("a");
  link.href = url;
  link.target = "_blank";
  link.rel = "noopener";
  link.style.display = "none";
  document.body.appendChild(link);
  link.click();
  link.remove();
}
function positionPrintChooser(popover, trigger) {
  if (!(popover instanceof HTMLElement) || !(trigger instanceof HTMLElement)) {
    return;
  }
  const rect = trigger.getBoundingClientRect();
  const width = popover.offsetWidth;
  const height = popover.offsetHeight;
  const gap = 8;
  let left = rect.right - width;
  let top = rect.bottom + gap;
  if (left < gap) {
    left = gap;
  }
  if (left + width > window.innerWidth - gap) {
    left = Math.max(gap, window.innerWidth - width - gap);
  }
  if (top + height > window.innerHeight - gap) {
    top = rect.top - height - gap;
  }
  if (top < gap) {
    top = gap;
  }
  popover.style.left = `${Math.round(left)}px`;
  popover.style.top = `${Math.round(top)}px`;
}
function handlePrintChoice(trigger, format, shouldRemember, options) {
  const url = resolvePrintUrl(trigger, format, options);
  if (!url) {
    return;
  }
  if (shouldRemember) {
    setStoredPrintFormat(format);
  }
  openPrintUrl(url);
}
function handleProjectPrintScopeChoice(trigger, useCurrentFilters) {
  const preferredFormat = getStoredPrintFormat();
  if (preferredFormat) {
    const preferredUrl = buildProjectPrintUrl(trigger, preferredFormat, useCurrentFilters);
    if (preferredUrl) {
      closePrintChooser({ restoreFocus: false });
      openPrintUrl(preferredUrl);
      return;
    }
  }
  showPrintChooser(trigger, {
    quickMode: false,
    pdfUrlOverride: buildProjectPrintUrl(trigger, "pdf", useCurrentFilters),
    wordUrlOverride: buildProjectPrintUrl(trigger, "word", useCurrentFilters)
  });
}
function createProjectPrintScopeChooser(trigger) {
  const label = trigger.getAttribute("data-print-label") || "Tisk projektu";
  const popover = document.createElement("div");
  popover.className = "print-format-popover";
  popover.setAttribute("role", "dialog");
  popover.setAttribute("aria-modal", "false");
  popover.setAttribute("data-print-popover", "true");
  popover.setAttribute("tabindex", "-1");
  const title = document.createElement("h3");
  title.className = "print-format-title";
  title.textContent = "Použít aktuální filtry?";
  popover.appendChild(title);
  const subtitle = document.createElement("p");
  subtitle.className = "print-format-subtitle";
  subtitle.textContent = label;
  popover.appendChild(subtitle);
  const actions = document.createElement("div");
  actions.className = "print-format-actions";
  const filteredButton = document.createElement("button");
  filteredButton.type = "button";
  filteredButton.className = "btn small";
  filteredButton.textContent = "Použít aktuální filtry";
  filteredButton.setAttribute("data-print-filter-scope", "current");
  actions.appendChild(filteredButton);
  const fullProjectButton = document.createElement("button");
  fullProjectButton.type = "button";
  fullProjectButton.className = "btn small ghost";
  fullProjectButton.textContent = "Tisknout celý projekt";
  fullProjectButton.setAttribute("data-print-filter-scope", "all");
  actions.appendChild(fullProjectButton);
  popover.appendChild(actions);
  const note = document.createElement("p");
  note.className = "print-format-note";
  note.textContent = "Volba se použije jen pro tento tisk a neukládá se.";
  popover.appendChild(note);
  const closeButton = document.createElement("button");
  closeButton.type = "button";
  closeButton.className = "print-format-close";
  closeButton.setAttribute("aria-label", "Zavřít výběr filtru pro tisk projektu");
  closeButton.textContent = "×";
  popover.appendChild(closeButton);
  popover.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }
    if (target.closest(".print-format-close")) {
      event.preventDefault();
      closePrintChooser({ restoreFocus: true });
      return;
    }
    const choice = target.closest("[data-print-filter-scope]");
    if (!choice) {
      return;
    }
    event.preventDefault();
    const scope = choice.getAttribute("data-print-filter-scope");
    handleProjectPrintScopeChoice(trigger, scope === "current");
  });
  return popover;
}
function createPrintChooser(trigger, options) {
  const settings = options || {};
  const quickMode = Boolean(settings.quickMode);
  const label = trigger.getAttribute("data-print-label") || "Tisk";
  const popover = document.createElement("div");
  popover.className = "print-format-popover";
  popover.setAttribute("role", "dialog");
  popover.setAttribute("aria-modal", "false");
  popover.setAttribute("data-print-popover", "true");
  popover.setAttribute("tabindex", "-1");
  const title = document.createElement("h3");
  title.className = "print-format-title";
  title.textContent = quickMode ? "Jednorázová volba formátu" : "Vyberte formát tisku";
  popover.appendChild(title);
  const subtitle = document.createElement("p");
  subtitle.className = "print-format-subtitle";
  subtitle.textContent = label;
  popover.appendChild(subtitle);
  const actions = document.createElement("div");
  actions.className = "print-format-actions";
  const pdfButton = document.createElement("button");
  pdfButton.type = "button";
  pdfButton.className = "btn small";
  pdfButton.textContent = "PDF";
  pdfButton.setAttribute("data-print-choice", "pdf");
  actions.appendChild(pdfButton);
  const wordButton = document.createElement("button");
  wordButton.type = "button";
  wordButton.className = "btn small";
  wordButton.textContent = "WORD";
  wordButton.setAttribute("data-print-choice", "word");
  actions.appendChild(wordButton);
  popover.appendChild(actions);
  const rememberLabel = document.createElement("label");
  rememberLabel.className = "print-format-remember";
  const rememberCheckbox = document.createElement("input");
  rememberCheckbox.type = "checkbox";
  rememberCheckbox.setAttribute("data-print-remember", "true");
  rememberLabel.appendChild(rememberCheckbox);
  rememberLabel.append(quickMode ? " Nastavit jako novou preferenci pro tento počítač" : " Zapamatovat pro tento počítač");
  popover.appendChild(rememberLabel);
  const note = document.createElement("p");
  note.className = "print-format-note";
  note.textContent = quickMode ? "Ve výchozím stavu jde o jednorázovou volbu. Uloženou preferenci změníte jen zaškrtnutím volby výše." : "Volba se ukládá pouze pro tento počítač/prohlížeč.";
  popover.appendChild(note);
  const closeButton = document.createElement("button");
  closeButton.type = "button";
  closeButton.className = "print-format-close";
  closeButton.setAttribute("aria-label", "Zavřít výběr formátu tisku");
  closeButton.textContent = "×";
  popover.appendChild(closeButton);
  popover.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }
    if (target.closest(".print-format-close")) {
      event.preventDefault();
      closePrintChooser({ restoreFocus: true });
      return;
    }
    const choice = target.closest("[data-print-choice]");
    if (!choice) {
      return;
    }
    event.preventDefault();
    const format = choice.getAttribute("data-print-choice");
    if (format !== "pdf" && format !== "word") {
      return;
    }
    const remember = rememberCheckbox.checked;
    closePrintChooser({ restoreFocus: false });
    handlePrintChoice(trigger, format, remember, settings);
  });
  return popover;
}
function showPrintChooser(trigger, options) {
  if (!(trigger instanceof HTMLElement)) {
    return;
  }
  closePrintChooser({ restoreFocus: false });
  const popover = createPrintChooser(trigger, options);
  document.body.appendChild(popover);
  positionPrintChooser(popover, trigger);
  printState.popover = popover;
  printState.trigger = trigger;
  const firstAction = popover.querySelector("[data-print-choice], [data-print-filter-scope]");
  if (firstAction instanceof HTMLElement) {
    firstAction.focus({ preventScroll: true });
  } else {
    popover.focus({ preventScroll: true });
  }
}
function showProjectPrintScopeChooser(trigger) {
  if (!(trigger instanceof HTMLElement)) {
    return;
  }
  closePrintChooser({ restoreFocus: false });
  const popover = createProjectPrintScopeChooser(trigger);
  document.body.appendChild(popover);
  positionPrintChooser(popover, trigger);
  printState.popover = popover;
  printState.trigger = trigger;
  const firstAction = popover.querySelector("[data-print-filter-scope]");
  if (firstAction instanceof HTMLElement) {
    firstAction.focus({ preventScroll: true });
  } else {
    popover.focus({ preventScroll: true });
  }
}
function handlePrintTriggerClick(trigger) {
  if (!(trigger instanceof HTMLElement)) {
    return;
  }
  clearPrintHoverTimer();
  if (shouldPromptForProjectPrintFilters(trigger)) {
    showProjectPrintScopeChooser(trigger);
    return;
  }
  const preferredFormat = getStoredPrintFormat();
  if (preferredFormat) {
    const preferredUrl = resolvePrintUrl(trigger, preferredFormat);
    if (preferredUrl) {
      closePrintChooser({ restoreFocus: false });
      openPrintUrl(preferredUrl);
      return;
    }
  }
  showPrintChooser(trigger, { quickMode: false });
}
function initPrintFormatChooser() {
  refreshPrintPreferenceUi();
  document.addEventListener("pointerover", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }
    const trigger = target.closest("[data-print-trigger]");
    if (!(trigger instanceof HTMLElement)) {
      return;
    }
    if (event.relatedTarget instanceof Element && trigger.contains(event.relatedTarget)) {
      return;
    }
    if (event instanceof PointerEvent && event.pointerType !== "mouse") {
      return;
    }
    if (shouldPromptForProjectPrintFilters(trigger)) {
      return;
    }
    if (!getStoredPrintFormat()) {
      return;
    }
    clearPrintHoverTimer();
    printState.hoverTimerId = window.setTimeout(() => {
      showPrintChooser(trigger, { quickMode: true });
    }, 2000);
  });
  document.addEventListener("pointerout", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }
    const trigger = target.closest("[data-print-trigger]");
    if (!(trigger instanceof HTMLElement)) {
      return;
    }
    if (event.relatedTarget instanceof Element && trigger.contains(event.relatedTarget)) {
      return;
    }
    clearPrintHoverTimer();
  });
  window.addEventListener("scroll", () => {
    if (printState.popover instanceof HTMLElement && printState.trigger instanceof HTMLElement) {
      positionPrintChooser(printState.popover, printState.trigger);
    }
    if (recordEditorState.chooser instanceof HTMLElement && recordEditorState.chooserTrigger instanceof HTMLElement) {
      positionPrintChooser(recordEditorState.chooser, recordEditorState.chooserTrigger);
    }
  }, true);
  window.addEventListener("resize", () => {
    if (printState.popover instanceof HTMLElement && printState.trigger instanceof HTMLElement) {
      positionPrintChooser(printState.popover, printState.trigger);
    }
    if (recordEditorState.chooser instanceof HTMLElement && recordEditorState.chooserTrigger instanceof HTMLElement) {
      positionPrintChooser(recordEditorState.chooser, recordEditorState.chooserTrigger);
    }
  });
}
function renderRainbowSegmentLabel(segment) {
  if (!(segment instanceof HTMLElement)) {
    return;
  }
  const fullLabel = segment.dataset.rainbowSegmentLabelFull || "";
  const shortLabel = segment.dataset.rainbowSegmentLabelShort || "";
  if (!fullLabel && !shortLabel) {
    return;
  }
  const width = segment.getBoundingClientRect().width;
  const computed = window.getComputedStyle(segment);
  const fontSpec = `${computed.fontWeight} ${computed.fontSize} ${computed.fontFamily}`;
  const selected = pickSegmentLabel(fullLabel, shortLabel, width, fontSpec);
  segment.textContent = selected;
  if (!selected) {
    segment.style.removeProperty("color");
    return;
  }
  segment.style.color = getContrastTextColor(computed.backgroundColor);
}
function renderAllRainbowSegmentLabels(scope) {
  const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
  root.querySelectorAll(".schedule-overview-segment[data-rainbow-segment-label-short], " + ".schedule-layered-segment[data-rainbow-segment-label-short], " + ".schedule-mini-gantt-segment[data-rainbow-segment-label-short]").forEach((segment) => {
    renderRainbowSegmentLabel(segment);
  });
}
function queueRainbowSegmentRender(scope) {
  window.requestAnimationFrame(() => renderAllRainbowSegmentLabels(scope));
}
function getGlobalFloatingLayerRoot() {
  if (globalFloatingRoot instanceof HTMLElement && globalFloatingRoot.isConnected) {
    return globalFloatingRoot;
  }
  const root = document.createElement("div");
  root.className = "app-floating-root";
  root.setAttribute("data-app-floating-root", "true");
  root.setAttribute("aria-hidden", "true");
  document.body.appendChild(root);
  globalFloatingRoot = root;
  return root;
}
function getFloatingLayerRoot(container) {
  const overlay = container instanceof Element ? container.closest(".modal-overlay") : null;
  if (overlay instanceof HTMLElement) {
    const modalRoot = overlay.querySelector("[data-modal-floating-root]");
    if (modalRoot instanceof HTMLElement) {
      return modalRoot;
    }
  }
  return getGlobalFloatingLayerRoot();
}
function getFloatingPanelAnchor(panel) {
  if (!(panel instanceof HTMLElement)) {
    return null;
  }
  const storedAnchor = panel._pmtrackerFloatingAnchor;
  if (storedAnchor instanceof HTMLElement && storedAnchor.isConnected) {
    return storedAnchor;
  }
  const fallbackAnchor = panel.closest("[data-floating-anchor]");
  return fallbackAnchor instanceof HTMLElement ? fallbackAnchor : null;
}
function applyFloatingPanelKind(panel, kind) {
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  panel.classList.remove("floating-panel--person-search", "floating-panel--ad-search", "floating-panel--date", "floating-panel--time");
  switch (kind) {
    case "person-search":
      panel.classList.add("floating-panel--person-search");
      break;
    case "ad-search":
      panel.classList.add("floating-panel--ad-search");
      break;
    case "date":
      panel.classList.add("floating-panel--date");
      break;
    case "time":
      panel.classList.add("floating-panel--time");
      break;
    default:
      break;
  }
}
function mountFloatingPanel(panel, anchor, options = {}) {
  if (!(panel instanceof HTMLElement) || !(anchor instanceof HTMLElement)) {
    return;
  }
  const root = getFloatingLayerRoot(anchor);
  const existingMount = panel._pmtrackerFloatingMount;
  const kind = options.kind || panel.dataset.floatingKind || "";
  const matchWidth = options.matchWidth === true || panel.dataset.floatingMatchWidth === "true";
  if (!existingMount) {
    const placeholder = document.createElement("span");
    placeholder.hidden = true;
    placeholder.style.display = "none";
    panel.parentNode?.insertBefore(placeholder, panel);
    panel._pmtrackerFloatingMount = {
      placeholder,
      originParent: panel.parentElement
    };
  }
  if (panel.parentElement !== root) {
    root.appendChild(panel);
  }
  panel._pmtrackerFloatingAnchor = anchor;
  panel._pmtrackerFloatingOptions = {
    gap: Number.isFinite(options.gap) ? options.gap : 8,
    flipVertical: options.flipVertical !== false,
    kind,
    matchWidth,
    lockVerticalSide: options.lockVerticalSide === true
  };
  panel.classList.add("floating-panel");
  panel.classList.toggle("floating-panel--match-anchor", matchWidth);
  applyFloatingPanelKind(panel, kind);
  floatingPanelRegistry.add(panel);
  positionFloatingPanel(panel, anchor, panel._pmtrackerFloatingOptions);
}
function unmountFloatingPanel(panel) {
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  const mount = panel._pmtrackerFloatingMount;
  if (mount?.placeholder instanceof HTMLElement && mount.placeholder.parentNode) {
    mount.placeholder.parentNode.insertBefore(panel, mount.placeholder);
    mount.placeholder.remove();
  }
  panel.classList.remove("floating-panel", "floating-panel--match-anchor", "floating-panel--person-search", "floating-panel--ad-search", "floating-panel--date", "floating-panel--time");
  panel.style.removeProperty("position");
  panel.style.removeProperty("left");
  panel.style.removeProperty("right");
  panel.style.removeProperty("top");
  panel.style.removeProperty("bottom");
  panel.style.removeProperty("width");
  panel.style.removeProperty("max-width");
  panel.style.removeProperty("min-width");
  panel.style.removeProperty("max-height");
  panel.style.removeProperty("overflow-y");
  panel.style.removeProperty("visibility");
  delete panel._pmtrackerFloatingMount;
  delete panel._pmtrackerFloatingAnchor;
  delete panel._pmtrackerFloatingOptions;
  delete panel._pmtrackerVerticalSide;
  floatingPanelRegistry.delete(panel);
}
function closeAllFloatingPanels(scope, exceptPanel) {
  floatingPanelRegistry.forEach((panel) => {
    if (!(panel instanceof HTMLElement) || panel === exceptPanel) {
      return;
    }
    const anchor = getFloatingPanelAnchor(panel);
    if (scope instanceof Element || scope instanceof Document) {
      const scopeContainsPanel = scope.contains(panel);
      const scopeContainsAnchor = anchor instanceof HTMLElement && scope.contains(anchor);
      if (!scopeContainsPanel && !scopeContainsAnchor) {
        return;
      }
    }
    panel.hidden = true;
    unmountFloatingPanel(panel);
  });
}
function resolveRecordEditorFloatingBoundary(anchor, boundary, kind) {
  if (!(anchor instanceof HTMLElement) || !boundary) {
    return boundary;
  }
  if (kind !== "date" && kind !== "time" && kind !== "person-search") {
    return boundary;
  }
  const modalContainer = anchor.closest("[data-modal-container]");
  if (!(modalContainer instanceof HTMLElement)) {
    return boundary;
  }
  const recordEditorForm = anchor.closest('form[data-record-editor-form="true"]');
  if (!(recordEditorForm instanceof HTMLElement)) {
    return boundary;
  }
  const actionBar = recordEditorForm.querySelector(".record-editor-actions");
  if (!(actionBar instanceof HTMLElement)) {
    return boundary;
  }
  const actionBarRect = actionBar.getBoundingClientRect();
  if (actionBarRect.height <= 0) {
    return boundary;
  }
  const adjustedBottom = Math.min(boundary.bottom, actionBarRect.top - 8);
  if (adjustedBottom <= boundary.top + 72) {
    return boundary;
  }
  return {
    ...boundary,
    bottom: adjustedBottom
  };
}
function positionFloatingPanel(panel, anchor, options = {}) {
  if (!(panel instanceof HTMLElement) || !(anchor instanceof HTMLElement) || panel.hidden) {
    return;
  }
  const gap = Number.isFinite(options.gap) ? options.gap : 8;
  const matchWidth = options.matchWidth === true || panel.dataset.floatingMatchWidth === "true";
  const kind = typeof options.kind === "string" ? options.kind : "";
  const lockVerticalSide = options.lockVerticalSide === true;
  const viewportBoundary = {
    left: 8,
    right: window.innerWidth - 8,
    top: 8,
    bottom: window.innerHeight - 8
  };
  const modalContainer = anchor.closest("[data-modal-container]");
  const baseBoundary = modalContainer instanceof HTMLElement ? (() => {
    const modalRect = modalContainer.getBoundingClientRect();
    return {
      left: Math.max(viewportBoundary.left, modalRect.left + 8),
      right: Math.min(viewportBoundary.right, modalRect.right - 8),
      top: Math.max(viewportBoundary.top, modalRect.top + 8),
      bottom: Math.min(viewportBoundary.bottom, modalRect.bottom - 8)
    };
  })() : viewportBoundary;
  const boundary = resolveRecordEditorFloatingBoundary(anchor, baseBoundary, kind);
  const anchorRect = anchor.getBoundingClientRect();
  if (anchorRect.width <= 0 || anchorRect.height <= 0) {
    return;
  }
  panel.style.position = "fixed";
  panel.style.visibility = "hidden";
  panel.style.left = "0px";
  panel.style.top = "0px";
  panel.style.right = "auto";
  panel.style.bottom = "auto";
  panel.style.maxHeight = "";
  panel.style.overflowY = "";
  panel.style.maxWidth = `${Math.max(boundary.right - boundary.left, 0)}px`;
  panel.style.width = matchWidth ? `${Math.min(Math.round(anchorRect.width), Math.max(boundary.right - boundary.left, 0))}px` : "";
  panel.style.minWidth = matchWidth ? `${Math.min(Math.round(anchorRect.width), Math.max(boundary.right - boundary.left, 0))}px` : "";
  let panelRect = panel.getBoundingClientRect();
  const availableBelow = Math.max(0, boundary.bottom - anchorRect.bottom - gap);
  const availableAbove = Math.max(0, anchorRect.top - boundary.top - gap);
  const fitsBelow = availableBelow >= panelRect.height;
  const fitsAbove = availableAbove >= panelRect.height;
  let shouldOpenAbove = false;
  const storedVerticalSide = panel._pmtrackerVerticalSide === "above" || panel._pmtrackerVerticalSide === "below" ? panel._pmtrackerVerticalSide : null;
  if (!lockVerticalSide) {
    delete panel._pmtrackerVerticalSide;
  }
  if (lockVerticalSide && storedVerticalSide) {
    shouldOpenAbove = storedVerticalSide === "above";
  } else {
    if (fitsBelow) {
      shouldOpenAbove = false;
    } else if (fitsAbove) {
      shouldOpenAbove = true;
    } else {
      shouldOpenAbove = availableAbove > availableBelow;
    }
    if (lockVerticalSide) {
      panel._pmtrackerVerticalSide = shouldOpenAbove ? "above" : "below";
    }
  }
  const availableOnSelectedSide = shouldOpenAbove ? availableAbove : availableBelow;
  if (availableOnSelectedSide > 0 && availableOnSelectedSide < panelRect.height) {
    const maxHeight = Math.floor(Math.max(availableOnSelectedSide - 4, 0));
    if (maxHeight > 0) {
      panel.style.maxHeight = `${maxHeight}px`;
      panel.style.overflowY = "auto";
      panelRect = panel.getBoundingClientRect();
    }
  }
  let left = anchorRect.left;
  if (left + panelRect.width > boundary.right) {
    left = boundary.right - panelRect.width;
  }
  if (left < boundary.left) {
    left = boundary.left;
  }
  let top = shouldOpenAbove ? anchorRect.top - panelRect.height - gap : anchorRect.bottom + gap;
  if (top + panelRect.height > boundary.bottom) {
    top = boundary.bottom - panelRect.height;
  }
  if (top < boundary.top) {
    top = boundary.top;
  }
  panel.style.left = `${Math.round(left)}px`;
  panel.style.top = `${Math.round(top)}px`;
  panel.style.visibility = "";
}
function repositionFloatingPanels() {
  floatingPanelRegistry.forEach((panel) => {
    if (!(panel instanceof HTMLElement) || panel.hidden) {
      return;
    }
    const anchor = getFloatingPanelAnchor(panel);
    if (!(anchor instanceof HTMLElement) || !anchor.isConnected) {
      panel.hidden = true;
      unmountFloatingPanel(panel);
      return;
    }
    positionFloatingPanel(panel, anchor, panel._pmtrackerFloatingOptions || {});
  });
}
var queueFloatingPanelReposition = debounce(() => {
  repositionFloatingPanels();
}, 16);
function isInteractionInsideFloatingControl(target, anchor, panel) {
  if (!(target instanceof Element)) {
    return false;
  }
  return anchor instanceof HTMLElement && anchor.contains(target) || panel instanceof HTMLElement && panel.contains(target);
}

// PmTracker.Web/wwwroot/js/modules/recordLazyLoading.js
var cardInteractiveSelector = [
  "button",
  "a",
  "input",
  "select",
  "textarea",
  "label",
  "[data-stop-propagation]",
  "[contenteditable='true']"
].join(", ");
function applyRecordCommentSortDirection(card, direction) {
  const normalizedDirection = direction === "desc" ? "desc" : "asc";
  const section = card.querySelector("[data-comment-sort-section]");
  if (!(section instanceof HTMLElement)) {
    return;
  }
  applyCommentSort(section, normalizedDirection);
  const toggle = section.querySelector("[data-comment-sort-toggle]");
  if (toggle instanceof HTMLButtonElement) {
    setCommentSortButtonLabel(toggle, normalizedDirection);
  }
}
async function loadRecordDetail(cardOrChild, options = {}) {
  const card = resolveRecordCardElement(cardOrChild);
  if (!(card instanceof HTMLElement)) {
    return false;
  }
  const detailUrl = typeof options.url === "string" && options.url ? options.url : (card.dataset.recordDetailUrl || "").trim();
  if (!detailUrl) {
    return false;
  }
  const forceReload = options.force === true;
  if (card.dataset.recordDetailLoaded === "true" && !forceReload) {
    return true;
  }
  const detailShell = card.querySelector("[data-record-detail-shell]");
  if (!(detailShell instanceof HTMLElement)) {
    return false;
  }
  const placeholder = detailShell.querySelector("[data-record-detail-placeholder]");
  const errorContainer = resolveOrCreateErrorContainer(detailShell, "data-record-detail-error");
  setLazyLoadingState(detailShell, placeholder, errorContainer, true);
  try {
    detailShell.innerHTML = await fetchHtmlFragment(detailUrl);
    card.dataset.recordDetailLoaded = "true";
    navigationRuntime.initRecordFormEnhancements?.(detailShell);
    queueRainbowSegmentRender(detailShell);
    return true;
  } catch (error) {
    card.dataset.recordDetailLoaded = "false";
    setLazyLoadingState(detailShell, placeholder, errorContainer, false);
    renderLazyLoadError(errorContainer, "Nepodařilo se načíst detail záznamu.", "data-record-detail-retry");
    return false;
  }
}
async function loadRecordComments(cardOrChild, options = {}) {
  const card = resolveRecordCardElement(cardOrChild);
  if (!(card instanceof HTMLElement)) {
    return false;
  }
  const commentsShell = card.querySelector("[data-record-comments-shell]");
  if (!(commentsShell instanceof HTMLElement)) {
    return false;
  }
  const baseCommentsUrl = typeof options.url === "string" && options.url ? options.url : (commentsShell.dataset.recordCommentsBaseUrl || commentsShell.dataset.recordCommentsUrl || card.dataset.recordCommentsBaseUrl || card.dataset.recordCommentsUrl || "").trim();
  if (!baseCommentsUrl) {
    return false;
  }
  const requestUrl = buildRecordCommentsRequestUrl(baseCommentsUrl, {
    limit: options.limit,
    loadAll: options.loadAll === true
  }) || baseCommentsUrl;
  const forceReload = options.force === true;
  const alreadyLoaded = commentsShell.dataset.recordCommentsLoaded === "true" || card.dataset.recordCommentsLoaded === "true";
  if (alreadyLoaded && !forceReload) {
    return true;
  }
  const placeholder = commentsShell.querySelector("[data-record-comments-placeholder]");
  const errorContainer = resolveOrCreateErrorContainer(commentsShell, "data-record-comments-error");
  setLazyLoadingState(commentsShell, placeholder, errorContainer, true);
  try {
    commentsShell.innerHTML = await fetchHtmlFragment(requestUrl);
    commentsShell.dataset.recordCommentsLoaded = "true";
    commentsShell.dataset.recordCommentsUrl = requestUrl;
    commentsShell.dataset.recordCommentsBaseUrl = baseCommentsUrl;
    card.dataset.recordCommentsLoaded = "true";
    card.dataset.recordCommentsBaseUrl = baseCommentsUrl;
    navigationRuntime.initRecordFormEnhancements?.(commentsShell);
    initCommentSortUi(commentsShell);
    if (options.sortDirection === "asc" || options.sortDirection === "desc") {
      applyRecordCommentSortDirection(card, options.sortDirection);
    }
    return true;
  } catch (error) {
    commentsShell.dataset.recordCommentsLoaded = "false";
    card.dataset.recordCommentsLoaded = "false";
    setLazyLoadingState(commentsShell, placeholder, errorContainer, false);
    renderLazyLoadError(errorContainer, "Nepodařilo se načíst vyjádření.", "data-record-comments-retry");
    return false;
  }
}
async function toggleRecordCard(cardOrChild, options = {}) {
  const card = resolveRecordCardElement(cardOrChild);
  if (!(card instanceof HTMLElement)) {
    return;
  }
  const shouldExpand = options.expand === true ? true : options.collapse === true ? false : card.classList.contains("collapsed");
  card.classList.toggle("collapsed", !shouldExpand);
  const header = card.querySelector("[data-record-toggle]");
  if (header instanceof HTMLElement) {
    header.setAttribute("aria-expanded", String(shouldExpand));
  }
  if (shouldExpand) {
    await Promise.all([
      loadRecordDetail(card, { force: options.force === true }),
      loadRecordComments(card, { force: options.force === true })
    ]);
  }
}
function initProjectRecordDeepLink(scope = document) {
  const panel = scope instanceof HTMLElement && scope.matches('[data-tab-panel="zaznamy"]') ? scope : scope.querySelector?.('[data-tab-panel="zaznamy"][data-project-detail-root]');
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  const targetRecordId = String(panel.dataset.recordTargetId || "").trim();
  if (!targetRecordId || panel.dataset.recordTargetHandled === "true") {
    return;
  }
  const targetCard = panel.querySelector(`.record-card[data-record-id="${CSS.escape(targetRecordId)}"]`);
  if (!(targetCard instanceof HTMLElement)) {
    return;
  }
  panel.dataset.recordTargetHandled = "true";
  const openComments = panel.dataset.recordTargetOpenComments === "true";
  window.requestAnimationFrame(async () => {
    await toggleRecordCard(targetCard, { expand: true });
    targetCard.scrollIntoView({ behavior: "smooth", block: "start" });
    if (openComments) {
      const commentsShell = targetCard.querySelector("[data-record-comments-shell]");
      if (commentsShell instanceof HTMLElement) {
        commentsShell.scrollIntoView({ behavior: "smooth", block: "nearest" });
      }
    }
  });
}
function handleNavigationCardClick(target) {
  if (!(target instanceof Element)) {
    return false;
  }
  const navCard = target.closest("[data-href]");
  if (!(navCard instanceof HTMLElement)) {
    return false;
  }
  if (target.closest(cardInteractiveSelector)) {
    return false;
  }
  const href = navCard.getAttribute("data-href");
  if (!href) {
    return false;
  }
  window.location.href = href;
  return true;
}
function handleNavigationCardKeydown(event, target) {
  if (!(event instanceof KeyboardEvent) || !(target instanceof Element)) {
    return false;
  }
  if (event.key !== "Enter" && event.key !== " ") {
    return false;
  }
  if (!(target instanceof HTMLElement) || !target.matches("[data-href]")) {
    return false;
  }
  const href = target.getAttribute("data-href");
  if (!href) {
    return false;
  }
  event.preventDefault();
  window.location.href = href;
  return true;
}

// PmTracker.Web/wwwroot/js/modules/meetingOverview.js
var FIRST_ROW_TOLERANCE_PX = 2;
function getMeetingYearGroups(scope) {
  return Array.from(scope.querySelectorAll("[data-meeting-year-group]")).filter((group) => group instanceof HTMLElement);
}
function getMeetingCardSlots(scope) {
  return Array.from(scope.querySelectorAll("[data-meeting-card-wrap]")).filter((slot) => slot instanceof HTMLElement);
}
function getMeetingId(slot) {
  if (!(slot instanceof HTMLElement)) {
    return "";
  }
  return slot.dataset.meetingId || "";
}
function getFirstVisualRowSlots(grid) {
  const slots = getMeetingCardSlots(grid).filter((slot) => !slot.hidden);
  if (slots.length === 0) {
    return [];
  }
  const firstTop = Math.min(...slots.map((slot) => slot.offsetTop));
  return slots.filter((slot) => Math.abs(slot.offsetTop - firstTop) <= FIRST_ROW_TOLERANCE_PX);
}
function formatMeetingCount(count) {
  return `${count} jednání`;
}
function updateMeetingYearCount(group, visibleCount) {
  const count = group.querySelector("[data-meeting-year-count]");
  if (!(count instanceof HTMLElement)) {
    return;
  }
  count.textContent = formatMeetingCount(visibleCount);
}
function clearPreviewHiddenSlots(scope) {
  getMeetingCardSlots(scope).forEach((slot) => {
    if (slot.dataset.meetingPreviewHidden !== "true") {
      return;
    }
    slot.hidden = false;
    delete slot.dataset.meetingPreviewHidden;
  });
}
function countMeetingSlots(group) {
  return getMeetingCardSlots(group).length;
}
function applyMeetingYearState(group) {
  if (!(group instanceof HTMLElement)) {
    return;
  }
  const body = group.querySelector("[data-meeting-year-body]");
  const grid = group.querySelector("[data-meeting-year-grid]");
  const toggle = group.querySelector("[data-meeting-year-toggle]");
  if (!(body instanceof HTMLElement) || !(grid instanceof HTMLElement) || !(toggle instanceof HTMLElement)) {
    return;
  }
  const state = group.dataset.meetingYearState || "collapsed";
  clearPreviewHiddenSlots(group);
  if (state === "collapsed") {
    body.hidden = true;
    body.classList.remove("is-preview");
    body.style.removeProperty("max-height");
    toggle.setAttribute("aria-expanded", "false");
    return;
  }
  body.hidden = false;
  toggle.setAttribute("aria-expanded", "true");
  if (state !== "preview") {
    body.classList.remove("is-preview");
    body.style.removeProperty("max-height");
    return;
  }
  body.classList.add("is-preview");
  body.style.removeProperty("max-height");
  const firstRowSlots = getFirstVisualRowSlots(grid);
  const firstRowIds = new Set(firstRowSlots.map(getMeetingId).filter(Boolean));
  getMeetingCardSlots(grid).forEach((slot) => {
    const meetingId = getMeetingId(slot);
    if (!meetingId || firstRowIds.has(meetingId)) {
      return;
    }
    slot.hidden = true;
    slot.dataset.meetingPreviewHidden = "true";
  });
}
function syncYearGroupedMeetingOverview(root) {
  getMeetingYearGroups(root).forEach((group) => {
    updateMeetingYearCount(group, countMeetingSlots(group));
    applyMeetingYearState(group);
  });
}
function initMeetingOverview(scope = document) {
  const roots = Array.from(scope.querySelectorAll("[data-meeting-overview]")).filter((root) => root instanceof HTMLElement);
  roots.forEach((root) => {
    syncYearGroupedMeetingOverview(root);
  });
}
function toggleMeetingYearGroup(toggle) {
  const group = toggle instanceof HTMLElement ? toggle.closest("[data-meeting-year-group]") : null;
  if (!(group instanceof HTMLElement)) {
    return;
  }
  const currentState = group.dataset.meetingYearState || "collapsed";
  group.dataset.meetingYearState = currentState === "preview" ? "open" : currentState === "open" ? "collapsed" : "open";
  applyMeetingYearState(group);
}

// PmTracker.Web/wwwroot/js/modules/schedule.js
function syncScheduleExpandButton(button, details) {
  if (!(button instanceof HTMLButtonElement) || !(details instanceof HTMLElement)) {
    return;
  }
  const expanded = !details.hidden;
  button.textContent = expanded ? "Skrýt rozpad" : "Rozpad";
  button.setAttribute("aria-expanded", String(expanded));
}
function toggleScheduleBreakdown(toggleOrTarget) {
  const button = toggleOrTarget instanceof HTMLButtonElement ? toggleOrTarget : toggleOrTarget instanceof Element ? toggleOrTarget.closest("[data-schedule-expand-toggle]") : null;
  if (!(button instanceof HTMLButtonElement)) {
    return false;
  }
  const owningCard = button.closest("[data-schedule-item]");
  if (!(owningCard instanceof HTMLElement)) {
    return false;
  }
  const details = owningCard.querySelector("[data-schedule-steps]");
  if (!(details instanceof HTMLElement)) {
    return false;
  }
  const expanded = details.hidden;
  details.hidden = !expanded;
  syncScheduleExpandButton(button, details);
  if (expanded) {
    renderStaticTimelineAxes(details);
    queueRainbowSegmentRender(details);
    window.requestAnimationFrame(() => {
      renderStaticTimelineAxes(details);
      queueRainbowSegmentRender(details);
    });
  }
  return true;
}
function initScheduleExpandUi(scope) {
  const root = scope instanceof Element ? scope : document;
  root.querySelectorAll("[data-schedule-expand-toggle]").forEach((button) => {
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }
    const card = button.closest("[data-schedule-item]");
    const details = card instanceof HTMLElement ? card.querySelector("[data-schedule-steps]") : null;
    if (details instanceof HTMLElement) {
      syncScheduleExpandButton(button, details);
    }
  });
}
function setScheduleFilterPanelOpen(open) {
  const panel = document.querySelector("[data-schedule-filter-panel]");
  const toggle = document.querySelector("[data-schedule-filter-toggle]");
  const key = "pmtracker.schedule.filters.open";
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  panel.classList.toggle("collapsed", !open);
  if (toggle instanceof HTMLElement) {
    toggle.setAttribute("aria-expanded", String(open));
  }
  localStorage.setItem(key, String(open));
}
function restoreScheduleFilterState() {
  return restoreProjectFilterScope("schedule");
}
function persistScheduleFilterState() {
  handleProjectFilterInputChange("schedule", {
    applyScope: (resolvedScope) => {
      if (resolvedScope === "schedule") {
        applyProjectScheduleFilters();
      } else if (resolvedScope === "gantt") {
        applyProjectGanttFilters();
      }
    }
  });
}
function applyProjectScheduleFilters() {
  const cards = document.querySelectorAll("[data-schedule-item]");
  if (cards.length === 0) {
    return;
  }
  const state = buildProjectFilterStateFromInputs("schedule");
  const filters = {
    subsystem: normalizeFilterToken(state.subsystem)
  };
  cards.forEach((item) => {
    if (!(item instanceof HTMLElement)) {
      return;
    }
    const subsystem = normalizeFilterToken(item.dataset.scheduleFilterSubsystemKod || item.dataset.scheduleFilterSubsystem);
    const matches = !filters.subsystem || subsystem === filters.subsystem;
    setRecordFilterVisibility(item, matches);
  });
  document.querySelectorAll("[data-project-schedule-list] [data-subsystem-group]").forEach((group) => {
    if (!(group instanceof HTMLElement)) {
      return;
    }
    const hasVisibleItems = Array.from(group.querySelectorAll("[data-schedule-item]")).some((item) => item instanceof HTMLElement && !item.hidden);
    group.hidden = !hasVisibleItems;
  });
  const scheduleList = document.querySelector("[data-project-schedule-list]");
  if (scheduleList instanceof HTMLElement) {
    const sortMode = normalizeSubsystemSortMode(state.sortBy);
    sortSubsystemGroupsInContainer(scheduleList, sortMode);
  }
  renderStaticTimelineAxes(document.querySelector('[data-tab-panel="harmonogram"]'));
  queueRainbowSegmentRender(document.querySelector('[data-tab-panel="harmonogram"]'));
  scheduleSubsystemIndicatorSync();
}
function getGanttFilterValue(key) {
  const input = getProjectFilterInput("gantt", key);
  if (input instanceof HTMLInputElement && input.type === "checkbox") {
    return input.checked;
  }
  if (input instanceof HTMLInputElement || input instanceof HTMLSelectElement) {
    return input.value;
  }
  return "";
}
class ProjectGanttBoard {
  constructor(panel) {
    this.panel = panel;
    this.projectId = String(panel.dataset.projectId || "0");
    this.pickerItems = Array.from(panel.querySelectorAll("[data-gantt-picker-item]")).filter((node) => node instanceof HTMLElement);
    this.boardItems = Array.from(panel.querySelectorAll("[data-gantt-item]")).filter((node) => node instanceof HTMLElement);
    this.pinInputs = Array.from(panel.querySelectorAll("[data-gantt-pin-input]")).filter((node) => node instanceof HTMLInputElement);
    this.pinnedKey = `pmtracker.gantt.pinned.${this.projectId}`;
    this.expandedKey = `pmtracker.gantt.expanded.${this.projectId}`;
    const fallbackPinned = this.pinInputs.map((input) => String(input.dataset.ganttRecordId || "").trim()).filter(Boolean);
    this.pinnedIds = this.readIdSet(this.pinnedKey, fallbackPinned);
    this.expandedIds = this.readIdSet(this.expandedKey, []);
  }
  readIdSet(storageKey, fallbackValues) {
    const raw = localStorage.getItem(storageKey);
    if (!raw) {
      return new Set(fallbackValues);
    }
    try {
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) {
        return new Set(fallbackValues);
      }
      return new Set(parsed.map((value) => String(value || "").trim()).filter(Boolean));
    } catch (error) {
      return new Set(fallbackValues);
    }
  }
  writeIdSet(storageKey, set) {
    localStorage.setItem(storageKey, JSON.stringify(Array.from(set)));
  }
  getFilters() {
    const currentUserId = getProjectFilterCurrentUserId("gantt");
    const hasCurrentUser = currentUserId && currentUserId !== "0";
    return {
      subsystem: normalizeFilterToken(getGanttFilterValue("subsystem")),
      kategorie: normalizeFilterToken(getGanttFilterValue("kategorie")),
      stav: normalizeFilterToken(getGanttFilterValue("stav")),
      typ: normalizeFilterToken(getGanttFilterValue("typ")),
      vlastnik: normalizeFilterToken(getGanttFilterValue("vlastnik")),
      onlyActive: Boolean(getGanttFilterValue("aktivni")),
      mine: Boolean(getGanttFilterValue("mine")),
      currentUserId,
      hasCurrentUser,
      stihani: normalizeFilterToken(getGanttFilterValue("stihani"))
    };
  }
  matchesFilters(node, filters) {
    if (!(node instanceof HTMLElement)) {
      return false;
    }
    const subsystem = normalizeFilterToken(node.dataset.ganttFilterSubsystemKod || node.dataset.ganttFilterSubsystem);
    const kategorie = normalizeFilterToken(node.dataset.ganttFilterKategorieKod || node.dataset.ganttFilterKategorie);
    const stav = normalizeFilterToken(node.dataset.ganttFilterStavKod || node.dataset.ganttFilterStav);
    const typ = normalizeFilterToken(node.dataset.ganttFilterTypKod || node.dataset.ganttFilterTyp);
    const vlastnik = normalizeFilterToken(node.dataset.ganttFilterVlastnikId || node.dataset.ganttFilterVlastnik);
    const isActive = node.dataset.ganttFilterAktivni === "true";
    const stihani = normalizeFilterToken(node.dataset.ganttFilterStihani);
    const matchesMine = !filters.mine || filters.hasCurrentUser && vlastnik === filters.currentUserId;
    return (!filters.subsystem || subsystem === filters.subsystem) && (!filters.kategorie || kategorie === filters.kategorie) && (!filters.stav || stav === filters.stav) && (!filters.typ || typ === filters.typ) && (!filters.vlastnik || vlastnik === filters.vlastnik) && (!filters.onlyActive || isActive) && matchesMine && (!filters.stihani || stihani === filters.stihani);
  }
  setExpanded(recordId, expanded) {
    const normalized = String(recordId || "").trim();
    if (!normalized) {
      return;
    }
    if (expanded) {
      this.expandedIds.add(normalized);
    } else {
      this.expandedIds.delete(normalized);
    }
    this.writeIdSet(this.expandedKey, this.expandedIds);
    this.syncExpandedState();
  }
  setPinned(recordId, pinned) {
    const normalized = String(recordId || "").trim();
    if (!normalized) {
      return;
    }
    if (pinned) {
      this.pinnedIds.add(normalized);
    } else {
      this.pinnedIds.delete(normalized);
      this.expandedIds.delete(normalized);
      this.writeIdSet(this.expandedKey, this.expandedIds);
    }
    this.writeIdSet(this.pinnedKey, this.pinnedIds);
    this.apply();
  }
  syncPinnedInputs() {
    this.pinInputs.forEach((input) => {
      const recordId = String(input.dataset.ganttRecordId || "").trim();
      input.checked = this.pinnedIds.has(recordId);
    });
  }
  syncExpandedState() {
    this.boardItems.forEach((item) => {
      if (!(item instanceof HTMLElement)) {
        return;
      }
      const recordId = String(item.dataset.ganttRecordId || "").trim();
      const expanded = this.expandedIds.has(recordId);
      const details = item.querySelector("[data-gantt-steps]");
      if (details instanceof HTMLElement) {
        details.hidden = !expanded;
      }
      const button = item.querySelector("[data-gantt-expand-toggle]");
      if (button instanceof HTMLButtonElement) {
        button.textContent = expanded ? "Skrýt rozpad" : "Rozpad";
        button.setAttribute("aria-expanded", String(expanded));
      }
    });
  }
  apply() {
    const filters = this.getFilters();
    this.pickerItems.forEach((item) => {
      const visible = this.matchesFilters(item, filters);
      setRecordFilterVisibility(item, visible);
    });
    this.boardItems.forEach((item) => {
      if (!(item instanceof HTMLElement)) {
        return;
      }
      const recordId = String(item.dataset.ganttRecordId || "").trim();
      const visibleByFilter = this.matchesFilters(item, filters);
      const visible = visibleByFilter && this.pinnedIds.has(recordId);
      setRecordFilterVisibility(item, visible);
      item.dataset.ganttPinned = visible ? "true" : "false";
    });
    this.syncPinnedInputs();
    this.syncExpandedState();
    updateProjectGanttAxis(this.panel);
    queueRainbowSegmentRender(this.panel);
  }
  bind() {
    this.pinInputs.forEach((input) => {
      input.addEventListener("change", () => {
        this.setPinned(input.dataset.ganttRecordId, input.checked);
      });
    });
    this.panel.querySelectorAll("[data-gantt-expand-toggle]").forEach((button) => {
      if (!(button instanceof HTMLButtonElement)) {
        return;
      }
      button.addEventListener("click", () => {
        const recordId = String(button.dataset.ganttRecordId || "").trim();
        const expanded = this.expandedIds.has(recordId);
        this.setExpanded(recordId, !expanded);
      });
    });
  }
}
function applyProjectGanttFilters() {
  const panel = document.querySelector("[data-gantt-panel]");
  if (!(panel instanceof HTMLElement) || !(panel._ganttBoard instanceof ProjectGanttBoard)) {
    return;
  }
  panel._ganttBoard.apply();
}
function updateProjectGanttAxis(panel) {
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  const axis = panel.querySelector("[data-gantt-axis]");
  if (!(axis instanceof HTMLElement)) {
    return;
  }
  const visibleItems = Array.from(panel.querySelectorAll("[data-gantt-item]")).filter((node) => node instanceof HTMLElement && !node.hidden);
  if (visibleItems.length === 0) {
    axis.hidden = true;
    axis.replaceChildren();
    return;
  }
  const dates = visibleItems.flatMap((item) => {
    const startDate = parseIsoDate(item.dataset.ganttAxisStart);
    const endDate = parseIsoDate(item.dataset.ganttAxisEnd);
    return [startDate, endDate];
  }).filter((value) => value instanceof Date);
  if (dates.length === 0) {
    axis.hidden = true;
    axis.replaceChildren();
    return;
  }
  const ordered = dates.slice().sort((a, b) => a.getTime() - b.getTime());
  axis.hidden = false;
  renderTimelineAxis(axis, ordered[0], ordered[ordered.length - 1]);
}
function resolveTimelineAxisTickTargetCount(containerWidth) {
  if (!Number.isFinite(containerWidth) || containerWidth <= 0) {
    return 2;
  }
  if (containerWidth < 320) {
    return 2;
  }
  const estimated = Math.round(containerWidth / 120);
  return Math.max(5, Math.min(10, estimated));
}
function queueTimelineAxisRetry(container, startDate, endDate, attempt) {
  if (!(container instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
    return;
  }
  const retryAttempt = Number.isFinite(attempt) ? Math.trunc(attempt) : 0;
  if (retryAttempt >= 10 || !container.isConnected) {
    return;
  }
  const pendingFrame = Number.parseInt(container.dataset.axisRetryFrame || "0", 10);
  if (Number.isInteger(pendingFrame) && pendingFrame > 0) {
    window.cancelAnimationFrame(pendingFrame);
  }
  const frameId = window.requestAnimationFrame(() => {
    container.dataset.axisRetryFrame = "0";
    renderTimelineAxis(container, startDate, endDate, {
      retryAttempt: retryAttempt + 1
    });
  });
  container.dataset.axisRetryFrame = String(frameId);
}
function buildTimelineAxisTicks(startDate, endDate, desiredTickCount) {
  const start = new Date(startDate.getFullYear(), startDate.getMonth(), startDate.getDate());
  const end = new Date(endDate.getFullYear(), endDate.getMonth(), endDate.getDate());
  const totalDays = Math.max(1, diffCalendarDays(end, start));
  const maxDistinctTicks = totalDays + 1;
  const requestedTicks = Number.isFinite(desiredTickCount) ? Math.trunc(desiredTickCount) : 7;
  const tickCount = Math.max(2, Math.min(maxDistinctTicks, requestedTicks));
  const useMonthYearLabels = totalDays > 120;
  const formatTickLabel = useMonthYearLabels ? formatAxisMonthYear : formatAxisDayMonth;
  const selectedOffsets = new Set([0, totalDays]);
  for (let index = 1;index < tickCount - 1; index += 1) {
    const offset = Math.round(index * totalDays / (tickCount - 1));
    selectedOffsets.add(Math.max(0, Math.min(totalDays, offset)));
  }
  for (let dayOffset = 1;selectedOffsets.size < tickCount && dayOffset < totalDays; dayOffset += 1) {
    selectedOffsets.add(dayOffset);
  }
  const orderedOffsets = Array.from(selectedOffsets).map((value) => Number.parseInt(String(value), 10)).filter((value) => Number.isFinite(value)).sort((a, b) => a - b);
  return orderedOffsets.map((dayOffset) => {
    const date = addCalendarDays(start, dayOffset);
    return {
      date,
      label: formatTickLabel(date),
      left: dayOffset * 100 / totalDays
    };
  });
}
function renderTimelineAxis(container, startDate, endDate, options) {
  if (!(container instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
    return;
  }
  const settings = options && typeof options === "object" ? options : {};
  const retryAttempt = Number.isFinite(settings.retryAttempt) ? Math.max(0, Math.trunc(settings.retryAttempt)) : 0;
  const containerWidth = Math.max(0, container.clientWidth);
  if (containerWidth <= 0 || containerWidth <= 32 && retryAttempt < 10) {
    queueTimelineAxisRetry(container, startDate, endDate, retryAttempt);
    return;
  }
  const startStamp = toUtcDayStamp(startDate);
  const endStamp = toUtcDayStamp(endDate);
  const axisStart = startStamp <= endStamp ? startDate : endDate;
  const axisEnd = startStamp <= endStamp ? endDate : startDate;
  const edgeInsetPx = Math.max(2, Math.min(4, Math.round(containerWidth * 0.006)));
  const usableAxisWidth = Math.max(1, containerWidth - edgeInsetPx * 2);
  const percentToAxisPx = (percentValue) => {
    const normalized = Math.max(0, Math.min(100, Number.isFinite(percentValue) ? percentValue : 0));
    return edgeInsetPx + normalized / 100 * usableAxisWidth;
  };
  const pendingFrame = Number.parseInt(container.dataset.axisRetryFrame || "0", 10);
  if (Number.isInteger(pendingFrame) && pendingFrame > 0) {
    window.cancelAnimationFrame(pendingFrame);
  }
  container.dataset.axisRetryFrame = "0";
  container.replaceChildren();
  const desiredTickCount = resolveTimelineAxisTickTargetCount(containerWidth);
  const ticks = buildTimelineAxisTicks(axisStart, axisEnd, desiredTickCount);
  ticks.forEach((tick, index) => {
    const tickNode = document.createElement("span");
    tickNode.className = "timeline-axis-tick";
    if (index === 0 || index === ticks.length - 1) {
      tickNode.classList.add("edge");
    }
    const tickLeftPx = percentToAxisPx(tick.left);
    tickNode.dataset.axisLeftPx = tickLeftPx.toFixed(4);
    tickNode.style.left = `${tickLeftPx.toFixed(4)}px`;
    const labelNode = document.createElement("span");
    labelNode.className = "timeline-axis-label";
    labelNode.textContent = tick.label;
    tickNode.appendChild(labelNode);
    container.appendChild(tickNode);
  });
  if (ticks.length === 0) {
    return;
  }
  const minLabelGap = 6;
  const tickNodes = Array.from(container.querySelectorAll(".timeline-axis-tick")).filter((tickNode) => tickNode instanceof HTMLElement);
  const lastIndex = tickNodes.length - 1;
  const resolveLabelWidth = (labelNode) => {
    if (!(labelNode instanceof HTMLElement)) {
      return 0;
    }
    const measuredLabelWidth = labelNode.offsetWidth;
    const computedStyle = window.getComputedStyle(labelNode);
    const fallbackFontSpec = `${computedStyle.fontWeight} ${computedStyle.fontSize} ${computedStyle.fontFamily}`;
    const fallbackLabelWidth = Math.ceil(measureTextWidth(labelNode.textContent || "", fallbackFontSpec));
    return measuredLabelWidth > 0 ? measuredLabelWidth : fallbackLabelWidth;
  };
  const resolveTickLeftPx = (tickNode) => {
    if (!(tickNode instanceof HTMLElement)) {
      return 0;
    }
    const serializedPx = Number.parseFloat(tickNode.dataset.axisLeftPx || "");
    if (Number.isFinite(serializedPx)) {
      return serializedPx;
    }
    const measuredLeft = Number.parseFloat(tickNode.style.left || "0");
    return Number.isFinite(measuredLeft) ? measuredLeft : 0;
  };
  const resolveLabelPlacement = (index) => {
    if (tickNodes.length === 1 || index === 0) {
      return "start";
    }
    if (index === lastIndex) {
      return "end";
    }
    return "center";
  };
  const clampAbsoluteLeft = (value, width) => Math.max(0, Math.min(value, Math.max(0, containerWidth - width)));
  const resolveLabelLayout = (tickNode, labelNode, index) => {
    const placement = resolveLabelPlacement(index);
    const tickLeftPx = resolveTickLeftPx(tickNode);
    const maxWidthByPlacement = placement === "start" ? Math.max(1, containerWidth - tickLeftPx) : placement === "end" ? Math.max(1, tickLeftPx) : Math.max(1, containerWidth);
    labelNode.style.maxWidth = `${Math.max(1, Math.floor(maxWidthByPlacement))}px`;
    const labelWidthRaw = resolveLabelWidth(labelNode);
    const labelWidth = Math.max(1, Math.min(maxWidthByPlacement, labelWidthRaw > 0 ? labelWidthRaw : 1));
    const desiredLeft = placement === "start" ? tickLeftPx : placement === "end" ? tickLeftPx - labelWidth : tickLeftPx - labelWidth / 2;
    const absoluteLeft = clampAbsoluteLeft(desiredLeft, labelWidth);
    return {
      tickNode,
      labelNode,
      tickLeftPx,
      labelWidth,
      absoluteLeft,
      absoluteRight: absoluteLeft + labelWidth
    };
  };
  const applyLabelLayout = (layout, hidden) => {
    if (!layout || !(layout.labelNode instanceof HTMLElement)) {
      return;
    }
    layout.labelNode.hidden = hidden;
    if (hidden) {
      return;
    }
    layout.labelNode.style.left = `${Math.round(layout.absoluteLeft - layout.tickLeftPx)}px`;
  };
  const labelLayouts = tickNodes.map((tickNode, index) => {
    if (!(tickNode instanceof HTMLElement)) {
      return null;
    }
    const labelNode = tickNode.querySelector(".timeline-axis-label");
    if (!(labelNode instanceof HTMLElement)) {
      return null;
    }
    labelNode.hidden = false;
    labelNode.style.left = "0px";
    return resolveLabelLayout(tickNode, labelNode, index);
  }).filter((layout) => layout && layout.labelNode instanceof HTMLElement);
  if (labelLayouts.length === 0) {
    return;
  }
  if (labelLayouts.length === 1) {
    applyLabelLayout(labelLayouts[0], false);
    return;
  }
  const firstLayout = labelLayouts[0];
  const lastLayout = labelLayouts[labelLayouts.length - 1];
  applyLabelLayout(firstLayout, false);
  applyLabelLayout(lastLayout, false);
  let previousLabelRight = firstLayout.absoluteRight;
  const reservedLastLeft = lastLayout.absoluteLeft;
  for (let index = 1;index < labelLayouts.length - 1; index += 1) {
    const currentLayout = labelLayouts[index];
    const overlapsPrevious = currentLayout.absoluteLeft < previousLabelRight + minLabelGap;
    const overlapsLast = currentLayout.absoluteRight > reservedLastLeft - minLabelGap;
    const shouldHide = overlapsPrevious || overlapsLast;
    applyLabelLayout(currentLayout, shouldHide);
    if (!shouldHide) {
      previousLabelRight = currentLayout.absoluteRight;
    }
  }
}
function renderStaticTimelineAxes(scope) {
  const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
  root.querySelectorAll("[data-timeline-axis][data-axis-start][data-axis-end]").forEach((container) => {
    if (!(container instanceof HTMLElement)) {
      return;
    }
    const startDate = parseIsoDate(container.dataset.axisStart);
    const endDate = parseIsoDate(container.dataset.axisEnd);
    if (!(startDate instanceof Date) || !(endDate instanceof Date)) {
      return;
    }
    renderTimelineAxis(container, startDate, endDate);
  });
}
function buildSchedulePlanAndActual(state, startDate) {
  const plan = [];
  const actual = [];
  let planCursor = new Date(startDate.getTime());
  let actualCursor = new Date(startDate.getTime());
  state.forEach((item) => {
    const planStart = new Date(planCursor.getTime());
    const planEnd = addCalendarDays(planStart, item.duration);
    plan.push({ start: planStart, end: planEnd });
    planCursor = new Date(planEnd.getTime());
    const actualStart = new Date(actualCursor.getTime());
    const actualEnd = addCalendarDays(actualStart, Math.max(0, item.duration + item.delay));
    actual.push({ start: actualStart, end: actualEnd });
    actualCursor = new Date(actualEnd.getTime());
  });
  return { plan, actual };
}
function buildScheduleScale(startDate, deadlineDate, actualEndDate) {
  const startStamp = toUtcDayStamp(startDate);
  const axisEndStamp = Math.max(startStamp, toUtcDayStamp(deadlineDate), toUtcDayStamp(actualEndDate), toUtcDayStamp(new Date));
  const totalDays = Math.max(1, Math.round((axisEndStamp - startStamp) / msPerDay));
  return {
    totalDays,
    axisEndDate: addCalendarDays(startDate, totalDays)
  };
}
function toSchedulePercent(valueDate, axisStart, totalDays) {
  const days = diffCalendarDays(valueDate, axisStart);
  return Math.max(0, Math.min(100, days * 100 / totalDays));
}
function toScheduleWidthPercent(startDate, endDate, totalDays) {
  const days = Math.max(0, diffCalendarDays(endDate, startDate));
  return Math.max(0, Math.min(100, days * 100 / totalDays));
}
function formatSchedulePercent(value) {
  return `${Number.isFinite(value) ? value.toFixed(4) : "0.0000"}%`;
}
function formatScheduleSegmentWidth(value) {
  if (!Number.isFinite(value) || value <= 0) {
    return "0%";
  }
  return `calc(${value.toFixed(4)}% + 1px)`;
}
function formatScheduleOffsetLabel(delay) {
  return delay > 0 ? `+${delay} dnů` : `${delay} dnů`;
}
function queueRecordSchedulePlannerRecalc(form, attempt) {
  if (!(form instanceof HTMLFormElement) || !form.isConnected) {
    return;
  }
  const retryAttempt = Number.isFinite(attempt) ? Math.max(0, Math.trunc(attempt)) : 0;
  const schedulePanel = form.querySelector('[data-record-modal-panel="schedule"]');
  if (schedulePanel instanceof HTMLElement && schedulePanel.hidden) {
    return;
  }
  const planner = form._recordSchedulePlanner;
  if (planner && typeof planner.recalcAll === "function") {
    planner.recalcAll();
    return;
  }
  if (retryAttempt >= 6) {
    return;
  }
  window.requestAnimationFrame(() => {
    queueRecordSchedulePlannerRecalc(form, retryAttempt + 1);
  });
}

class ScheduleBlockRenderer {
  constructor(root, options = {}) {
    this.root = root;
    this.mode = String(root.dataset.scheduleMode || "project-readonly").trim() || "project-readonly";
    this.form = options.form instanceof HTMLFormElement ? options.form : root.closest('form[data-record-editor-form="true"]');
    this.startInput = this.form instanceof HTMLFormElement ? this.form.querySelector('input[name="DatumZalozeni"]') : null;
    this.deadlineInput = this.form instanceof HTMLFormElement ? this.form.querySelector('input[name="TerminUkonceni"]') : null;
    this.summaryDeadline = root.querySelector("[data-schedule-summary-deadline]");
    this.summaryBaseline = root.querySelector("[data-schedule-summary-baseline]");
    this.summaryShifted = root.querySelector("[data-schedule-summary-shifted]");
    this.summaryDuration = root.querySelector("[data-schedule-summary-duration]");
    this.summaryDelay = root.querySelector("[data-schedule-summary-delay]");
    this.summaryState = root.querySelector("[data-schedule-summary-state]");
    this.summaryOverrun = root.querySelector("[data-schedule-summary-overrun]");
    this.statusLine = root.querySelector(".schedule-status-line");
    this.overviewAxis = root.querySelector('[data-schedule-axis="overview"]');
    this.breakdownAxis = root.querySelector('[data-schedule-axis="breakdown"]');
    this.overviewTodayMarkers = Array.from(root.querySelectorAll('[data-schedule-marker="today"]')).filter((node) => node instanceof HTMLElement);
    this.overviewDeadlineMarkers = Array.from(root.querySelectorAll('[data-schedule-marker="deadline"]')).filter((node) => node instanceof HTMLElement);
    this.overviewPlannedSegments = this.collectSegmentMap('[data-schedule-segment-kind="planned"]');
    this.overviewActualSegments = this.collectSegmentMap('[data-schedule-segment-kind="actual"]');
    this.breakdownRows = Array.from(root.querySelectorAll("[data-schedule-breakdown-track]")).map((track) => {
      if (!(track instanceof HTMLElement)) {
        return null;
      }
      const row = track.closest("[data-schedule-step-row]");
      if (!(row instanceof HTMLElement)) {
        return null;
      }
      const stepIndex = Number.parseInt(row.dataset.stepIndex || "", 10);
      if (!Number.isInteger(stepIndex)) {
        return null;
      }
      return {
        row,
        stepIndex,
        offset: row.querySelector("[data-schedule-offset]"),
        track,
        plannedSegment: track.querySelector('[data-schedule-breakdown-segment="planned"]'),
        actualSegment: track.querySelector('[data-schedule-breakdown-segment="actual"]'),
        todayMarker: track.querySelector("[data-schedule-breakdown-today]")
      };
    }).filter((entry) => entry && Number.isInteger(entry.stepIndex)).sort((left, right) => left.stepIndex - right.stepIndex);
    this.editorRows = Array.from(root.querySelectorAll("tr[data-schedule-step-row]")).map((row) => {
      if (!(row instanceof HTMLTableRowElement)) {
        return null;
      }
      const stepIndex = Number.parseInt(row.dataset.stepIndex || "", 10);
      if (!Number.isInteger(stepIndex)) {
        return null;
      }
      return {
        row,
        stepIndex,
        durationInput: row.querySelector("[data-schedule-duration]"),
        delayInput: row.querySelector("[data-schedule-delay]"),
        dateInput: row.querySelector("[data-schedule-date]"),
        delayDateInput: row.querySelector("[data-schedule-delay-date]"),
        durationInc: row.querySelector("[data-schedule-duration-inc]"),
        durationDec: row.querySelector("[data-schedule-duration-dec]"),
        delayInc: row.querySelector("[data-schedule-delay-inc]"),
        delayDec: row.querySelector("[data-schedule-delay-dec]")
      };
    }).filter((entry) => entry && Number.isInteger(entry.stepIndex)).sort((left, right) => left.stepIndex - right.stepIndex);
    const inlineDelayColor = String(root.style.getPropertyValue("--record-schedule-delay-color") || "").trim();
    const computedDelayColor = window.getComputedStyle(root).getPropertyValue("--record-schedule-delay-color").trim();
    this.delayColor = inlineDelayColor || computedDelayColor || "var(--schedule-delay)";
  }
  collectSegmentMap(selector) {
    const segments = new Map;
    this.root.querySelectorAll(selector).forEach((node) => {
      if (!(node instanceof HTMLElement)) {
        return;
      }
      const stepIndex = Number.parseInt(node.dataset.stepIndex || "", 10);
      if (!Number.isInteger(stepIndex)) {
        return;
      }
      segments.set(stepIndex, node);
    });
    return segments;
  }
  isReady() {
    return this.root instanceof HTMLElement && (this.editorRows.length > 0 || this.breakdownRows.length > 0);
  }
  normalizeInt(input) {
    if (!(input instanceof HTMLInputElement)) {
      return 0;
    }
    const parsed = Number.parseInt((input.value || "").trim(), 10);
    return Number.isFinite(parsed) ? Math.max(0, parsed) : 0;
  }
  normalizeSignedInt(input) {
    if (!(input instanceof HTMLInputElement)) {
      return 0;
    }
    const parsed = Number.parseInt((input.value || "").trim(), 10);
    return Number.isFinite(parsed) ? parsed : 0;
  }
  setDateInputValue(input, value) {
    if (!(input instanceof HTMLInputElement)) {
      return;
    }
    input.value = formatIsoDate(value);
    const dateField = input.closest("[data-app-date-field]");
    if (!(dateField instanceof HTMLElement)) {
      return;
    }
    const display = dateField.querySelector("[data-app-date-display]");
    if (display instanceof HTMLInputElement) {
      display.value = formatDisplayDate(value);
    }
  }
  getStartDate() {
    if (this.startInput instanceof HTMLInputElement) {
      return parseIsoDate(this.startInput.value) || new Date;
    }
    return parseIsoDate(this.root.dataset.scheduleStart) || new Date;
  }
  getDeadlineDate(startDate) {
    if (this.deadlineInput instanceof HTMLInputElement) {
      return parseIsoDate(this.deadlineInput.value) || startDate;
    }
    return parseIsoDate(this.root.dataset.scheduleDeadline) || startDate;
  }
  readState() {
    if (this.editorRows.length > 0) {
      return this.editorRows.map((entry) => ({
        stepIndex: entry.stepIndex,
        name: String(entry.row.dataset.stepName || "").trim(),
        color: String(entry.row.dataset.stepColor || "").trim(),
        duration: this.normalizeInt(entry.durationInput),
        delay: this.normalizeSignedInt(entry.delayInput)
      }));
    }
    return this.breakdownRows.map((entry) => ({
      stepIndex: entry.stepIndex,
      name: String(entry.row.dataset.stepName || "").trim(),
      color: String(entry.row.dataset.stepColor || "").trim(),
      duration: Math.max(0, Number.parseInt(entry.row.dataset.stepDuration || "0", 10) || 0),
      delay: Number.parseInt(entry.row.dataset.stepDelay || "0", 10) || 0
    }));
  }
  writeState(state) {
    state.forEach((item, index) => {
      const entry = this.editorRows[index];
      if (!entry) {
        return;
      }
      if (entry.durationInput instanceof HTMLInputElement) {
        entry.durationInput.value = String(item.duration);
      }
      if (entry.delayInput instanceof HTMLInputElement) {
        entry.delayInput.value = String(item.delay);
      }
      entry.row.dataset.stepDuration = String(item.duration);
      entry.row.dataset.stepDelay = String(item.delay);
    });
  }
  recalcFromDuration() {
    this.recalcAll();
  }
  recalcFromDelay() {
    this.recalcAll();
  }
  recalcFromDate(stepIndex) {
    const entry = this.editorRows[stepIndex];
    if (!entry || !(entry.dateInput instanceof HTMLInputElement)) {
      this.recalcAll();
      return;
    }
    const state = this.readState();
    const startDate = this.getStartDate();
    const { plan } = buildSchedulePlanAndActual(state, startDate);
    const previousPlanEnd = stepIndex === 0 ? startDate : plan[stepIndex - 1]?.end || startDate;
    const selectedDate = parseIsoDate(entry.dateInput.value) || previousPlanEnd;
    state[stepIndex] = { ...state[stepIndex], duration: Math.max(0, diffCalendarDays(selectedDate, previousPlanEnd)) };
    this.writeState(state);
    this.recalcAll();
  }
  recalcFromDelayDate(stepIndex) {
    const entry = this.editorRows[stepIndex];
    if (!entry || !(entry.delayDateInput instanceof HTMLInputElement)) {
      this.recalcAll();
      return;
    }
    const state = this.readState();
    const startDate = this.getStartDate();
    const { plan } = buildSchedulePlanAndActual(state, startDate);
    const planEnd = plan[stepIndex]?.end || startDate;
    const selectedDate = parseIsoDate(entry.delayDateInput.value) || planEnd;
    state[stepIndex] = { ...state[stepIndex], delay: diffCalendarDays(selectedDate, planEnd) };
    this.writeState(state);
    this.recalcAll();
  }
  setAxisRange(axis, startDate, endDate) {
    if (!(axis instanceof HTMLElement) || !(startDate instanceof Date) || !(endDate instanceof Date)) {
      return;
    }
    axis.dataset.axisStart = formatIsoDate(startDate);
    axis.dataset.axisEnd = formatIsoDate(endDate);
    renderTimelineAxis(axis, startDate, endDate);
  }
  applySegmentLayout(segment, leftPercent, widthPercent, title) {
    if (!(segment instanceof HTMLElement)) {
      return;
    }
    segment.style.left = formatSchedulePercent(leftPercent);
    segment.style.width = formatScheduleSegmentWidth(widthPercent);
    if (title) {
      segment.title = title;
    }
  }
  resetSegmentLayout(segment) {
    if (!(segment instanceof HTMLElement)) {
      return;
    }
    segment.style.left = "0%";
    segment.style.width = "0%";
  }
  renderSummary(plan, actual, state, startDate, deadlineDate) {
    const baselineEnd = plan.length > 0 ? plan[plan.length - 1].end : startDate;
    const shiftedEnd = actual.length > 0 ? actual[actual.length - 1].end : startDate;
    const totalDuration = state.reduce((sum, item) => sum + item.duration, 0);
    const totalDelay = state.reduce((sum, item) => sum + item.delay, 0);
    const stihame = toUtcDayStamp(shiftedEnd) <= toUtcDayStamp(deadlineDate);
    const overrunDays = stihame ? 0 : diffCalendarDays(shiftedEnd, deadlineDate);
    if (this.summaryDeadline instanceof HTMLElement) {
      this.summaryDeadline.textContent = formatDisplayDate(deadlineDate);
    }
    if (this.summaryBaseline instanceof HTMLElement) {
      this.summaryBaseline.textContent = formatDisplayDate(baselineEnd);
    }
    if (this.summaryShifted instanceof HTMLElement) {
      this.summaryShifted.textContent = formatDisplayDate(shiftedEnd);
    }
    if (this.summaryDuration instanceof HTMLElement) {
      this.summaryDuration.textContent = String(totalDuration);
    }
    if (this.summaryDelay instanceof HTMLElement) {
      this.summaryDelay.textContent = totalDelay > 0 ? `+${totalDelay}` : String(totalDelay);
    }
    if (this.summaryState instanceof HTMLElement) {
      this.summaryState.textContent = stihame ? "Stíháme" : "Nestíháme";
    }
    if (this.summaryOverrun instanceof HTMLElement) {
      this.summaryOverrun.textContent = this.mode === "record-editor" ? stihame ? "" : `(+${overrunDays} dnů)` : `${overrunDays} dnů`;
    }
    if (this.statusLine instanceof HTMLElement) {
      this.statusLine.classList.toggle("ok", stihame);
      this.statusLine.classList.toggle("late", !stihame);
    }
  }
  renderOverview(plan, actual, state, startDate, deadlineDate) {
    const actualEnd = actual.length > 0 ? actual[actual.length - 1].end : startDate;
    const { totalDays, axisEndDate } = buildScheduleScale(startDate, deadlineDate, actualEnd);
    const today = new Date;
    const todayDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const todayPercent = toSchedulePercent(todayDate, startDate, totalDays);
    const deadlinePercent = toSchedulePercent(deadlineDate, startDate, totalDays);
    this.overviewTodayMarkers.forEach((marker) => {
      marker.style.left = formatSchedulePercent(todayPercent);
      marker.title = `Dnes: ${formatDisplayDate(todayDate)}`;
    });
    this.overviewDeadlineMarkers.forEach((marker) => {
      marker.style.left = formatSchedulePercent(deadlinePercent);
      marker.title = `Termín úkolu: ${formatDisplayDate(deadlineDate)}`;
    });
    this.setAxisRange(this.overviewAxis, startDate, axisEndDate);
    let previousPlanRight = 0;
    let previousActualRight = 0;
    let skippedCompactActualWidth = 0;
    state.forEach((item, index) => {
      const planItem = plan[index];
      const actualItem = actual[index];
      const plannedSegment = this.overviewPlannedSegments.get(item.stepIndex);
      const actualSegment = this.overviewActualSegments.get(item.stepIndex);
      if (!planItem || !actualItem) {
        this.resetSegmentLayout(plannedSegment);
        this.resetSegmentLayout(actualSegment);
        return;
      }
      const rawPlanLeft = toSchedulePercent(planItem.start, startDate, totalDays);
      const rawPlanRight = toSchedulePercent(planItem.end, startDate, totalDays);
      const rawActualLeft = toSchedulePercent(actualItem.start, startDate, totalDays);
      const rawActualRight = toSchedulePercent(actualItem.end, startDate, totalDays);
      if (item.duration > 0) {
        const planLeft = Math.max(previousPlanRight, rawPlanLeft);
        const planRight = Math.max(planLeft, rawPlanRight);
        const planWidth = Math.max(0, planRight - planLeft);
        this.applySegmentLayout(plannedSegment, planLeft, planWidth, `${item.name}: plán ${formatDisplayDate(planItem.start)} - ${formatDisplayDate(planItem.end)}`);
        previousPlanRight = planRight;
        const adjustedActualLeft = Math.max(0, rawActualLeft - skippedCompactActualWidth);
        const adjustedActualRight = Math.max(adjustedActualLeft, rawActualRight - skippedCompactActualWidth);
        const actualLeft = Math.max(previousActualRight, adjustedActualLeft);
        const actualRight = Math.max(actualLeft, adjustedActualRight);
        const actualWidth = Math.max(0, actualRight - actualLeft);
        this.applySegmentLayout(actualSegment, actualLeft, actualWidth, `${item.name}: skutečnost ${formatDisplayDate(actualItem.start)} - ${formatDisplayDate(actualItem.end)}`);
        previousActualRight = actualRight;
      } else {
        skippedCompactActualWidth += Math.max(0, rawActualRight - rawActualLeft);
        this.resetSegmentLayout(plannedSegment);
        this.resetSegmentLayout(actualSegment);
      }
    });
  }
  resolveBreakdownAxis(plan, actual, startDate, deadlineDate) {
    const dates = [];
    plan.forEach((item) => {
      dates.push(item.start, item.end);
    });
    actual.forEach((item) => {
      dates.push(item.start, item.end);
    });
    if (dates.length === 0) {
      return {
        axisStart: startDate,
        axisEnd: deadlineDate
      };
    }
    const sorted = dates.slice().sort((left, right) => left.getTime() - right.getTime());
    const axisStart = sorted[0];
    const latest = sorted[sorted.length - 1];
    const axisEnd = latest.getTime() > deadlineDate.getTime() ? latest : deadlineDate;
    return {
      axisStart,
      axisEnd
    };
  }
  renderBreakdown(plan, actual, state, startDate, deadlineDate) {
    if (this.breakdownRows.length === 0) {
      return;
    }
    const { axisStart, axisEnd } = this.resolveBreakdownAxis(plan, actual, startDate, deadlineDate);
    const totalDays = Math.max(1, diffCalendarDays(axisEnd, axisStart));
    const today = new Date;
    const todayDate = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const todayPercent = toSchedulePercent(todayDate, axisStart, totalDays);
    this.setAxisRange(this.breakdownAxis, axisStart, axisEnd);
    this.breakdownRows.forEach((entry, index) => {
      const item = state[index];
      const planItem = plan[index];
      const actualItem = actual[index];
      if (!item || !planItem || !actualItem) {
        return;
      }
      if (entry.offset instanceof HTMLElement) {
        entry.offset.textContent = formatScheduleOffsetLabel(item.delay);
        entry.offset.classList.toggle("late", item.delay > 0);
        entry.offset.classList.toggle("ahead", item.delay < 0);
      }
      if (entry.todayMarker instanceof HTMLElement) {
        entry.todayMarker.style.left = formatSchedulePercent(todayPercent);
        entry.todayMarker.title = `Dnes: ${formatDisplayDate(todayDate)}`;
      }
      if (item.duration <= 0) {
        this.resetSegmentLayout(entry.plannedSegment);
        this.resetSegmentLayout(entry.actualSegment);
        return;
      }
      const plannedLeft = toSchedulePercent(planItem.start, axisStart, totalDays);
      const plannedWidth = toScheduleWidthPercent(planItem.start, planItem.end, totalDays);
      this.applySegmentLayout(entry.plannedSegment, plannedLeft, plannedWidth, `${item.name}: plán ${formatDisplayDate(planItem.start)} - ${formatDisplayDate(planItem.end)}`);
      const actualLeft = toSchedulePercent(actualItem.start, axisStart, totalDays);
      const actualWidth = toScheduleWidthPercent(actualItem.start, actualItem.end, totalDays);
      this.applySegmentLayout(entry.actualSegment, actualLeft, actualWidth, `${item.name}: skutečnost ${formatDisplayDate(actualItem.start)} - ${formatDisplayDate(actualItem.end)}`);
      if (entry.actualSegment instanceof HTMLElement) {
        entry.actualSegment.style.setProperty("--schedule-actual-color", this.delayColor);
      }
    });
  }
  renderEditorRows(plan, actual) {
    this.editorRows.forEach((entry, index) => {
      const planEnd = plan[index]?.end;
      const actualEnd = actual[index]?.end;
      if (entry.dateInput instanceof HTMLInputElement && planEnd instanceof Date) {
        this.setDateInputValue(entry.dateInput, planEnd);
      }
      if (entry.delayDateInput instanceof HTMLInputElement && actualEnd instanceof Date) {
        this.setDateInputValue(entry.delayDateInput, actualEnd);
      }
      entry.row.dataset.stepDuration = String(this.normalizeInt(entry.durationInput));
      entry.row.dataset.stepDelay = String(this.normalizeSignedInt(entry.delayInput));
    });
  }
  recalcAll() {
    const state = this.readState();
    if (state.length === 0) {
      return;
    }
    const startDate = this.getStartDate();
    const deadlineDate = this.getDeadlineDate(startDate);
    const { plan, actual } = buildSchedulePlanAndActual(state, startDate);
    this.renderEditorRows(plan, actual);
    this.renderSummary(plan, actual, state, startDate, deadlineDate);
    this.renderOverview(plan, actual, state, startDate, deadlineDate);
    this.renderBreakdown(plan, actual, state, startDate, deadlineDate);
    queueRainbowSegmentRender(this.root);
  }
  bindNumericStepper(button, input, delta, onChange) {
    if (!(button instanceof HTMLButtonElement) || !(input instanceof HTMLInputElement)) {
      return;
    }
    let repeatDelayTimer = null;
    let repeatIntervalTimer = null;
    let suppressClickOnce = false;
    const repeatDelayMs = 350;
    const repeatIntervalMs = 70;
    const stopRepeat = () => {
      if (repeatDelayTimer !== null) {
        window.clearTimeout(repeatDelayTimer);
        repeatDelayTimer = null;
      }
      if (repeatIntervalTimer !== null) {
        window.clearInterval(repeatIntervalTimer);
        repeatIntervalTimer = null;
      }
    };
    const stepOnce = () => {
      if (button.disabled || input.disabled) {
        return;
      }
      const isDelayInput = input.hasAttribute("data-schedule-delay");
      const currentValue = isDelayInput ? this.normalizeSignedInt(input) : this.normalizeInt(input);
      const nextValue = isDelayInput ? currentValue + delta : Math.max(0, currentValue + delta);
      input.value = String(nextValue);
      if (nextValue !== currentValue) {
        onChange();
      }
    };
    button.addEventListener("mousedown", (event) => {
      if (!(event instanceof MouseEvent) || event.button !== 0) {
        return;
      }
      event.preventDefault();
      suppressClickOnce = true;
      stepOnce();
      stopRepeat();
      repeatDelayTimer = window.setTimeout(() => {
        repeatIntervalTimer = window.setInterval(() => {
          stepOnce();
        }, repeatIntervalMs);
      }, repeatDelayMs);
    });
    button.addEventListener("click", () => {
      if (suppressClickOnce) {
        suppressClickOnce = false;
        return;
      }
      stepOnce();
    });
    button.addEventListener("mouseup", stopRepeat);
    button.addEventListener("mouseleave", stopRepeat);
    button.addEventListener("blur", () => {
      stopRepeat();
      suppressClickOnce = false;
    });
    window.addEventListener("mouseup", (event) => {
      stopRepeat();
      if (!(event.target instanceof Element) || !button.contains(event.target)) {
        suppressClickOnce = false;
      }
    });
  }
  bind() {
    if (this.mode !== "record-editor") {
      return;
    }
    this.editorRows.forEach((entry, index) => {
      if (entry.durationInput instanceof HTMLInputElement) {
        entry.durationInput.addEventListener("input", () => this.recalcFromDuration(index));
        entry.durationInput.addEventListener("change", () => {
          entry.durationInput.value = String(this.normalizeInt(entry.durationInput));
          this.recalcFromDuration(index);
        });
      }
      if (entry.delayInput instanceof HTMLInputElement) {
        entry.delayInput.addEventListener("input", () => this.recalcFromDelay(index));
        entry.delayInput.addEventListener("change", () => {
          entry.delayInput.value = String(this.normalizeSignedInt(entry.delayInput));
          this.recalcFromDelay(index);
        });
      }
      if (entry.dateInput instanceof HTMLInputElement) {
        entry.dateInput.addEventListener("change", () => this.recalcFromDate(index));
      }
      if (entry.delayDateInput instanceof HTMLInputElement) {
        entry.delayDateInput.addEventListener("change", () => this.recalcFromDelayDate(index));
      }
      this.bindNumericStepper(entry.durationInc, entry.durationInput, 1, () => this.recalcFromDuration(index));
      this.bindNumericStepper(entry.durationDec, entry.durationInput, -1, () => this.recalcFromDuration(index));
      this.bindNumericStepper(entry.delayInc, entry.delayInput, 1, () => this.recalcFromDelay(index));
      this.bindNumericStepper(entry.delayDec, entry.delayInput, -1, () => this.recalcFromDelay(index));
    });
    if (this.startInput instanceof HTMLInputElement) {
      this.startInput.addEventListener("change", () => this.recalcAll());
    }
    if (this.deadlineInput instanceof HTMLInputElement) {
      this.deadlineInput.addEventListener("change", () => this.recalcAll());
    }
  }
}
function initScheduleBlockRenderers(scope) {
  const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
  const blocks = [];
  if (scope instanceof HTMLElement && scope.matches("[data-schedule-block]")) {
    blocks.push(scope);
  }
  root.querySelectorAll("[data-schedule-block]").forEach((block) => {
    if (block instanceof HTMLElement) {
      blocks.push(block);
    }
  });
  blocks.forEach((block) => {
    if (!(block instanceof HTMLElement)) {
      return;
    }
    if (block._scheduleRenderer instanceof ScheduleBlockRenderer) {
      block._scheduleRenderer.recalcAll();
      return;
    }
    const form = block.closest('form[data-record-editor-form="true"]');
    const renderer = new ScheduleBlockRenderer(block, { form });
    if (!renderer.isReady()) {
      return;
    }
    renderer.bind();
    renderer.recalcAll();
    block._scheduleRenderer = renderer;
    if (form instanceof HTMLFormElement && renderer.mode === "record-editor") {
      form._recordSchedulePlanner = renderer;
      form.dataset.recordScheduleReady = "true";
    }
  });
}
function initRecordSchedulePlanner(scope) {
  if (!(scope instanceof HTMLElement || scope instanceof Document)) {
    return;
  }
  const forms = [];
  if (scope instanceof HTMLFormElement && scope.matches('form[data-record-schedule-form="true"]')) {
    forms.push(scope);
  }
  scope.querySelectorAll('form[data-record-schedule-form="true"]').forEach((form) => {
    if (form instanceof HTMLFormElement) {
      forms.push(form);
    }
  });
  forms.forEach((form) => {
    if (!(form instanceof HTMLFormElement)) {
      return;
    }
    const editor = form.querySelector('[data-schedule-block][data-schedule-mode="record-editor"]');
    const schedulePanel = form.querySelector("[data-record-schedule-panel]");
    if (!(editor instanceof HTMLElement) || schedulePanel instanceof HTMLElement && schedulePanel.dataset.scheduleDisabled === "true") {
      return;
    }
    initScheduleBlockRenderers(editor);
    queueRecordSchedulePlannerRecalc(form, 0);
  });
}
function initProjectScheduleUi() {
  const schedulePanel = document.querySelector('[data-tab-panel="harmonogram"]');
  if (!(schedulePanel instanceof HTMLElement)) {
    return;
  }
  const filterPanel = document.querySelector("[data-schedule-filter-panel]");
  if (filterPanel instanceof HTMLElement) {
    const storedOpen = localStorage.getItem("pmtracker.schedule.filters.open");
    setScheduleFilterPanelOpen(storedOpen === "true");
  }
  restoreScheduleFilterState();
  setProjectFilterSaveStatus("schedule", "");
  initScheduleBlockRenderers(schedulePanel);
  applyProjectScheduleFilters();
  renderStaticTimelineAxes(schedulePanel);
  queueRainbowSegmentRender(schedulePanel);
  initScheduleExpandUi(schedulePanel);
}

// PmTracker.Web/wwwroot/js/modules/tableTools.js
function parseCzechDateTime(value) {
  const text = (value || "").trim();
  if (!text || text === "-") {
    return null;
  }
  const match = text.match(/^(\d{2})\.(\d{2})\.(\d{4})(?:\s+(\d{2}):(\d{2}))?$/);
  if (!match) {
    return null;
  }
  const [, dayText, monthText, yearText, hourText, minuteText] = match;
  const day = Number.parseInt(dayText, 10);
  const month = Number.parseInt(monthText, 10) - 1;
  const year = Number.parseInt(yearText, 10);
  const hour = Number.parseInt(hourText || "0", 10);
  const minute = Number.parseInt(minuteText || "0", 10);
  const valueDate = new Date(year, month, day, hour, minute, 0, 0);
  return Number.isNaN(valueDate.getTime()) ? null : valueDate.getTime();
}
function parseBooleanValue(value) {
  const normalized = normalizeFilterText2(value);
  if (!normalized || normalized === "-") {
    return null;
  }
  if (["ano", "true", "1", "yes"].includes(normalized)) {
    return 1;
  }
  if (["ne", "false", "0", "no"].includes(normalized)) {
    return 0;
  }
  return null;
}
function parseComparableValue(rawValue, type) {
  if (type === "datetime") {
    return parseCzechDateTime(rawValue);
  }
  if (type === "boolean") {
    return parseBooleanValue(rawValue);
  }
  if (type === "number") {
    const normalized = (rawValue || "").trim().replace(/\s+/g, "").replace(",", ".");
    if (!normalized || normalized === "-") {
      return null;
    }
    const parsed = Number.parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed : null;
  }
  return normalizeFilterText2(rawValue);
}
function resolveCellValue(row, columnIndex) {
  const cells = Array.from(row.children).filter((cell2) => cell2 instanceof HTMLTableCellElement);
  const cell = cells[columnIndex];
  if (!(cell instanceof HTMLTableCellElement)) {
    return "";
  }
  return (cell.dataset.tableSortValue || cell.textContent || "").trim();
}
function buildSearchText(row) {
  const explicit = (row.dataset.tableSearchText || "").trim();
  if (explicit) {
    return normalizeFilterText2(explicit);
  }
  const text = Array.from(row.children).filter((cell) => cell instanceof HTMLTableCellElement && !cell.classList.contains("table-actions")).map((cell) => cell.textContent || "").join(" ");
  return normalizeFilterText2(text);
}
function ensureEmptyRow(table) {
  const tbody = table.tBodies[0];
  if (!(tbody instanceof HTMLTableSectionElement)) {
    return null;
  }
  const existing = tbody.querySelector("[data-table-empty-row]");
  if (existing instanceof HTMLTableRowElement) {
    return existing;
  }
  const headerCells = table.tHead?.rows[0]?.cells;
  const colspan = headerCells?.length || 1;
  const message = (table.dataset.tableEmptyMessage || "Žádné výsledky.").trim();
  const row = document.createElement("tr");
  row.setAttribute("data-table-empty-row", "");
  row.hidden = true;
  const cell = document.createElement("td");
  cell.colSpan = colspan;
  cell.className = "table-empty-row";
  cell.textContent = message;
  row.appendChild(cell);
  tbody.appendChild(row);
  return row;
}
function getDataRows(table) {
  return Array.from(table.tBodies[0]?.querySelectorAll("tr[data-table-row]") || []).filter((row) => row instanceof HTMLTableRowElement);
}
function updateSortIndicators(table) {
  const activeIndex = table.dataset.tableSortIndex || "";
  const direction = table.dataset.tableSortDirection === "desc" ? "desc" : "asc";
  table.querySelectorAll("[data-table-sort-button]").forEach((button) => {
    if (!(button instanceof HTMLButtonElement)) {
      return;
    }
    const sortIndex = button.dataset.tableSortIndex || "";
    const isActive = sortIndex === activeIndex;
    button.dataset.tableSortDirection = isActive ? direction : "";
    button.classList.toggle("is-active", isActive);
    button.setAttribute("aria-sort", isActive ? direction === "desc" ? "descending" : "ascending" : "none");
  });
}
function compareRows(left, right, columnIndex, type, direction) {
  const leftValue = parseComparableValue(resolveCellValue(left, columnIndex), type);
  const rightValue = parseComparableValue(resolveCellValue(right, columnIndex), type);
  if (leftValue === null && rightValue === null) {
    return 0;
  }
  if (leftValue === null) {
    return 1;
  }
  if (rightValue === null) {
    return -1;
  }
  if (leftValue < rightValue) {
    return direction === "desc" ? 1 : -1;
  }
  if (leftValue > rightValue) {
    return direction === "desc" ? -1 : 1;
  }
  return 0;
}
function sortTableRows(table) {
  const sortIndexText = table.dataset.tableSortIndex || "";
  if (!sortIndexText) {
    return;
  }
  const sortIndex = Number.parseInt(sortIndexText, 10);
  if (!Number.isInteger(sortIndex) || sortIndex < 0) {
    return;
  }
  const button = table.querySelector(`[data-table-sort-button][data-table-sort-index="${sortIndex}"]`);
  const sortType = button instanceof HTMLButtonElement ? button.dataset.tableSortType || "text" : "text";
  const direction = table.dataset.tableSortDirection === "desc" ? "desc" : "asc";
  const rows = getDataRows(table);
  const sorted = rows.map((row, index) => ({ row, index })).sort((left, right) => {
    const result = compareRows(left.row, right.row, sortIndex, sortType, direction);
    return result !== 0 ? result : left.index - right.index;
  });
  const tbody = table.tBodies[0];
  if (!(tbody instanceof HTMLTableSectionElement)) {
    return;
  }
  const emptyRow = ensureEmptyRow(table);
  sorted.forEach((entry) => tbody.appendChild(entry.row));
  if (emptyRow instanceof HTMLTableRowElement) {
    tbody.appendChild(emptyRow);
  }
}
function filterTableRows(root, table) {
  const query = normalizeFilterText2(root.querySelector("[data-table-tools-search-input]") instanceof HTMLInputElement ? root.querySelector("[data-table-tools-search-input]").value : "");
  const rows = getDataRows(table);
  let visibleCount = 0;
  rows.forEach((row) => {
    const matches = !query || buildSearchText(row).includes(query);
    row.hidden = !matches;
    if (matches) {
      visibleCount += 1;
    }
  });
  const emptyRow = ensureEmptyRow(table);
  if (emptyRow instanceof HTMLTableRowElement) {
    emptyRow.hidden = visibleCount > 0;
  }
}
function applyTableState(root, table) {
  sortTableRows(table);
  filterTableRows(root, table);
  updateSortIndicators(table);
}
function handleSortButtonClick(root, button) {
  const table = button.closest("[data-table-tools-table]");
  if (!(table instanceof HTMLTableElement)) {
    return;
  }
  const requestedIndex = button.dataset.tableSortIndex || "";
  const currentIndex = table.dataset.tableSortIndex || "";
  const currentDirection = table.dataset.tableSortDirection === "desc" ? "desc" : "asc";
  const nextDirection = currentIndex === requestedIndex && currentDirection === "asc" ? "desc" : "asc";
  table.dataset.tableSortIndex = requestedIndex;
  table.dataset.tableSortDirection = nextDirection;
  applyTableState(root, table);
}
function initTable(root) {
  if (!(root instanceof HTMLElement) || root.dataset.tableToolsReady === "true") {
    return;
  }
  root.dataset.tableToolsReady = "true";
  const tables = Array.from(root.querySelectorAll("[data-table-tools-table]")).filter((table) => table instanceof HTMLTableElement);
  if (tables.length === 0) {
    return;
  }
  const searchInput = root.querySelector("[data-table-tools-search-input]");
  if (searchInput instanceof HTMLInputElement) {
    searchInput.addEventListener("input", () => {
      tables.forEach((table) => applyTableState(root, table));
    });
  }
  root.addEventListener("click", (event) => {
    const target = event.target instanceof Element ? event.target.closest("[data-table-sort-button]") : null;
    if (!(target instanceof HTMLButtonElement)) {
      return;
    }
    event.preventDefault();
    handleSortButtonClick(root, target);
  });
  tables.forEach((table) => {
    const defaultButton = table.querySelector("[data-table-sort-button][data-table-sort-default='true']");
    if (defaultButton instanceof HTMLButtonElement) {
      table.dataset.tableSortIndex = defaultButton.dataset.tableSortIndex || "";
      table.dataset.tableSortDirection = defaultButton.dataset.tableSortDefaultDirection === "desc" ? "desc" : "asc";
    }
    applyTableState(root, table);
  });
}
function initTableTools(scope = document) {
  const roots = scope instanceof HTMLElement ? scope.matches("[data-table-tools-root]") ? [scope] : Array.from(scope.querySelectorAll("[data-table-tools-root]")) : Array.from(document.querySelectorAll("[data-table-tools-root]"));
  roots.forEach((root) => {
    if (root instanceof HTMLElement) {
      initTable(root);
    }
  });
}

// PmTracker.Web/wwwroot/js/modules/projectTabs.js
var projectTabStorageKey = "pmtracker.tab.active";
var projectRecordFilterPanelStorageKey2 = "pmtracker.filters.open";

class ProjectNavigationController {
  constructor(options = {}) {
    this.options = options;
  }
  normalizeTabName(tabName) {
    return tabName === "gant" ? "harmonogram" : tabName;
  }
  setActiveTab(tabName) {
    const normalizedTabName = this.normalizeTabName(tabName);
    if (!normalizedTabName) {
      return;
    }
    const tabs = document.querySelectorAll(".tab");
    const panels = document.querySelectorAll(".tab-panel");
    let activePanel = null;
    tabs.forEach((item) => {
      item.classList.toggle("active", item.getAttribute("data-tab") === normalizedTabName);
    });
    panels.forEach((panel) => {
      const isActive = panel.getAttribute("data-tab-panel") === normalizedTabName;
      panel.classList.toggle("active", isActive);
      if (isActive) {
        activePanel = panel;
      }
    });
    localStorage.setItem(projectTabStorageKey, normalizedTabName);
    if (activePanel instanceof HTMLElement) {
      if (normalizedTabName === "harmonogram") {
        this.options.renderStaticTimelineAxes?.(activePanel);
      } else if (normalizedTabName === "jednani") {
        initMeetingOverview(activePanel);
      }
      this.options.queueRainbowSegmentRender?.(activePanel);
    }
    this.options.scheduleSubsystemIndicatorSync?.();
  }
  syncTabQuery(tabName) {
    const normalizedTabName = this.normalizeTabName(tabName);
    if (!normalizedTabName) {
      return;
    }
    const url = new URL(window.location.href);
    url.searchParams.set("tab", normalizedTabName);
    history.replaceState(history.state, "", `${url.pathname}${url.search}${url.hash}`);
  }
  async ensureTabLoaded(tabName, options = {}) {
    return ensureProjectTabLoaded(this.normalizeTabName(tabName), options);
  }
  initTabs() {
    const tabs = document.querySelectorAll(".tab");
    if (tabs.length === 0) {
      return;
    }
    const availableTabs = new Set(Array.from(tabs).map((tab) => tab.getAttribute("data-tab")));
    const urlTab = this.normalizeTabName(new URL(window.location.href).searchParams.get("tab"));
    const defaultTab = tabs[0].getAttribute("data-tab");
    const tabToActivate = urlTab && availableTabs.has(urlTab) ? urlTab : defaultTab;
    this.setActiveTab(tabToActivate);
    tabs.forEach((tab) => {
      if (!(tab instanceof HTMLElement) || tab instanceof HTMLAnchorElement || tab.dataset.tabReady === "true") {
        return;
      }
      tab.dataset.tabReady = "true";
      tab.addEventListener("click", async (event) => {
        event.preventDefault();
        const requestedTab = tab.getAttribute("data-tab");
        this.setActiveTab(requestedTab);
        this.syncTabQuery(requestedTab);
        try {
          const loaded = await this.ensureTabLoaded(requestedTab);
          if (!loaded && tab instanceof HTMLAnchorElement && tab.href) {
            window.location.assign(tab.href);
          }
        } catch {
          if (tab instanceof HTMLAnchorElement && tab.href) {
            window.location.assign(tab.href);
          }
        }
      });
    });
  }
  initRecordsUi(options = {}) {
    const filterPanel = document.querySelector("[data-filter-panel]");
    if (filterPanel instanceof HTMLElement) {
      this.options.setFilterPanelOpen?.(localStorage.getItem(projectRecordFilterPanelStorageKey2) === "true");
    }
    const preserveServerView = options && options.preserveServerView === true;
    this.options.restoreFilterState?.();
    const recordsPanel = document.querySelector('[data-tab-panel="zaznamy"]');
    const groupBySubsystemInput = document.querySelector('[data-project-filter-scope="records"] [data-filter-key="groupBySubsystem"]');
    const showGroupedView = groupBySubsystemInput instanceof HTMLInputElement ? groupBySubsystemInput.checked : true;
    const hasServerRenderedGroups = recordsPanel instanceof HTMLElement && recordsPanel.querySelector("[data-record-grouped-list] [data-subsystem-group]") instanceof HTMLElement;
    this.options.setProjectFilterSaveStatus?.("records", "");
    if (preserveServerView && recordsPanel instanceof HTMLElement) {
      const groupedShell = recordsPanel.querySelector('[data-records-view="subsystem"]');
      const flatShell = recordsPanel.querySelector('[data-records-view="flat"]');
      const syncServerView = () => {
        if (groupedShell instanceof HTMLElement) {
          groupedShell.hidden = false;
        }
        if (flatShell instanceof HTMLElement) {
          flatShell.hidden = true;
        }
      };
      syncServerView();
      window.requestAnimationFrame(syncServerView);
    } else if (showGroupedView && hasServerRenderedGroups && recordsPanel instanceof HTMLElement) {
      const groupedShell = recordsPanel.querySelector('[data-records-view="subsystem"]');
      const flatShell = recordsPanel.querySelector('[data-records-view="flat"]');
      if (groupedShell instanceof HTMLElement) {
        groupedShell.hidden = false;
      }
      if (flatShell instanceof HTMLElement) {
        flatShell.hidden = true;
      }
    } else {
      this.options.applyRecordsView?.(showGroupedView ? "subsystem" : "flat");
    }
    this.options.initSubsystemScrollIndicator?.();
  }
}
var projectNavigationController = new ProjectNavigationController({
  renderStaticTimelineAxes,
  queueRainbowSegmentRender,
  scheduleSubsystemIndicatorSync,
  setFilterPanelOpen,
  restoreFilterState,
  setProjectFilterSaveStatus,
  applyRecordsView,
  initSubsystemScrollIndicator
});
function replaceProjectTabPanelFromHtml(tabKey, html, loadUrl) {
  const nextDoc = parseHtmlFragment(html);
  const replacement = nextDoc.querySelector(`[data-tab-panel="${CSS.escape(tabKey)}"]`);
  const current = document.querySelector(`[data-tab-panel="${CSS.escape(tabKey)}"]`);
  if (!(replacement instanceof HTMLElement) || !(current instanceof HTMLElement)) {
    throw new Error(`Nepodařilo se načíst panel ${tabKey}.`);
  }
  if (current.classList.contains("active")) {
    replacement.classList.add("active");
  }
  replacement.dataset.projectTabLoaded = "true";
  if (loadUrl) {
    replacement.dataset.projectTabLazyUrl = loadUrl;
  }
  current.replaceWith(replacement);
  return document.querySelector(`[data-tab-panel="${CSS.escape(tabKey)}"]`);
}
function setActiveTab(tabName) {
  projectNavigationController.setActiveTab(tabName);
}
function syncTabQuery(tabName) {
  projectNavigationController.syncTabQuery(tabName);
}
function initProjectTabs() {
  projectNavigationController.initTabs();
}
function initProjectRecordsUi(options = {}) {
  projectNavigationController.initRecordsUi(options);
  initProjectRecordDeepLink(document);
}
async function loadProjectTabPanel(tabNameOrPanel, options = {}) {
  const panel = resolveProjectTabPanel(tabNameOrPanel);
  if (!(panel instanceof HTMLElement)) {
    return false;
  }
  const tabKey = (panel.dataset.tabPanel || "").trim();
  const loadUrl = typeof options.url === "string" && options.url ? options.url : (panel.dataset.projectTabLazyUrl || "").trim();
  if (!tabKey || !loadUrl) {
    return false;
  }
  const forceReload = options.force === true;
  if (panel.dataset.projectTabLoaded === "true" && !forceReload) {
    return true;
  }
  const placeholder = panel.querySelector("[data-project-tab-placeholder]");
  const errorContainer = resolveOrCreateErrorContainer(panel, "data-project-tab-error");
  setLazyLoadingState(panel, placeholder, errorContainer, true);
  try {
    const html = await fetchHtmlFragment(loadUrl);
    const currentPanel = replaceProjectTabPanelFromHtml(tabKey, html, loadUrl);
    if (currentPanel instanceof HTMLElement) {
      navigationRuntime.initRecordFormEnhancements?.(currentPanel);
      initCommentSortUi(currentPanel);
      if (tabKey === "zaznamy") {
        initProjectRecordsUi();
        initProjectRecordDeepLink(currentPanel);
      } else if (tabKey === "harmonogram") {
        initProjectScheduleUi();
        renderStaticTimelineAxes(currentPanel);
      }
      initTableTools(currentPanel);
      initMeetingOverview(currentPanel);
      queueRainbowSegmentRender(currentPanel);
      scheduleSubsystemIndicatorSync();
    }
    return true;
  } catch (error) {
    setLazyLoadingState(panel, placeholder, errorContainer, false);
    renderLazyLoadError(errorContainer, "Nepodařilo se načíst obsah záložky.", "data-project-tab-retry");
    return false;
  }
}
async function ensureProjectTabLoaded(tabName, options = {}) {
  const normalizedTabName = tabName === "gant" ? "harmonogram" : tabName;
  if (!normalizedTabName || normalizedTabName === "zaznamy") {
    return true;
  }
  return loadProjectTabPanel(normalizedTabName, options);
}
// PmTracker.Web/wwwroot/js/modules/recordRefresh.js
function invalidateRecordMeetingCommentStateCacheForPayload(payload) {
  const projectId = payload?.projectId != null ? String(payload.projectId) : "";
  if (!projectId) {
    return;
  }
  invalidateRecordMeetingCommentStates(projectId);
}
function readRecordCommentsReloadState(card) {
  if (!(card instanceof HTMLElement)) {
    return {
      loadedCount: 0,
      loadAll: false,
      sortDirection: "asc"
    };
  }
  const section = card.querySelector("[data-comment-sort-section]");
  const panelState = readRecordCommentsPanelState(section instanceof HTMLElement ? section : card);
  return {
    loadedCount: panelState?.loadedCount ?? 0,
    loadAll: panelState?.isFullyLoaded === true && (panelState?.totalCount ?? 0) > 0,
    sortDirection: getCommentSortDirection(section instanceof HTMLElement ? section : card)
  };
}
function buildRecordUiState(scopeRoot) {
  const root = scopeRoot instanceof HTMLElement ? scopeRoot : document;
  const expandedRecordIds = Array.from(root.querySelectorAll(".record-card[data-record-id]")).filter((card) => card instanceof HTMLElement && !isElementInHiddenTree(card) && !card.classList.contains("collapsed")).map((card) => card.getAttribute("data-record-id") || "").filter(Boolean);
  const loadedCommentRecordIds = Array.from(root.querySelectorAll(".record-card[data-record-id]")).filter((card) => card instanceof HTMLElement && !isElementInHiddenTree(card) && (card.dataset.recordCommentsLoaded === "true" || card.querySelector('[data-record-comments-shell][data-record-comments-loaded="true"]') instanceof HTMLElement)).map((card) => card.getAttribute("data-record-id") || "").filter(Boolean);
  const commentSortDirectionByRecordId = {};
  const commentLoadStateByRecordId = {};
  root.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
    if (!(card instanceof HTMLElement)) {
      return;
    }
    if (isElementInHiddenTree(card)) {
      return;
    }
    const recordId = card.getAttribute("data-record-id");
    if (!recordId) {
      return;
    }
    const section = card.querySelector("[data-comment-sort-section]");
    if (!(section instanceof HTMLElement)) {
      return;
    }
    const commentsState = readRecordCommentsReloadState(card);
    commentSortDirectionByRecordId[recordId] = commentsState.sortDirection;
    commentLoadStateByRecordId[recordId] = {
      loadedCount: commentsState.loadedCount,
      loadAll: commentsState.loadAll
    };
  });
  return {
    activeTab: localStorage.getItem("pmtracker.tab.active") || "zaznamy",
    scrollY: window.scrollY,
    expandedRecordIds,
    loadedCommentRecordIds,
    commentSortDirectionByRecordId,
    commentLoadStateByRecordId
  };
}
async function restoreRecordUiState(state) {
  if (!state || typeof state !== "object") {
    return;
  }
  if (typeof state.activeTab === "string" && state.activeTab) {
    setActiveTab(state.activeTab);
    syncTabQuery(state.activeTab);
  }
  const expandedSet = new Set(Array.isArray(state.expandedRecordIds) ? state.expandedRecordIds : []);
  const loadedCommentSet = new Set(Array.isArray(state.loadedCommentRecordIds) ? state.loadedCommentRecordIds : []);
  document.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
    if (!(card instanceof HTMLElement)) {
      return;
    }
    const recordId = card.getAttribute("data-record-id") || "";
    const shouldExpand = expandedSet.has(recordId);
    card.classList.toggle("collapsed", !shouldExpand);
    const header = card.querySelector("[data-record-toggle]");
    if (header instanceof HTMLElement) {
      header.setAttribute("aria-expanded", String(shouldExpand));
    }
  });
  const directionMap = state.commentSortDirectionByRecordId && typeof state.commentSortDirectionByRecordId === "object" ? state.commentSortDirectionByRecordId : {};
  const loadStateMap = state.commentLoadStateByRecordId && typeof state.commentLoadStateByRecordId === "object" ? state.commentLoadStateByRecordId : {};
  const restoreTasks = [];
  document.querySelectorAll(".record-card[data-record-id]").forEach((card) => {
    if (!(card instanceof HTMLElement)) {
      return;
    }
    const recordId = card.getAttribute("data-record-id") || "";
    if (!recordId || !expandedSet.has(recordId)) {
      return;
    }
    restoreTasks.push((async () => {
      await loadRecordDetail(card);
      if (loadedCommentSet.has(recordId)) {
        const loadState = loadStateMap[recordId] || {};
        await loadRecordComments(card, {
          sortDirection: directionMap[recordId],
          limit: loadState.loadedCount,
          loadAll: loadState.loadAll === true
        });
      } else if (directionMap[recordId] === "asc" || directionMap[recordId] === "desc") {
        applyRecordCommentSortDirection(card, directionMap[recordId]);
      }
    })());
  });
  await Promise.all(restoreTasks);
  if (typeof state.scrollY === "number" && Number.isFinite(state.scrollY)) {
    window.scrollTo({ top: state.scrollY, behavior: "auto" });
  }
}
async function refreshRecordCard(payload) {
  const refreshUrl = typeof payload.refreshUrl === "string" ? payload.refreshUrl : "";
  const recordId = payload.recordId != null ? String(payload.recordId) : "";
  if (!refreshUrl || !recordId) {
    return;
  }
  const currentCards = Array.from(document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"]`)).filter((card) => card instanceof HTMLElement);
  if (currentCards.length === 0) {
    return;
  }
  const anchorCurrentCard = currentCards.find((card) => !isElementInHiddenTree(card)) ?? currentCards[0];
  const beforeTop = anchorCurrentCard.getBoundingClientRect().top;
  const wasCollapsed = anchorCurrentCard.classList.contains("collapsed");
  const detailWasLoaded = anchorCurrentCard.dataset.recordDetailLoaded === "true";
  const commentsWereLoaded = anchorCurrentCard.dataset.recordCommentsLoaded === "true";
  const previousCommentState = readRecordCommentsReloadState(anchorCurrentCard);
  const html = await fetchHtmlFragment(refreshUrl);
  const parsed = new DOMParser().parseFromString(html, "text/html");
  const replacementCard = parsed.querySelector(".record-card[data-record-id]");
  if (!(replacementCard instanceof HTMLElement)) {
    throw new Error("Nepodařilo se načíst aktualizovanou kartu záznamu.");
  }
  currentCards.forEach((card, index) => {
    const nextCard = index === 0 ? replacementCard : replacementCard.cloneNode(true);
    card.replaceWith(nextCard);
  });
  const refreshedCards = Array.from(document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"]`)).filter((card) => card instanceof HTMLElement);
  refreshedCards.forEach((card) => {
    card.classList.toggle("collapsed", wasCollapsed);
    const header = card.querySelector("[data-record-toggle]");
    if (header instanceof HTMLElement) {
      header.setAttribute("aria-expanded", String(!wasCollapsed));
    }
    navigationRuntime.initRecordFormEnhancements?.(card);
  });
  const hydrateTasks = refreshedCards.filter((card) => card instanceof HTMLElement && !card.classList.contains("collapsed")).map(async (card) => {
    await loadRecordDetail(card, { force: detailWasLoaded });
    if (commentsWereLoaded) {
      await loadRecordComments(card, {
        sortDirection: previousCommentState.sortDirection,
        limit: previousCommentState.loadedCount,
        loadAll: previousCommentState.loadAll
      });
    }
  });
  await Promise.all(hydrateTasks);
  const anchorRefreshedCard = refreshedCards.find((card) => !isElementInHiddenTree(card)) ?? refreshedCards[0];
  if (anchorRefreshedCard instanceof HTMLElement) {
    const afterTop = anchorRefreshedCard.getBoundingClientRect().top;
    const delta = afterTop - beforeTop;
    if (Math.abs(delta) > 1) {
      window.scrollBy({ top: delta, behavior: "auto" });
    }
  }
}
async function refreshRecordComments(payload) {
  const refreshUrl = typeof payload.refreshUrl === "string" ? payload.refreshUrl : "";
  const recordId = payload.recordId != null ? String(payload.recordId) : "";
  if (!refreshUrl || !recordId) {
    return;
  }
  const currentCards = Array.from(document.querySelectorAll(`.record-card[data-record-id="${CSS.escape(recordId)}"]`)).filter((card) => card instanceof HTMLElement);
  if (currentCards.length === 0) {
    return;
  }
  await Promise.all(currentCards.map((card) => {
    const commentsState = readRecordCommentsReloadState(card);
    return loadRecordComments(card, {
      force: true,
      url: refreshUrl,
      sortDirection: commentsState.sortDirection,
      limit: commentsState.loadedCount,
      loadAll: commentsState.loadAll
    });
  }));
}
async function refreshMeetingTaskItem(payload) {
  const refreshUrl = typeof payload.refreshUrl === "string" ? payload.refreshUrl : "";
  const recordId = payload.recordId != null ? String(payload.recordId) : "";
  if (!refreshUrl || !recordId) {
    return;
  }
  const currentTask = document.querySelector(`.task-item[data-task-record-id="${CSS.escape(recordId)}"]`);
  if (!(currentTask instanceof HTMLElement)) {
    return;
  }
  const beforeTop = currentTask.getBoundingClientRect().top;
  const previousSortDirection = currentTask.getAttribute("data-comment-sort-direction") === "desc" ? "desc" : "asc";
  const html = await fetchHtmlFragment(refreshUrl);
  const parsed = new DOMParser().parseFromString(html, "text/html");
  const replacementTask = parsed.querySelector(".task-item[data-task-record-id]");
  if (!(replacementTask instanceof HTMLElement)) {
    throw new Error("Nepodařilo se načíst aktualizovaný blok úkolu.");
  }
  currentTask.replaceWith(replacementTask);
  navigationRuntime.initRecordFormEnhancements?.(replacementTask);
  const refreshedTask = document.querySelector(`.task-item[data-task-record-id="${CSS.escape(recordId)}"]`);
  if (refreshedTask instanceof HTMLElement) {
    applyRecordCommentSortDirection(refreshedTask, previousSortDirection);
    const afterTop = refreshedTask.getBoundingClientRect().top;
    const delta = afterTop - beforeTop;
    if (Math.abs(delta) > 1) {
      window.scrollBy({ top: delta, behavior: "auto" });
    }
  }
}
function replaceSelectorFromDocument(nextDoc, selector) {
  const current = document.querySelector(selector);
  const replacement = nextDoc.querySelector(selector);
  if (!(current instanceof HTMLElement) || !(replacement instanceof HTMLElement)) {
    return false;
  }
  current.replaceWith(replacement);
  return true;
}
async function refreshProjectSchedulePanels() {
  const panel = document.querySelector('[data-tab-panel="harmonogram"]');
  if (!(panel instanceof HTMLElement) || panel.dataset.projectTabLoaded !== "true") {
    return;
  }
  await loadProjectTabPanel(panel, { force: true });
}
async function refreshPageScope(payload) {
  if (!payload || typeof payload !== "object") {
    return;
  }
  closeAllFloatingPanels();
  const scope = typeof payload.refreshScope === "string" ? payload.refreshScope : "";
  const refreshUrl = typeof payload.refreshUrl === "string" && payload.refreshUrl ? payload.refreshUrl : window.location.href;
  if (!scope) {
    return;
  }
  if (scope === "page") {
    const pageEditorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
    if (pageEditorForm instanceof HTMLFormElement) {
      navigationRuntime.prepareRecordEditorFormNavigation?.(pageEditorForm);
    }
    window.location.assign(refreshUrl);
    return;
  }
  if (scope === "record-card") {
    invalidateRecordMeetingCommentStateCacheForPayload(payload);
    await refreshRecordCard(payload);
    initProjectRecordsUi();
    initProjectScheduleUi();
    initCommentSortUi(document);
    return;
  }
  if (scope === "record-comments") {
    invalidateRecordMeetingCommentStateCacheForPayload(payload);
    await refreshRecordComments(payload);
    initCommentSortUi(document);
    return;
  }
  if (scope === "record-card-with-schedules") {
    invalidateRecordMeetingCommentStateCacheForPayload(payload);
    const activeTab = localStorage.getItem("pmtracker.tab.active") || "zaznamy";
    const scrollY = window.scrollY;
    await refreshRecordCard(payload);
    await refreshProjectSchedulePanels();
    initProjectTabs();
    initProjectRecordsUi();
    initProjectScheduleUi();
    initCommentSortUi(document);
    setActiveTab(activeTab);
    syncTabQuery(activeTab);
    window.scrollTo({ top: scrollY, behavior: "auto" });
    return;
  }
  if (scope === "meeting-task-item") {
    await refreshMeetingTaskItem(payload);
    initCommentSortUi(document);
    return;
  }
  if (scope === "nastaveni-panel") {
    const shell = document.querySelector("[data-settings-shell]");
    const panel = shell?.querySelector("[data-settings-panel]");
    if (!(shell instanceof HTMLElement) || !(panel instanceof HTMLElement)) {
      return;
    }
    const endpointUrl = new URL(refreshUrl, window.location.origin);
    const section = endpointUrl.searchParams.get("section") || "role";
    const userId = endpointUrl.searchParams.get("userId");
    const projektId = endpointUrl.searchParams.get("projektId");
    panel.setAttribute("aria-busy", "true");
    try {
      panel.innerHTML = await fetchHtmlFragment(`${endpointUrl.pathname}${endpointUrl.search}`);
      navigationRuntime.initRecordFormEnhancements?.(panel);
      shell.querySelectorAll("[data-settings-link]").forEach((link) => {
        if (!(link instanceof HTMLAnchorElement)) {
          return;
        }
        const key = link.dataset.key || "role";
        const linkUrl = new URL(link.href, window.location.origin);
        linkUrl.searchParams.set("section", key);
        if (userId) {
          linkUrl.searchParams.set("userId", userId);
        } else {
          linkUrl.searchParams.delete("userId");
        }
        if (projektId) {
          linkUrl.searchParams.set("projektId", projektId);
        } else {
          linkUrl.searchParams.delete("projektId");
        }
        link.href = `${linkUrl.pathname}${linkUrl.search}${linkUrl.hash}`;
        const active = key === section;
        link.classList.toggle("active", active);
        if (active) {
          link.setAttribute("aria-current", "page");
        } else {
          link.removeAttribute("aria-current");
        }
      });
      const nextUrl = new URL(window.location.href);
      nextUrl.searchParams.set("section", section);
      if (userId) {
        nextUrl.searchParams.set("userId", userId);
      } else {
        nextUrl.searchParams.delete("userId");
      }
      if (projektId) {
        nextUrl.searchParams.set("projektId", projektId);
      } else {
        nextUrl.searchParams.delete("projektId");
      }
      history.replaceState({
        ...history.state || {},
        settingsSection: section,
        settingsUserId: userId,
        settingsProjektId: projektId
      }, "", `${nextUrl.pathname}${nextUrl.search}${nextUrl.hash}`);
    } finally {
      panel.setAttribute("aria-busy", "false");
    }
    return;
  }
  if (scope === "ciselniky-detail") {
    const panel = document.querySelector("[data-ciselnik-panel]");
    if (!(panel instanceof HTMLElement)) {
      return;
    }
    panel.innerHTML = await fetchHtmlFragment(refreshUrl);
    navigationRuntime.initRecordFormEnhancements?.(panel);
    return;
  }
  switch (scope) {
    case "projekty-index": {
      const nextDoc = await fetchHtmlDocument(refreshUrl);
      replaceSelectorFromDocument(nextDoc, "[data-project-list-shell]");
      break;
    }
    case "osoby-index": {
      const nextDoc = await fetchHtmlDocument(refreshUrl);
      replaceSelectorFromDocument(nextDoc, "[data-osoby-table-card]");
      break;
    }
    case "projekty-detail-zaznamy":
    case "projekty-detail-jednani":
    case "projekty-detail-tym":
    case "projekty-detail-navrhy":
    case "projekty-detail-zaznamy-preserve": {
      const preserveRecordUi = scope === "projekty-detail-zaznamy-preserve";
      const recordUiState = preserveRecordUi ? buildRecordUiState(document) : null;
      const tab = typeof payload.tab === "string" && payload.tab ? payload.tab : scope === "projekty-detail-zaznamy" ? "zaznamy" : scope === "projekty-detail-jednani" ? "jednani" : scope === "projekty-detail-navrhy" ? "navrhy" : "tym";
      const targetTabPanelKey = tab === "harmonogram" || tab === "gant" ? "harmonogram" : tab;
      const html = await fetchHtmlFragment(refreshUrl);
      const nextDoc = parseHtmlFragment(html);
      const selector = `[data-tab-panel="${targetTabPanelKey}"]`;
      const replacement = nextDoc.querySelector(selector);
      const current = document.querySelector(selector);
      if (!(replacement instanceof HTMLElement) || !(current instanceof HTMLElement)) {
        throw new Error("Nepodařilo se obnovit detail projektu.");
      }
      current.replaceWith(replacement);
      const refreshedPanel = document.querySelector(`[data-tab-panel="${targetTabPanelKey}"]`);
      if (refreshedPanel instanceof HTMLElement) {
        if (targetTabPanelKey !== "zaznamy") {
          refreshedPanel.dataset.projectTabLoaded = "true";
          if (typeof refreshUrl === "string" && refreshUrl) {
            refreshedPanel.dataset.projectTabLazyUrl = refreshUrl;
          }
        }
        navigationRuntime.initRecordFormEnhancements?.(refreshedPanel);
      }
      setActiveTab(tab);
      syncTabQuery(tab);
      initProjectTabs();
      initProjectRecordsUi();
      initProjectScheduleUi();
      initMeetingOverview(document);
      initCommentSortUi(document);
      if (preserveRecordUi) {
        await restoreRecordUiState(recordUiState);
      }
      break;
    }
    default:
      break;
  }
}
function initProjectRecordPageshowSync() {
  if (!(document.body instanceof HTMLElement) || document.body.dataset.projectRecordPageshowSyncReady === "true") {
    return;
  }
  const hasProjectRecordsPanel = document.querySelector('[data-tab-panel="zaznamy"]') instanceof HTMLElement;
  if (!hasProjectRecordsPanel) {
    return;
  }
  document.body.dataset.projectRecordPageshowSyncReady = "true";
  window.addEventListener("pageshow", async (event) => {
    if (!event.persisted) {
      return;
    }
    try {
      const recordsPanel = document.querySelector('[data-tab-panel="zaznamy"]');
      const refreshUrl = recordsPanel instanceof HTMLElement ? recordsPanel.dataset.projectTabRefreshUrl || window.location.href : window.location.href;
      await refreshPageScope({
        refreshScope: "projekty-detail-zaznamy-preserve",
        refreshUrl,
        tab: "zaznamy"
      });
    } catch {
      window.location.reload();
    }
  });
}
// PmTracker.Web/wwwroot/js/modules/pageSwitchers.js
function readBooleanStorageDefaultTrue(key) {
  const rawValue = window.localStorage.getItem(key);
  if (rawValue === null) {
    return true;
  }
  return rawValue === "true";
}
function writeBooleanStorage(key, value) {
  window.localStorage.setItem(key, value ? "true" : "false");
}
function resolveQueryRoot(scope) {
  return scope && typeof scope.querySelector === "function" ? scope : document;
}
function readProjectListStatusFilterState(doneKey, deletedKey) {
  return {
    hideDone: readBooleanStorageDefaultTrue(doneKey),
    hideDeleted: readBooleanStorageDefaultTrue(deletedKey)
  };
}
function syncProjectListStatusFilterInputs(root, doneKey, deletedKey) {
  const state = readProjectListStatusFilterState(doneKey, deletedKey);
  root.querySelectorAll("[data-project-status-hide]").forEach((input) => {
    if (!(input instanceof HTMLInputElement)) {
      return;
    }
    const statusCode = (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase();
    if (statusCode === "DONE") {
      input.checked = state.hideDone;
    } else if (statusCode === "DELETED") {
      input.checked = state.hideDeleted;
    }
  });
}
function isPrimaryNavigationClick(event) {
  return !(event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0);
}
function setActiveLink(container, selector, activeKey) {
  container.querySelectorAll(selector).forEach((link) => {
    if (!(link instanceof HTMLAnchorElement)) {
      return;
    }
    const isActive = link.dataset.key === activeKey;
    link.classList.toggle("active", isActive);
    if (isActive) {
      link.setAttribute("aria-current", "page");
    } else {
      link.removeAttribute("aria-current");
    }
  });
}
async function fetchPanelHtml(endpoint) {
  return fetchHtmlFragment(endpoint);
}
function applyProjectIndexFilters(scope, options = {}) {
  const root = resolveQueryRoot(scope);
  const shell = root.querySelector("[data-project-list-shell]");
  if (!(shell instanceof HTMLElement)) {
    return;
  }
  syncProjectListStatusFilterInputs(root, options.hideDoneStorageKey, options.hideDeletedStorageKey);
  const hiddenStatusCodes = Array.from(root.querySelectorAll("[data-project-status-hide]")).filter((input) => input instanceof HTMLInputElement && input.checked).map((input) => (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase()).filter(Boolean);
  shell.querySelectorAll("[data-project-list-row]").forEach((row) => {
    if (!(row instanceof HTMLElement)) {
      return;
    }
    const statusCode = (row.getAttribute("data-project-status") || "").trim().toUpperCase();
    row.hidden = hiddenStatusCodes.includes(statusCode);
  });
}
function toggleProjectStatusFilterPanel(button) {
  if (!(button instanceof HTMLButtonElement)) {
    return;
  }
  const panel = document.querySelector("[data-project-status-filter-panel]");
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  const shouldOpen = panel.hidden;
  panel.hidden = !shouldOpen;
  button.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
}
function handleProjectStatusFilterInput(input, options = {}) {
  if (!(input instanceof HTMLInputElement)) {
    return;
  }
  const statusCode = (input.getAttribute("data-project-status-hide") || "").trim().toUpperCase();
  if (statusCode === "DONE") {
    writeBooleanStorage(options.hideDoneStorageKey, input.checked);
  } else if (statusCode === "DELETED") {
    writeBooleanStorage(options.hideDeletedStorageKey, input.checked);
  }
  applyProjectIndexFilters(document, options);
}
function initProjectIndexStatusFilters(scope, options = {}) {
  const root = resolveQueryRoot(scope);
  const shell = root.querySelector("[data-project-list-shell]");
  if (!(shell instanceof HTMLElement)) {
    return;
  }
  const toggleButton = document.querySelector("[data-project-status-filter-toggle]");
  if (toggleButton instanceof HTMLButtonElement && toggleButton.dataset.boundProjectStatusFilter !== "true") {
    toggleButton.dataset.boundProjectStatusFilter = "true";
    toggleButton.addEventListener("click", () => {
      toggleProjectStatusFilterPanel(toggleButton);
    });
  }
  document.querySelectorAll("[data-project-status-hide]").forEach((input) => {
    if (input instanceof HTMLInputElement && input.dataset.boundProjectStatusFilter !== "true") {
      input.dataset.boundProjectStatusFilter = "true";
      input.addEventListener("change", () => {
        handleProjectStatusFilterInput(input, options);
      });
    }
  });
  syncProjectListStatusFilterInputs(document, options.hideDoneStorageKey, options.hideDeletedStorageKey);
  applyProjectIndexFilters(root, options);
}
function toggleMeetingAttendancePanel(button) {
  if (!(button instanceof HTMLButtonElement)) {
    return;
  }
  const card = button.closest("[data-meeting-attendance-card]");
  if (!(card instanceof HTMLElement)) {
    return;
  }
  const panel = card.querySelector("[data-meeting-attendance-panel]");
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  const shouldOpen = panel.hidden;
  panel.hidden = !shouldOpen;
  button.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
  button.textContent = shouldOpen ? "Skrýt účast" : "Zobrazit účast";
}
function initCiselnikAjaxSwitch(options = {}) {
  const shell = document.querySelector("[data-ciselnik-shell]");
  if (!(shell instanceof HTMLElement)) {
    return;
  }
  const panel = shell.querySelector("[data-ciselnik-panel]");
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  const contentUrl = shell.getAttribute("data-content-url");
  if (!contentUrl) {
    return;
  }
  const initRecordFormEnhancements = typeof options.initRecordFormEnhancements === "function" ? options.initRecordFormEnhancements : () => {};
  const getCurrentKeyFromUrl = () => new URL(window.location.href).searchParams.get("id");
  const loadDetail = async (key, push, href) => {
    if (!key) {
      return;
    }
    panel.setAttribute("aria-busy", "true");
    try {
      const endpoint = `${contentUrl}?id=${encodeURIComponent(key)}`;
      const html = await fetchPanelHtml(endpoint);
      panel.innerHTML = html;
      initRecordFormEnhancements(panel);
      setActiveLink(shell, "[data-ciselnik-link]", key);
      const nextState = { ...history.state || {}, ciselnikKey: key };
      if (push && href) {
        history.pushState(nextState, "", href);
      } else if (!push) {
        history.replaceState(nextState, "", window.location.href);
      }
    } catch (error) {
      if (href) {
        window.location.href = href;
      }
    } finally {
      panel.setAttribute("aria-busy", "false");
    }
  };
  const initialActiveLink = shell.querySelector("[data-ciselnik-link].active");
  const initialKey = initialActiveLink instanceof HTMLAnchorElement ? initialActiveLink.dataset.key : getCurrentKeyFromUrl();
  if (initialKey) {
    history.replaceState({ ...history.state || {}, ciselnikKey: initialKey }, "", window.location.href);
    setActiveLink(shell, "[data-ciselnik-link]", initialKey);
  }
  shell.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }
    const link = target.closest("[data-ciselnik-link]");
    if (!(link instanceof HTMLAnchorElement) || !isPrimaryNavigationClick(event)) {
      return;
    }
    event.preventDefault();
    loadDetail(link.dataset.key, true, link.href);
  });
  window.addEventListener("popstate", (event) => {
    const key = event.state?.ciselnikKey || getCurrentKeyFromUrl();
    const link = shell.querySelector(`[data-ciselnik-link][data-key="${key}"]`);
    const href = link instanceof HTMLAnchorElement ? link.href : null;
    if (key) {
      loadDetail(key, false, href);
    }
  });
}
function initSettingsAjaxSwitch() {
  const shell = document.querySelector("[data-settings-shell]");
  if (!(shell instanceof HTMLElement)) {
    return;
  }
  const panel = shell.querySelector("[data-settings-panel]");
  if (!(panel instanceof HTMLElement)) {
    return;
  }
  const panelUrl = shell.getAttribute("data-panel-url");
  if (!panelUrl) {
    return;
  }
  const getCurrentStateFromUrl = () => {
    const url = new URL(window.location.href);
    return {
      section: url.searchParams.get("section") || "role",
      userId: url.searchParams.get("userId"),
      projektId: url.searchParams.get("projektId")
    };
  };
  const buildHref = (section, userId, projektId) => {
    const url = new URL(window.location.href);
    url.searchParams.set("section", section);
    if (userId) {
      url.searchParams.set("userId", userId);
    } else {
      url.searchParams.delete("userId");
    }
    if (projektId) {
      url.searchParams.set("projektId", projektId);
    } else {
      url.searchParams.delete("projektId");
    }
    return `${url.pathname}${url.search}${url.hash}`;
  };
  const loadSection = async (section, push, options = {}) => {
    if (!section) {
      return;
    }
    const userId = options.userId ?? null;
    const projektId = options.projektId ?? null;
    const fallbackHref = options.href ?? null;
    panel.setAttribute("aria-busy", "true");
    try {
      const endpointUrl = new URL(panelUrl, window.location.origin);
      endpointUrl.searchParams.set("section", section);
      if (userId) {
        endpointUrl.searchParams.set("userId", userId);
      }
      if (projektId) {
        endpointUrl.searchParams.set("projektId", projektId);
      }
      const html = await fetchPanelHtml(`${endpointUrl.pathname}${endpointUrl.search}`);
      panel.innerHTML = html;
      setActiveLink(shell, "[data-settings-link]", section);
      const nextHref = buildHref(section, userId, projektId);
      const nextState = {
        ...history.state || {},
        settingsSection: section,
        settingsUserId: userId,
        settingsProjektId: projektId
      };
      if (push) {
        history.pushState(nextState, "", nextHref);
      } else {
        history.replaceState(nextState, "", nextHref);
      }
    } catch (error) {
      if (fallbackHref) {
        window.location.href = fallbackHref;
      }
    } finally {
      panel.setAttribute("aria-busy", "false");
    }
  };
  const initialStateFromUrl = getCurrentStateFromUrl();
  const initialActiveLink = shell.querySelector("[data-settings-link].active");
  const initialSection = initialActiveLink instanceof HTMLAnchorElement ? initialActiveLink.dataset.key : initialStateFromUrl.section;
  history.replaceState({
    ...history.state || {},
    settingsSection: initialSection || "role",
    settingsUserId: initialStateFromUrl.userId,
    settingsProjektId: initialStateFromUrl.projektId
  }, "", window.location.href);
  shell.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }
    const link = target.closest("[data-settings-link]");
    if (!(link instanceof HTMLAnchorElement) || !isPrimaryNavigationClick(event)) {
      return;
    }
    event.preventDefault();
    const linkUrl = new URL(link.href);
    loadSection(link.dataset.key, true, {
      href: link.href,
      userId: linkUrl.searchParams.get("userId"),
      projektId: linkUrl.searchParams.get("projektId")
    });
  });
  shell.addEventListener("change", (event) => {
    const target = event.target;
    if (!(target instanceof Element) || !target.matches("[data-settings-filter-user], [data-settings-filter-project]")) {
      return;
    }
    const form = target.closest("[data-settings-filter-form]");
    if (!(form instanceof HTMLFormElement)) {
      return;
    }
    const sectionInput = form.querySelector('input[name="section"]');
    const userSelect = form.querySelector("[data-settings-filter-user]");
    const projectSelect = form.querySelector("[data-settings-filter-project]");
    loadSection(sectionInput instanceof HTMLInputElement ? sectionInput.value : "efektivni-prava", true, {
      userId: userSelect instanceof HTMLSelectElement ? userSelect.value : null,
      projektId: projectSelect instanceof HTMLSelectElement ? projectSelect.value : null
    });
  });
  shell.addEventListener("submit", (event) => {
    const target = event.target;
    if (!(target instanceof HTMLFormElement) || !target.matches("[data-settings-filter-form]")) {
      return;
    }
    event.preventDefault();
    const sectionInput = target.querySelector('input[name="section"]');
    const userSelect = target.querySelector("[data-settings-filter-user]");
    const projectSelect = target.querySelector("[data-settings-filter-project]");
    loadSection(sectionInput instanceof HTMLInputElement ? sectionInput.value : "efektivni-prava", true, {
      userId: userSelect instanceof HTMLSelectElement ? userSelect.value : null,
      projektId: projectSelect instanceof HTMLSelectElement ? projectSelect.value : null
    });
  });
  window.addEventListener("popstate", (event) => {
    const urlState = getCurrentStateFromUrl();
    const section = event.state?.settingsSection || urlState.section || "role";
    const userId = event.state?.settingsUserId ?? urlState.userId;
    const projektId = event.state?.settingsProjektId ?? urlState.projektId;
    const link = shell.querySelector(`[data-settings-link][data-key="${section}"]`);
    const href = link instanceof HTMLAnchorElement ? link.href : null;
    loadSection(section, false, { href, userId, projektId });
  });
}
function initProfileRightsFilter() {
  const form = document.querySelector("[data-profile-rights-form]");
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  const projectSelect = form.querySelector("[data-profile-rights-project]");
  if (!(projectSelect instanceof HTMLSelectElement)) {
    return;
  }
  projectSelect.addEventListener("change", () => {
    const url = new URL(window.location.href);
    if (projectSelect.value) {
      url.searchParams.set("projektId", projectSelect.value);
    } else {
      url.searchParams.delete("projektId");
    }
    url.hash = "moje-prava";
    window.location.href = url.toString();
  });
}
function initUserMenu() {
  const menu = document.querySelector("[data-user-menu]");
  if (!(menu instanceof HTMLElement)) {
    return;
  }
  const toggle = menu.querySelector("[data-user-menu-toggle]");
  const panel = menu.querySelector("[data-user-menu-panel]");
  if (!(toggle instanceof HTMLButtonElement) || !(panel instanceof HTMLElement)) {
    return;
  }
  const setOpen = (open) => {
    panel.hidden = !open;
    toggle.setAttribute("aria-expanded", String(open));
  };
  toggle.addEventListener("click", () => {
    setOpen(panel.hidden);
  });
  document.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }
    if (!menu.contains(target)) {
      setOpen(false);
    }
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      setOpen(false);
    }
  });
}
// PmTracker.Web/wwwroot/js/modules/modals.js
var modalRoot = document.getElementById("modal-root");
var modalFocusableSelector = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[contenteditable='true']",
  "[tabindex]:not([tabindex='-1'])"
].join(", ");
var modalRuntime = {
  closeAllFloatingPanels: null,
  initRecordFormEnhancements: null,
  initPermissionMetadataBindings: null
};
var modalState = {
  lastTrigger: null
};
function configureModalRuntime(runtime = {}) {
  if (typeof runtime.closeAllFloatingPanels === "function") {
    modalRuntime.closeAllFloatingPanels = runtime.closeAllFloatingPanels;
  }
  if (typeof runtime.initRecordFormEnhancements === "function") {
    modalRuntime.initRecordFormEnhancements = runtime.initRecordFormEnhancements;
  }
  if (typeof runtime.initPermissionMetadataBindings === "function") {
    modalRuntime.initPermissionMetadataBindings = runtime.initPermissionMetadataBindings;
  }
}
function getActiveModalOverlay() {
  if (!(modalRoot instanceof HTMLElement)) {
    return null;
  }
  return modalRoot.querySelector(".modal-overlay");
}
function getActiveModalContainer2() {
  const overlay = getActiveModalOverlay();
  if (!(overlay instanceof HTMLElement)) {
    return null;
  }
  return overlay.querySelector("[data-modal-container]");
}
function isModalOpen() {
  return modalRoot instanceof HTMLElement && modalRoot.getAttribute("aria-hidden") !== "true" && modalRoot.childElementCount > 0;
}
function getFocusableElementsWithinModal(container) {
  if (!(container instanceof HTMLElement)) {
    return [];
  }
  return Array.from(container.querySelectorAll(modalFocusableSelector)).filter((element) => element instanceof HTMLElement).filter((element) => {
    if (element.hasAttribute("disabled")) {
      return false;
    }
    if (element.getAttribute("aria-hidden") === "true") {
      return false;
    }
    return element.getClientRects().length > 0;
  });
}
function focusInitialModalElement() {
  const modal = getActiveModalContainer2();
  if (!(modal instanceof HTMLElement)) {
    return;
  }
  const autofocusCandidate = modal.querySelector("[autofocus]");
  if (autofocusCandidate instanceof HTMLElement && !autofocusCandidate.hasAttribute("disabled")) {
    autofocusCandidate.focus({ preventScroll: true });
    return;
  }
  const focusable = getFocusableElementsWithinModal(modal);
  if (focusable.length > 0) {
    focusable[0].focus({ preventScroll: true });
    return;
  }
  modal.focus({ preventScroll: true });
}
function setModalContent(content, trigger) {
  if (!(modalRoot instanceof HTMLElement)) {
    return;
  }
  modalRuntime.closeAllFloatingPanels?.();
  modalRoot.innerHTML = "";
  modalRoot.appendChild(content);
  modalRoot.style.pointerEvents = "auto";
  modalRoot.setAttribute("aria-hidden", "false");
  document.body.classList.add("modal-open");
  modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;
  modalRuntime.initRecordFormEnhancements?.(modalRoot);
  modalRuntime.initPermissionMetadataBindings?.(modalRoot);
  window.requestAnimationFrame(() => {
    focusInitialModalElement();
  });
}
function closeModal() {
  if (!(modalRoot instanceof HTMLElement)) {
    return;
  }
  const focusTarget = modalState.lastTrigger;
  modalRuntime.closeAllFloatingPanels?.();
  modalRoot.innerHTML = "";
  modalRoot.style.pointerEvents = "none";
  modalRoot.setAttribute("aria-hidden", "true");
  document.body.classList.remove("modal-open");
  modalState.lastTrigger = null;
  if (focusTarget instanceof HTMLElement && focusTarget.isConnected) {
    focusTarget.focus({ preventScroll: true });
  }
}
async function openUrlModal(url, trigger) {
  if (!url) {
    return;
  }
  try {
    const response = await fetch(appendCurrentAsUser(url), { headers: { "X-Requested-With": "XMLHttpRequest" } });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    const html = await response.text();
    const wrapper = document.createElement("div");
    wrapper.innerHTML = html;
    setModalContent(wrapper, trigger);
  } catch (error) {
    reportClientDiagnostic("modal-load-failed", { url });
    if (modalRoot instanceof HTMLElement) {
      modalRoot.innerHTML = `
                <div class="modal-overlay" aria-hidden="false">
                    <div class="modal-container" role="dialog" aria-modal="true" tabindex="-1">
                        <p>Nepodařilo se načíst obsah dialogu.</p>
                        <div class="modal-actions">
                            <button type="button" class="btn btn-secondary" data-modal-close>Zavřít</button>
                        </div>
                    </div>
                </div>`;
      modalRoot.style.pointerEvents = "auto";
      modalRoot.setAttribute("aria-hidden", "false");
      document.body.classList.add("modal-open");
      modalState.lastTrigger = trigger instanceof HTMLElement ? trigger : null;
      focusInitialModalElement();
    }
  }
}
function trapFocusInModal(event) {
  const modal = getActiveModalContainer2();
  if (!(modal instanceof HTMLElement)) {
    return;
  }
  const focusable = getFocusableElementsWithinModal(modal);
  if (focusable.length === 0) {
    event.preventDefault();
    modal.focus({ preventScroll: true });
    return;
  }
  const first = focusable[0];
  const last = focusable[focusable.length - 1];
  const activeElement = document.activeElement;
  const activeInsideModal = activeElement instanceof Element && modal.contains(activeElement);
  if (event.shiftKey) {
    if (!activeInsideModal || activeElement === first) {
      event.preventDefault();
      last.focus({ preventScroll: true });
    }
    return;
  }
  if (!activeInsideModal || activeElement === last) {
    event.preventDefault();
    first.focus({ preventScroll: true });
  }
}

// PmTracker.Web/wwwroot/js/modules/session.js
var sessionStaleErrorCode = "SESSION_STALE_CLIENT_BLOCK";
var keepAliveEndpointPath = "/App/KeepAlive";
var keepAliveIntervalMs = 5 * 60 * 1000;
var keepAliveTimeoutMs = 10 * 1000;
var keepAliveFailureThreshold = 2;
var sessionState = {
  intervalId: 0,
  inFlightPromise: null,
  stale: false,
  consecutiveFailures: 0,
  lastSuccessUtc: "",
  lastTraceId: "",
  lastFailureReason: ""
};
function hasAjaxSubmitFormsInDom() {
  return document.querySelector('form[data-ajax-submit="true"]') instanceof HTMLFormElement;
}
function setSessionStaleState(stale, reason) {
  sessionState.stale = Boolean(stale);
  document.documentElement.dataset.sessionStale = sessionState.stale ? "true" : "false";
  if (sessionState.stale) {
    if (reason) {
      sessionState.lastFailureReason = String(reason);
    }
    return;
  }
  sessionState.consecutiveFailures = 0;
  sessionState.lastFailureReason = "";
}
function updateRequestVerificationTokens(nextToken) {
  const token = String(nextToken || "").trim();
  if (!token) {
    return false;
  }
  let updated = 0;
  document.querySelectorAll('input[name="__RequestVerificationToken"]').forEach((input) => {
    if (!(input instanceof HTMLInputElement)) {
      return;
    }
    input.value = token;
    updated += 1;
  });
  return updated > 0;
}
function buildKeepAliveFailureReason(response, payload, error) {
  if (isPlainObject(payload) && typeof payload.message === "string" && payload.message.trim()) {
    return payload.message.trim();
  }
  if (response instanceof Response) {
    const status = response.status || 0;
    const statusText = response.statusText || "";
    return status > 0 ? `HTTP ${status}${statusText ? ` ${statusText}` : ""}` : "KeepAlive response failed.";
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return "KeepAlive request failed.";
}
async function performKeepAliveRequest(source, force) {
  if (!force && document.hidden) {
    return true;
  }
  if (!force && !hasAjaxSubmitFormsInDom()) {
    return true;
  }
  const abortController = typeof AbortController === "function" ? new AbortController : null;
  const timeoutId = window.setTimeout(() => {
    if (abortController) {
      abortController.abort();
    }
  }, keepAliveTimeoutMs);
  try {
    const response = await fetch(keepAliveEndpointPath, {
      method: "GET",
      headers: {
        "X-Requested-With": "XMLHttpRequest",
        Accept: "application/json"
      },
      credentials: "same-origin",
      cache: "no-store",
      signal: abortController ? abortController.signal : undefined
    });
    const rawBody = await response.text();
    const payload = parseJsonPayload(rawBody);
    if (isPlainObject(payload) && typeof payload.traceId === "string" && payload.traceId.trim()) {
      sessionState.lastTraceId = payload.traceId.trim();
    }
    const hasToken = isPlainObject(payload) && typeof payload.requestVerificationToken === "string" && payload.requestVerificationToken.trim().length > 0;
    if (response.ok && isPlainObject(payload) && payload.ok === true && hasToken) {
      updateRequestVerificationTokens(payload.requestVerificationToken);
      sessionState.lastSuccessUtc = new Date().toISOString();
      setSessionStaleState(false, "");
      return true;
    }
    sessionState.consecutiveFailures += 1;
    const reason = buildKeepAliveFailureReason(response, payload, null);
    sessionState.lastFailureReason = reason;
    if (sessionState.consecutiveFailures >= keepAliveFailureThreshold) {
      setSessionStaleState(true, `${source}: ${reason}`);
    }
    return false;
  } catch (error) {
    sessionState.consecutiveFailures += 1;
    const reason = buildKeepAliveFailureReason(null, null, error);
    sessionState.lastFailureReason = reason;
    if (sessionState.consecutiveFailures >= keepAliveFailureThreshold) {
      setSessionStaleState(true, `${source}: ${reason}`);
    }
    return false;
  } finally {
    window.clearTimeout(timeoutId);
  }
}
async function ensureSessionKeepAlive(source, force) {
  if (sessionState.stale && !force) {
    return false;
  }
  if (sessionState.inFlightPromise && typeof sessionState.inFlightPromise.then === "function") {
    return sessionState.inFlightPromise;
  }
  sessionState.inFlightPromise = performKeepAliveRequest(source, Boolean(force)).finally(() => {
    sessionState.inFlightPromise = null;
  });
  return sessionState.inFlightPromise;
}
function initSessionCoordinator() {
  if (!(document.body instanceof HTMLElement) || document.body.dataset.sessionCoordinatorReady === "true") {
    return;
  }
  const scheduleKeepAlive = () => {
    ensureSessionKeepAlive("scheduled", false);
  };
  document.body.dataset.sessionCoordinatorReady = "true";
  document.addEventListener("visibilitychange", () => {
    if (!document.hidden) {
      scheduleKeepAlive();
    }
  });
  window.addEventListener("focus", () => {
    scheduleKeepAlive();
  });
  sessionState.intervalId = window.setInterval(scheduleKeepAlive, keepAliveIntervalMs);
  window.setTimeout(scheduleKeepAlive, 3000);
}

// PmTracker.Web/wwwroot/js/modules/pickers.js
function setAppDateFieldValue(valueInput, isoValue) {
  if (!(valueInput instanceof HTMLInputElement)) {
    return false;
  }
  const normalizedIso = typeof isoValue === "string" ? isoValue.trim() : "";
  const parsed = parseIsoDate(normalizedIso);
  if (!(parsed instanceof Date)) {
    return false;
  }
  const previous = valueInput.value || "";
  valueInput.value = normalizedIso;
  const dateField = valueInput.closest("[data-app-date-field]");
  const displayInput = dateField?.querySelector("[data-app-date-display]");
  if (displayInput instanceof HTMLInputElement) {
    displayInput.value = formatDisplayDate(parsed);
  }
  if (previous !== normalizedIso) {
    valueInput.dispatchEvent(new Event("change", { bubbles: true }));
  }
  return true;
}
function closeAllDatePanels(exceptField) {
  document.querySelectorAll("[data-app-date-field]").forEach((candidate) => {
    if (!(candidate instanceof HTMLElement)) {
      return;
    }
    if (exceptField && candidate === exceptField) {
      return;
    }
    const panel = candidate.querySelector("[data-app-date-panel]");
    if (panel instanceof HTMLElement) {
      panel.hidden = true;
      unmountFloatingPanel(panel);
    }
  });
}
function closeAllTimePanels(exceptField) {
  document.querySelectorAll("[data-app-time-field]").forEach((candidate) => {
    if (!(candidate instanceof HTMLElement)) {
      return;
    }
    if (exceptField && candidate === exceptField) {
      return;
    }
    const panel = candidate.querySelector("[data-app-time-panel]");
    if (panel instanceof HTMLElement) {
      panel.hidden = true;
      unmountFloatingPanel(panel);
    }
  });
}
function initCustomDatePickers(scope) {
  scope.querySelectorAll("[data-app-date-field]").forEach((field) => {
    if (!(field instanceof HTMLElement) || field.dataset.appDateReady === "true") {
      return;
    }
    const displayInput = field.querySelector("[data-app-date-display]");
    const valueInput = field.querySelector("[data-app-date-value]");
    const openButton = field.querySelector("[data-app-date-open]");
    const panel = field.querySelector("[data-app-date-panel]");
    const prevButton = field.querySelector("[data-app-date-prev]");
    const nextButton = field.querySelector("[data-app-date-next]");
    const monthSelect = field.querySelector("[data-app-date-month]");
    const yearSelect = field.querySelector("[data-app-date-year]");
    const grid = field.querySelector("[data-app-date-grid]");
    if (!(displayInput instanceof HTMLInputElement) || !(valueInput instanceof HTMLInputElement) || !(openButton instanceof HTMLButtonElement) || !(panel instanceof HTMLElement) || !(prevButton instanceof HTMLButtonElement) || !(nextButton instanceof HTMLButtonElement) || !(monthSelect instanceof HTMLSelectElement) || !(yearSelect instanceof HTMLSelectElement) || !(grid instanceof HTMLElement)) {
      return;
    }
    field.dataset.appDateReady = "true";
    const isFieldLocked = () => field.dataset.appDateLocked === "true" || openButton.disabled;
    let selectedDate = parseIsoDate(valueInput.value) || parseDisplayDate(displayInput.value) || null;
    let viewDate = selectedDate ? new Date(selectedDate.getTime()) : new Date;
    const syncValue = () => {
      const previous = valueInput.value;
      valueInput.value = selectedDate ? formatIsoDate(selectedDate) : "";
      displayInput.value = selectedDate ? formatDisplayDate(selectedDate) : "";
      if (previous !== valueInput.value) {
        valueInput.dispatchEvent(new Event("change", { bubbles: true }));
      }
    };
    const ensureMonthOptions = () => {
      if (monthSelect.options.length > 0) {
        return;
      }
      dateMonths.forEach((month, index) => {
        const option = document.createElement("option");
        option.value = String(index);
        option.textContent = month;
        monthSelect.appendChild(option);
      });
    };
    const ensureYearOptions = (centerYear) => {
      const fromYear = centerYear - 20;
      const toYear = centerYear + 20;
      const currentFrom = Number.parseInt(yearSelect.dataset.fromYear || "", 10);
      const currentTo = Number.parseInt(yearSelect.dataset.toYear || "", 10);
      if (currentFrom === fromYear && currentTo === toYear) {
        return;
      }
      yearSelect.innerHTML = "";
      for (let year = fromYear;year <= toYear; year += 1) {
        const option = document.createElement("option");
        option.value = String(year);
        option.textContent = String(year);
        yearSelect.appendChild(option);
      }
      yearSelect.dataset.fromYear = String(fromYear);
      yearSelect.dataset.toYear = String(toYear);
    };
    const renderGrid = () => {
      ensureMonthOptions();
      ensureYearOptions(viewDate.getFullYear());
      monthSelect.value = String(viewDate.getMonth());
      yearSelect.value = String(viewDate.getFullYear());
      grid.innerHTML = "";
      const currentMonth = viewDate.getMonth();
      const currentYear = viewDate.getFullYear();
      const firstDayOfMonth = new Date(currentYear, currentMonth, 1);
      const mondayOffset = (firstDayOfMonth.getDay() + 6) % 7;
      const firstVisibleDate = new Date(currentYear, currentMonth, 1 - mondayOffset);
      const today = new Date;
      today.setHours(0, 0, 0, 0);
      for (let i = 0;i < 42; i += 1) {
        const dayDate = new Date(firstVisibleDate.getFullYear(), firstVisibleDate.getMonth(), firstVisibleDate.getDate() + i);
        const button = document.createElement("button");
        button.type = "button";
        button.className = "app-date-day";
        button.textContent = String(dayDate.getDate());
        button.dataset.iso = formatIsoDate(dayDate);
        button.setAttribute("role", "gridcell");
        if (dayDate.getMonth() !== currentMonth) {
          button.classList.add("outside");
        }
        if (selectedDate && isSameCalendarDate(dayDate, selectedDate)) {
          button.classList.add("selected");
        }
        if (isSameCalendarDate(dayDate, today)) {
          button.title = "Dnes";
        }
        button.addEventListener("click", () => {
          selectedDate = dayDate;
          viewDate = new Date(dayDate.getFullYear(), dayDate.getMonth(), 1);
          syncValue();
          closePanel();
        });
        grid.appendChild(button);
      }
      if (!panel.hidden) {
        positionFloatingPanel(panel, field, panel._pmtrackerFloatingOptions || {
          gap: 8,
          flipVertical: true,
          kind: "date"
        });
      }
    };
    const closePanel = () => {
      panel.hidden = true;
      unmountFloatingPanel(panel);
    };
    const openPanel = () => {
      if (isFieldLocked()) {
        return;
      }
      closeAllDatePanels(field);
      closeAllTimePanels();
      renderGrid();
      panel.hidden = false;
      mountFloatingPanel(panel, field, { gap: 8, flipVertical: true, kind: "date" });
    };
    syncValue();
    openButton.addEventListener("click", () => {
      if (panel.hidden) {
        openPanel();
      } else {
        closePanel();
      }
    });
    displayInput.addEventListener("click", () => {
      openPanel();
    });
    displayInput.addEventListener("focus", () => {
      openPanel();
    });
    displayInput.addEventListener("keydown", (event) => {
      if (event.key === "Enter" || event.key === "ArrowDown") {
        event.preventDefault();
        openPanel();
      }
    });
    prevButton.addEventListener("click", () => {
      viewDate = new Date(viewDate.getFullYear(), viewDate.getMonth() - 1, 1);
      renderGrid();
    });
    nextButton.addEventListener("click", () => {
      viewDate = new Date(viewDate.getFullYear(), viewDate.getMonth() + 1, 1);
      renderGrid();
    });
    monthSelect.addEventListener("change", () => {
      const month = Number.parseInt(monthSelect.value, 10);
      if (!Number.isFinite(month)) {
        return;
      }
      viewDate = new Date(viewDate.getFullYear(), month, 1);
      renderGrid();
    });
    yearSelect.addEventListener("change", () => {
      const year = Number.parseInt(yearSelect.value, 10);
      if (!Number.isFinite(year)) {
        return;
      }
      viewDate = new Date(year, viewDate.getMonth(), 1);
      renderGrid();
    });
    document.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      if (!isInteractionInsideFloatingControl(target, field, panel)) {
        closePanel();
      }
    });
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        closePanel();
      }
    });
    displayInput.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        closePanel();
      }
    });
  });
}
function initCustomTimePickers(scope) {
  scope.querySelectorAll("[data-app-time-field]").forEach((field) => {
    if (!(field instanceof HTMLElement) || field.dataset.appTimeReady === "true") {
      return;
    }
    const displayInput = field.querySelector("[data-app-time-display]");
    const valueInput = field.querySelector("[data-app-time-value]");
    const openButton = field.querySelector("[data-app-time-open]");
    const panel = field.querySelector("[data-app-time-panel]");
    const grid = field.querySelector("[data-app-time-grid]");
    if (!(displayInput instanceof HTMLInputElement) || !(valueInput instanceof HTMLInputElement) || !(openButton instanceof HTMLButtonElement) || !(panel instanceof HTMLElement) || !(grid instanceof HTMLElement)) {
      return;
    }
    field.dataset.appTimeReady = "true";
    const isLocked = field.dataset.appTimeLocked === "true" || openButton.disabled;
    const form = field.closest("form");
    const initialDateTime = parseIsoDateTime(valueInput.value);
    const initialTime = parseTimeValue(displayInput.value) || parseTimeValue(valueInput.value) || (initialDateTime ? { hours: initialDateTime.getHours(), minutes: initialDateTime.getMinutes() } : null);
    let selected = initialTime || { hours: new Date().getHours(), minutes: new Date().getMinutes() };
    const syncValue = () => {
      const normalizedTime = formatTime(selected.hours, selected.minutes);
      displayInput.value = normalizedTime;
      valueInput.value = normalizedTime;
    };
    const closePanel = () => {
      panel.hidden = true;
      unmountFloatingPanel(panel);
    };
    const render = () => {
      grid.innerHTML = "";
      for (let hour = 6;hour <= 22; hour += 1) {
        for (let minute = 0;minute < 60; minute += 15) {
          const timeText = formatTime(hour, minute);
          const button = document.createElement("button");
          button.type = "button";
          button.className = "app-time-option";
          button.textContent = timeText;
          button.dataset.time = timeText;
          button.setAttribute("role", "option");
          button.setAttribute("aria-selected", String(selected.hours === hour && selected.minutes === minute));
          if (selected.hours === hour && selected.minutes === minute) {
            button.classList.add("selected");
          }
          button.addEventListener("click", () => {
            selected = { hours: hour, minutes: minute };
            syncValue();
            closePanel();
          });
          grid.appendChild(button);
        }
      }
      if (!panel.hidden) {
        positionFloatingPanel(panel, field, panel._pmtrackerFloatingOptions || {
          gap: 8,
          flipVertical: true,
          kind: "time"
        });
      }
    };
    const openPanel = () => {
      if (isLocked) {
        return;
      }
      closeAllDatePanels();
      closeAllTimePanels(field);
      render();
      panel.hidden = false;
      mountFloatingPanel(panel, field, { gap: 8, flipVertical: true, kind: "time" });
    };
    syncValue();
    openButton.addEventListener("click", () => {
      if (panel.hidden) {
        openPanel();
      } else {
        closePanel();
      }
    });
    displayInput.addEventListener("mousedown", (event) => {
      event.preventDefault();
      openPanel();
    });
    displayInput.addEventListener("click", () => {
      openPanel();
    });
    displayInput.addEventListener("focus", () => {
      openPanel();
    });
    displayInput.addEventListener("keydown", (event) => {
      if (event.key === "Enter" || event.key === "ArrowDown") {
        event.preventDefault();
        openPanel();
      } else if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        closePanel();
      }
    });
    if (form instanceof HTMLFormElement) {
      form.addEventListener("submit", () => {
        syncValue();
      });
    }
    document.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      if (!isInteractionInsideFloatingControl(target, field, panel)) {
        closePanel();
      }
    });
    document.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        closePanel();
      }
    });
  });
}
function formatPersonEntryLabel(entry) {
  if (entry.email) {
    return `${entry.label} <${entry.email}>`;
  }
  return entry.label;
}
function initSinglePersonPickers(scope) {
  scope.querySelectorAll('[data-person-picker="single"]').forEach((wrapper) => {
    if (!(wrapper instanceof HTMLElement) || wrapper.dataset.pickerReady === "true") {
      return;
    }
    const input = wrapper.querySelector("[data-person-picker-input]");
    const anchor = wrapper.querySelector("[data-floating-anchor]");
    let hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
    if (!(hiddenInput instanceof HTMLInputElement)) {
      const formScope = wrapper.closest("form");
      if (formScope instanceof HTMLFormElement) {
        const formHidden = formScope.querySelector("[data-person-picker-hidden]");
        if (formHidden instanceof HTMLInputElement) {
          hiddenInput = formHidden;
        }
      }
    }
    const searchUrl = (wrapper.dataset.personPickerSearchUrl || "").trim();
    const hasRemoteSearch = searchUrl.length > 0;
    const source = wrapper.querySelector("[data-person-picker-source]");
    const panel = wrapper.querySelector("[data-person-picker-panel]");
    const results = wrapper.querySelector("[data-person-picker-results]");
    const message = wrapper.querySelector("[data-person-picker-message]");
    if (!(input instanceof HTMLInputElement) || !(anchor instanceof HTMLElement) || !(hiddenInput instanceof HTMLInputElement) || !(panel instanceof HTMLElement) || !(results instanceof HTMLElement)) {
      return;
    }
    if (!hasRemoteSearch && !(source instanceof HTMLElement)) {
      return;
    }
    wrapper.dataset.pickerReady = "true";
    input.placeholder = wrapper.dataset.personPickerPlaceholder || input.placeholder || "Vyhledejte osobu...";
    let entries = Array.from(source instanceof HTMLElement ? source.querySelectorAll("[data-id]") : []).map((item) => {
      if (!(item instanceof HTMLElement)) {
        return null;
      }
      return {
        id: item.dataset.id || "",
        label: (item.dataset.label || "").trim(),
        email: (item.dataset.email || "").trim(),
        org: (item.dataset.org || "").trim(),
        unit: (item.dataset.unit || "").trim()
      };
    }).filter((entry) => entry && entry.id && entry.label);
    if (!hasRemoteSearch && entries.length === 0) {
      return;
    }
    let filtered = [];
    let activeIndex = -1;
    let remoteSearchVersion = 0;
    let activeRemoteSearchController = null;
    const minRemoteQueryLength = 2;
    const lockVerticalSide = anchor.closest("[data-modal-container]") instanceof HTMLElement && anchor.closest('form[data-record-editor-form="true"][data-record-editor-presentation="modal"]') instanceof HTMLElement;
    const closePanel = () => {
      panel.hidden = true;
      activeIndex = -1;
      unmountFloatingPanel(panel);
    };
    const openPanel = () => {
      panel.hidden = false;
      mountFloatingPanel(panel, anchor, {
        gap: 6,
        flipVertical: true,
        kind: "person-search",
        matchWidth: true,
        lockVerticalSide
      });
    };
    const setMessage = (text) => {
      if (!(message instanceof HTMLElement)) {
        return;
      }
      message.textContent = text;
    };
    const renderStatus = (text) => {
      results.innerHTML = "";
      const statusRow = document.createElement("button");
      statusRow.type = "button";
      statusRow.className = "office-search-item disabled";
      statusRow.disabled = true;
      statusRow.tabIndex = -1;
      const primary = document.createElement("span");
      primary.className = "office-search-primary";
      primary.textContent = text;
      statusRow.appendChild(primary);
      results.appendChild(statusRow);
      setMessage(text);
      openPanel();
    };
    const findEntryById = (id) => entries.find((entry) => entry.id === id) || null;
    const findEntryByInput = () => {
      const query = normalizeSearchText2(input.value || "");
      if (!query) {
        return null;
      }
      const exactDisplay = entries.find((entry) => normalizeSearchText2(formatPersonEntryLabel(entry)) === query);
      if (exactDisplay) {
        return exactDisplay;
      }
      const exactLabel = entries.find((entry) => normalizeSearchText2(entry.label) === query);
      if (exactLabel) {
        return exactLabel;
      }
      const exactEmail = entries.find((entry) => entry.email && normalizeSearchText2(entry.email) === query);
      if (exactEmail) {
        return exactEmail;
      }
      return null;
    };
    const selectEntry = (entry, sourceKind = "user") => {
      hiddenInput.value = entry.id;
      input.value = formatPersonEntryLabel(entry);
      input.setCustomValidity("");
      setMessage(entry.email ? `Vybraná osoba: ${entry.label}, ${entry.email}` : `Vybraná osoba: ${entry.label}`);
      closePanel();
      wrapper.dispatchEvent(new CustomEvent("person-picker:selected", {
        bubbles: true,
        detail: {
          source: sourceKind,
          id: entry.id,
          label: entry.label,
          email: entry.email
        }
      }));
    };
    const render = () => {
      results.innerHTML = "";
      if (filtered.length === 0) {
        if (hasRemoteSearch) {
          renderStatus(wrapper.dataset.personPickerEmpty || "Nenalezeny žádné odpovídající osoby.");
        } else {
          setMessage(wrapper.dataset.personPickerEmpty || "Nenalezeny žádné odpovídající osoby.");
          closePanel();
        }
        return;
      }
      filtered.forEach((entry, index) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "office-search-item";
        button.setAttribute("role", "option");
        button.dataset.index = String(index);
        if (index === activeIndex) {
          button.classList.add("active");
        }
        const primary = document.createElement("span");
        primary.className = "office-search-primary";
        primary.textContent = entry.label;
        button.appendChild(primary);
        const secondary = document.createElement("span");
        secondary.className = "office-search-secondary";
        const hasOrgOrUnit = Boolean(entry.org || entry.unit);
        if (entry.email && hasOrgOrUnit) {
          const orgPart = `${entry.org || "-"} / ${entry.unit || "-"}`;
          secondary.textContent = `${entry.email} | ${orgPart}`;
          button.appendChild(secondary);
        } else if (entry.email) {
          secondary.textContent = entry.email;
          button.appendChild(secondary);
        } else if (hasOrgOrUnit) {
          secondary.textContent = `${entry.org || "-"} / ${entry.unit || "-"}`;
          button.appendChild(secondary);
        }
        results.appendChild(button);
      });
      openPanel();
    };
    const rank = (query) => {
      const normalized = normalizeSearchText2(query);
      const ranked = entries.map((entry) => {
        const searchable = `${entry.label} ${entry.email} ${entry.org} ${entry.unit}`;
        const score = normalized ? scoreSearchCandidate(normalized, searchable) : 1;
        return { entry, score };
      }).filter((row) => row.score > 0).sort((a, b) => b.score - a.score || a.entry.label.localeCompare(b.entry.label, "cs"));
      return ranked.slice(0, 15).map((row) => row.entry);
    };
    const runSearch = () => {
      filtered = rank(input.value || "");
      activeIndex = filtered.length > 0 ? 0 : -1;
      hiddenInput.value = "";
      input.setCustomValidity("");
      render();
    };
    const normalizeRemoteEntries = (payload) => {
      const sourceEntries = Array.isArray(payload) ? payload : Array.isArray(payload?.results) ? payload.results : [];
      return sourceEntries.map((item) => {
        if (!item || typeof item !== "object") {
          return null;
        }
        const id = item.id ? String(item.id).trim() : "";
        const label = typeof item.label === "string" ? item.label.trim() : "";
        if (!id || !label) {
          return null;
        }
        return {
          id,
          label,
          email: typeof item.email === "string" ? item.email.trim() : "",
          org: typeof item.organizace === "string" ? item.organizace.trim() : "",
          unit: typeof item.organizacniCelek === "string" ? item.organizacniCelek.trim() : ""
        };
      }).filter((entry) => entry && entry.id && entry.label);
    };
    const performRemoteSearch = async () => {
      const query = (input.value || "").trim();
      hiddenInput.value = "";
      input.setCustomValidity("");
      if (query.length < minRemoteQueryLength) {
        filtered = [];
        if (query.length === 0) {
          results.innerHTML = "";
          closePanel();
        } else {
          renderStatus(`Zadejte alespoň ${minRemoteQueryLength} znaky.`);
        }
        return;
      }
      const requestVersion = ++remoteSearchVersion;
      activeRemoteSearchController?.abort();
      const controller = new AbortController;
      activeRemoteSearchController = controller;
      renderStatus("Vyhledávám...");
      try {
        const separator = searchUrl.includes("?") ? "&" : "?";
        const response = await fetch(`${searchUrl}${separator}q=${encodeURIComponent(query)}`, {
          headers: {
            "X-Requested-With": "XMLHttpRequest",
            Accept: "application/json"
          },
          credentials: "same-origin",
          signal: controller.signal
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const payload = parseJsonPayload(await response.text());
        if (requestVersion !== remoteSearchVersion) {
          return;
        }
        entries = normalizeRemoteEntries(payload);
        filtered = entries;
        activeIndex = filtered.length > 0 ? 0 : -1;
        render();
      } catch (error) {
        if (error instanceof DOMException && error.name === "AbortError") {
          return;
        }
        if (requestVersion !== remoteSearchVersion) {
          return;
        }
        reportClientDiagnostic("person-picker-search-failed", { searchUrl });
        renderStatus("Vyhledávání osob se nepodařilo.");
      }
    };
    const debouncedSearch = debounce(() => {
      if (hasRemoteSearch) {
        performRemoteSearch();
        return;
      }
      runSearch();
    }, hasRemoteSearch ? 220 : 140);
    const initial = entries.find((entry) => entry.id === hiddenInput.value);
    if (initial && !input.value.trim()) {
      input.value = formatPersonEntryLabel(initial);
    }
    wrapper.addEventListener("person-picker:select-id", (event) => {
      if (!(event instanceof CustomEvent)) {
        return;
      }
      const requestedId = event.detail?.id ? String(event.detail.id) : "";
      if (!requestedId) {
        return;
      }
      const selected = findEntryById(requestedId);
      if (!selected) {
        return;
      }
      const sourceKind = typeof event.detail?.source === "string" ? event.detail.source : "auto";
      selectEntry(selected, sourceKind === "user" ? "user" : "auto");
    });
    input.addEventListener("input", () => {
      setMessage("Vyhledávám...");
      debouncedSearch();
    });
    input.addEventListener("focus", () => {
      if (hasRemoteSearch) {
        if (hiddenInput.value.trim() && input.value.trim()) {
          return;
        }
        const query = (input.value || "").trim();
        if (query.length >= minRemoteQueryLength) {
          performRemoteSearch();
        } else if (!hiddenInput.value.trim()) {
          renderStatus(`Zadejte alespoň ${minRemoteQueryLength} znaky.`);
        }
        return;
      }
      filtered = rank(input.value || "");
      activeIndex = filtered.length > 0 ? 0 : -1;
      render();
    });
    input.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        closePanel();
        return;
      }
      if (event.key === "ArrowDown") {
        event.preventDefault();
        if (filtered.length === 0) {
          if (hasRemoteSearch) {
            const query = (input.value || "").trim();
            if (query.length >= minRemoteQueryLength) {
              performRemoteSearch();
              return;
            }
          } else {
            filtered = rank(input.value || "");
          }
        }
        activeIndex = Math.min(activeIndex + 1, filtered.length - 1);
        render();
        return;
      }
      if (event.key === "ArrowUp") {
        event.preventDefault();
        activeIndex = Math.max(activeIndex - 1, 0);
        render();
        return;
      }
      if (event.key === "Enter" && activeIndex >= 0 && filtered[activeIndex]) {
        event.preventDefault();
        selectEntry(filtered[activeIndex]);
      }
    });
    results.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      const button = target.closest("[data-index]");
      if (!(button instanceof HTMLElement)) {
        return;
      }
      const index = Number.parseInt(button.dataset.index || "-1", 10);
      if (!Number.isFinite(index) || index < 0 || index >= filtered.length) {
        return;
      }
      selectEntry(filtered[index]);
    });
    const form = wrapper.closest("form");
    if (form instanceof HTMLFormElement) {
      form.addEventListener("submit", (event) => {
        if (hiddenInput.value) {
          input.setCustomValidity("");
          return;
        }
        if (!hasRemoteSearch) {
          const matchedEntry = findEntryByInput();
          if (matchedEntry) {
            selectEntry(matchedEntry, "auto");
            return;
          }
        }
        event.preventDefault();
        setMessage("Vyberte osobu ze seznamu výsledků.");
        input.setCustomValidity("Vyberte osobu ze seznamu výsledků.");
        input.reportValidity();
        input.focus();
      });
    }
    document.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      if (!isInteractionInsideFloatingControl(target, anchor, panel)) {
        closePanel();
      }
    });
  });
}
function initAdPersonPickers(scope) {
  scope.querySelectorAll("[data-ad-picker]").forEach((wrapper) => {
    if (!(wrapper instanceof HTMLElement) || wrapper.dataset.adPickerReady === "true") {
      return;
    }
    const searchUrl = wrapper.dataset.searchUrl || "";
    const form = wrapper.closest("form");
    const anchor = wrapper.querySelector("[data-floating-anchor]");
    const queryInput = wrapper.querySelector("[data-ad-query-input]");
    const queryHidden = form?.querySelector("[data-ad-query-hidden]");
    const guidInput = form?.querySelector("[data-ad-guid]");
    const adLoginInput = form?.querySelector("[data-ad-login]");
    const adCompanyInput = form?.querySelector("[data-ad-company]");
    const adDepartmentInput = form?.querySelector("[data-ad-department]");
    const jmenoInput = form?.querySelector("[data-ad-jmeno]");
    const prijmeniInput = form?.querySelector("[data-ad-prijmeni]");
    const titulInput = form?.querySelector("[data-ad-titul]");
    const emailInput = form?.querySelector("[data-ad-email]");
    const orgSelect = form?.querySelector("[data-ad-org-select]");
    const orgCreateHint = form?.querySelector("[data-ad-org-create-hint]");
    const orgUnitSelect = form?.querySelector("[data-ad-org-unit-select]");
    const orgUnitCreateHint = form?.querySelector("[data-ad-org-unit-create-hint]");
    const submitButton = form?.querySelector("[data-ad-submit]");
    const panel = wrapper.querySelector("[data-ad-search-panel]");
    const results = wrapper.querySelector("[data-ad-results]");
    if (!searchUrl || !(form instanceof HTMLFormElement) || !(anchor instanceof HTMLElement) || !(queryInput instanceof HTMLInputElement) || !(queryHidden instanceof HTMLInputElement) || !(guidInput instanceof HTMLInputElement) || !(adLoginInput instanceof HTMLInputElement) || !(adCompanyInput instanceof HTMLInputElement) || !(adDepartmentInput instanceof HTMLInputElement) || !(jmenoInput instanceof HTMLInputElement) || !(prijmeniInput instanceof HTMLInputElement) || !(titulInput instanceof HTMLInputElement) || !(emailInput instanceof HTMLInputElement) || !(orgSelect instanceof HTMLSelectElement) || !(orgCreateHint instanceof HTMLElement) || !(orgUnitSelect instanceof HTMLSelectElement) || !(orgUnitCreateHint instanceof HTMLElement) || !(panel instanceof HTMLElement) || !(results instanceof HTMLElement) || !(submitButton instanceof HTMLButtonElement)) {
      return;
    }
    wrapper.dataset.adPickerReady = "true";
    let activeIndex = -1;
    let currentResults = [];
    let adAvailabilityKnown = false;
    let adIsUnavailable = false;
    const adUnavailableMessage = "Active Directory ACR není dostupné.";
    const closePanel = () => {
      panel.hidden = true;
      activeIndex = -1;
      unmountFloatingPanel(panel);
    };
    const clearSelection = () => {
      guidInput.value = "";
      adLoginInput.value = "";
      adCompanyInput.value = "";
      adDepartmentInput.value = "";
      jmenoInput.value = "";
      prijmeniInput.value = "";
      titulInput.value = "";
      emailInput.value = "";
      clearGeneratedOption(orgSelect, orgCreateHint);
      clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
      submitButton.disabled = true;
    };
    const normalizeText = (value) => (value || "").toString().trim().toLowerCase();
    const clearGeneratedOption = (select, hint) => {
      Array.from(select.options).filter((option) => option.dataset.generated === "true").forEach((option) => option.remove());
      hint.hidden = true;
    };
    const ensureGeneratedOption = (select, hint, rawValue) => {
      clearGeneratedOption(select, hint);
      const normalized = (rawValue || "").toString().trim();
      if (!normalized) {
        return;
      }
      const option = document.createElement("option");
      option.value = `__new__:${normalized}`;
      option.textContent = `${normalized} (+ bude přidáno do číselníku)`;
      option.dataset.generated = "true";
      select.appendChild(option);
      select.value = option.value;
      hint.hidden = false;
    };
    const ensureGeneratedOrgUnitOption = (code, name) => {
      clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
      const normalizedCode = (code || "").toString().trim();
      const normalizedName = (name || "").toString().trim();
      if (!normalizedCode && !normalizedName) {
        return;
      }
      const option = document.createElement("option");
      const encoded = normalizedCode && normalizedName ? `${normalizedCode}|${normalizedName}` : normalizedCode || normalizedName;
      option.value = `__new__:${encoded}`;
      option.textContent = normalizedCode && normalizedName ? `${normalizedCode} - ${normalizedName} (+ bude přidáno do číselníku)` : `${encoded} (+ bude přidáno do číselníku)`;
      option.dataset.generated = "true";
      orgUnitSelect.appendChild(option);
      orgUnitSelect.value = option.value;
      orgUnitCreateHint.hidden = false;
    };
    const extractCodePrefix = (value) => {
      const text = (value || "").toString().trim();
      const hyphenIndex = text.indexOf("-");
      if (hyphenIndex < 2 || hyphenIndex > 6) {
        return null;
      }
      const prefix = text.slice(0, hyphenIndex).trim().toUpperCase();
      if (!/^[A-Z0-9]{2,8}$/.test(prefix)) {
        return null;
      }
      return prefix;
    };
    const findOptionByCodeOrText = (select, rawText, preferredCodes = []) => {
      const options = Array.from(select.options);
      const normalizedText = normalizeText(rawText);
      const codeFromText = extractCodePrefix(rawText);
      const normalizedCodes = preferredCodes.map((code) => normalizeText(code)).filter((code) => code);
      if (codeFromText) {
        normalizedCodes.unshift(normalizeText(codeFromText));
      }
      for (const normalizedCode of normalizedCodes) {
        const byCode = options.find((option) => normalizeText(option.value) === normalizedCode);
        if (byCode) {
          return byCode.value;
        }
      }
      if (normalizedText) {
        const byText = options.find((option) => normalizeText(option.textContent).includes(normalizedText));
        if (byText) {
          return byText.value;
        }
      }
      return null;
    };
    const parseCompanyLocation = (rawCompany) => {
      const company = (rawCompany || "").toString().trim();
      if (!company) {
        return {
          organizationCode: null,
          organizationName: null,
          orgUnitCode: null,
          orgUnitName: null,
          source: ""
        };
      }
      const slashIndex = company.indexOf("/");
      const left = slashIndex >= 0 ? company.slice(0, slashIndex).trim() : company;
      const right = slashIndex >= 0 ? company.slice(slashIndex + 1).trim() : "";
      let organizationCode = null;
      let organizationName = left;
      const hyphenIndex = left.indexOf("-");
      if (hyphenIndex >= 2 && hyphenIndex <= 6) {
        const maybeCode = left.slice(0, hyphenIndex).trim().toUpperCase();
        if (/^[A-Z0-9]{2,8}$/.test(maybeCode)) {
          organizationCode = maybeCode;
          organizationName = left.slice(hyphenIndex + 1).trim();
        }
      }
      return {
        organizationCode,
        organizationName: organizationName || null,
        orgUnitCode: right || null,
        orgUnitName: organizationName || null,
        source: company
      };
    };
    const tryAutoSelectOrganization = (row) => {
      const parsed = parseCompanyLocation(row.company);
      const company = parsed.source;
      const normalizedCompany = normalizeText(company);
      const forcedCodes = [];
      if (parsed.organizationCode) {
        forcedCodes.push(parsed.organizationCode);
      }
      if (normalizedCompany.includes("ministerstvo obrany") || normalizedCompany.includes("armada ceske republiky") || normalizedCompany.includes("armáda české republiky") || normalizedCompany.includes("acr")) {
        forcedCodes.push("MO");
      } else if (normalizedCompany.includes("gordic")) {
        forcedCodes.push("DOD");
      }
      const selected = findOptionByCodeOrText(orgSelect, parsed.organizationName || company, forcedCodes);
      if (selected) {
        clearGeneratedOption(orgSelect, orgCreateHint);
        orgSelect.value = selected;
        return;
      }
      if (parsed.organizationName || company) {
        ensureGeneratedOption(orgSelect, orgCreateHint, parsed.organizationName || company);
      } else if (orgSelect.options.length > 0) {
        clearGeneratedOption(orgSelect, orgCreateHint);
        const fallback = findOptionByCodeOrText(orgSelect, "", ["MO"]);
        orgSelect.value = fallback || orgSelect.options[0].value;
      }
    };
    const tryAutoSelectOrgUnit = (row) => {
      const parsedCompany = parseCompanyLocation(row.company);
      const departmentRaw = (row.department || "").toString().trim();
      const preferredCodes = [];
      if (parsedCompany.orgUnitCode) {
        preferredCodes.push(parsedCompany.orgUnitCode);
      }
      if (departmentRaw && /^[0-9]+$/.test(departmentRaw)) {
        preferredCodes.push(departmentRaw);
      }
      const selected = findOptionByCodeOrText(orgUnitSelect, parsedCompany.orgUnitName || departmentRaw, preferredCodes);
      if (selected) {
        clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
        orgUnitSelect.value = selected;
        return;
      }
      const generatedCode = parsedCompany.orgUnitCode || (/^[0-9]+$/.test(departmentRaw) ? departmentRaw : "");
      const generatedName = parsedCompany.orgUnitName || (!/^[0-9]+$/.test(departmentRaw) ? departmentRaw : "");
      if (generatedCode || generatedName) {
        ensureGeneratedOrgUnitOption(generatedCode, generatedName);
      } else {
        clearGeneratedOption(orgUnitSelect, orgUnitCreateHint);
        orgUnitSelect.value = "";
      }
    };
    const renderStatusRow = (text, type = "info") => {
      currentResults = [];
      activeIndex = -1;
      results.innerHTML = "";
      const status = document.createElement("div");
      status.className = `office-search-info ${type}`.trim();
      status.textContent = text;
      status.setAttribute("role", "status");
      status.setAttribute("aria-live", "polite");
      results.appendChild(status);
      panel.hidden = false;
      mountFloatingPanel(panel, anchor, {
        gap: 6,
        flipVertical: true,
        kind: "ad-search",
        matchWidth: true
      });
    };
    const fillFromResult = (row) => {
      if (!row.canSelect) {
        renderStatusRow(row.disabledReason || "Tuto osobu nelze vybrat.", "error");
        return;
      }
      guidInput.value = row.guidAd || "";
      adLoginInput.value = row.adLogin || "";
      adCompanyInput.value = row.company || "";
      adDepartmentInput.value = row.department || "";
      jmenoInput.value = row.jmeno || "";
      prijmeniInput.value = row.prijmeni || "";
      titulInput.value = row.titul || "";
      emailInput.value = row.email || "";
      tryAutoSelectOrganization(row);
      tryAutoSelectOrgUnit(row);
      queryHidden.value = queryInput.value.trim();
      queryInput.value = row.email ? `${row.displayName} <${row.email}>` : row.displayName;
      submitButton.disabled = false;
      closePanel();
    };
    const renderResults = () => {
      results.innerHTML = "";
      if (currentResults.length === 0) {
        closePanel();
        return;
      }
      currentResults.forEach((row, index) => {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "office-search-item";
        button.dataset.index = String(index);
        button.setAttribute("role", "option");
        if (index === activeIndex) {
          button.classList.add("active");
        }
        if (!row.canSelect) {
          button.classList.add("disabled");
          button.disabled = true;
        }
        const primary = document.createElement("span");
        primary.className = "office-search-primary";
        primary.textContent = row.displayName || `${row.jmeno || ""} ${row.prijmeni || ""}`.trim();
        button.appendChild(primary);
        const secondary = document.createElement("span");
        secondary.className = "office-search-secondary";
        const orgPart = `${row.company || "-"} / ${row.department || "-"}`;
        secondary.textContent = row.email ? `${row.email} | ${orgPart}` : orgPart;
        button.appendChild(secondary);
        if (!row.canSelect && row.disabledReason) {
          const reason = document.createElement("span");
          reason.className = "office-search-warning";
          reason.textContent = row.disabledReason;
          button.appendChild(reason);
        }
        results.appendChild(button);
      });
      panel.hidden = false;
      mountFloatingPanel(panel, anchor, {
        gap: 6,
        flipVertical: true,
        kind: "ad-search",
        matchWidth: true
      });
    };
    const performSearch = async () => {
      const query = queryInput.value.trim();
      queryHidden.value = query;
      clearSelection();
      if (query.length < 1) {
        if (adIsUnavailable) {
          renderStatusRow(adUnavailableMessage, "error");
        } else {
          results.innerHTML = "";
          closePanel();
        }
        return;
      }
      renderStatusRow("Vyhledávám v Active Directory...");
      try {
        const response = await fetch(`${searchUrl}?q=${encodeURIComponent(query)}`, {
          headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const payload = parseJsonPayload(await response.text());
        if (!payload || typeof payload !== "object") {
          throw new Error("INVALID_AD_PAYLOAD");
        }
        if (!payload.available) {
          adAvailabilityKnown = true;
          adIsUnavailable = true;
          renderStatusRow(payload.message || adUnavailableMessage, "error");
          return;
        }
        adAvailabilityKnown = true;
        adIsUnavailable = false;
        currentResults = Array.isArray(payload.results) ? payload.results.slice(0, 5) : [];
        activeIndex = currentResults.length > 0 ? 0 : -1;
        if (currentResults.length > 0) {
          renderResults();
        } else {
          renderStatusRow("Žádná shoda.", "empty");
        }
      } catch {
        adAvailabilityKnown = true;
        adIsUnavailable = true;
        renderStatusRow(adUnavailableMessage, "error");
        reportClientDiagnostic("ad-search-failed", { searchUrl });
      }
    };
    const debouncedAdSearch = debounce(performSearch, 220);
    const probeAvailability = async () => {
      if (adAvailabilityKnown) {
        if (adIsUnavailable) {
          renderStatusRow(adUnavailableMessage, "error");
        }
        return;
      }
      try {
        const response = await fetch(`${searchUrl}?q=${encodeURIComponent("__pmtracker_probe__")}`, {
          headers: { "X-Requested-With": "XMLHttpRequest" }
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const payload = parseJsonPayload(await response.text());
        if (!payload || typeof payload !== "object") {
          throw new Error("INVALID_AD_PROBE_PAYLOAD");
        }
        adAvailabilityKnown = true;
        adIsUnavailable = !payload.available;
        if (adIsUnavailable) {
          renderStatusRow(payload.message || adUnavailableMessage, "error");
        }
      } catch {
        adAvailabilityKnown = true;
        adIsUnavailable = true;
        renderStatusRow(adUnavailableMessage, "error");
        reportClientDiagnostic("ad-probe-failed", { searchUrl });
      }
    };
    queryInput.addEventListener("input", () => {
      debouncedAdSearch();
    });
    queryInput.addEventListener("focus", () => {
      probeAvailability();
      if (currentResults.length > 0) {
        panel.hidden = false;
        mountFloatingPanel(panel, anchor, {
          gap: 6,
          flipVertical: true,
          kind: "ad-search",
          matchWidth: true
        });
      }
    });
    queryInput.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        event.preventDefault();
        event.stopPropagation();
        closePanel();
        return;
      }
      if (event.key === "ArrowDown") {
        if (currentResults.length === 0) {
          return;
        }
        event.preventDefault();
        activeIndex = Math.min(activeIndex + 1, currentResults.length - 1);
        renderResults();
        return;
      }
      if (event.key === "ArrowUp") {
        if (currentResults.length === 0) {
          return;
        }
        event.preventDefault();
        activeIndex = Math.max(activeIndex - 1, 0);
        renderResults();
        return;
      }
      if (event.key === "Enter" && activeIndex >= 0 && currentResults[activeIndex]) {
        event.preventDefault();
        fillFromResult(currentResults[activeIndex]);
      }
    });
    results.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      const button = target.closest("[data-index]");
      if (!(button instanceof HTMLElement)) {
        return;
      }
      const index = Number.parseInt(button.dataset.index || "-1", 10);
      if (!Number.isFinite(index) || index < 0 || index >= currentResults.length) {
        return;
      }
      fillFromResult(currentResults[index]);
    });
    if (form instanceof HTMLFormElement) {
      form.addEventListener("submit", (event) => {
        if (guidInput.value && !submitButton.disabled) {
          return;
        }
        event.preventDefault();
        renderStatusRow("Nejprve vyberte osobu z AD výsledků.", "empty");
        queryInput.focus();
      });
    }
    document.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      if (!isInteractionInsideFloatingControl(target, anchor, panel)) {
        closePanel();
      }
    });
  });
}
function initCollabPickers(scope) {
  scope.querySelectorAll(".collab-picker").forEach((wrapper) => {
    if (!(wrapper instanceof HTMLElement) || wrapper.dataset.collabPickerReady === "true") {
      return;
    }
    const search = wrapper.querySelector("[data-collab-search]");
    const optionsContainer = wrapper.querySelector("[data-collab-options]");
    if (!(search instanceof HTMLInputElement) || !(optionsContainer instanceof HTMLElement)) {
      return;
    }
    wrapper.dataset.collabPickerReady = "true";
    const options = Array.from(optionsContainer.querySelectorAll("[data-collab-option]")).filter((item) => item instanceof HTMLElement);
    options.forEach((item, index) => {
      item.dataset.collabOrder = String(index);
    });
    const applySearch = () => {
      const query = search.value || "";
      const normalizedQuery = normalizeSearchText2(query);
      const scored = options.map((option) => {
        const label = option.dataset.collabLabel || option.textContent || "";
        const score = normalizedQuery ? scoreSearchCandidate(normalizedQuery, label) : 1;
        const order = Number.parseInt(option.dataset.collabOrder || "0", 10);
        return { option, score, order };
      }).sort((a, b) => {
        if (!normalizedQuery) {
          return a.order - b.order;
        }
        return b.score - a.score || a.order - b.order;
      });
      scored.forEach((row) => {
        row.option.hidden = normalizedQuery.length > 0 && row.score <= 0;
        optionsContainer.appendChild(row.option);
      });
    };
    const debouncedApply = debounce(applySearch, 120);
    search.addEventListener("input", () => {
      debouncedApply();
    });
    applySearch();
  });
}

// PmTracker.Web/wwwroot/js/modules/recordEditor.js
var modalRoot2 = document.getElementById("modal-root");
var recordEditorPreferenceStorageKey = "pmtracker.recordEditor.preference";
var recordEditorReturnStateStoragePrefix = "pmtracker.recordEditor.returnState.project.";
var recordEditorDraftStoragePrefix = "pmtracker.recordEditor.draft.";
var recordEditorDraftTtlMs = 12 * 60 * 60 * 1000;
var recordEditorState2 = {
  chooser: null,
  chooserTrigger: null,
  closeGuard: null,
  closeGuardTrigger: null
};
function getRecordEditorPreferenceLabel(mode) {
  if (mode === "modal") {
    return "Otevřít v modalu";
  }
  if (mode === "page") {
    return "Otevřít na stránce";
  }
  return "není nastaveno";
}
function getStoredRecordEditorPreference() {
  const value = localStorage.getItem(recordEditorPreferenceStorageKey);
  if (value === "modal" || value === "page") {
    return value;
  }
  return null;
}
function setStoredRecordEditorPreference(mode) {
  if (mode !== "modal" && mode !== "page") {
    return;
  }
  localStorage.setItem(recordEditorPreferenceStorageKey, mode);
  refreshRecordEditorPreferenceUi();
}
function clearStoredRecordEditorPreference() {
  localStorage.removeItem(recordEditorPreferenceStorageKey);
  refreshRecordEditorPreferenceUi();
}
function refreshRecordEditorPreferenceUi() {
  const preferred = getStoredRecordEditorPreference();
  document.querySelectorAll("[data-record-editor-preference-current]").forEach((element) => {
    element.textContent = getRecordEditorPreferenceLabel(preferred);
  });
  document.querySelectorAll("[data-record-editor-preference-reset]").forEach((element) => {
    if (element instanceof HTMLButtonElement) {
      element.disabled = preferred === null;
    }
  });
}
function getCurrentLocalUrl() {
  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}
function getRecordEditorReturnStateKey(projectId) {
  return `${recordEditorReturnStateStoragePrefix}${projectId}`;
}
function closeRecordEditorChooser(options) {
  const settings = options || {};
  const restoreFocus = Boolean(settings.restoreFocus);
  const trigger = recordEditorState2.chooserTrigger;
  if (recordEditorState2.chooser instanceof HTMLElement) {
    recordEditorState2.chooser.remove();
  }
  recordEditorState2.chooser = null;
  recordEditorState2.chooserTrigger = null;
  if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
    trigger.focus({ preventScroll: true });
  }
}
function buildRecordEditorUrl(trigger, mode) {
  if (!(trigger instanceof HTMLElement)) {
    return "";
  }
  const rawUrl = trigger.getAttribute("data-record-editor-url") || "";
  if (!rawUrl) {
    return "";
  }
  const editorUrl = new URL(rawUrl, window.location.origin);
  editorUrl.searchParams.set("presentation", mode === "page" ? "page" : "modal");
  editorUrl.searchParams.set("returnUrl", getCurrentLocalUrl());
  return `${editorUrl.pathname}${editorUrl.search}${editorUrl.hash}`;
}
function captureRecordEditorReturnState(trigger) {
  if (!(trigger instanceof HTMLElement)) {
    return;
  }
  const projectId = Number.parseInt(trigger.getAttribute("data-record-editor-project-id") || "", 10);
  if (!Number.isInteger(projectId) || projectId <= 0) {
    return;
  }
  const scopeRoot = document.querySelector(`[data-project-detail-root][data-project-id="${CSS.escape(String(projectId))}"]`) || document.querySelector("[data-project-detail-root]");
  const baseState = buildRecordUiState(scopeRoot instanceof HTMLElement ? scopeRoot : document);
  const state = {
    projectId,
    returnUrl: getCurrentLocalUrl(),
    activeTab: baseState.activeTab,
    scrollY: baseState.scrollY,
    expandedRecordIds: Array.isArray(baseState.expandedRecordIds) ? baseState.expandedRecordIds : [],
    commentSortDirectionByRecordId: baseState.commentSortDirectionByRecordId || {},
    capturedAt: new Date().toISOString()
  };
  sessionStorage.setItem(getRecordEditorReturnStateKey(projectId), JSON.stringify(state));
}
function navigateToRecordEditorPage(trigger) {
  const targetUrl = buildRecordEditorUrl(trigger, "page");
  if (!targetUrl) {
    return;
  }
  captureRecordEditorReturnState(trigger);
  window.location.assign(targetUrl);
}
function handleRecordEditorChoice(trigger, mode, shouldSkipRemember) {
  if (!(trigger instanceof HTMLElement)) {
    return;
  }
  if (!shouldSkipRemember) {
    setStoredRecordEditorPreference(mode);
  }
  if (mode === "page") {
    navigateToRecordEditorPage(trigger);
    return;
  }
  openUrlModal(buildRecordEditorUrl(trigger, "modal"), trigger);
}
function createRecordEditorChooser(trigger) {
  const label = trigger.getAttribute("data-record-editor-label") || "Editor záznamu";
  const popover = document.createElement("div");
  popover.className = "record-editor-popover";
  popover.setAttribute("role", "dialog");
  popover.setAttribute("aria-modal", "false");
  popover.setAttribute("data-record-editor-popover", "true");
  popover.setAttribute("tabindex", "-1");
  const title = document.createElement("h3");
  title.className = "record-editor-popover-title";
  title.textContent = "Vyberte způsob otevření";
  popover.appendChild(title);
  const subtitle = document.createElement("p");
  subtitle.className = "record-editor-popover-subtitle";
  subtitle.textContent = label;
  popover.appendChild(subtitle);
  const actions = document.createElement("div");
  actions.className = "record-editor-popover-actions";
  const modalButton = document.createElement("button");
  modalButton.type = "button";
  modalButton.className = "btn small";
  modalButton.textContent = "Otevřít v modalu";
  modalButton.setAttribute("data-record-editor-mode", "modal");
  actions.appendChild(modalButton);
  const pageButton = document.createElement("button");
  pageButton.type = "button";
  pageButton.className = "btn small";
  pageButton.textContent = "Otevřít na stránce";
  pageButton.setAttribute("data-record-editor-mode", "page");
  actions.appendChild(pageButton);
  popover.appendChild(actions);
  const rememberLabel = document.createElement("label");
  rememberLabel.className = "record-editor-popover-remember";
  const rememberCheckbox = document.createElement("input");
  rememberCheckbox.type = "checkbox";
  rememberCheckbox.setAttribute("data-record-editor-remember", "true");
  rememberLabel.appendChild(rememberCheckbox);
  rememberLabel.append(" Neukládat pro tentokrát jako výchozí volbu");
  popover.appendChild(rememberLabel);
  const note = document.createElement("p");
  note.className = "record-editor-popover-note";
  note.textContent = "Pokud volbu neuložíte, systém se při dalším otevření zeptá znovu.";
  popover.appendChild(note);
  const closeButton = document.createElement("button");
  closeButton.type = "button";
  closeButton.className = "record-editor-popover-close";
  closeButton.setAttribute("aria-label", "Zavřít výběr způsobu otevření editoru");
  closeButton.textContent = "×";
  popover.appendChild(closeButton);
  popover.addEventListener("click", (event) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }
    if (target.closest(".record-editor-popover-close")) {
      event.preventDefault();
      closeRecordEditorChooser({ restoreFocus: true });
      return;
    }
    const choice = target.closest("[data-record-editor-mode]");
    if (!choice) {
      return;
    }
    event.preventDefault();
    const mode = choice.getAttribute("data-record-editor-mode");
    if (mode !== "modal" && mode !== "page") {
      return;
    }
    const skipRemember = rememberCheckbox.checked;
    closeRecordEditorChooser({ restoreFocus: false });
    handleRecordEditorChoice(trigger, mode, skipRemember);
  });
  return popover;
}
function showRecordEditorChooser(trigger) {
  if (!(trigger instanceof HTMLElement)) {
    return;
  }
  closeRecordEditorChooser({ restoreFocus: false });
  const popover = createRecordEditorChooser(trigger);
  document.body.appendChild(popover);
  positionPrintChooser(popover, trigger);
  recordEditorState2.chooser = popover;
  recordEditorState2.chooserTrigger = trigger;
  const firstAction = popover.querySelector("[data-record-editor-mode]");
  if (firstAction instanceof HTMLElement) {
    firstAction.focus({ preventScroll: true });
  } else {
    popover.focus({ preventScroll: true });
  }
}
function openRecordEditor(trigger, forcedMode) {
  if (!(trigger instanceof HTMLElement)) {
    return;
  }
  const mode = forcedMode || getStoredRecordEditorPreference();
  if (mode === "modal") {
    openUrlModal(buildRecordEditorUrl(trigger, "modal"), trigger);
    return;
  }
  if (mode === "page") {
    navigateToRecordEditorPage(trigger);
    return;
  }
  showRecordEditorChooser(trigger);
}
function restoreRecordEditorReturnStateFromUrl() {
  const projectRoot = document.querySelector("[data-project-detail-root]");
  if (!(projectRoot instanceof HTMLElement)) {
    return;
  }
  const currentUrl = new URL(window.location.href);
  if (currentUrl.searchParams.get("restoreRecordEditorState") !== "1") {
    return;
  }
  const cleanupUrl = () => {
    currentUrl.searchParams.delete("restoreRecordEditorState");
    history.replaceState(history.state || {}, "", `${currentUrl.pathname}${currentUrl.search}${currentUrl.hash}`);
  };
  const projectId = Number.parseInt(projectRoot.dataset.projectId || "", 10);
  if (!Number.isInteger(projectId) || projectId <= 0) {
    cleanupUrl();
    return;
  }
  const storageKey = getRecordEditorReturnStateKey(projectId);
  const rawState = sessionStorage.getItem(storageKey);
  if (!rawState) {
    cleanupUrl();
    return;
  }
  try {
    const state = JSON.parse(rawState);
    restoreRecordUiState(state);
  } catch {
    reportClientDiagnostic("record-editor-return-state-invalid", { projectId });
  } finally {
    sessionStorage.removeItem(storageKey);
    cleanupUrl();
  }
}
function initPermissionMetadataBindings(scope) {
  const root = scope instanceof Element ? scope : document;
  const forms = root.querySelectorAll("form");
  forms.forEach((form) => {
    if (!(form instanceof HTMLFormElement)) {
      return;
    }
    const keySelect = form.querySelector("[data-authz-permission-key]");
    const categorySelect = form.querySelector("[data-authz-permission-category]");
    const scopeSelect = form.querySelector("[data-authz-permission-scope]");
    const help = form.querySelector("[data-authz-permission-help]");
    if (!(keySelect instanceof HTMLSelectElement) || !(categorySelect instanceof HTMLSelectElement) || !(scopeSelect instanceof HTMLSelectElement)) {
      return;
    }
    if (keySelect.dataset.authzPermissionBound === "true") {
      return;
    }
    keySelect.dataset.authzPermissionBound = "true";
    const applyCatalogMetadata = () => {
      const selectedOption = keySelect.selectedOptions.length > 0 ? keySelect.selectedOptions[0] : null;
      if (!(selectedOption instanceof HTMLOptionElement)) {
        return;
      }
      const categoryId = (selectedOption.dataset.categoryId || "").trim();
      const categoryKod = (selectedOption.dataset.categoryKod || "").trim();
      const scopeLevel = (selectedOption.dataset.scopeLevel || "").trim().toUpperCase();
      const description = (selectedOption.dataset.description || "").trim();
      if (categoryId) {
        categorySelect.value = categoryId;
      }
      if (scopeLevel === "GLOBAL" || scopeLevel === "PROJECT") {
        scopeSelect.value = scopeLevel;
      }
      if (help instanceof HTMLElement) {
        if (description || categoryKod || scopeLevel) {
          const fragments = [];
          if (description) {
            fragments.push(description);
          }
          if (categoryKod) {
            fragments.push(`Kategorie: ${categoryKod}`);
          }
          if (scopeLevel) {
            fragments.push(`Rozsah: ${scopeLevel}`);
          }
          help.textContent = fragments.join(" | ");
        } else {
          help.textContent = "";
        }
      }
    };
    keySelect.addEventListener("change", applyCatalogMetadata);
    categorySelect.addEventListener("change", applyCatalogMetadata);
    scopeSelect.addEventListener("change", applyCatalogMetadata);
    applyCatalogMetadata();
  });
}
function updateTaskTypeVisibility(categorySelect) {
  if (!(categorySelect instanceof HTMLSelectElement)) {
    return;
  }
  const form = categorySelect.closest("form");
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  const topRow = form.querySelector("[data-record-row-top]");
  const typeRow = form.querySelector("[data-typ-ukolu-row]");
  const selectedLabel = categorySelect.selectedIndex >= 0 ? categorySelect.options[categorySelect.selectedIndex]?.textContent || "" : "";
  const normalized = `${categorySelect.value || ""} ${selectedLabel}`.toLowerCase();
  const isTask = normalized.includes("úkol") || normalized.includes("ukol");
  const scheduleTab = form.querySelector("[data-record-schedule-tab]");
  const schedulePanel = form.querySelector("[data-record-schedule-panel]");
  const scheduleNote = form.querySelector("[data-record-schedule-note]");
  if (topRow instanceof HTMLElement) {
    topRow.dataset.hasType = isTask ? "true" : "false";
  }
  if (!(typeRow instanceof HTMLElement)) {
    return;
  }
  const typeSelect = typeRow.querySelector("select");
  typeRow.hidden = !isTask;
  if (typeSelect instanceof HTMLSelectElement) {
    typeSelect.disabled = !isTask;
    if (!isTask) {
      typeSelect.value = "";
    }
  }
  if (scheduleTab instanceof HTMLElement) {
    scheduleTab.hidden = !isTask;
  }
  if (scheduleNote instanceof HTMLElement) {
    scheduleNote.hidden = isTask;
  }
  if (schedulePanel instanceof HTMLElement) {
    const schedulePermissionMode = (form.dataset.schedulePermissionMode || "full").toLowerCase();
    const canScheduleEditFull = schedulePermissionMode === "full";
    const canScheduleAddOnly = schedulePermissionMode === "add";
    const canScheduleAny = canScheduleEditFull || canScheduleAddOnly;
    const canEditDurationInput = (input) => {
      if (!(input instanceof HTMLInputElement)) {
        return false;
      }
      if (input.dataset.scheduleStaticDisabled === "true") {
        return false;
      }
      if (canScheduleEditFull) {
        return true;
      }
      if (!canScheduleAddOnly) {
        return false;
      }
      const originalDuration = Number.parseInt((input.dataset.scheduleOriginalDuration || "").trim(), 10);
      return !Number.isFinite(originalDuration) || originalDuration <= 0;
    };
    const syncStepperButtons = (buttonSelector, inputSelector) => {
      form.querySelectorAll(buttonSelector).forEach((button) => {
        if (!(button instanceof HTMLButtonElement)) {
          return;
        }
        const row = button.closest("[data-schedule-step-row]");
        const input = row?.querySelector(inputSelector);
        button.disabled = !(input instanceof HTMLInputElement) || input.disabled;
      });
    };
    const setScheduleDateFieldState = () => {
      form.querySelectorAll(".schedule-date-field[data-app-date-field]").forEach((dateField) => {
        if (!(dateField instanceof HTMLElement)) {
          return;
        }
        const valueInput = dateField.querySelector("[data-schedule-date], [data-schedule-delay-date]");
        const staticDisabled = valueInput instanceof HTMLInputElement && valueInput.dataset.scheduleStaticDisabled === "true";
        const isDelayDate = valueInput instanceof HTMLInputElement && valueInput.hasAttribute("data-schedule-delay-date");
        const row = dateField.closest("[data-schedule-step-row]");
        const linkedInput = row?.querySelector(isDelayDate ? "[data-schedule-delay]" : "[data-schedule-duration]");
        const linkedLocked = linkedInput instanceof HTMLInputElement ? linkedInput.disabled : true;
        const shouldDisable = !canScheduleAny || staticDisabled || linkedLocked;
        dateField.dataset.appDateLocked = shouldDisable ? "true" : "false";
        if (valueInput instanceof HTMLInputElement) {
          valueInput.disabled = shouldDisable;
        }
        const trigger = dateField.querySelector("[data-app-date-open]");
        if (trigger instanceof HTMLButtonElement) {
          trigger.disabled = shouldDisable;
        }
      });
    };
    if (!isTask) {
      schedulePanel.hidden = true;
      schedulePanel.setAttribute("data-schedule-disabled", "true");
      form.querySelectorAll("[data-schedule-duration], [data-schedule-delay]").forEach((input) => {
        if (input instanceof HTMLInputElement) {
          input.disabled = true;
        }
      });
      form.querySelectorAll("[data-schedule-duration-inc], [data-schedule-duration-dec], [data-schedule-delay-inc], [data-schedule-delay-dec]").forEach((button) => {
        if (button instanceof HTMLButtonElement) {
          button.disabled = true;
        }
      });
      setScheduleDateFieldState();
      setRecordFormTab2(form, "basic");
    } else {
      schedulePanel.removeAttribute("data-schedule-disabled");
      form.querySelectorAll("[data-schedule-duration]").forEach((input) => {
        if (input instanceof HTMLInputElement) {
          input.disabled = !canEditDurationInput(input);
        }
      });
      form.querySelectorAll("[data-schedule-delay]").forEach((input) => {
        if (input instanceof HTMLInputElement) {
          const staticDisabled = input.dataset.scheduleStaticDisabled === "true";
          input.disabled = !canScheduleAny || staticDisabled;
        }
      });
      syncStepperButtons("[data-schedule-duration-inc], [data-schedule-duration-dec]", "[data-schedule-duration]");
      syncStepperButtons("[data-schedule-delay-inc], [data-schedule-delay-dec]", "[data-schedule-delay]");
      setScheduleDateFieldState();
      if (!form._recordSchedulePlanner && canScheduleAny) {
        initRecordSchedulePlanner(form);
      }
    }
  }
  if (isTask && form._recordSchedulePlanner && typeof form._recordSchedulePlanner.recalcAll === "function") {
    form._recordSchedulePlanner.recalcAll();
  }
}
function setRecordFormTab2(form, tabKey) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  const tabs = form.querySelectorAll("[data-record-modal-tab]");
  const panels = form.querySelectorAll("[data-record-modal-panel]");
  if (tabs.length === 0 || panels.length === 0) {
    return;
  }
  const requestedTab = typeof tabKey === "string" ? tabKey : "basic";
  const requestedButton = form.querySelector(`[data-record-modal-tab="${requestedTab}"]`);
  const normalizedTab = requestedButton instanceof HTMLElement && !requestedButton.hidden ? requestedTab : "basic";
  tabs.forEach((tab) => {
    if (!(tab instanceof HTMLElement)) {
      return;
    }
    tab.classList.toggle("active", tab.dataset.recordModalTab === normalizedTab);
  });
  panels.forEach((panel) => {
    if (!(panel instanceof HTMLElement)) {
      return;
    }
    const active = panel.dataset.recordModalPanel === normalizedTab;
    panel.hidden = !active;
    panel.classList.toggle("active", active);
  });
  const activeTabInput = form.querySelector("[data-record-active-tab-input]");
  if (activeTabInput instanceof HTMLInputElement) {
    activeTabInput.value = normalizedTab;
  }
  if (normalizedTab === "schedule") {
    if (!form._recordSchedulePlanner) {
      initRecordSchedulePlanner(form);
    }
    if (form._recordSchedulePlanner && typeof form._recordSchedulePlanner.recalcAll === "function") {
      form._recordSchedulePlanner.recalcAll();
    }
    queueRecordSchedulePlannerRecalc(form, 0);
    const harmonogramPanel = form.querySelector('[data-record-modal-panel="schedule"]');
    if (harmonogramPanel instanceof HTMLElement) {
      queueRainbowSegmentRender(harmonogramPanel);
    }
    // Schedule planner přepsal hodnoty UiHarmonogramDatumy[*] a normalizoval
    // duration/delay inputy. Toto není uživatelská změna — obnovíme snapshot,
    // aby se po přepnutí na schedule tab nespouštěl close guard a modal šel zavřít.
    // Viz docs/specs/record-proposal-editor.md / modal-close-guard.
    window.requestAnimationFrame(() => {
      if (form.isConnected) {
        form.dataset.recordEditorSnapshot = buildRecordEditorFormSnapshot(form);
      }
    });
  }
}
function initExternalLinksEditors(scope) {
  scope.querySelectorAll("[data-external-links-editor]").forEach((editor) => {
    if (!(editor instanceof HTMLElement) || editor.dataset.externalLinksReady === "true") {
      return;
    }
    const rowsContainer = editor.querySelector("[data-external-links]");
    const addButton = editor.querySelector("[data-external-add]");
    const template = editor.querySelector("template[data-external-template]");
    if (!(rowsContainer instanceof HTMLElement) || !(addButton instanceof HTMLButtonElement) || !(template instanceof HTMLTemplateElement)) {
      return;
    }
    editor.dataset.externalLinksReady = "true";
    const rowNamePattern = /ExterniVazby\[\d+\]\./g;
    const estimatedPriceTypes = new Set(["PMP", "PNF"]);
    const syncEstimatedPriceField = (row) => {
      if (!(row instanceof HTMLElement)) {
        return;
      }
      const typeSelect = row.querySelector("[data-external-type-select]");
      const priceField = row.querySelector("[data-external-price-field]");
      const priceInput = row.querySelector("[data-external-price-input]");
      if (!(typeSelect instanceof HTMLSelectElement) || !(priceField instanceof HTMLElement) || !(priceInput instanceof HTMLInputElement)) {
        return;
      }
      const shouldShow = estimatedPriceTypes.has(String(typeSelect.value || "").trim().toUpperCase());
      priceField.hidden = !shouldShow;
      priceInput.disabled = !shouldShow;
      if (!shouldShow) {
        priceInput.value = "";
      }
    };
    const reindexRows = () => {
      const rows = Array.from(rowsContainer.querySelectorAll("[data-external-row]")).filter((item) => item instanceof HTMLElement);
      rows.forEach((row, index) => {
        row.querySelectorAll("[name]").forEach((field) => {
          if (!(field instanceof HTMLElement)) {
            return;
          }
          const name = field.getAttribute("name");
          if (!name) {
            return;
          }
          field.setAttribute("name", name.replace(rowNamePattern, `ExterniVazby[${index}].`));
        });
      });
    };
    const buildRowFromTemplate = (index) => {
      const html = template.innerHTML.replace(/__index__/g, String(index)).trim();
      if (!html) {
        return null;
      }
      const wrapper = document.createElement("div");
      wrapper.innerHTML = html;
      const row = wrapper.firstElementChild;
      return row instanceof HTMLElement ? row : null;
    };
    addButton.addEventListener("click", () => {
      const index = rowsContainer.querySelectorAll("[data-external-row]").length;
      const row = buildRowFromTemplate(index);
      if (!(row instanceof HTMLElement)) {
        return;
      }
      rowsContainer.appendChild(row);
      initCustomDatePickers(row);
      syncEstimatedPriceField(row);
      reindexRows();
    });
    rowsContainer.querySelectorAll("[data-external-row]").forEach((row) => {
      if (row instanceof HTMLElement) {
        syncEstimatedPriceField(row);
      }
    });
    rowsContainer.addEventListener("change", (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      const typeSelect = target.closest("[data-external-type-select]");
      if (!(typeSelect instanceof HTMLSelectElement)) {
        return;
      }
      syncEstimatedPriceField(typeSelect.closest("[data-external-row]"));
    });
    rowsContainer.addEventListener("click", (event) => {
      const target = event.target;
      if (!(target instanceof Element)) {
        return;
      }
      const removeButton = target.closest("[data-external-remove]");
      if (!(removeButton instanceof HTMLButtonElement)) {
        return;
      }
      const row = removeButton.closest("[data-external-row]");
      if (!(row instanceof HTMLElement)) {
        return;
      }
      row.remove();
      reindexRows();
    });
    reindexRows();
  });
}
function initRecordOwnerAutofill(scope) {
  scope.querySelectorAll('form[data-record-owner-autofill="true"]').forEach((form) => {
    if (!(form instanceof HTMLFormElement) || form.dataset.recordOwnerAutofillReady === "true") {
      return;
    }
    const subsystemSelect = form.querySelector("[data-record-subsystem-select]");
    const ownerPicker = form.querySelector("[data-record-owner-picker]");
    const ownerInput = ownerPicker?.querySelector("[data-person-picker-input]");
    const ownerHiddenInput = ownerPicker?.querySelector("[data-person-picker-hidden]");
    const ownerSource = ownerPicker?.querySelector("[data-person-picker-source]");
    if (!(subsystemSelect instanceof HTMLSelectElement) || !(ownerPicker instanceof HTMLElement)) {
      return;
    }
    form.dataset.recordOwnerAutofillReady = "true";
    const clearOwnerPicker = () => {
      if (ownerHiddenInput instanceof HTMLInputElement) {
        ownerHiddenInput.value = "";
      }
      if (ownerInput instanceof HTMLInputElement) {
        ownerInput.value = "";
        ownerInput.setCustomValidity("");
      }
    };
    const resolveSelectedOwnerId = () => {
      const selectedOption = subsystemSelect.selectedOptions[0];
      if (!(selectedOption instanceof HTMLOptionElement)) {
        return "";
      }
      const rawOwnerId = (selectedOption.dataset.ownerId || "").trim();
      const parsedOwnerId = Number.parseInt(rawOwnerId, 10);
      if (!Number.isInteger(parsedOwnerId) || parsedOwnerId <= 0) {
        return "";
      }
      return String(parsedOwnerId);
    };
    const findOwnerItem = (ownerId) => {
      if (!ownerId || !(ownerSource instanceof HTMLElement)) {
        return null;
      }
      return Array.from(ownerSource.querySelectorAll("[data-id]")).find((item) => item instanceof HTMLElement && (item.dataset.id || "").trim() === ownerId);
    };
    const applyOwnerFromSubsystem = () => {
      const ownerId = resolveSelectedOwnerId();
      if (!ownerId) {
        clearOwnerPicker();
        return;
      }
      const ownerItem = findOwnerItem(ownerId);
      if (!(ownerItem instanceof HTMLElement)) {
        clearOwnerPicker();
        return;
      }
      ownerPicker.dispatchEvent(new CustomEvent("person-picker:select-id", {
        bubbles: true,
        detail: {
          id: ownerId,
          source: "auto"
        }
      }));
      if (ownerHiddenInput instanceof HTMLInputElement) {
        ownerHiddenInput.value = ownerId;
      }
      if (ownerInput instanceof HTMLInputElement) {
        const label = (ownerItem.dataset.label || "").trim();
        const email = (ownerItem.dataset.email || "").trim();
        ownerInput.value = email ? `${label} <${email}>` : label;
        ownerInput.setCustomValidity("");
      }
    };
    subsystemSelect.addEventListener("change", () => {
      applyOwnerFromSubsystem();
    });
    form.addEventListener("submit", () => {
      if (ownerHiddenInput instanceof HTMLInputElement && !ownerHiddenInput.value.trim()) {
        applyOwnerFromSubsystem();
      }
    });
    if (ownerHiddenInput instanceof HTMLInputElement && !ownerHiddenInput.value.trim()) {
      applyOwnerFromSubsystem();
    }
  });
}
function initMeetingNumberValidation(scope) {
  scope.querySelectorAll('form[data-meeting-number-unique="true"]').forEach((form) => {
    if (!(form instanceof HTMLFormElement) || form.dataset.meetingNumberValidationReady === "true") {
      return;
    }
    const input = form.querySelector("[data-meeting-number-input]");
    if (!(input instanceof HTMLInputElement)) {
      return;
    }
    const warning = form.querySelector("[data-meeting-number-warning]");
    const submitButton = form.querySelector('button[type="submit"]');
    const existingNumbers = new Set((input.dataset.existingMeetingNumbers || "").split(",").map((value) => Number.parseInt(value.trim(), 10)).filter((value) => Number.isInteger(value) && value > 0));
    const currentMeetingNumber = Number.parseInt((input.dataset.currentMeetingNumber || "").trim(), 10);
    if (Number.isInteger(currentMeetingNumber) && currentMeetingNumber > 0) {
      existingNumbers.delete(currentMeetingNumber);
    }
    form.dataset.meetingNumberValidationReady = "true";
    const setWarningState = (isDuplicate) => {
      if (warning instanceof HTMLElement) {
        warning.hidden = !isDuplicate;
      }
      if (isDuplicate) {
        input.classList.add("field-invalid");
      } else {
        input.classList.remove("field-invalid");
      }
      if (submitButton instanceof HTMLButtonElement) {
        submitButton.disabled = isDuplicate;
      }
    };
    const validate = () => {
      const parsed = Number.parseInt((input.value || "").trim(), 10);
      const isDuplicate = Number.isInteger(parsed) && existingNumbers.has(parsed);
      if (isDuplicate) {
        input.setCustomValidity("Jednání s tímto číslem už v projektu existuje.");
      } else {
        input.setCustomValidity("");
      }
      setWarningState(isDuplicate);
    };
    input.addEventListener("input", validate);
    input.addEventListener("change", validate);
    form.addEventListener("submit", validate);
    validate();
  });
}
function initRecordFormTabs(scope) {
  scope.querySelectorAll('form[data-record-form-tabs="true"]').forEach((form) => {
    if (!(form instanceof HTMLFormElement) || form.dataset.recordFormTabsReady === "true") {
      return;
    }
    form.dataset.recordFormTabsReady = "true";
    const activeTabInput = form.querySelector("[data-record-active-tab-input]");
    const initialTab = activeTabInput instanceof HTMLInputElement ? activeTabInput.value : "basic";
    setRecordFormTab2(form, initialTab);
    form.querySelectorAll("[data-record-modal-tab]").forEach((tabButton) => {
      if (!(tabButton instanceof HTMLButtonElement)) {
        return;
      }
      tabButton.addEventListener("click", () => {
        const tabKey = tabButton.dataset.recordModalTab || "basic";
        setRecordFormTab2(form, tabKey);
      });
    });
  });
}
function initRecordGoalAutoGrow(scope) {
  scope.querySelectorAll("textarea[data-record-goal-autogrow='true']").forEach((textarea) => {
    if (!(textarea instanceof HTMLTextAreaElement) || textarea.dataset.recordGoalAutogrowReady === "true") {
      return;
    }
    textarea.dataset.recordGoalAutogrowReady = "true";
    const resize = () => {
      const minHeight = Number.parseFloat(window.getComputedStyle(textarea).minHeight || "0");
      textarea.style.height = "auto";
      const nextHeight = Math.max(textarea.scrollHeight, Number.isFinite(minHeight) ? minHeight : 0);
      textarea.style.height = `${Math.round(nextHeight)}px`;
    };
    textarea.addEventListener("input", resize);
    textarea.addEventListener("change", resize);
    window.requestAnimationFrame(resize);
  });
}
function initRecordMeetingDateSync(scope) {
  scope.querySelectorAll('form[data-record-editor-form="true"][data-is-create="true"]').forEach((form) => {
    if (!(form instanceof HTMLFormElement) || form.dataset.recordMeetingDateSyncReady === "true") {
      return;
    }
    const meetingSelect = form.querySelector("[data-record-meeting-number-select]");
    const startDateInput = form.querySelector('[data-app-date-value][name="DatumZalozeni"]');
    if (!(meetingSelect instanceof HTMLSelectElement) || !(startDateInput instanceof HTMLInputElement)) {
      return;
    }
    form.dataset.recordMeetingDateSyncReady = "true";
    const syncToSelectedMeeting = () => {
      const selectedOption = meetingSelect.options[meetingSelect.selectedIndex];
      const meetingDateIso = selectedOption?.dataset.recordMeetingDate || "";
      if (!meetingDateIso) {
        return;
      }
      setAppDateFieldValue(startDateInput, meetingDateIso);
    };
    meetingSelect.addEventListener("change", syncToSelectedMeeting);
    syncToSelectedMeeting();
  });
}
function looksLikeHtml(value) {
  return /<\s*\/?\s*[a-z][^>]*>/i.test(value || "");
}
function getOrCreateRichTextSourceContainer(form) {
  if (!(form instanceof HTMLFormElement)) {
    return null;
  }
  const existing = form.querySelector("[data-rich-text-source-container]");
  if (existing instanceof HTMLElement) {
    return existing;
  }
  const container = document.createElement("div");
  container.className = "richtext-source-container";
  container.setAttribute("data-rich-text-source-container", "true");
  container.setAttribute("aria-hidden", "true");
  form.appendChild(container);
  return container;
}
function initRichTextEditors(scope) {
  if (!(scope instanceof HTMLElement || scope instanceof Document)) {
    return;
  }
  if (typeof window.Quill !== "function") {
    return;
  }
  const quillCtor = window.Quill;
  scope.querySelectorAll("textarea[data-rich-text='true']").forEach((textarea) => {
    if (!(textarea instanceof HTMLTextAreaElement) || textarea.disabled || textarea.dataset.richTextReady === "true") {
      return;
    }
    const host = document.createElement("div");
    host.className = "richtext-host";
    textarea.insertAdjacentElement("beforebegin", host);
    const form = textarea.closest("form");
    const labelParent = textarea.closest("label");
    if (labelParent instanceof HTMLLabelElement && form instanceof HTMLFormElement) {
      const sourceContainer = getOrCreateRichTextSourceContainer(form);
      if (sourceContainer instanceof HTMLElement) {
        sourceContainer.appendChild(textarea);
      } else {
        host.appendChild(textarea);
      }
    } else {
      host.appendChild(textarea);
    }
    textarea.classList.add("richtext-source-hidden");
    textarea.setAttribute("aria-hidden", "true");
    textarea.setAttribute("tabindex", "-1");
    const editorShell = document.createElement("div");
    editorShell.className = "richtext-editor-shell";
    host.appendChild(editorShell);
    const placeholder = (textarea.getAttribute("placeholder") || "").trim();
    const quill = new quillCtor(editorShell, {
      theme: "snow",
      placeholder,
      modules: {
        toolbar: [
          ["bold", "italic", "underline"],
          ["link"],
          [{ list: "ordered" }, { list: "bullet" }],
          [{ indent: "-1" }, { indent: "+1" }]
        ]
      }
    });
    textarea.dataset.richTextReady = "true";
    textarea._richTextEditor = quill;
    const editorNode = editorShell.querySelector(".ql-editor");
    const configuredMinHeight = Number.parseFloat((textarea.dataset.richTextMinHeight || "").trim());
    const minHeight = Number.isFinite(configuredMinHeight) ? configuredMinHeight : Math.max(88, Number.parseInt(textarea.getAttribute("rows") || "4", 10) * 22);
    if (editorNode instanceof HTMLElement) {
      editorNode.style.minHeight = `${Math.round(minHeight)}px`;
    }
    const syncTextarea = () => {
      const text = (quill.getText() || "").replace(/\u00a0/g, " ").trim();
      if (!text) {
        textarea.value = "";
        return;
      }
      const html = (quill.root?.innerHTML || "").trim();
      textarea.value = html && html !== "<p><br></p>" ? html : "";
    };
    const resize = () => {
      if (!(editorNode instanceof HTMLElement)) {
        return;
      }
      editorNode.style.height = "auto";
      const nextHeight = Math.max(editorNode.scrollHeight, minHeight);
      editorNode.style.height = `${Math.round(nextHeight)}px`;
    };
    const initialValue = textarea.value || "";
    if (initialValue.trim().length > 0) {
      if (looksLikeHtml(initialValue)) {
        quill.clipboard.dangerouslyPasteHTML(initialValue);
      } else {
        quill.setText(initialValue);
      }
    } else {
      quill.setText("");
    }
    if (form instanceof HTMLFormElement) {
      form.addEventListener("submit", syncTextarea);
    }
    quill.on("text-change", () => {
      syncTextarea();
      resize();
    });
    quill.on("editor-change", () => {
      resize();
    });
    syncTextarea();
    window.requestAnimationFrame(resize);
  });
}
function initRecordFormEnhancements(scope) {
  if (!(scope instanceof HTMLElement || scope instanceof Document)) {
    return;
  }
  initCustomDatePickers(scope);
  initCustomTimePickers(scope);
  scope.querySelectorAll("[data-kategorie-select]").forEach((element) => {
    if (element instanceof HTMLSelectElement) {
      updateTaskTypeVisibility(element);
    }
  });
  initSinglePersonPickers(scope);
  initRecordOwnerAutofill(scope);
  initAdPersonPickers(scope);
  initExternalLinksEditors(scope);
  initCollabPickers(scope);
  initMeetingNumberValidation(scope);
  initConfirmSubmitToggles(scope);
  initRecordFormTabs(scope);
  initRecordGoalAutoGrow(scope);
  initRecordMeetingDateSync(scope);
  initRichTextEditors(scope);
  initRecordSchedulePlanner(scope);
  initRecordEditorDirtyTracking(scope);
}
function shouldIgnoreRecordEditorField(name) {
  if (!name) {
    return true;
  }
  const normalized = String(name).trim().toLowerCase();
  if (!normalized) {
    return true;
  }
  // UiHarmonogramDatumy jsou vypočítaná datumy, nikoli uživatelský vstup — viz modules/recordEditor.js
  if (normalized.startsWith("uiharmonogramdatumy")) {
    return true;
  }
  return normalized === "__requestverificationtoken" || normalized === "presentation" || normalized === "returnurl" || normalized === "editortab";
}
function buildRecordEditorFormSnapshot(form) {
  if (!(form instanceof HTMLFormElement)) {
    return "";
  }
  const entries = [];
  const formData = new FormData(form);
  formData.forEach((value, key) => {
    if (shouldIgnoreRecordEditorField(key)) {
      return;
    }
    const normalizedValue = value instanceof File ? value.name : String(value ?? "");
    entries.push(`${key}=${normalizedValue}`);
  });
  entries.sort();
  return entries.join("&");
}
function getRecordEditorDraftStorageKey(form) {
  if (!(form instanceof HTMLFormElement)) {
    return "";
  }
  const projectId = Number.parseInt(form.dataset.recordEditorProjectId || "", 10);
  if (!Number.isInteger(projectId) || projectId <= 0) {
    return "";
  }
  const idInput = form.querySelector('input[name="Id"]');
  const rawRecordId = idInput instanceof HTMLInputElement ? idInput.value.trim() : "";
  const recordId = rawRecordId || "new";
  const presentation = (form.dataset.recordEditorPresentation || "modal").trim().toLowerCase();
  return `${recordEditorDraftStoragePrefix}${projectId}.${recordId}.${presentation}`;
}
function buildRecordEditorDraftValues(form) {
  const values = {};
  if (!(form instanceof HTMLFormElement)) {
    return values;
  }
  const formData = new FormData(form);
  formData.forEach((value, key) => {
    if (shouldIgnoreRecordEditorField(key) || value instanceof File) {
      return;
    }
    const normalized = String(value ?? "");
    if (!Array.isArray(values[key])) {
      values[key] = [];
    }
    values[key].push(normalized);
  });
  return values;
}
function buildRecordEditorDraftSnapshotFromValues(values) {
  if (!values || typeof values !== "object") {
    return "";
  }
  const entries = [];
  Object.entries(values).forEach(([key, list]) => {
    if (shouldIgnoreRecordEditorField(key) || !Array.isArray(list)) {
      return;
    }
    list.forEach((value) => {
      entries.push(`${key}=${String(value ?? "")}`);
    });
  });
  entries.sort();
  return entries.join("&");
}
function normalizeRecordEditorDraftValues(rawValues) {
  if (!rawValues || typeof rawValues !== "object") {
    return {};
  }
  const normalized = {};
  Object.entries(rawValues).forEach(([key, list]) => {
    if (shouldIgnoreRecordEditorField(key)) {
      return;
    }
    if (Array.isArray(list)) {
      const values = list.map((item) => String(item ?? ""));
      normalized[key] = values;
      return;
    }
    normalized[key] = [String(list ?? "")];
  });
  return normalized;
}
function clearRecordEditorDraftSaveTimer(form) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  const timerId = Number.parseInt(form.dataset.recordEditorDraftTimerId || "", 10);
  if (Number.isFinite(timerId) && timerId > 0) {
    window.clearTimeout(timerId);
  }
  delete form.dataset.recordEditorDraftTimerId;
}
function clearRecordEditorDraft(form) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  clearRecordEditorDraftSaveTimer(form);
  const storageKey = getRecordEditorDraftStorageKey(form);
  if (!storageKey) {
    return;
  }
  sessionStorage.removeItem(storageKey);
}
function saveRecordEditorDraft(form) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  if (form.dataset.recordEditorNavigating === "true") {
    return;
  }
  const storageKey = getRecordEditorDraftStorageKey(form);
  if (!storageKey) {
    return;
  }
  if (!isRecordEditorFormDirty(form)) {
    sessionStorage.removeItem(storageKey);
    return;
  }
  const values = buildRecordEditorDraftValues(form);
  const snapshot = buildRecordEditorDraftSnapshotFromValues(values);
  if (!snapshot) {
    sessionStorage.removeItem(storageKey);
    return;
  }
  const payload = {
    version: 1,
    savedAtUtc: new Date().toISOString(),
    values
  };
  sessionStorage.setItem(storageKey, JSON.stringify(payload));
}
function scheduleRecordEditorDraftSave(form) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  clearRecordEditorDraftSaveTimer(form);
  const timerId = window.setTimeout(() => {
    delete form.dataset.recordEditorDraftTimerId;
    saveRecordEditorDraft(form);
  }, 1500);
  form.dataset.recordEditorDraftTimerId = String(timerId);
}
function readRecordEditorDraft(form) {
  if (!(form instanceof HTMLFormElement)) {
    return null;
  }
  const storageKey = getRecordEditorDraftStorageKey(form);
  if (!storageKey) {
    return null;
  }
  const raw = sessionStorage.getItem(storageKey);
  if (!raw) {
    return null;
  }
  try {
    const parsed = JSON.parse(raw);
    const savedAtUtc = typeof parsed.savedAtUtc === "string" ? parsed.savedAtUtc : "";
    const savedAtMs = savedAtUtc ? Date.parse(savedAtUtc) : NaN;
    if (!Number.isFinite(savedAtMs) || Date.now() - savedAtMs > recordEditorDraftTtlMs) {
      sessionStorage.removeItem(storageKey);
      return null;
    }
    const values = normalizeRecordEditorDraftValues(parsed.values);
    const snapshot = buildRecordEditorDraftSnapshotFromValues(values);
    if (!snapshot) {
      sessionStorage.removeItem(storageKey);
      return null;
    }
    return {
      key: storageKey,
      values,
      snapshot
    };
  } catch (error) {
    sessionStorage.removeItem(storageKey);
    return null;
  }
}
function setRecordEditorRichTextValue(textarea, nextValue) {
  if (!(textarea instanceof HTMLTextAreaElement)) {
    return;
  }
  const normalized = String(nextValue ?? "");
  textarea.value = normalized;
  const editor = textarea._richTextEditor;
  if (!editor) {
    return;
  }
  if (!normalized.trim()) {
    if (typeof editor.setText === "function") {
      editor.setText("");
    }
    return;
  }
  if (looksLikeHtml(normalized) && editor.clipboard && typeof editor.clipboard.dangerouslyPasteHTML === "function") {
    editor.clipboard.dangerouslyPasteHTML(normalized);
    return;
  }
  if (typeof editor.setText === "function") {
    editor.setText(normalized);
  }
}
function applyRecordEditorDraft(form, values) {
  if (!(form instanceof HTMLFormElement) || !values || typeof values !== "object") {
    return false;
  }
  const controls = Array.from(form.querySelectorAll("[name]")).filter((control) => control instanceof HTMLInputElement || control instanceof HTMLTextAreaElement || control instanceof HTMLSelectElement);
  if (controls.length === 0) {
    return false;
  }
  const groupedControls = new Map;
  controls.forEach((control) => {
    const name = control.getAttribute("name") || "";
    if (!name || shouldIgnoreRecordEditorField(name)) {
      return;
    }
    if (!groupedControls.has(name)) {
      groupedControls.set(name, []);
    }
    groupedControls.get(name).push(control);
  });
  groupedControls.forEach((group, name) => {
    const incoming = Array.isArray(values[name]) ? values[name].map((item) => String(item ?? "")) : [];
    if (group.length === 0) {
      return;
    }
    const first = group[0];
    if (first instanceof HTMLInputElement && first.type === "radio") {
      group.forEach((radio) => {
        if (radio instanceof HTMLInputElement) {
          radio.checked = incoming.includes(radio.value);
        }
      });
      return;
    }
    if (first instanceof HTMLInputElement && first.type === "checkbox") {
      group.forEach((checkbox) => {
        if (checkbox instanceof HTMLInputElement) {
          checkbox.checked = incoming.includes(checkbox.value);
        }
      });
      return;
    }
    if (first instanceof HTMLSelectElement && first.multiple) {
      const selected = new Set(incoming);
      group.forEach((selectControl) => {
        if (!(selectControl instanceof HTMLSelectElement)) {
          return;
        }
        Array.from(selectControl.options).forEach((option) => {
          option.selected = selected.has(option.value);
        });
      });
      return;
    }
    const nextValue = incoming.length > 0 ? incoming[0] : "";
    group.forEach((control) => {
      if (control instanceof HTMLTextAreaElement && control.dataset.richText === "true") {
        setRecordEditorRichTextValue(control, nextValue);
        return;
      }
      if (control instanceof HTMLInputElement || control instanceof HTMLTextAreaElement || control instanceof HTMLSelectElement) {
        control.value = nextValue;
      }
    });
  });
  controls.forEach((control) => {
    if (!(control instanceof HTMLElement)) {
      return;
    }
    control.dispatchEvent(new Event("input", { bubbles: true }));
    control.dispatchEvent(new Event("change", { bubbles: true }));
  });
  return true;
}
function maybeRestoreRecordEditorDraft(form) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  const draft = readRecordEditorDraft(form);
  if (!draft) {
    return;
  }
  const initialSnapshot = form.dataset.recordEditorSnapshot || "";
  if (!draft.snapshot || draft.snapshot === initialSnapshot) {
    clearRecordEditorDraft(form);
    return;
  }
  const shouldRestore = window.confirm("Byla nalezena rozpracovaná verze záznamu. Chcete ji obnovit?");
  if (!shouldRestore) {
    clearRecordEditorDraft(form);
    return;
  }
  applyRecordEditorDraft(form, draft.values);
}
function markRecordEditorFormClean(form) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  form.dataset.recordEditorSnapshot = buildRecordEditorFormSnapshot(form);
}
function isRecordEditorFormDirty(form) {
  if (!(form instanceof HTMLFormElement)) {
    return false;
  }
  return buildRecordEditorFormSnapshot(form) !== (form.dataset.recordEditorSnapshot || "");
}
function prepareRecordEditorFormNavigation(form) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  form.dataset.recordEditorNavigating = "true";
  clearRecordEditorDraft(form);
  markRecordEditorFormClean(form);
}
function closeRecordEditorCloseGuard(options) {
  const settings = options || {};
  const restoreFocus = Boolean(settings.restoreFocus);
  const trigger = recordEditorState2.closeGuardTrigger;
  if (recordEditorState2.closeGuard instanceof HTMLElement) {
    recordEditorState2.closeGuard.remove();
  }
  recordEditorState2.closeGuard = null;
  recordEditorState2.closeGuardTrigger = null;
  if (restoreFocus && trigger instanceof HTMLElement && trigger.isConnected) {
    trigger.focus({ preventScroll: true });
  }
}
function promptRecordEditorDiscard(form, trigger) {
  if (!(form instanceof HTMLFormElement) || !isRecordEditorFormDirty(form)) {
    return Promise.resolve(true);
  }
  closeRecordEditorCloseGuard({ restoreFocus: false });
  return new Promise((resolve) => {
    const isModalForm = modalRoot2 instanceof HTMLElement && modalRoot2.contains(form);
    const host = isModalForm ? getActiveModalContainer() : document.body;
    if (!(host instanceof HTMLElement)) {
      resolve(window.confirm("Máte neuložené změny. Chcete je zahodit?"));
      return;
    }
    const overlay = document.createElement("div");
    overlay.className = `record-editor-close-guard${isModalForm ? " record-editor-close-guard-modal" : ""}`;
    overlay.setAttribute("data-record-editor-close-guard", "true");
    const dialog = document.createElement("div");
    dialog.className = "record-editor-close-guard-dialog";
    dialog.setAttribute("role", "alertdialog");
    dialog.setAttribute("aria-modal", "true");
    dialog.setAttribute("tabindex", "-1");
    const title = document.createElement("h3");
    title.className = "record-editor-close-guard-title";
    title.textContent = "Máte neuložené změny.";
    dialog.appendChild(title);
    const text = document.createElement("p");
    text.className = "record-editor-close-guard-text";
    text.textContent = "Chcete pokračovat v úpravách, nebo změny zahodit?";
    dialog.appendChild(text);
    const actions = document.createElement("div");
    actions.className = "record-editor-close-guard-actions";
    const keepEditingButton = document.createElement("button");
    keepEditingButton.type = "button";
    keepEditingButton.className = "btn";
    keepEditingButton.textContent = "Pokračovat v úpravách";
    actions.appendChild(keepEditingButton);
    const discardButton = document.createElement("button");
    discardButton.type = "button";
    discardButton.className = "btn danger";
    discardButton.textContent = "Zahodit změny";
    actions.appendChild(discardButton);
    dialog.appendChild(actions);
    overlay.appendChild(dialog);
    const finish = (shouldDiscard, restoreFocus) => {
      closeRecordEditorCloseGuard({ restoreFocus });
      if (shouldDiscard) {
        prepareRecordEditorFormNavigation(form);
      }
      resolve(shouldDiscard);
    };
    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) {
        finish(false, true);
      }
    });
    keepEditingButton.addEventListener("click", () => finish(false, true));
    discardButton.addEventListener("click", () => finish(true, false));
    host.appendChild(overlay);
    recordEditorState2.closeGuard = overlay;
    recordEditorState2.closeGuardTrigger = trigger instanceof HTMLElement ? trigger : null;
    window.requestAnimationFrame(() => {
      keepEditingButton.focus({ preventScroll: true });
    });
  });
}
async function requestRecordEditorModalClose(trigger) {
  const editorForm = modalRoot2?.querySelector('form[data-record-editor-form="true"]');
  if (!(editorForm instanceof HTMLFormElement)) {
    closeModal();
    return;
  }
  const canClose = await promptRecordEditorDiscard(editorForm, trigger);
  if (canClose) {
    closeModal();
  }
}
async function requestRecordEditorPageCancel(trigger) {
  const editorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
  if (!(editorForm instanceof HTMLFormElement)) {
    const fallbackUrl = trigger instanceof HTMLElement ? trigger.getAttribute("data-record-editor-back-url") || window.location.href : window.location.href;
    window.location.assign(fallbackUrl);
    return;
  }
  const canClose = await promptRecordEditorDiscard(editorForm, trigger);
  if (!canClose) {
    return;
  }
  const targetUrl = editorForm.dataset.recordEditorBackUrl || (trigger instanceof HTMLElement ? trigger.getAttribute("data-record-editor-back-url") : "") || window.location.href;
  window.location.assign(targetUrl);
}
function initRecordEditorDirtyTracking(scope) {
  if (!(scope instanceof HTMLElement || scope instanceof Document)) {
    return;
  }
  scope.querySelectorAll('form[data-record-editor-form="true"]').forEach((form) => {
    if (!(form instanceof HTMLFormElement) || form.dataset.recordEditorDirtyReady === "true") {
      return;
    }
    form.dataset.recordEditorDirtyReady = "true";
    form.dataset.recordEditorNavigating = "false";
    form.addEventListener("submit", () => {
      form.dataset.recordEditorNavigating = "true";
      clearRecordEditorDraftSaveTimer(form);
    });
    form.addEventListener("input", () => {
      scheduleRecordEditorDraftSave(form);
    });
    form.addEventListener("change", () => {
      scheduleRecordEditorDraftSave(form);
    });
    window.requestAnimationFrame(() => {
      if (form.isConnected) {
        form.dataset.recordEditorSnapshot = buildRecordEditorFormSnapshot(form);
        maybeRestoreRecordEditorDraft(form);
        form.dataset.recordEditorNavigating = "false";
      }
    });
  });
}
function resolveRecordEditorTabForFieldKey(rawKey) {
  const normalizedKey = normalizeServerFieldKey(rawKey).toLowerCase();
  if (!normalizedKey) {
    return "";
  }
  if (normalizedKey.startsWith("externivazby[")) {
    return "external";
  }
  if (normalizedKey.startsWith("vybranispolupracovniciids")) {
    return "collaboration";
  }
  if (normalizedKey.startsWith("harmonogramhodnoty[") || normalizedKey.startsWith("uiharmonogramdatumy[") || normalizedKey.startsWith("uiharmonogramposunutedatumy[")) {
    return "schedule";
  }
  const basicPrefixes = [
    "kategorie",
    "typukolu",
    "stav",
    "nazev",
    "cil",
    "popis",
    "vlastnikid",
    "datumzalozeni",
    "terminukonceni",
    "subsystem",
    "jednaniidprocislo"
  ];
  return basicPrefixes.some((prefix) => normalizedKey.startsWith(prefix)) ? "basic" : "";
}
function resolveRecordEditorTabLabel(tabKey) {
  switch ((tabKey || "").toLowerCase()) {
    case "basic":
      return "Základní údaje";
    case "external":
      return "Externí vazby";
    case "collaboration":
      return "Spolupráce";
    case "schedule":
      return "Harmonogram";
    default:
      return "";
  }
}
function resolveRecordEditorFieldLabel(rawKey) {
  const normalizedKey = normalizeServerFieldKey(rawKey);
  if (!normalizedKey) {
    return "";
  }
  const lower = normalizedKey.toLowerCase();
  const direct = {
    kategorie: "Kategorie záznamu",
    typukolu: "Typ úkolu",
    stav: "Stav úkolu",
    nazev: "Název",
    cil: "Cíl",
    popis: "Popis",
    vlastnikid: "Vlastník",
    datumzalozeni: "Datum založení",
    terminukonceni: "Termín ukončení",
    subsystem: "Subsystém",
    jednaniidprocislo: "Jednání pro identifikátor",
    vybranispolupracovniciids: "Spolupráce",
    harmonogramhodnoty: "Harmonogram"
  };
  if (direct[lower]) {
    return direct[lower];
  }
  const externalMatch = /^externivazby\[(\d+)\]\.([a-z0-9_]+)$/i.exec(normalizedKey);
  if (externalMatch) {
    const row = Number.parseInt(externalMatch[1], 10) + 1;
    const fieldRaw = externalMatch[2].toLowerCase();
    const fieldLabelByKey = {
      typ: "Typ odkazu",
      cislo: "Číslo",
      predpokladanacena: "Předpokládaná cena",
      vyzva: "Výzva",
      datumobjednani: "Datum objednání",
      plandodani: "Plán dodání",
      datumdodani: "Datum dodání",
      datumprevzeti: "Datum převzetí"
    };
    const fieldLabel = fieldLabelByKey[fieldRaw] || externalMatch[2];
    return `Řádek ${row}: ${fieldLabel}`;
  }
  const scheduleMatch = /^harmonogramhodnoty\[(\d+)\]\.([a-z0-9_]+)$/i.exec(normalizedKey);
  if (scheduleMatch) {
    const row = Number.parseInt(scheduleMatch[1], 10) + 1;
    const fieldRaw = scheduleMatch[2].toLowerCase();
    const fieldLabelByKey = {
      typid: "Typ kroku",
      hodnota: "Hodnota"
    };
    const fieldLabel = fieldLabelByKey[fieldRaw] || scheduleMatch[2];
    return `Řádek ${row}: ${fieldLabel}`;
  }
  return normalizedKey;
}
function buildContextualSummaryMessage(rawKey, message) {
  const trimmedMessage = String(message || "").trim();
  if (!trimmedMessage) {
    return "";
  }
  const tabKey = resolveRecordEditorTabForFieldKey(rawKey);
  const tabLabel = resolveRecordEditorTabLabel(tabKey);
  const fieldLabel = resolveRecordEditorFieldLabel(rawKey);
  const context = [tabLabel, fieldLabel].filter(Boolean).join(" / ");
  return context ? `[${context}] ${trimmedMessage}` : trimmedMessage;
}
function normalizeServerFieldKey(rawKey) {
  if (!rawKey) {
    return "";
  }
  const key = String(rawKey).trim();
  const dotIndex = key.indexOf(".");
  if (dotIndex > 0) {
    const prefix = key.slice(0, dotIndex);
    if (/^[a-zA-Z][a-zA-Z0-9]*$/.test(prefix) && (prefix.toLowerCase() === "command" || prefix.toLowerCase().endsWith("command"))) {
      return key.slice(dotIndex + 1);
    }
  }
  return key;
}

// PmTracker.Web/wwwroot/js/modules/ajax.js
var sessionExpiredErrorCode = "SESSION_EXPIRED";
function resolveErrorTarget(field, form) {
  if (!(field instanceof HTMLElement)) {
    return null;
  }
  if (field instanceof HTMLInputElement && field.type === "hidden") {
    if (field.matches("[data-person-picker-hidden]")) {
      const pickerInput = field.closest('[data-person-picker="single"]')?.querySelector("[data-person-picker-input]");
      if (pickerInput instanceof HTMLElement) {
        return pickerInput;
      }
    }
    if (field.matches("[data-ad-guid], [data-ad-query-hidden]")) {
      const adInput = form.querySelector("[data-ad-query-input]");
      if (adInput instanceof HTMLElement) {
        return adInput;
      }
    }
    if (field.matches("[data-app-date-value]")) {
      const displayInput = field.closest("[data-app-date-field]")?.querySelector("[data-app-date-display]");
      if (displayInput instanceof HTMLElement) {
        return displayInput;
      }
    }
    if (field.matches("[data-app-time-value]")) {
      const displayInput = field.closest("[data-app-time-field]")?.querySelector("[data-app-time-display]");
      if (displayInput instanceof HTMLElement) {
        return displayInput;
      }
    }
  }
  return field;
}
function clearModalFormErrors(form) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  form.querySelectorAll(".modal-submit-summary").forEach((node) => node.remove());
  form.querySelectorAll(".field-error-message").forEach((node) => node.remove());
  form.querySelectorAll(".field-invalid").forEach((node) => {
    if (node instanceof HTMLElement) {
      node.classList.remove("field-invalid");
      node.removeAttribute("aria-invalid");
    }
  });
}
function findFieldByName(form, rawKey) {
  if (!(form instanceof HTMLFormElement)) {
    return null;
  }
  const normalizedKey = normalizeServerFieldKey(rawKey);
  if (!normalizedKey) {
    return null;
  }
  const controls = Array.from(form.querySelectorAll("[name]")).filter((candidate) => candidate instanceof HTMLInputElement || candidate instanceof HTMLSelectElement || candidate instanceof HTMLTextAreaElement);
  const normalizedLower = normalizedKey.toLowerCase();
  const exactMatch = controls.find((candidate) => {
    const fieldName = candidate.getAttribute("name");
    return fieldName && fieldName.toLowerCase() === normalizedLower;
  });
  if (exactMatch instanceof HTMLElement) {
    return exactMatch;
  }
  const suffixMatch = controls.find((candidate) => {
    const fieldName = (candidate.getAttribute("name") || "").toLowerCase();
    return fieldName.endsWith(`.${normalizedLower}`);
  });
  return suffixMatch instanceof HTMLElement ? suffixMatch : null;
}
function renderModalFormErrors(form, payload) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  clearModalFormErrors(form);
  const fieldErrors = payload && typeof payload === "object" && payload.fieldErrors && typeof payload.fieldErrors === "object" ? payload.fieldErrors : {};
  const summaryMessages = new Set;
  const invalidTargets = [];
  let firstInvalidTab = "";
  Object.entries(fieldErrors).forEach(([rawKey, messages]) => {
    if (!Array.isArray(messages) || messages.length === 0) {
      return;
    }
    const normalizedMessages = messages.map((value) => String(value || "").trim()).filter((value) => value.length > 0);
    if (normalizedMessages.length === 0) {
      return;
    }
    const field = findFieldByName(form, rawKey);
    if (!(field instanceof HTMLElement)) {
      if (!firstInvalidTab) {
        firstInvalidTab = resolveRecordEditorTabForFieldKey(rawKey);
      }
      normalizedMessages.forEach((message) => {
        const contextual = buildContextualSummaryMessage(rawKey, message);
        if (contextual) {
          summaryMessages.add(contextual);
        }
      });
      return;
    }
    if (!firstInvalidTab) {
      firstInvalidTab = resolveRecordEditorTabForFieldKey(rawKey);
    }
    const target = resolveErrorTarget(field, form) || field;
    target.classList.add("field-invalid");
    target.setAttribute("aria-invalid", "true");
    invalidTargets.push(target);
    const errorHost = target.closest("label") || target.closest(".office-picker") || target.parentElement || form;
    if (!(errorHost instanceof HTMLElement)) {
      normalizedMessages.forEach((message) => {
        const contextual = buildContextualSummaryMessage(rawKey, message);
        if (contextual) {
          summaryMessages.add(contextual);
        }
      });
      return;
    }
    const errorLine = document.createElement("div");
    errorLine.className = "field-error-message";
    errorLine.textContent = normalizedMessages.join(" ");
    errorHost.appendChild(errorLine);
    normalizedMessages.forEach((message) => {
      const contextual = buildContextualSummaryMessage(rawKey, message);
      if (contextual) {
        summaryMessages.add(contextual);
      }
    });
  });
  const topMessage = payload && typeof payload.message === "string" ? payload.message.trim() : "";
  const errorCode = payload && typeof payload.errorCode === "string" ? payload.errorCode.trim() : "";
  const traceId = payload && typeof payload.traceId === "string" ? payload.traceId.trim() : "";
  const diagnosticLog = payload && typeof payload.diagnosticLog === "string" ? payload.diagnosticLog.trim() : "";
  if (topMessage || summaryMessages.size > 0 || errorCode || traceId || diagnosticLog) {
    const summary = document.createElement("div");
    summary.className = "alert alert-error modal-submit-summary";
    summary.setAttribute("role", "alert");
    const merged = [];
    if (topMessage) {
      merged.push(topMessage);
    }
    merged.push(...Array.from(summaryMessages));
    const summaryText = merged.filter(Boolean).join(" | ");
    if (summaryText) {
      const messageLine = document.createElement("div");
      messageLine.className = "modal-submit-summary-text";
      messageLine.textContent = summaryText;
      summary.appendChild(messageLine);
    }
    if (errorCode || traceId) {
      const metaLine = document.createElement("div");
      metaLine.className = "modal-submit-meta";
      const metaParts = [];
      if (errorCode) {
        metaParts.push(`Kód chyby: ${errorCode}`);
      }
      if (traceId) {
        metaParts.push(`TraceId: ${traceId}`);
      }
      metaLine.textContent = metaParts.join(" | ");
      summary.appendChild(metaLine);
    }
    const normalizedErrorCode = (errorCode || "").toUpperCase();
    if (normalizedErrorCode === sessionStaleErrorCode || normalizedErrorCode === sessionExpiredErrorCode) {
      const recoveryActions = document.createElement("div");
      recoveryActions.className = "modal-submit-diagnostics-actions";
      const reloadButton = document.createElement("button");
      reloadButton.type = "button";
      reloadButton.className = "btn small";
      reloadButton.textContent = "Obnovit stránku";
      reloadButton.addEventListener("click", () => {
        window.location.reload();
      });
      recoveryActions.appendChild(reloadButton);
      summary.appendChild(recoveryActions);
    }
    if (diagnosticLog) {
      const diagnosticBlock = document.createElement("details");
      diagnosticBlock.className = "modal-submit-diagnostics";
      const diagnosticSummary = document.createElement("summary");
      diagnosticSummary.textContent = "Diagnostický log";
      diagnosticBlock.appendChild(diagnosticSummary);
      const actions = document.createElement("div");
      actions.className = "modal-submit-diagnostics-actions";
      const copyButton = document.createElement("button");
      copyButton.type = "button";
      copyButton.className = "btn small ghost";
      copyButton.textContent = "Kopírovat log";
      copyButton.addEventListener("click", async () => {
        const copied = await copyTextToClipboard(diagnosticLog);
        copyButton.textContent = copied ? "Zkopírováno" : "Kopírování selhalo";
        window.setTimeout(() => {
          copyButton.textContent = "Kopírovat log";
        }, 1800);
      });
      actions.appendChild(copyButton);
      diagnosticBlock.appendChild(actions);
      const logPre = document.createElement("pre");
      logPre.className = "modal-submit-diagnostics-log";
      logPre.textContent = diagnosticLog;
      diagnosticBlock.appendChild(logPre);
      summary.appendChild(diagnosticBlock);
    }
    form.insertBefore(summary, form.firstElementChild);
    summary.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }
  if (firstInvalidTab) {
    setRecordFormTab(form, firstInvalidTab);
  }
  if (invalidTargets.length > 0 && invalidTargets[0] instanceof HTMLElement) {
    invalidTargets[0].focus();
  }
}
function syncSinglePersonPickerInForm(form, wrapper) {
  if (!(form instanceof HTMLFormElement) || !(wrapper instanceof HTMLElement)) {
    return;
  }
  const hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
  const queryInput = wrapper.querySelector("[data-person-picker-input]");
  const source = wrapper.querySelector("[data-person-picker-source]");
  if (!(hiddenInput instanceof HTMLInputElement) || !(queryInput instanceof HTMLInputElement) || !(source instanceof HTMLElement)) {
    return;
  }
  if (hiddenInput.value.trim()) {
    queryInput.setCustomValidity("");
    return;
  }
  const query = normalizeSearchText(queryInput.value || "");
  if (!query) {
    return;
  }
  const candidates = Array.from(source.querySelectorAll("[data-id]")).filter((entry) => entry instanceof HTMLElement).map((entry) => {
    const id = (entry.dataset.id || "").trim();
    const label = (entry.dataset.label || "").trim();
    const email = (entry.dataset.email || "").trim();
    if (!id || !label) {
      return null;
    }
    const display = email ? `${label} <${email}>` : label;
    return { id, label, email, display };
  }).filter((entry) => entry !== null);
  const matches = candidates.filter((entry) => {
    const normalizedLabel = normalizeSearchText(entry.label);
    const normalizedEmail = normalizeSearchText(entry.email);
    const normalizedDisplay = normalizeSearchText(entry.display);
    return normalizedLabel === query || normalizedEmail === query || normalizedDisplay === query;
  });
  if (matches.length === 1) {
    const match = matches[0];
    hiddenInput.value = match.id;
    queryInput.value = match.display;
    queryInput.setCustomValidity("");
  }
}
function validateRequiredPersonPickers(form) {
  if (!(form instanceof HTMLFormElement)) {
    return true;
  }
  let firstInvalidInput = null;
  form.querySelectorAll('[data-person-picker="single"]').forEach((wrapper) => {
    if (!(wrapper instanceof HTMLElement)) {
      return;
    }
    syncSinglePersonPickerInForm(form, wrapper);
    const hiddenInput = wrapper.querySelector("[data-person-picker-hidden]");
    const queryInput = wrapper.querySelector("[data-person-picker-input]");
    if (!(hiddenInput instanceof HTMLInputElement) || !(queryInput instanceof HTMLInputElement)) {
      return;
    }
    const isRequired = hiddenInput.required || queryInput.required;
    if (!isRequired) {
      queryInput.setCustomValidity("");
      return;
    }
    if (!hiddenInput.value.trim()) {
      queryInput.setCustomValidity("Vyberte osobu ze seznamu výsledků.");
      if (!(firstInvalidInput instanceof HTMLInputElement)) {
        firstInvalidInput = queryInput;
      }
    } else {
      queryInput.setCustomValidity("");
    }
  });
  if (firstInvalidInput instanceof HTMLInputElement) {
    firstInvalidInput.reportValidity();
    firstInvalidInput.focus();
    return false;
  }
  return true;
}
function initConfirmSubmitToggles(scope) {
  if (!(scope instanceof HTMLElement || scope instanceof Document)) {
    return;
  }
  scope.querySelectorAll('form[data-confirm-submit-toggle="true"]').forEach((form) => {
    if (!(form instanceof HTMLFormElement) || form.dataset.confirmSubmitReady === "true") {
      return;
    }
    const checkbox = form.querySelector("[data-confirm-submit-checkbox]");
    const submit = form.querySelector('[data-confirm-submit-button], button[type="submit"], input[type="submit"]');
    if (!(checkbox instanceof HTMLInputElement) || checkbox.type !== "checkbox" || !(submit instanceof HTMLButtonElement || submit instanceof HTMLInputElement)) {
      return;
    }
    const sync = () => {
      submit.disabled = !checkbox.checked;
    };
    checkbox.addEventListener("change", sync);
    sync();
    form.dataset.confirmSubmitReady = "true";
  });
}
function setFormSubmitting(form, submitting) {
  if (!(form instanceof HTMLFormElement)) {
    return;
  }
  const confirmationCheckbox = form.querySelector("[data-confirm-submit-checkbox]");
  const hasConfirmationGate = form.dataset.confirmSubmitToggle === "true" && confirmationCheckbox instanceof HTMLInputElement && confirmationCheckbox.type === "checkbox";
  form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach((element) => {
    if (element instanceof HTMLButtonElement || element instanceof HTMLInputElement) {
      if (submitting) {
        element.disabled = true;
        return;
      }
      if (hasConfirmationGate && !confirmationCheckbox.checked) {
        element.disabled = true;
        return;
      }
      element.disabled = false;
    }
  });
}
function buildSessionStalePayload(action, method, requestFormSnapshot, details, isRecordEditorForm) {
  const traceId = sessionState.lastTraceId || "";
  const lines = [
    `TimestampUtc: ${new Date().toISOString()}`,
    `ErrorCode: ${sessionStaleErrorCode}`,
    `TraceId: ${traceId || "-"}`,
    "ClientSource: site.js:SessionCoordinator",
    `Request: ${(method || "POST").toUpperCase()} ${action || window.location.href}`,
    `LastKeepAliveSuccessUtc: ${sessionState.lastSuccessUtc || "-"}`,
    `KeepAliveFailuresInRow: ${sessionState.consecutiveFailures}`,
    `KeepAliveLastFailure: ${sessionState.lastFailureReason || "-"}`,
    `RecordEditorForm: ${isRecordEditorForm ? "true" : "false"}`,
    "RequestFormData:",
    requestFormSnapshot || "<unavailable>"
  ];
  if (details) {
    lines.push("Details:", String(details));
  }
  const message = isRecordEditorForm ? "Relace vypršela během úprav. Uložení je zablokováno, obnovte stránku. Rozpracovaný návrh záznamu zůstává uložen." : "Relace vypršela během úprav. Uložení je zablokováno, obnovte stránku a akci opakujte.";
  return {
    ok: false,
    message,
    errorCode: sessionStaleErrorCode,
    traceId,
    diagnosticLog: lines.join(`
`),
    fieldErrors: {}
  };
}
function shouldAttemptSessionRecovery(payload) {
  if (!isPlainObject(payload)) {
    return false;
  }
  const code = typeof payload.errorCode === "string" ? payload.errorCode.trim().toUpperCase() : "";
  if (!code) {
    return false;
  }
  return code === "REQUEST_VALIDATION_FAILED" || code === sessionExpiredErrorCode || code === "HTTP_401" || code === "HTTP_403" || code === "NON_JSON_RESPONSE" || code === "EMPTY_AJAX_RESPONSE";
}
function buildAjaxDiagnosticLines(errorCode, traceId, action, method, response, contentType, rawBody, requestFormSnapshot, extraLines) {
  const status = response instanceof Response ? response.status : 0;
  const statusText = response instanceof Response ? response.statusText || "" : "";
  const responseUrl = response instanceof Response ? response.url || action || window.location.href : action || window.location.href;
  const body = truncateDiagnosticBody(rawBody, 12000);
  const redirected = response instanceof Response ? response.redirected : false;
  const headersSnapshot = buildResponseHeadersSnapshot(response, 80);
  const lines = [
    `TimestampUtc: ${new Date().toISOString()}`,
    `ErrorCode: ${errorCode}`,
    `TraceId: ${traceId || "-"}`,
    "ClientSource: site.js:initModalAjaxSubmit",
    "ExpectedContract: ModalSubmitResultViewModel { ok:boolean, message?, errorCode?, traceId?, diagnosticLog?, fieldErrors? }",
    `Request: ${(method || "POST").toUpperCase()} ${action || window.location.href}`,
    `ResponseUrl: ${responseUrl}`,
    `Status: ${status}${statusText ? ` ${statusText}` : ""}`,
    `Redirected: ${redirected ? "true" : "false"}`,
    `ContentType: ${contentType || "-"}`,
    "ResponseHeaders:",
    headersSnapshot,
    "RequestFormData:",
    requestFormSnapshot || "<unavailable>",
    "Body:",
    body || "<empty>"
  ];
  if (Array.isArray(extraLines) && extraLines.length > 0) {
    lines.push(...extraLines.filter((line) => String(line || "").trim().length > 0));
  }
  return lines;
}
function buildNonJsonAjaxFailureMessage(response, rawBody) {
  const status = response instanceof Response ? response.status : 0;
  const body = String(rawBody || "").toLowerCase();
  if (status === 401 || status === 403) {
    return "Relace vypršela nebo nemáte oprávnění. Obnovte stránku a zkuste akci znovu.";
  }
  if (status === 400 && (body.includes("antiforgery") || body.includes("requestverificationtoken") || body.includes("csrf"))) {
    return "Bezpečnostní token formuláře vypršel nebo je neplatný. Obnovte stránku a akci opakujte.";
  }
  if (status >= 500) {
    return "Server během zpracování požadavku selhal. Zkuste akci opakovat.";
  }
  return "Server vrátil neočekávanou odpověď.";
}
function buildNonJsonAjaxErrorPayload(response, action, method, contentType, rawBody, requestFormSnapshot) {
  const traceId = resolveAjaxResponseTraceId(response);
  const isEmptyBody = String(rawBody || "").trim().length === 0;
  const errorCode = isEmptyBody ? "EMPTY_AJAX_RESPONSE" : "NON_JSON_RESPONSE";
  const message = buildNonJsonAjaxFailureMessage(response, rawBody);
  const diagnosticLines = buildAjaxDiagnosticLines(errorCode, traceId, action, method, response, contentType, rawBody, requestFormSnapshot, null);
  const payload = {
    ok: false,
    message,
    errorCode,
    traceId,
    diagnosticLog: diagnosticLines.join(`
`),
    fieldErrors: {}
  };
  if (message.includes("token formuláře")) {
    payload.fieldErrors = {
      __RequestVerificationToken: [
        "Token formuláře není platný nebo vypršel."
      ]
    };
  }
  return payload;
}
function buildUnexpectedJsonContractPayload(response, action, method, contentType, rawBody, parsedPayload, requestFormSnapshot) {
  const plainPayload = isPlainObject(parsedPayload) ? parsedPayload : {};
  const traceId = resolveAjaxResponseTraceId(response);
  const payloadKeys = Object.keys(plainPayload);
  const messageFromPayload = typeof plainPayload.message === "string" ? plainPayload.message.trim() : typeof plainPayload.error === "string" ? plainPayload.error.trim() : "";
  const message = messageFromPayload || "Server vrátil nečekaný JSON kontrakt.";
  const errorCode = "UNEXPECTED_JSON_CONTRACT";
  const diagnosticLines = buildAjaxDiagnosticLines(errorCode, traceId, action, method, response, contentType, rawBody, requestFormSnapshot, [
    `PayloadKeys: ${payloadKeys.length > 0 ? payloadKeys.join(", ") : "-"}`,
    `PayloadType: ${Array.isArray(parsedPayload) ? "array" : typeof parsedPayload}`
  ]);
  return {
    ok: false,
    message,
    errorCode,
    traceId,
    diagnosticLog: diagnosticLines.join(`
`),
    fieldErrors: {}
  };
}
function ensureAjaxErrorPayloadDiagnostics(parsedPayload, response, action, method, contentType, rawBody, requestFormSnapshot) {
  if (!isPlainObject(parsedPayload)) {
    return buildNonJsonAjaxErrorPayload(response, action, method, contentType, rawBody, requestFormSnapshot);
  }
  if (typeof parsedPayload.ok !== "boolean") {
    return buildUnexpectedJsonContractPayload(response, action, method, contentType, rawBody, parsedPayload, requestFormSnapshot);
  }
  if (parsedPayload.ok === true) {
    return parsedPayload;
  }
  const normalized = { ...parsedPayload };
  if (!isPlainObject(normalized.fieldErrors)) {
    normalized.fieldErrors = {};
  }
  if (typeof normalized.errorCode !== "string" || !normalized.errorCode.trim()) {
    const status = response instanceof Response ? response.status : 0;
    normalized.errorCode = status > 0 ? `HTTP_${status}` : "AJAX_OPERATION_FAILED";
  }
  if (typeof normalized.traceId !== "string" || !normalized.traceId.trim()) {
    normalized.traceId = resolveAjaxResponseTraceId(response);
  }
  if (typeof normalized.diagnosticLog !== "string" || !normalized.diagnosticLog.trim()) {
    const diagnosticLines = buildAjaxDiagnosticLines(normalized.errorCode, normalized.traceId, action, method, response, contentType, rawBody, requestFormSnapshot, [
      "Details:",
      "Server error payload did not include diagnosticLog.",
      `PayloadKeys: ${Object.keys(parsedPayload).join(", ") || "-"}`
    ]);
    normalized.diagnosticLog = diagnosticLines.join(`
`);
  }
  return normalized;
}
function buildAjaxExceptionPayload(error, action, method, requestFormSnapshot) {
  const errorCode = "CLIENT_AJAX_EXCEPTION";
  const exceptionName = error instanceof Error ? error.name : "UnknownError";
  const exceptionMessage = error instanceof Error ? error.message : String(error || "Unknown error");
  const exceptionStack = error instanceof Error && typeof error.stack === "string" ? truncateDiagnosticBody(error.stack, 12000) : "";
  const diagnosticLines = [
    `TimestampUtc: ${new Date().toISOString()}`,
    `ErrorCode: ${errorCode}`,
    "TraceId: -",
    "ClientSource: site.js:initModalAjaxSubmit",
    "ExpectedContract: ModalSubmitResultViewModel { ok:boolean, message?, errorCode?, traceId?, diagnosticLog?, fieldErrors? }",
    `Request: ${(method || "POST").toUpperCase()} ${action || window.location.href}`,
    `NavigatorOnline: ${navigator.onLine ? "true" : "false"}`,
    "RequestFormData:",
    requestFormSnapshot || "<unavailable>",
    `ExceptionName: ${exceptionName}`,
    `ExceptionMessage: ${exceptionMessage}`
  ];
  if (exceptionStack) {
    diagnosticLines.push("ExceptionStack:", exceptionStack);
  }
  return {
    ok: false,
    message: "Požadavek se nepodařilo dokončit na klientu.",
    errorCode,
    traceId: "",
    diagnosticLog: diagnosticLines.join(`
`),
    fieldErrors: {}
  };
}
function initModalAjaxSubmit() {
  if (!(document.body instanceof HTMLElement) || document.body.dataset.modalAjaxReady === "true") {
    return;
  }
  document.body.dataset.modalAjaxReady = "true";
  document.addEventListener("submit", async (event) => {
    const target = event.target;
    if (!(target instanceof HTMLFormElement) || target.dataset.ajaxSubmit !== "true") {
      return;
    }
    const isModalForm = target.closest(".modal-overlay") instanceof HTMLElement;
    if (event.defaultPrevented) {
      return;
    }
    event.preventDefault();
    clearModalFormErrors(target);
    const isRecordEditorForm = target.matches('[data-record-editor-form="true"]');
    const submitter = event instanceof SubmitEvent ? event.submitter : null;
    if (!validateRequiredPersonPickers(target)) {
      return;
    }
    if (!target.checkValidity()) {
      target.reportValidity();
      return;
    }
    const submitterAction = submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement ? submitter.getAttribute("formaction") || "" : "";
    const submitterMethod = submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement ? submitter.getAttribute("formmethod") || "" : "";
    const action = appendCurrentAsUser(submitterAction || target.getAttribute("action") || window.location.href);
    const method = (submitterMethod || target.getAttribute("method") || "post").toUpperCase();
    const blockedSnapshot = buildFormDataSnapshot(new FormData(target, submitter instanceof HTMLElement ? submitter : undefined), 120);
    if (sessionState.stale) {
      target.dataset.recordEditorNavigating = "false";
      renderModalFormErrors(target, buildSessionStalePayload(action, method, blockedSnapshot, "Submit blocked because session is stale.", isRecordEditorForm));
      return;
    }
    setFormSubmitting(target, true);
    try {
      const executeSubmitAttempt = async () => {
        const formData = new FormData(target, submitter instanceof HTMLElement ? submitter : undefined);
        const requestFormSnapshot = buildFormDataSnapshot(formData, 120);
        const response = await fetch(action, {
          method,
          body: formData,
          headers: {
            "X-Requested-With": "XMLHttpRequest"
          },
          credentials: "same-origin"
        });
        const contentType = (response.headers.get("content-type") || "").toLowerCase();
        const rawBody = await response.text();
        const parsedPayload = parseJsonPayload(rawBody);
        const payload = ensureAjaxErrorPayloadDiagnostics(parsedPayload, response, action, method, contentType, rawBody, requestFormSnapshot);
        return {
          response,
          payload,
          requestFormSnapshot
        };
      };
      let result = await executeSubmitAttempt();
      const firstAttemptFailed = !result.response.ok || !result.payload || result.payload.ok !== true;
      if (firstAttemptFailed && shouldAttemptSessionRecovery(result.payload)) {
        const recovered = await ensureSessionKeepAlive("submit-recovery", true);
        if (recovered) {
          result = await executeSubmitAttempt();
        }
      }
      if (sessionState.stale) {
        target.dataset.recordEditorNavigating = "false";
        renderModalFormErrors(target, buildSessionStalePayload(action, method, result.requestFormSnapshot, "Submit blocked after repeated keepalive failures.", isRecordEditorForm));
        return;
      }
      if (!result.response.ok || !result.payload || result.payload.ok !== true) {
        target.dataset.recordEditorNavigating = "false";
        renderModalFormErrors(target, result.payload || { message: "Uložení se nezdařilo." });
        return;
      }
      if (isRecordEditorForm) {
        clearRecordEditorDraft(target);
        markRecordEditorFormClean(target);
        target.dataset.recordEditorNavigating = "true";
      }
      if (isModalForm) {
        closeModal();
      }
      await refreshPageScope(result.payload);
    } catch (error) {
      target.dataset.recordEditorNavigating = "false";
      const fallbackSnapshot = buildFormDataSnapshot(new FormData(target, submitter instanceof HTMLElement ? submitter : undefined), 120);
      renderModalFormErrors(target, buildAjaxExceptionPayload(error, action, method, fallbackSnapshot));
    } finally {
      setFormSubmitting(target, false);
    }
  });
}

// PmTracker.Web/wwwroot/js/modules/theme.js
var themeStorageKey = "pmtracker.theme.mode";
var themeSwitchSelector = "[data-theme-switch]";
var govThemeSwitchTag = "gov-theme-switch";
var mediaDark = window.matchMedia("(prefers-color-scheme: dark)");
var themeCookieMaxAgeSeconds = 60 * 60 * 24 * 365;
function isThemeMode(value) {
  return value === "dark" || value === "light" || value === "auto";
}
function getStoredThemeMode() {
  const cookieValue = getThemeCookieMode();
  if (isThemeMode(cookieValue)) {
    return cookieValue;
  }
  try {
    const storedValue = localStorage.getItem(themeStorageKey);
    return isThemeMode(storedValue) ? storedValue : null;
  } catch {
    return null;
  }
}
function getThemeCookieMode() {
  const cookies = document.cookie.split(";");
  for (const entry of cookies) {
    const [rawName, rawValue = ""] = entry.split("=");
    if (rawName.trim() !== themeStorageKey) {
      continue;
    }
    const value = decodeURIComponent(rawValue.trim());
    return isThemeMode(value) ? value : null;
  }
  return null;
}
function persistThemeMode(mode) {
  try {
    localStorage.setItem(themeStorageKey, mode);
  } catch {}
  document.cookie = `${themeStorageKey}=${encodeURIComponent(mode)}; Path=/; Max-Age=${themeCookieMaxAgeSeconds}; SameSite=Lax`;
}
function resolveEffectiveTheme(mode) {
  if (mode === "dark" || mode === "light") {
    return mode;
  }
  return mediaDark.matches ? "dark" : "light";
}
function setTheme(mode, persist) {
  const normalized = isThemeMode(mode) ? mode : "auto";
  const effective = resolveEffectiveTheme(normalized);
  document.documentElement.setAttribute("data-theme", effective);
  document.documentElement.setAttribute("data-theme-mode", normalized);
  if (persist) {
    persistThemeMode(normalized);
  }
  syncThemeSwitches(effective);
}
function syncThemeSwitches(effectiveTheme) {
  document.querySelectorAll(themeSwitchSelector).forEach((element) => {
    if (!(element instanceof HTMLElement)) {
      return;
    }
    syncThemeSwitchElement(element, effectiveTheme);
  });
}
function syncThemeSwitchElement(element, effectiveTheme) {
  // Synchronizace gov-theme-switch Web Component
  const govSwitch = element.querySelector(govThemeSwitchTag);
  if (govSwitch instanceof HTMLElement) {
    govSwitch.setAttribute("theme", effectiveTheme);
    return;
  }
  // Fallback: původní custom input
  const input = element.querySelector("[data-theme-switch-input]");
  const label = element.querySelector("[data-theme-switch-label]");
  if (!(input instanceof HTMLInputElement) || !(label instanceof HTMLElement)) {
    return;
  }
  const isDark = effectiveTheme === "dark";
  const displayLabel = shouldDisplayThemeLabel(element);
  const labelLight = getThemeSwitchAttribute(input, "data-label-light", "Světlý mód");
  const labelDark = getThemeSwitchAttribute(input, "data-label-dark", "Tmavý mód");
  const ariaLabelLight = getThemeSwitchAttribute(input, "data-aria-label-light", "Přepnout na tmavý mód");
  const ariaLabelDark = getThemeSwitchAttribute(input, "data-aria-label-dark", "Přepnout na světlý mód");
  element.setAttribute("data-theme-switch-state", effectiveTheme);
  input.checked = isDark;
  label.hidden = !displayLabel;
  label.textContent = isDark ? labelDark : labelLight;
  input.setAttribute("aria-label", isDark ? ariaLabelDark : ariaLabelLight);
}
function shouldDisplayThemeLabel(element) {
  if (!element.hasAttribute("display-label")) {
    return false;
  }
  const value = element.getAttribute("display-label");
  return value === "" || value === "true";
}
function getThemeSwitchAttribute(element, attributeName, fallback) {
  const value = element.getAttribute(attributeName);
  return value && value.trim().length > 0 ? value.trim() : fallback;
}
function bindGovThemeSwitch(element) {
  const govSwitch = element.querySelector(govThemeSwitchTag);
  if (!(govSwitch instanceof HTMLElement)) {
    return false;
  }
  if (element.dataset.themeSwitchBound === "true") {
    return true;
  }
  govSwitch.addEventListener("gov-change", (event) => {
    const detail = event.detail;
    const newMode = detail && isThemeMode(detail.state) ? detail.state : null;
    if (!newMode) {
      return;
    }
    persistThemeMode(newMode);
    document.documentElement.setAttribute("data-theme-mode", newMode);
    document.documentElement.setAttribute("data-theme", resolveEffectiveTheme(newMode));
    element.setAttribute("data-theme-switch-state", newMode);
  });
  element.dataset.themeSwitchBound = "true";
  return true;
}
function initTheme() {
  const stored = getStoredThemeMode();
  if (stored && !getThemeCookieMode()) {
    persistThemeMode(stored);
  }
  setTheme(stored || "auto", false);
  const handleMediaChange = () => {
    const mode = getStoredThemeMode() || "auto";
    if (mode === "auto") {
      setTheme("auto", false);
    }
  };
  if (typeof mediaDark.addEventListener === "function") {
    mediaDark.addEventListener("change", handleMediaChange);
  } else if (typeof mediaDark.addListener === "function") {
    mediaDark.addListener(handleMediaChange);
  }
  document.querySelectorAll(themeSwitchSelector).forEach((element) => {
    if (!(element instanceof HTMLElement)) {
      return;
    }
    if (element.dataset.themeSwitchBound === "true") {
      return;
    }
    // Pokus o binding gov-theme-switch Web Component
    if (bindGovThemeSwitch(element)) {
      return;
    }
    // Fallback: původní custom input
    const input = element.querySelector("[data-theme-switch-input]");
    if (input instanceof HTMLInputElement) {
      input.addEventListener("change", () => {
        setTheme(input.checked ? "dark" : "light", true);
      });
    }
    element.dataset.themeSwitchBound = "true";
  });
}

// PmTracker.Web/wwwroot/js/modules/dashboard.js
function resolvePanelShell(panelOrKey) {
  if (panelOrKey instanceof HTMLElement && panelOrKey.matches("[data-dashboard-panel]")) {
    return panelOrKey;
  }
  if (typeof panelOrKey !== "string" || !panelOrKey) {
    return null;
  }
  const panel = document.querySelector(`[data-dashboard-panel="${CSS.escape(panelOrKey)}"]`);
  return panel instanceof HTMLElement ? panel : null;
}
async function loadDashboardPanel(panelOrKey, options = {}) {
  const panel = resolvePanelShell(panelOrKey);
  if (!(panel instanceof HTMLElement)) {
    return false;
  }
  const loadUrl = typeof options.url === "string" && options.url ? options.url : (panel.dataset.dashboardPanelUrl || "").trim();
  if (!loadUrl) {
    return false;
  }
  const content = panel.querySelector("[data-dashboard-panel-content]");
  const placeholder = panel.querySelector("[data-dashboard-panel-placeholder]");
  const errorContainer = resolveOrCreateErrorContainer(panel, "data-dashboard-panel-error");
  if (!(content instanceof HTMLElement)) {
    return false;
  }
  setLazyLoadingState(panel, placeholder, errorContainer, true);
  try {
    content.innerHTML = await fetchHtmlFragment(loadUrl);
    panel.dataset.dashboardPanelUrl = loadUrl;
    setLazyLoadingState(panel, placeholder, errorContainer, false);
    return true;
  } catch {
    setLazyLoadingState(panel, placeholder, errorContainer, false);
    renderLazyLoadError(errorContainer, "Nepodařilo se načíst obsah panelu.", "data-dashboard-panel-retry");
    return false;
  }
}
function initDashboardShell() {
  const shell = document.querySelector("[data-dashboard-shell]");
  if (!(shell instanceof HTMLElement) || shell.dataset.dashboardReady === "true") {
    return;
  }
  shell.dataset.dashboardReady = "true";
  shell.querySelectorAll("[data-dashboard-panel]").forEach((panel) => {
    if (panel instanceof HTMLElement) {
      loadDashboardPanel(panel);
    }
  });
}
function handleDashboardClick(target) {
  if (!(target instanceof Element)) {
    return false;
  }
  const retryButton = target.closest("[data-dashboard-panel-retry]");
  if (retryButton instanceof HTMLButtonElement) {
    const panel = retryButton.closest("[data-dashboard-panel]");
    if (panel instanceof HTMLElement) {
      loadDashboardPanel(panel);
    }
    return true;
  }
  const loadMoreButton = target.closest("[data-dashboard-news-load-more]");
  if (loadMoreButton instanceof HTMLButtonElement) {
    const panel = loadMoreButton.closest("[data-dashboard-panel]");
    if (panel instanceof HTMLElement) {
      const loadUrl = loadMoreButton.getAttribute("data-dashboard-news-load-more") || "";
      if (loadUrl) {
        loadDashboardPanel(panel, { url: loadUrl });
      }
    }
    return true;
  }
  return false;
}

// PmTracker.Web/wwwroot/js/modules/bootstrap.js
var projectIndexFilterOptions = {
  hideDoneStorageKey: "pmtracker.projects.hideDone",
  hideDeletedStorageKey: "pmtracker.projects.hideDeleted"
};
function initProjectIndexUi() {
  initProjectIndexStatusFilters(document, projectIndexFilterOptions);
}
function initPageSwitchers() {
  initProfileRightsFilter();
  initCiselnikAjaxSwitch({ initRecordFormEnhancements });
  initSettingsAjaxSwitch();
}
function handleProjectFilterInputChange2(scope) {
  handleProjectFilterInputChange(scope, {
    applyScope: (resolvedScope) => {
      if (resolvedScope === "schedule") {
        applyProjectScheduleFilters();
      } else if (resolvedScope === "gantt") {
        applyProjectGanttFilters();
      }
    }
  });
}
configureNavigationRuntime({
  initRecordFormEnhancements,
  prepareRecordEditorFormNavigation
});
configureModalRuntime({
  closeAllFloatingPanels,
  initRecordFormEnhancements,
  initPermissionMetadataBindings
});
function handleDocumentClick(event) {
  const target = event.target instanceof Element ? event.target : event.target instanceof Node ? event.target.parentElement : null;
  if (!(target instanceof Element)) {
    return;
  }
  const resetPrintPreference = target.closest("[data-print-preference-reset]");
  if (resetPrintPreference instanceof HTMLButtonElement) {
    event.preventDefault();
    clearStoredPrintFormat();
    return;
  }
  const resetProjectFilterPreferences = target.closest("[data-project-filter-preferences-reset]");
  if (resetProjectFilterPreferences instanceof HTMLButtonElement) {
    event.preventDefault();
    clearProjectFilterPreferenceStorage();
    const status = document.querySelector("[data-project-filter-preferences-status]");
    if (status instanceof HTMLElement) {
      status.textContent = "Uložené projektové filtry byly odstraněny.";
    }
    return;
  }
  const resetRecordEditorPreference = target.closest("[data-record-editor-preference-reset]");
  if (resetRecordEditorPreference instanceof HTMLButtonElement) {
    event.preventDefault();
    clearStoredRecordEditorPreference();
    const status = document.querySelector("[data-record-editor-preference-status]");
    if (status instanceof HTMLElement) {
      status.textContent = "Uložená výchozí volba byla odstraněna.";
    }
    return;
  }
  const printTrigger = target.closest("[data-print-trigger]");
  if (printTrigger) {
    event.preventDefault();
    handlePrintTriggerClick(printTrigger);
    return;
  }
  const filterChipRemove = target.closest("[data-filter-chip-remove]");
  if (filterChipRemove instanceof HTMLButtonElement) {
    event.preventDefault();
    const scope = filterChipRemove.getAttribute("data-filter-chip-remove") || "";
    const inputKey = filterChipRemove.getAttribute("data-filter-chip-key") || "";
    if (scope && inputKey) {
      clearProjectFilterInput(scope, inputKey);
      handleProjectFilterInputChange2(scope);
    }
    return;
  }
  const saveDefaultsButton = target.closest("[data-filter-save-defaults]");
  if (saveDefaultsButton instanceof HTMLButtonElement) {
    event.preventDefault();
    const scope = saveDefaultsButton.getAttribute("data-filter-save-defaults") || "";
    if (scope) {
      saveProjectFilterDefaults(scope);
    }
    return;
  }
  const attendanceToggle = target.closest("[data-meeting-attendance-toggle]");
  if (attendanceToggle instanceof HTMLButtonElement) {
    event.preventDefault();
    toggleMeetingAttendancePanel(attendanceToggle);
    return;
  }
  const meetingYearToggle = target.closest("[data-meeting-year-toggle]");
  if (meetingYearToggle instanceof HTMLButtonElement) {
    event.preventDefault();
    toggleMeetingYearGroup(meetingYearToggle);
    return;
  }
  const scheduleExpandToggle = target.closest("[data-schedule-expand-toggle]");
  if (scheduleExpandToggle instanceof HTMLButtonElement) {
    event.preventDefault();
    toggleScheduleBreakdown(scheduleExpandToggle);
    return;
  }
  if (printState.popover instanceof HTMLElement && !target.closest("[data-print-popover]") && !target.closest("[data-print-trigger]")) {
    closePrintChooser({ restoreFocus: false });
  }
  if (recordEditorState2.chooser instanceof HTMLElement && !target.closest("[data-record-editor-popover]") && !target.closest("[data-record-editor-url]")) {
    closeRecordEditorChooser({ restoreFocus: false });
  }
  const recordEditorCancel = target.closest("[data-record-editor-cancel]");
  if (recordEditorCancel) {
    event.preventDefault();
    requestRecordEditorPageCancel(recordEditorCancel instanceof HTMLElement ? recordEditorCancel : null);
    return;
  }
  const recordEditorTrigger = target.closest("[data-record-editor-url]");
  if (recordEditorTrigger) {
    event.preventDefault();
    openRecordEditor(recordEditorTrigger instanceof HTMLElement ? recordEditorTrigger : null);
    return;
  }
  const openUrl = target.closest("[data-modal-url]");
  if (openUrl) {
    event.preventDefault();
    openUrlModal(openUrl.getAttribute("data-modal-url"), openUrl);
    return;
  }
  if (target.matches("[data-modal-close]") || target.closest("[data-modal-close]")) {
    event.preventDefault();
    const closeTarget = target.closest("[data-modal-close]");
    requestRecordEditorModalClose(closeTarget instanceof HTMLElement ? closeTarget : null);
    return;
  }
  if (target.classList.contains("modal-overlay")) {
    event.preventDefault();
    requestRecordEditorModalClose(target);
    return;
  }
  const filterToggle = target.closest("[data-filter-toggle]");
  if (filterToggle) {
    const filterPanel = document.querySelector("[data-filter-panel]");
    if (filterPanel) {
      const isCollapsed = filterPanel.classList.contains("collapsed");
      setFilterPanelOpen(isCollapsed);
    }
    return;
  }
  const scheduleFilterToggle = target.closest("[data-schedule-filter-toggle]");
  if (scheduleFilterToggle) {
    const filterPanel = document.querySelector("[data-schedule-filter-panel]");
    if (filterPanel instanceof HTMLElement) {
      const isCollapsed = filterPanel.classList.contains("collapsed");
      setScheduleFilterPanelOpen(isCollapsed);
    }
    return;
  }
  const projectTabRetry = target.closest("[data-project-tab-retry]");
  if (projectTabRetry instanceof HTMLButtonElement) {
    event.preventDefault();
    const panel = projectTabRetry.closest("[data-tab-panel]");
    if (panel instanceof HTMLElement) {
      loadProjectTabPanel(panel, { force: true });
    }
    return;
  }
  if (handleDashboardClick(target)) {
    event.preventDefault();
    return;
  }
  const recordCommentsRetry = target.closest("[data-record-comments-retry]");
  if (recordCommentsRetry instanceof HTMLButtonElement) {
    event.preventDefault();
    const card = recordCommentsRetry.closest(".record-card");
    if (card instanceof HTMLElement) {
      loadRecordComments(card, { force: true });
    }
    return;
  }
  const recordDetailRetry = target.closest("[data-record-detail-retry]");
  if (recordDetailRetry instanceof HTMLButtonElement) {
    event.preventDefault();
    const card = recordDetailRetry.closest(".record-card");
    if (card instanceof HTMLElement) {
      loadRecordDetail(card, { force: true });
    }
    return;
  }
  const recordToggle = target.closest("[data-record-toggle]");
  if (recordToggle) {
    if (target.closest("[data-stop-propagation]")) {
      return;
    }
    const card = recordToggle.closest(".record-card");
    if (card) {
      toggleRecordCard(card);
    }
    return;
  }
  if (target.closest("[data-stop-propagation]")) {
    return;
  }
  if (handleNavigationCardClick(target)) {
    event.preventDefault();
  }
}
function handleDocumentOverlayKeydown(event) {
  if (event.key === "Escape" && recordEditorState2.closeGuard instanceof HTMLElement) {
    event.preventDefault();
    closeRecordEditorCloseGuard({ restoreFocus: true });
    return;
  }
  if (event.key === "Escape" && printState.popover instanceof HTMLElement && !isModalOpen()) {
    event.preventDefault();
    closePrintChooser({ restoreFocus: true });
    return;
  }
  if (event.key === "Escape" && recordEditorState2.chooser instanceof HTMLElement && !isModalOpen()) {
    event.preventDefault();
    closeRecordEditorChooser({ restoreFocus: true });
    return;
  }
  if (!isModalOpen()) {
    return;
  }
  if (event.key === "Escape") {
    event.preventDefault();
    requestRecordEditorModalClose(document.activeElement instanceof HTMLElement ? document.activeElement : null);
    return;
  }
  if (event.key === "Tab") {
    trapFocusInModal(event);
  }
}
function handleDocumentChange(event) {
  const target = event.target;
  if (!(target instanceof Element)) {
    return;
  }
  const filterInput = target.closest("[data-filter-key]");
  if (filterInput instanceof HTMLInputElement || filterInput instanceof HTMLSelectElement) {
    persistFilterState(filterInput);
  }
  const scheduleFilterInput = target.closest("[data-schedule-filter-key]");
  if (scheduleFilterInput instanceof HTMLInputElement || scheduleFilterInput instanceof HTMLSelectElement) {
    persistScheduleFilterState(scheduleFilterInput);
  }
  const categorySelect = target.closest("[data-kategorie-select]");
  if (categorySelect instanceof HTMLSelectElement) {
    updateTaskTypeVisibility(categorySelect);
    const form = categorySelect.closest("form");
    if (form instanceof HTMLFormElement) {
      initRecordSchedulePlanner(form);
    }
  }
}
function handleDocumentInput(event) {
  const target = event.target;
  if (!(target instanceof Element)) {
    return;
  }
  const filterInput = target.closest("[data-filter-key]");
  if (filterInput instanceof HTMLInputElement || filterInput instanceof HTMLSelectElement) {
    if (filterInput instanceof HTMLInputElement && filterInput.type === "checkbox") {
      return;
    }
    persistFilterState(filterInput);
  }
  const scheduleFilterInput = target.closest("[data-schedule-filter-key]");
  if (scheduleFilterInput instanceof HTMLInputElement || scheduleFilterInput instanceof HTMLSelectElement) {
    if (scheduleFilterInput instanceof HTMLInputElement && scheduleFilterInput.type === "checkbox") {
      return;
    }
    persistScheduleFilterState(scheduleFilterInput);
  }
}
function handleDocumentCardKeydown(event) {
  const target = event.target;
  if (!(target instanceof Element)) {
    return;
  }
  handleNavigationCardKeydown(event, target);
}
function handleWindowBeforeUnload(event) {
  const pageEditorForm = document.querySelector('form[data-record-editor-form="true"][data-record-editor-presentation="page"]');
  if (!(pageEditorForm instanceof HTMLFormElement)) {
    return;
  }
  if (pageEditorForm.dataset.recordEditorNavigating === "true") {
    return;
  }
  if (!isRecordEditorFormDirty(pageEditorForm)) {
    return;
  }
  event.preventDefault();
  event.returnValue = "";
}
var rerenderRainbowLabelsOnResize = debounce(() => {
  renderAllRainbowSegmentLabels(document);
}, 120);
var rerenderTimelineAxesOnResize = debounce(() => {
  renderStaticTimelineAxes(document.querySelector(".tab-panel.active"));
  document.querySelectorAll('form[data-record-schedule-form="true"]').forEach((form) => {
    if (form instanceof HTMLFormElement) {
      queueRecordSchedulePlannerRecalc(form, 0);
    }
  });
}, 140);
var reflowMeetingOverviewsOnResize = debounce(() => {
  initMeetingOverview(document);
}, 120);
function normalizeEventBindings(bindings) {
  return Array.isArray(bindings) ? bindings : [];
}
function bindEventGroup(target, bindings) {
  normalizeEventBindings(bindings).forEach((binding) => {
    if (!binding || typeof binding.type !== "string" || typeof binding.handler !== "function") {
      return;
    }
    target.addEventListener(binding.type, binding.handler, binding.options);
  });
}
function runInitializers(initializers) {
  normalizeEventBindings(initializers).forEach((initializer) => {
    if (typeof initializer === "function") {
      initializer();
    }
  });
}
function bootstrapPmTrackerApp() {
  bindEventGroup(document, [
    { type: "click", handler: handleDocumentClick },
    { type: "keydown", handler: handleDocumentOverlayKeydown },
    { type: "change", handler: handleDocumentChange },
    { type: "input", handler: handleDocumentInput },
    { type: "keydown", handler: handleDocumentCardKeydown }
  ]);
  bindEventGroup(window, [
    { type: "beforeunload", handler: handleWindowBeforeUnload },
    { type: "scroll", handler: queueFloatingPanelReposition, options: true },
    { type: "resize", handler: queueFloatingPanelReposition },
    { type: "resize", handler: rerenderRainbowLabelsOnResize },
    { type: "resize", handler: rerenderTimelineAxesOnResize },
    { type: "resize", handler: reflowMeetingOverviewsOnResize }
  ]);
  runInitializers([
    () => initProjectTabs(),
    () => initProjectRecordsUi({ preserveServerView: true }),
    () => initProjectScheduleUi(),
    () => initProjectRecordPageshowSync(),
    () => initCommentSortUi(document),
    () => restoreRecordEditorReturnStateFromUrl(),
    () => initTheme(),
    () => initUserMenu(),
    () => initPrintFormatChooser(),
    () => refreshRecordEditorPreferenceUi(),
    () => initPageSwitchers(),
    () => initRecordFormEnhancements(document),
    () => initPermissionMetadataBindings(document),
    () => initTableTools(document),
    () => initMeetingOverview(document),
    () => initProjectIndexUi(),
    () => initDashboardShell(),
    () => initSessionCoordinator(),
    () => initModalAjaxSubmit()
  ]);
}

// PmTracker.Web/wwwroot/js/site.js
bootstrapPmTrackerApp();
