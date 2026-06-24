import * as monaco from "monaco-editor/esm/vs/editor/editor.api";
import "monaco-editor/esm/vs/basic-languages/csharp/csharp.contribution";
import editorWorker from "monaco-editor/esm/vs/editor/editor.worker?worker";
import "./styles.css";
import { currentCode, codeTitle } from "./codeSnippets.ts";
import { hoverPanels } from "./hoverPanels.ts";
import { machineExportDefaultIngredients, machineExportDefaultItemState, machineExportDefaultRecipeState, machineExportEnabledPageIds } from "./machineExport.ts";
import { pageBody, pages } from "./docsPages.ts";
import type { HoverPanel, Ingredient, ItemState, Page, PageId, RecipeState } from "./types.ts";

declare global {
  interface Window {
    MonacoEnvironment?: monaco.Environment;
  }
}

window.MonacoEnvironment = {
  getWorker() {
    return new editorWorker();
  }
};

const ingredientDefaults: Ingredient[] = machineExportDefaultIngredients;

const currentPageStorageKey = "cucorelib.docs.currentPage";
const pageScrollStoragePrefix = "cucorelib.docs.scroll.";

let ingredients: Ingredient[] = ingredientDefaults.map((item) => ({ ...item }));

const itemState: ItemState = machineExportDefaultItemState;

const recipeState: RecipeState = machineExportDefaultRecipeState;

const navGroups: Array<{ label: string; pages: PageId[] }> = [
  { label: "Introduction", pages: ["welcome", "unity-csharp", "setup", "harmony0"] },
  { label: "Tutorial", pages: ["tutorial-first-mod"] },
  {
    label: "Items / Liquids",
    pages: [
      "assets",
      "audio",
      "item",
      "advanced-item",
      "recipe",
      "liquids",
      "guns"
    ]
  },
  { label: "Player", pages: ["player", "statuses", "limb-statuses", "moodles"] },
  {
    label: "World",
    pages: [
      "building-entities",
      "advanced-building-entities",
      "minigames",
      "tiles",
      "traps",
      "enemies",
      "multi-block-structures"
    ]
  },
  {
    label: "Misc / API",
    pages: [
      "debug-testing",
      "utils",
      "console",
      "tools",
      "visuals",
      "shaders-vfx",
      "settings",
      "locale",
      "saving",
      "animations",
      "multi-mod-compatibility"
    ]
  }
];
const enabledPageIds = new Set<PageId>(machineExportEnabledPageIds);
const hiddenFromNavAndSearch = new Set<PageId>(["tutorial-first-mod"]);
const discoverablePageIds = new Set<PageId>(
  machineExportEnabledPageIds.filter((pageId) => !hiddenFromNavAndSearch.has(pageId))
);
const visibleNavGroups = navGroups
  .map((group) => ({
    ...group,
    pages: group.pages.filter((pageId) => discoverablePageIds.has(pageId))
  }))
  .filter((group) => group.pages.length > 0);
const navOrder = visibleNavGroups.flatMap((group) => group.pages).filter((page) => enabledPageIds.has(page));
let currentPage: PageId = loadStoredPage();

const root = document.querySelector<HTMLDivElement>("#app");

if (!root) {
  throw new Error("Missing #app root.");
}

root.innerHTML = `
  <div class="app-shell">
    <header class="topbar">
      <div class="brand">
        <img class="brand-mark" src="/logo.png" alt="" aria-hidden="true">
        <div class="brand-title">CUCoreLib</div>
      </div>
      <nav class="api-picker" aria-label="API pages">
        <button class="nav-button" type="button" id="top-prev" aria-label="Previous page">&lt;</button>
        <div class="api-menu" id="api-menu">
          <button class="api-menu-button" type="button" id="api-menu-button" aria-haspopup="true" aria-expanded="false">
            <span id="api-menu-label"></span>
            <span class="api-menu-chevron" aria-hidden="true">v</span>
          </button>
          <div class="api-menu-panel" id="api-menu-panel" hidden>
            ${visibleNavGroups.map((group) => `
              <details class="api-menu-group" open>
                <summary>${group.label}</summary>
                <div class="api-menu-links">
                  ${group.pages.map((pageId) => {
                    const page = pages.find((item) => item.id === pageId);
                    if (!page) return "";
                    if (!enabledPageIds.has(page.id)) {
                      return `<span class="api-menu-placeholder" aria-disabled="true">${page.label}</span>`;
                    }
                    return `<a href="${pagePath(page.id)}" data-page="${page.id}">${page.label}</a>`;
                  }).join("")}
                </div>
              </details>
            `).join("")}
          </div>
        </div>
        <button class="nav-button" type="button" id="top-next" aria-label="Next page">&gt;</button>
        <div class="docs-search">
          <label class="search-label" for="docs-search-input">Search docs</label>
          <input class="search-input" id="docs-search-input" type="search" placeholder="Search pages and API..." autocomplete="off" spellcheck="false">
          <div class="search-results" id="docs-search-results" hidden></div>
        </div>
      </nav>
      <div class="topbar-links" aria-label="Project links">
        <a class="topbar-link" href="https://www.nexusmods.com/scavprototype/mods/341" target="_blank" rel="noopener noreferrer">NexusMods</a>
        <a class="topbar-link" href="https://github.com/jimmyking9999999/CUCoreLib" target="_blank" rel="noopener noreferrer">GitHub</a>
      </div>
    </header>

    <div class="workspace">
      <main class="lesson">
        <div class="lesson-inner" id="lesson-content"></div>
      </main>

      <div class="resize-handle" id="resize-handle" title="Drag to resize code panel"></div>

      <aside class="code-panel" aria-label="Generated C# code">
        <div class="code-titlebar">
          <h2 id="code-title"></h2>
        </div>
        <div id="editor"></div>
        <div class="code-foot">
          <span></span>
          <div class="copy-actions">
            <button class="copy-button" type="button" id="copy-code-clean">Copy without comments</button>
            <button class="copy-button" type="button" id="copy-code">Copy</button>
          </div>
        </div>
      </aside>
    </div>
  </div>
`;

const lessonContent = query<HTMLDivElement>("#lesson-content");
const lessonScroller = query<HTMLElement>(".lesson");
const editorElement = query<HTMLDivElement>("#editor");
const searchInput = query<HTMLInputElement>("#docs-search-input");
const searchResults = query<HTMLDivElement>("#docs-search-results");
let pendingSearchJump: string | null = null;
let activeSearchJumpMark: HTMLElement | null = null;

type SearchEntry = {
  id: PageId;
  label: string;
  crumb: string;
  title: string;
  codeTitle: string;
  idText: string;
  labelText: string;
  titleText: string;
  crumbText: string;
  codeTitleText: string;
  bodyText: string;
  snippetText: string;
};

const searchIndex: SearchEntry[] = buildSearchIndex();

monaco.editor.defineTheme("cu-dark", {
  base: "vs-dark",
  inherit: true,
  rules: [
    { token: "keyword", foreground: "569cd6" },
    { token: "string", foreground: "ce9178" },
    { token: "number", foreground: "b5cea8" },
    { token: "comment", foreground: "6a9955" },
    { token: "type.identifier", foreground: "4ec9b0" }
  ],
  colors: {
    "editor.background": "#1e1f22",
    "editor.foreground": "#d7dce2",
    "editor.lineHighlightBackground": "#1e1f22",
    "editorCursor.foreground": "#f0d98c",
    "editorIndentGuide.background1": "#3f4147"
  }
});

const hoverDocs = Object.fromEntries(
  Object.entries(hoverPanels).map(([key, panel]) => [key, hoverMarkdown(panel)])
);

monaco.languages.registerHoverProvider("csharp", {
  provideHover(model, position) {
    const match = findHoverMatch(model, position);
    if (!match) return null;

    const doc = hoverDocs[match.key];
    if (!doc) return null;

    return {
      range: match.range,
      contents: [{ value: doc }]
    };
  }
});

const editor = monaco.editor.create(editorElement, {
  value: currentCode(currentPage, itemState, recipeState, ingredients),
  language: "csharp",
  theme: "cu-dark",
  readOnly: true,
  automaticLayout: true,
  minimap: { enabled: false },
  fontFamily: '"Cascadia Mono", Consolas, "Liberation Mono", monospace',
  fontSize: 14,
  lineNumbersMinChars: 3,
  scrollBeyondLastLine: false,
  wordWrap: "on"
});

const hoverDecorations = editor.createDecorationsCollection();
const hoverPopover = document.createElement("div");
hoverPopover.className = "hover-popover";
hoverPopover.hidden = true;
document.body.appendChild(hoverPopover);
bindCustomHoverPopover();

let altTextHoverTimer: number | undefined;
let pendingAltTextImage: HTMLImageElement | null = null;
let pendingAltTextX = 0;
let pendingAltTextY = 0;
const altTextHoverDelayMs = 2500;

const imageViewer = document.createElement("div");
imageViewer.className = "image-viewer";
imageViewer.hidden = true;
imageViewer.innerHTML = `
  <button class="image-viewer-close" type="button" aria-label="Close image viewer">x</button>
  <img class="image-viewer-image" alt="">
`;
document.body.appendChild(imageViewer);

const imageViewerImage = query<HTMLImageElement>(".image-viewer-image", imageViewer);

function query<T extends Element>(selector: string, host: ParentNode = document): T {
  const element = host.querySelector<T>(selector);
  if (!element) throw new Error(`Missing element: ${selector}`);
  return element;
}

function escapeHtml(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function pageIndex(id: PageId): number {
  return navOrder.findIndex((page) => page === id);
}

function buildSearchIndex(): SearchEntry[] {
  return navOrder
    .map((pageId) => {
      const page = pages.find((item) => item.id === pageId);
      if (!page) return null;

      const bodyText = htmlToText(pageBody(page.id, itemState, recipeState, ingredients));
      const snippetTitle = codeTitle(page.id);
      const snippetBody = currentCode(page.id, itemState, recipeState, ingredients);

      return {
        id: page.id,
        label: page.label,
        crumb: page.crumb,
        title: page.title,
        codeTitle: snippetTitle,
        idText: page.id.toLowerCase(),
        labelText: page.label.toLowerCase(),
        titleText: page.title.toLowerCase(),
        crumbText: page.crumb.toLowerCase(),
        codeTitleText: snippetTitle.toLowerCase(),
        bodyText: `${page.lead} ${bodyText}`.toLowerCase(),
        snippetText: snippetBody.toLowerCase()
      };
    })
    .filter((entry): entry is SearchEntry => entry !== null);
}

function htmlToText(value: string): string {
  const template = document.createElement("template");
  template.innerHTML = value;
  return template.content.textContent?.replace(/\s+/g, " ").trim() ?? "";
}

function currentPageInfo(): Page {
  return pages.find((page) => page.id === currentPage) ?? pages[0];
}

function isPageId(value: string | null): value is PageId {
  return value !== null && pages.some((page) => page.id === value) && enabledPageIds.has(value);
}

function loadStoredPage(): PageId {
  const routePage = pageFromRoute();
  if (routePage) {
    return routePage;
  }

  const hashPage = pageFromHash();
  if (hashPage) {
    return hashPage;
  }

  const route = window.location.pathname.replace(/\/+$/, "");
  if (route === "/tools") {
    return "tools";
  }

  try {
    const stored = window.localStorage.getItem(currentPageStorageKey);
    return isPageId(stored) ? stored : "welcome";
  } catch {
    return "welcome";
  }
}

function pageFromHash(): PageId | null {
  const hash = decodeURIComponent(window.location.hash.replace(/^#\/?/, ""));
  return isPageId(hash) ? hash : null;
}

function pageFromRoute(): PageId | null {
  const path = window.location.pathname.replace(/\/+$/, "");
  if (path === "/tools") {
    return "tools";
  }

  const match = path.match(/^\/docs\/([^/]+)$/);
  if (!match) {
    return null;
  }

  const page = decodeURIComponent(match[1]);
  return isPageId(page) ? page : null;
}

function storeCurrentPage(): void {
  try {
    window.localStorage.setItem(currentPageStorageKey, currentPage);
  } catch {
    // Locked-down browser storage should not break docs navigation.
  }
}

function scrollStorageKey(page: PageId): string {
  return `${pageScrollStoragePrefix}${page}`;
}

function storeCurrentScroll(): void {
  try {
    window.localStorage.setItem(scrollStorageKey(currentPage), String(lessonScroller.scrollTop));
  } catch {
    // Locked-down browser storage should not break docs navigation.
  }
}

function restoreCurrentScroll(): void {
  let scrollTop = 0;

  try {
    const stored = window.localStorage.getItem(scrollStorageKey(currentPage));
    scrollTop = stored === null ? 0 : Number(stored);
  } catch {
    scrollTop = 0;
  }

  window.requestAnimationFrame(() => {
    lessonScroller.scrollTop = Number.isFinite(scrollTop) ? scrollTop : 0;
  });
}

function renderPage(restoreScroll = true): void {
  const page = currentPageInfo();
  lessonContent.innerHTML = `
    <p class="crumb">${page.crumb}</p>
    <h1 class="page-title">${page.title}</h1>
    <p class="lead">${page.lead}</p>
    ${pageBody(page.id, itemState, recipeState, ingredients)}
    ${pagerHtml()}
  `;

  applyScaledScreenshots();
  bindCurrentPageInputs();
  syncRangeLabels();
  syncHeadMetadata(page);
  updateChrome();
  updateEditor();
  void highlightLessonCode();
  if (jumpToPendingSearchMatch()) {
    return;
  }
  if (restoreScroll) restoreCurrentScroll();
}

function applyScaledScreenshots(): void {
  lessonContent.querySelectorAll<HTMLImageElement>("img.screenshot[scale]").forEach((image) => {
    const rawScale = image.getAttribute("scale");
    const scale = rawScale === null ? Number.NaN : Number(rawScale);

    if (!Number.isFinite(scale) || scale <= 0) {
      image.style.removeProperty("width");
      image.style.removeProperty("height");
      return;
    }

    const clampedScale = Math.min(scale, 1);
    // Only adjust rendered size. The screenshot class still owns border, spacing, background, and cursor styles.
    image.style.width = `${clampedScale * 100}%`;
    image.style.height = "auto";
  });
}

function pagerHtml(): string {
  const index = pageIndex(currentPage);
  if (index === -1) {
    return "";
  }

  const previous = pages.find((page) => page.id === navOrder[index - 1]);
  const next = pages.find((page) => page.id === navOrder[index + 1]);

  return `
    <div class="page-controls">
      ${previous ? `<button class="pager previous" type="button" data-page="${previous.id}"><span>previous</span><strong>${previous.label}</strong></button>` : ""}
      ${next ? `<button class="pager next" type="button" data-page="${next.id}"><span>next</span><strong>${next.label}</strong></button>` : ""}
    </div>
  `;
}

function bindGlobalNav(): void {
  document.addEventListener("click", (event) => {
    const target = event.target as HTMLElement;
    const page = target.closest<HTMLElement>("[data-page]")?.dataset.page as PageId | undefined;
    if (!page) {
      if (!target.closest("#api-menu")) closeApiMenu();
      if (!target.closest(".docs-search")) hideSearchResults();
      return;
    }

    event.preventDefault();
    if (target.closest(".search-result")) {
      setPendingSearchJump(searchInput.value);
    }
    setPage(page);
    closeApiMenu();
    hideSearchResults(true);
  });

  query<HTMLButtonElement>("#top-prev").addEventListener("click", () => movePage(-1));
  query<HTMLButtonElement>("#top-next").addEventListener("click", () => movePage(1));
  query<HTMLButtonElement>("#api-menu-button").addEventListener("click", () => {
    const panel = query<HTMLDivElement>("#api-menu-panel");
    const button = query<HTMLButtonElement>("#api-menu-button");
    const isOpen = !panel.hidden;
    panel.hidden = isOpen;
    button.setAttribute("aria-expanded", String(!isOpen));
  });
}

function bindSearch(): void {
  searchInput.addEventListener("input", () => {
    renderSearchResults(searchInput.value);
  });

  searchInput.addEventListener("focus", () => {
    if (searchInput.value.trim().length > 0) {
      renderSearchResults(searchInput.value);
    }
  });

  searchInput.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      hideSearchResults();
      searchInput.blur();
      return;
    }

    if (event.key === "Enter") {
      const firstResult = searchResults.querySelector<HTMLElement>("[data-page]");
      const page = firstResult?.dataset.page as PageId | undefined;
      if (!page) return;

      event.preventDefault();
      setPendingSearchJump(searchInput.value);
      setPage(page);
      hideSearchResults(true);
    }
  });
}

function renderSearchResults(queryText: string): void {
  const query = queryText.trim().toLowerCase();
  if (query.length <= 2) {
    searchResults.hidden = true;
    searchResults.innerHTML = "";
    return;
  }

  const terms = query.split(/\s+/).filter(Boolean);
  const matches = searchIndex
    .map((entry) => ({ entry, score: searchScore(entry, query, terms) }))
    .filter((item) => item.score > 0)
    .sort((left, right) => right.score - left.score || left.entry.label.localeCompare(right.entry.label))
    .map((item) => item.entry)
    .slice(0, 8);

  if (matches.length === 0) {
    searchResults.hidden = false;
    searchResults.innerHTML = `<div class="search-empty">No matching pages yet.</div>`;
    return;
  }

  searchResults.hidden = false;
  searchResults.innerHTML = matches.map((entry) => `
    <button class="search-result" type="button" data-page="${entry.id}">
      <span class="search-result-crumb">${entry.crumb}</span>
      <strong>${entry.label}</strong>
      <span class="search-result-title">${entry.title}</span>
      <span class="search-result-code">${entry.codeTitle}</span>
    </button>
  `).join("");
}

function setPendingSearchJump(queryText: string): void {
  const trimmed = queryText.trim();
  pendingSearchJump = trimmed.length > 2 ? trimmed : null;
}

function searchScore(entry: SearchEntry, query: string, terms: string[]): number {
  const labelWords = splitSearchWords(entry.labelText);
  const titleWords = splitSearchWords(entry.titleText);
  const idWords = splitSearchWords(entry.idText);

  let score = 0;

  if (entry.labelText === query) score += 10000;
  if (entry.titleText === query) score += 9500;
  if (entry.idText === query) score += 9000;
  if (entry.labelText.startsWith(query)) score += 7000;
  if (entry.titleText.startsWith(query)) score += 6400;
  if (entry.idText.startsWith(query)) score += 6000;
  if (labelWords.includes(query)) score += 5600;
  if (titleWords.includes(query)) score += 5200;
  if (idWords.includes(query)) score += 5000;

  for (const term of terms) {
    let termScore = 0;

    if (labelWords.includes(term)) termScore = Math.max(termScore, 1600);
    else if (entry.labelText.includes(term)) termScore = Math.max(termScore, 1200);

    if (titleWords.includes(term)) termScore = Math.max(termScore, 1400);
    else if (entry.titleText.includes(term)) termScore = Math.max(termScore, 1000);

    if (idWords.includes(term)) termScore = Math.max(termScore, 1350);
    else if (entry.idText.includes(term)) termScore = Math.max(termScore, 950);

    if (entry.codeTitleText.includes(term)) termScore = Math.max(termScore, 420);
    if (entry.crumbText.includes(term)) termScore = Math.max(termScore, 220);
    if (entry.bodyText.includes(term)) termScore = Math.max(termScore, 120);
    if (entry.snippetText.includes(term)) termScore = Math.max(termScore, 80);

    if (termScore === 0) {
      return 0;
    }

    score += termScore;
  }

  return score;
}

function splitSearchWords(value: string): string[] {
  return value.split(/[^a-z0-9]+/).filter(Boolean);
}

function hideSearchResults(clearInput = false): void {
  searchResults.hidden = true;
  searchResults.innerHTML = "";
  if (clearInput) {
    searchInput.value = "";
  }
}

function closeApiMenu(): void {
  const panel = document.querySelector<HTMLDivElement>("#api-menu-panel");
  const button = document.querySelector<HTMLButtonElement>("#api-menu-button");
  if (!panel || !button) return;

  panel.hidden = true;
  button.setAttribute("aria-expanded", "false");
}

function bindCurrentPageInputs(): void {
  bindText("item-id", (value) => (itemState.id = value));
  bindText("item-name", (value) => (itemState.name = value));
  bindText("item-description", (value) => (itemState.description = value));
  bindText("item-category", (value) => (itemState.category = value));
  bindText("item-sprite", (value) => (itemState.sprite = value));
  bindText("item-weight", (value) => (itemState.weight = value));
  bindText("item-value", (value) => (itemState.value = value));
  bindText("item-spawn", (value) => (itemState.spawnFrequency = value));
  bindText("item-decay", (value) => (itemState.decayMinutes = value));
  bindText("item-recognition", (value) => (itemState.recognition = value));
  bindText("item-tags", (value) => (itemState.tags = value));
  bindChecked("item-usable", (checked) => (itemState.usable = checked), true);
  bindChecked("item-usable-limb", (checked) => (itemState.usableOnLimb = checked), true);
  bindText("item-sickness", (value) => (itemState.sickness = value));
  bindText("item-limb-skin", (value) => (itemState.limbSkinHealth = value));
  bindText("item-limb-muscle", (value) => (itemState.limbMuscleHealth = value));
  bindText("item-limb-pain", (value) => (itemState.limbPain = value));
  bindText("item-limb-temperature", (value) => (itemState.limbTemperature = value));
  bindText("item-limb-chill", (value) => (itemState.limbChillSeconds = value));
  bindText("item-eat", (value) => (itemState.eat = value));
  bindText("item-drink", (value) => (itemState.drink = value));
  bindText("item-happiness", (value) => (itemState.happiness = value));
  bindText("item-sound", (value) => (itemState.sound = value));

  bindText("recipe-result", (value) => (recipeState.resultId = value));
  bindText("recipe-category", (value) => (recipeState.category = value));
  bindText("recipe-int", (value) => (recipeState.intRequirement = value));
  bindText("recipe-amount", (value) => (recipeState.resultAmount = value));
  bindText("recipe-condition", (value) => (recipeState.resultCondition = value));
  bindChecked("recipe-liquid-result", (checked) => (recipeState.isLiquidResult = checked), true);

  bindIngredientEvents();
}

function bindText(id: string, assign: (value: string) => void): void {
  const input = document.getElementById(id) as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement | null;
  if (!input) return;

  input.addEventListener("input", () => {
    assign(input.value);
    syncRangeLabels();
    updateEditor();
  });
}

function bindChecked(id: string, assign: (checked: boolean) => void, rerender = false): void {
  const input = document.getElementById(id) as HTMLInputElement | null;
  if (!input) return;

  input.addEventListener("input", () => {
    const detailsState = snapshotDetailsState();
    assign(input.checked);
    if (rerender) {
      renderPage();
      restoreDetailsState(detailsState);
      return;
    }
    updateEditor();
  });
}

function snapshotDetailsState(): boolean[] {
  return Array.from(lessonContent.querySelectorAll<HTMLDetailsElement>("details")).map((details) => details.open);
}

function restoreDetailsState(state: boolean[]): void {
  lessonContent.querySelectorAll<HTMLDetailsElement>("details").forEach((details, index) => {
    if (state[index] !== undefined) details.open = state[index];
  });
}

function bindIngredientEvents(): void {
  const list = document.querySelector<HTMLDivElement>("#ingredient-list");

  list?.addEventListener("input", (event) => {
    const target = event.target as HTMLInputElement | HTMLSelectElement;
    const field = target.dataset.ingredientField as keyof Ingredient | undefined;
    const index = Number(target.dataset.index);
    if (!field || !Number.isInteger(index) || !ingredients[index]) return;

    if (field === "isLiquid" || field === "destroyItem") {
      ingredients[index][field] = (target as HTMLInputElement).checked;
    } else {
      ingredients[index][field] = target.value as never;
    }

    updateEditor();
  });

  list?.addEventListener("click", (event) => {
    const target = event.target as HTMLElement;
    const index = target.dataset.removeIngredient;
    if (index === undefined) return;

    ingredients.splice(Number(index), 1);
    renderPage();
  });

  document.querySelector("#add-ingredient")?.addEventListener("click", () => {
    ingredients.push({ mode: "specific", id: "woodscraps", amount: "1", isLiquid: false, destroyItem: true });
    renderPage();
  });

  document.querySelector("#reset-ingredients")?.addEventListener("click", () => {
    ingredients = ingredientDefaults.map((item) => ({ ...item }));
    renderPage();
  });
}

function syncRangeLabels(): void {
  document.querySelectorAll<HTMLSpanElement>("[data-range-for]").forEach((label) => {
    const input = document.getElementById(label.dataset.rangeFor ?? "") as HTMLInputElement | null;
    if (input) label.textContent = input.value;
  });
}

function setPage(page: PageId): void {
  if (!enabledPageIds.has(page)) return;
  if (currentPage === page) {
    renderPage(false);
    return;
  }

  storeCurrentScroll();
  currentPage = page;
  syncPathForPage(page);
  storeCurrentPage();
  renderPage();
}

function syncPathForPage(page: PageId): void {
  const nextPath = pagePath(page);
  if (`${window.location.pathname}${window.location.hash}` === nextPath) return;

  window.history.pushState({ page }, "", nextPath);
}

function replacePathForPage(page: PageId): void {
  const nextPath = pagePath(page);
  if (`${window.location.pathname}${window.location.hash}` === nextPath) return;

  window.history.replaceState({ page }, "", nextPath);
}

function syncPageFromLocation(): void {
  const nextPage = pageFromRoute() ?? pageFromHash() ?? loadStoredPage();
  if (nextPage === currentPage) return;

  storeCurrentScroll();
  currentPage = nextPage;
  storeCurrentPage();
  renderPage();
}

function pagePath(page: PageId): string {
  if (page === "tools") {
    return "/tools/";
  }

  return `/docs/${encodeURIComponent(page)}/`;
}

function jumpToPendingSearchMatch(): boolean {
  const query = pendingSearchJump?.trim();
  pendingSearchJump = null;

  if (!query) {
    clearSearchJumpMark();
    return false;
  }

  clearSearchJumpMark();
  const match = findSearchJumpLocation(query);
  if (!match) {
    return false;
  }

  const range = document.createRange();
  range.setStart(match.node, match.start);
  range.setEnd(match.node, match.end);
  const mark = document.createElement("mark");
  mark.className = "search-jump-mark";
  range.surroundContents(mark);
  activeSearchJumpMark = mark;

  window.requestAnimationFrame(() => {
    mark.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" });
  });

  window.setTimeout(() => {
    if (activeSearchJumpMark === mark) {
      clearSearchJumpMark();
    }
  }, 2200);

  return true;
}

function clearSearchJumpMark(): void {
  const mark = activeSearchJumpMark;
  if (!mark) return;

  const parent = mark.parentNode;
  if (!parent) {
    activeSearchJumpMark = null;
    return;
  }

  while (mark.firstChild) {
    parent.insertBefore(mark.firstChild, mark);
  }

  parent.removeChild(mark);
  parent.normalize();
  activeSearchJumpMark = null;
}

function findSearchJumpLocation(query: string): { node: Text; start: number; end: number } | null {
  const normalizedQuery = normalizeSearchText(query);
  if (!normalizedQuery) {
    return null;
  }

  const walker = document.createTreeWalker(lessonContent, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      const parent = node.parentElement;
      if (!parent) return NodeFilter.FILTER_REJECT;
      if (parent.closest(".page-controls")) return NodeFilter.FILTER_REJECT;
      if (parent.closest("script, style")) return NodeFilter.FILTER_REJECT;

      const text = node.textContent ?? "";
      return normalizeSearchText(text).length > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
    }
  });

  let currentNode = walker.nextNode();
  while (currentNode) {
    const textNode = currentNode as Text;
    const text = textNode.textContent ?? "";
    const normalized = normalizeSearchText(text);
    const normalizedIndex = normalized.indexOf(normalizedQuery);

    if (normalizedIndex >= 0) {
      const originalRange = normalizedIndexToOriginalRange(text, normalizedIndex, normalizedQuery.length);
      if (originalRange) {
        return { node: textNode, start: originalRange.start, end: originalRange.end };
      }
    }

    currentNode = walker.nextNode();
  }

  return null;
}

function normalizeSearchText(value: string): string {
  return value.toLowerCase().replace(/\s+/g, " ").trim();
}

function normalizedIndexToOriginalRange(value: string, normalizedIndex: number, normalizedLength: number): { start: number; end: number } | null {
  let sourceIndex = 0;
  let targetStart = -1;
  let targetEnd = -1;
  let normalizedPosition = 0;

  while (sourceIndex < value.length) {
    const char = value[sourceIndex];
    const isWhitespace = /\s/.test(char);

    if (isWhitespace) {
      while (sourceIndex < value.length && /\s/.test(value[sourceIndex])) {
        sourceIndex += 1;
      }

      if (normalizedPosition > 0 && sourceIndex < value.length) {
        if (normalizedPosition === normalizedIndex && targetStart === -1) {
          targetStart = sourceIndex;
        }

        normalizedPosition += 1;

        if (normalizedPosition === normalizedIndex + normalizedLength && targetEnd === -1) {
          targetEnd = sourceIndex;
          break;
        }
      }

      continue;
    }

    if (normalizedPosition === normalizedIndex && targetStart === -1) {
      targetStart = sourceIndex;
    }

    sourceIndex += 1;
    normalizedPosition += 1;

    if (normalizedPosition === normalizedIndex + normalizedLength) {
      targetEnd = sourceIndex;
      break;
    }
  }

  if (targetStart === -1 || targetEnd === -1 || targetEnd <= targetStart) {
    return null;
  }

  return { start: targetStart, end: targetEnd };
}

function movePage(delta: number): void {
  const next = navOrder[pageIndex(currentPage) + delta];
  if (next) setPage(next);
}

function syncHeadMetadata(page: Page): void {
  const title = `${page.title} | CUCoreLib Docs`;
  const description = page.lead.trim();
  const canonicalUrl = page.id === "tools"
    ? "https://cucorelib.jimmyking.dev/tools/"
    : `https://cucorelib.jimmyking.dev/docs/${encodeURIComponent(page.id)}/`;

  document.title = title;
  setHeadMeta('meta[name="description"]', "content", description);
  setHeadMeta('link[rel="canonical"]', "href", canonicalUrl);
  setHeadMeta('meta[property="og:title"]', "content", title);
  setHeadMeta('meta[property="og:description"]', "content", description);
  setHeadMeta('meta[property="og:url"]', "content", canonicalUrl);
  setHeadMeta('meta[name="twitter:title"]', "content", title);
  setHeadMeta('meta[name="twitter:description"]', "content", description);
}

function setHeadMeta(selector: string, attribute: "content" | "href", value: string): void {
  const element = document.head.querySelector<HTMLElement>(selector);
  if (!element) {
    return;
  }

  element.setAttribute(attribute, value);
}

function updateChrome(): void {
  const index = pageIndex(currentPage);

  const previousButton = query<HTMLButtonElement>("#top-prev");
  const nextButton = query<HTMLButtonElement>("#top-next");
  query<HTMLSpanElement>("#api-menu-label").textContent = currentPageInfo().label;
  document.querySelectorAll<HTMLAnchorElement>(".api-menu-links a").forEach((link) => {
    link.classList.toggle("is-active", link.dataset.page === currentPage);
  });
  previousButton.disabled = index <= 0;
  nextButton.disabled = index === -1 || index >= navOrder.length - 1;
}

function updateEditor(): void {
  query("#code-title").textContent = codeTitle(currentPage);
  editor.setValue(currentCode(currentPage, itemState, recipeState, ingredients));
  refreshHoverDecorations();
}

function refreshHoverDecorations(): void {
  const model = editor.getModel();
  if (!model) return;

  const decorations = Object.entries(hoverDocs).flatMap(([word, doc]) => {
    const matches = model.findMatches(`\\b${escapeRegExp(word)}\\b`, false, true, true, null, false);
    return matches.map((match) => ({
      range: match.range,
      options: {
        hoverMessage: { value: doc },
        inlineClassName: "hover-target"
      }
    }));
  });

  hoverDecorations.set(decorations);
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function bindCustomHoverPopover(): void {
  editor.onMouseMove((event) => {
    const position = event.target.position;
    const model = editor.getModel();
    if (!position || !model) {
      hideHoverPopover();
      return;
    }

    const match = findHoverMatch(model, position);
    const panel = match ? hoverPanels[match.key] : undefined;
    if (!match || !panel) {
      hideHoverPopover();
      return;
    }

    showHoverPopover(panel, event.event.browserEvent.clientX, event.event.browserEvent.clientY);
  });

  editor.onMouseLeave(() => hideHoverPopover());
}

function showHoverPopover(panel: HoverPanel, clientX: number, clientY: number): void {
  hoverPopover.innerHTML = `
    <h3>${escapeHtml(panel.title)}</h3>
    ${panel.signature ? `<pre>${escapeHtml(panel.signature)}</pre>` : ""}
    <p>${escapeHtml(panel.body)}</p>
    ${panel.items ? `<ul>${panel.items.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}</ul>` : ""}
  `;
  hoverPopover.hidden = false;
  positionHoverPopover(clientX, clientY);
}

function showAltTextPopover(altText: string, clientX: number, clientY: number): void {
  hoverPopover.classList.add("alt-text-popover");
  hoverPopover.textContent = altText;
  hoverPopover.hidden = false;
  positionHoverPopover(clientX, clientY);
}

function positionHoverPopover(clientX: number, clientY: number): void {
  const margin = 14;
  const rect = hoverPopover.getBoundingClientRect();
  const left = Math.min(clientX + 16, window.innerWidth - rect.width - margin);
  const top = Math.min(clientY + 16, window.innerHeight - rect.height - margin);

  hoverPopover.style.left = `${Math.max(margin, left)}px`;
  hoverPopover.style.top = `${Math.max(margin, top)}px`;
}

function hideHoverPopover(): void {
  hoverPopover.classList.remove("alt-text-popover");
  hoverPopover.hidden = true;
}

function queueAltTextPopover(image: HTMLImageElement, clientX: number, clientY: number): void {
  pendingAltTextX = clientX;
  pendingAltTextY = clientY;

  if (pendingAltTextImage === image && altTextHoverTimer !== undefined) {
    return;
  }

  clearAltTextPopover();
  pendingAltTextImage = image;
  altTextHoverTimer = window.setTimeout(() => {
    if (!pendingAltTextImage) return;
    showAltTextPopover(pendingAltTextImage.alt, pendingAltTextX, pendingAltTextY);
    altTextHoverTimer = undefined;
  }, altTextHoverDelayMs);
}

function clearAltTextPopover(): void {
  if (altTextHoverTimer !== undefined) {
    window.clearTimeout(altTextHoverTimer);
    altTextHoverTimer = undefined;
  }

  pendingAltTextImage = null;
  hoverPopover.classList.remove("alt-text-popover");
}

function bindLessonHoverPopover(): void {
  lessonContent.addEventListener("pointermove", (event) => {
    const target = event.target as HTMLElement;
    const image = target.closest<HTMLImageElement>("img[alt]:not([alt=''])");
    if (image) {
      queueAltTextPopover(image, event.clientX, event.clientY);
      return;
    }

    clearAltTextPopover();
    const token = target.closest<HTMLElement>(".lesson-hover-token");
    const panel = token?.dataset.hoverKey ? hoverPanels[token.dataset.hoverKey] : undefined;

    if (!token || !panel) {
      hideHoverPopover();
      return;
    }

    showHoverPopover(panel, event.clientX, event.clientY);
  });

  lessonContent.addEventListener("pointerleave", () => {
    clearAltTextPopover();
    hideHoverPopover();
  });

  lessonContent.addEventListener("focusin", (event) => {
    const image = (event.target as HTMLElement).closest<HTMLImageElement>("img[alt]:not([alt=''])");
    if (!image) return;

    const rect = image.getBoundingClientRect();
    queueAltTextPopover(image, rect.left + rect.width / 2, rect.top + 12);
  });

  lessonContent.addEventListener("focusout", (event) => {
    if ((event.target as HTMLElement).closest("img[alt]")) {
      clearAltTextPopover();
      hideHoverPopover();
    }
  });
}

function bindImageViewer(): void {
  lessonContent.addEventListener("click", (event) => {
    const image = (event.target as HTMLElement).closest<HTMLImageElement>("img.screenshot");
    if (!image) return;

    event.preventDefault();
    openImageViewer(image);
  });

  imageViewer.addEventListener("click", (event) => {
    if (event.target === imageViewer || (event.target as HTMLElement).closest(".image-viewer-close")) {
      closeImageViewer();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !imageViewer.hidden) {
      closeImageViewer();
    }
  });
}

function openImageViewer(image: HTMLImageElement): void {
  imageViewerImage.src = image.currentSrc || image.src;
  imageViewerImage.alt = image.alt;
  imageViewer.hidden = false;
}

function closeImageViewer(): void {
  imageViewer.hidden = true;
  imageViewerImage.removeAttribute("src");
}

async function highlightLessonCode(): Promise<void> {
  const blocks = lessonContent.querySelectorAll<HTMLElement>("pre code");
  await Promise.all(Array.from(blocks).map((block) => {
    normalizeLessonCodeBlock(block);
    block.classList.add("language-csharp");
    return monaco.editor.colorizeElement(block, {
      theme: "cu-dark",
      mimeType: "text/x-csharp"
    });
  }));

  markLessonHoverTerms();
}

function normalizeLessonCodeBlock(block: HTMLElement): void {
  const source = block.textContent;
  if (!source) return;

  block.textContent = dedentPreservingRelativeIndent(source);
}

function dedentPreservingRelativeIndent(source: string): string {
  const normalized = source.replace(/\r\n/g, "\n");
  const lines = normalized.split("\n");

  while (lines.length > 0 && lines[0].trim() === "") {
    lines.shift();
  }

  while (lines.length > 0 && lines[lines.length - 1].trim() === "") {
    lines.pop();
  }

  const nonEmptyLines = lines.filter((line) => line.trim().length > 0);
  if (nonEmptyLines.length === 0) {
    return lines.join("\n");
  }

  const indentLengths = nonEmptyLines.map((line) => {
    const match = line.match(/^[ \t]*/);
    return match?.[0].length ?? 0;
  });

  const candidateIndents = indentLengths.slice(1);
  const fallbackIndents = indentLengths;
  const relevantIndents = candidateIndents.length > 0 ? candidateIndents : fallbackIndents;
  const sharedIndent = relevantIndents.length > 0 ? Math.min(...relevantIndents) : 0;

  if (!Number.isFinite(sharedIndent) || sharedIndent <= 0) {
    return lines.join("\n");
  }

  return lines
    .map((line) => {
      if (line.trim().length === 0) {
        return "";
      }

      const removableIndent = Math.min(sharedIndent, line.match(/^[ \t]*/)?.[0].length ?? 0);
      return line.slice(removableIndent);
    })
    .join("\n");
}

function markLessonHoverTerms(): void {
  lessonContent.querySelectorAll<HTMLElement>("pre code").forEach((block) => {
    applyHoverTokens(block, (node) => {
      const parent = node.parentElement;
      return !parent || !isInsideLessonComment(parent, block);
    });
  });

  lessonContent.querySelectorAll<HTMLElement>(".inline-code").forEach((element) => {
    applyHoverTokens(element, () => true);
  });
}

function hoverMarkdown(panel: HoverPanel): string {
  if (panel.signature) {
    return `**${panel.title}**\n\n\`\`\`csharp\n${panel.signature}\n\`\`\`\n${panel.body}`;
  }

  return `**${panel.title}**  \n${panel.body}`;
}

function findHoverMatch(model: monaco.editor.ITextModel, position: monaco.Position): { key: string; range: monaco.Range } | null {
  const line = model.getLineContent(position.lineNumber);
  const offset = position.column - 1;

  for (const key of sortedHoverKeys()) {
    let start = line.indexOf(key);
    while (start !== -1) {
      const end = start + key.length;
      if (offset >= start && offset < end && hasIdentifierBoundaries(line, start, end)) {
        return {
          key,
          range: new monaco.Range(position.lineNumber, start + 1, position.lineNumber, end + 1)
        };
      }

      start = line.indexOf(key, start + 1);
    }
  }

  return null;
}

function sortedHoverKeys(): string[] {
  return Object.keys(hoverPanels).sort((left, right) => right.length - left.length);
}

function hasIdentifierBoundaries(text: string, start: number, end: number): boolean {
  return !isIdentifierChar(text[start - 1]) && !isIdentifierChar(text[end]);
}

function isIdentifierChar(value: string | undefined): boolean {
  return value !== undefined && /[A-Za-z0-9_]/.test(value);
}

function applyHoverTokens(container: HTMLElement, shouldProcessNode: (node: Text) => boolean): void {
  const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      if (!(node instanceof Text)) return NodeFilter.FILTER_REJECT;
      const parent = node.parentElement;
      if (!parent || parent.closest(".lesson-hover-token")) return NodeFilter.FILTER_REJECT;
      if (!shouldProcessNode(node)) return NodeFilter.FILTER_REJECT;
      return findTextHoverMatches(node.textContent ?? "").length > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
    }
  });

  const textNodes: Text[] = [];
  while (walker.nextNode()) {
    textNodes.push(walker.currentNode as Text);
  }

  textNodes.forEach((node) => {
    const text = node.textContent ?? "";
    const matches = findTextHoverMatches(text);
    if (matches.length === 0) return;

    const fragment = document.createDocumentFragment();
    let cursor = 0;

    matches.forEach((match) => {
      if (match.start > cursor) {
        fragment.append(document.createTextNode(text.slice(cursor, match.start)));
      }

      const token = document.createElement("span");
      token.className = "hover-target lesson-hover-token";
      token.dataset.hoverKey = match.key;
      token.textContent = match.key;
      fragment.append(token);
      cursor = match.end;
    });

    if (cursor < text.length) {
      fragment.append(document.createTextNode(text.slice(cursor)));
    }

    node.replaceWith(fragment);
  });
}

function findTextHoverMatches(text: string): Array<{ key: string; start: number; end: number }> {
  const matches: Array<{ key: string; start: number; end: number }> = [];
  let cursor = 0;

  while (cursor < text.length) {
    let bestMatch: { key: string; start: number; end: number } | null = null;

    for (const key of sortedHoverKeys()) {
      const start = text.indexOf(key, cursor);
      if (start === -1) continue;

      const end = start + key.length;
      if (!hasIdentifierBoundaries(text, start, end)) continue;

      if (!bestMatch || start < bestMatch.start || (start === bestMatch.start && key.length > bestMatch.key.length)) {
        bestMatch = { key, start, end };
      }
    }

    if (!bestMatch) break;

    matches.push(bestMatch);
    cursor = bestMatch.end;
  }

  return matches;
}

function isInsideLessonComment(element: HTMLElement, block: HTMLElement): boolean {
  const token = element.closest("span");
  if (!token || !block.contains(token)) return false;

  const tokenText = token.textContent ?? "";
  return tokenText.trimStart().startsWith("//") || tokenText.trimStart().startsWith("/*") || tokenText.trimEnd().endsWith("*/");
}

function stripCsharpComments(source: string): string {
  let output = "";
  let inLineComment = false;
  let inBlockComment = false;
  let inString = false;
  let inChar = false;
  let inVerbatimString = false;

  for (let index = 0; index < source.length; index += 1) {
    const char = source[index];
    const next = source[index + 1];
    const previous = source[index - 1];

    if (inLineComment) {
      if (char === "\n") {
        inLineComment = false;
        output = output.replace(/[ \t]+$/g, "");
        output += char;
      }
      continue;
    }

    if (inBlockComment) {
      if (char === "*" && next === "/") {
        inBlockComment = false;
        index += 1;
      }
      continue;
    }

    if (inString) {
      output += char;
      if (inVerbatimString && char === "\"" && next === "\"") {
        output += next;
        index += 1;
      } else if (char === "\"" && (inVerbatimString || previous !== "\\")) {
        inString = false;
        inVerbatimString = false;
      }
      continue;
    }

    if (inChar) {
      output += char;
      if (char === "'" && previous !== "\\") inChar = false;
      continue;
    }

    if (char === "/" && next === "/") {
      inLineComment = true;
      index += 1;
      continue;
    }

    if (char === "/" && next === "*") {
      inBlockComment = true;
      index += 1;
      continue;
    }

    if (char === "@" && next === "\"") {
      inString = true;
      inVerbatimString = true;
      output += char + next;
      index += 1;
      continue;
    }

    if (char === "\"") {
      inString = true;
      output += char;
      continue;
    }

    if (char === "'") {
      inChar = true;
      output += char;
      continue;
    }

    output += char;
  }

  return output
    .split("\n")
    .map((line) => line.replace(/[ \t]+$/g, ""))
    .join("\n")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

function bindCopy(): void {
  const copyToClipboard = async (button: HTMLButtonElement, value: string): Promise<void> => {
    await navigator.clipboard.writeText(value);
    const original = button.textContent ?? "Copy";
    button.textContent = "Copied";
    window.setTimeout(() => {
      button.textContent = original;
    }, 1200);
  };

  query<HTMLButtonElement>("#copy-code").addEventListener("click", async (event) => {
    const button = event.currentTarget as HTMLButtonElement;
    await copyToClipboard(button, currentCode(currentPage, itemState, recipeState, ingredients));
  });

  query<HTMLButtonElement>("#copy-code-clean").addEventListener("click", async (event) => {
    const button = event.currentTarget as HTMLButtonElement;
    await copyToClipboard(button, stripCsharpComments(currentCode(currentPage, itemState, recipeState, ingredients)));
  });
}

function bindResize(): void {
  const workspace = query<HTMLElement>(".workspace");
  const handle = query<HTMLElement>("#resize-handle");
  let dragging = false;

  handle.addEventListener("pointerdown", (event) => {
    dragging = true;
    handle.classList.add("is-dragging");
    handle.setPointerCapture(event.pointerId);
  });

  handle.addEventListener("pointermove", (event) => {
    if (!dragging) return;

    const width = Math.min(Math.max(window.innerWidth - event.clientX, 360), window.innerWidth - 560);
    workspace.style.setProperty("--code-width", `${width}px`);
    editor.layout();
  });

  handle.addEventListener("pointerup", (event) => {
    dragging = false;
    handle.classList.remove("is-dragging");
    handle.releasePointerCapture(event.pointerId);
  });
}

lessonScroller.addEventListener("scroll", storeCurrentScroll, { passive: true });
window.addEventListener("beforeunload", storeCurrentScroll);
window.addEventListener("popstate", syncPageFromLocation);
window.addEventListener("hashchange", syncPageFromLocation);
replacePathForPage(currentPage);
bindGlobalNav();
bindSearch();
bindCopy();
bindResize();
bindLessonHoverPopover();
bindImageViewer();
renderPage();
