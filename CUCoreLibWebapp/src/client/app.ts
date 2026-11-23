import type * as Monaco from "monaco-editor/esm/vs/editor/editor.api";
import { hoverPanels } from "../hoverPanels.ts";
import { defaultIngredients, defaultItemState, defaultRecipeState, itemCode, itemFormHtml, recipeCode, recipeFormHtml } from "../generators.ts";
import type { HoverPanel, Ingredient } from "../types.ts";
import { bindCalculators } from "./tools.ts";

type SearchEntry = {
  id: string;
  path: string;
  label: string;
  crumb: string;
  title: string;
  codeTitle: string;
  body: string;
  snippet: string;
};

const searchJumpKey = "cucorelib.docs.searchJump";
const scrollKey = `cucorelib.docs.scroll.${location.pathname}`;
const altTextHoverDelayMs = 2500;

const lessonContent = query<HTMLElement>("#lesson-content");
const lessonScroller = query<HTMLElement>(".lesson");
const editorElement = query<HTMLDivElement>("#editor");
const codeTitleElement = query<HTMLElement>("#code-title");
const searchInput = query<HTMLInputElement>("#docs-search-input");
const searchResults = query<HTMLDivElement>("#docs-search-results");
const panelSource = query<HTMLScriptElement>("#panel-source");

const initialTitle = codeTitleElement.textContent ?? "";
const panelLang = panelSource.dataset.lang === "json" ? "json" : "csharp";
let panelCode: string = JSON.parse(panelSource.textContent || "\"\"");
let editor: Monaco.editor.IStandaloneCodeEditor | null = null;
let monacoApi: typeof Monaco | null = null;
let searchIndex: SearchEntry[] | null = null;
let activeSearchJumpMark: HTMLElement | null = null;

const sortedHoverKeys = Object.keys(hoverPanels).sort((left, right) => right.length - left.length);

const hoverPopover = document.createElement("div");
hoverPopover.className = "hover-popover";
hoverPopover.hidden = true;
document.body.appendChild(hoverPopover);

const imageViewer = document.createElement("div");
imageViewer.className = "image-viewer";
imageViewer.hidden = true;
imageViewer.innerHTML = `
  <button class="image-viewer-close" type="button" aria-label="Close image viewer">x</button>
  <img class="image-viewer-image" alt="">
`;
document.body.appendChild(imageViewer);
const imageViewerImage = query<HTMLImageElement>(".image-viewer-image", imageViewer);

let altTextHoverTimer: number | undefined;
let pendingAltTextImage: HTMLImageElement | null = null;
let pendingAltTextX = 0;
let pendingAltTextY = 0;

function query<T extends Element>(selector: string, host: ParentNode = document): T {
  const element = host.querySelector<T>(selector);
  if (!element) throw new Error(`Missing element: ${selector}`);
  return element;
}

function escapeHtml(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

// Code panel!

function setPanel(code: string, title = initialTitle): void {
  panelCode = code;
  codeTitleElement.textContent = title;
  if (editor) {
    editor.setValue(code);
    refreshHoverDecorations();
  }
}

async function loadEditor(): Promise<void> {
  const [monaco, { default: EditorWorker }] = await Promise.all([
    import("monaco-editor/esm/vs/editor/editor.api"),
    import("monaco-editor/esm/vs/editor/editor.worker?worker"),
    import("monaco-editor/esm/vs/basic-languages/csharp/csharp.contribution"),
    import("monaco-editor/esm/vs/language/json/monaco.contribution")
  ]);
  (window as unknown as { MonacoEnvironment: Monaco.Environment }).MonacoEnvironment = { getWorker: () => new EditorWorker() };
  monacoApi = monaco;

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

  editorElement.replaceChildren();
  editor = monaco.editor.create(editorElement, {
    value: panelCode,
    language: panelLang,
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

  editor.onMouseMove((event) => {
    const position = event.target.position;
    const model = editor?.getModel();
    const key = position && model ? hoverKeyAt(model.getLineContent(position.lineNumber), position.column - 1) : null;
    const panel = key ? hoverPanels[key] : undefined;
    if (!panel) {
      hideHoverPopover();
      return;
    }
    showHoverPopover(panel, event.event.browserEvent.clientX, event.event.browserEvent.clientY);
  });
  editor.onMouseLeave(() => hideHoverPopover());
  refreshHoverDecorations();
}

let hoverDecorations: Monaco.editor.IEditorDecorationsCollection | null = null;

function refreshHoverDecorations(): void {
  const model = editor?.getModel();
  if (!editor || !model || !monacoApi) return;

  hoverDecorations ??= editor.createDecorationsCollection();
  hoverDecorations.set(sortedHoverKeys.flatMap((word) =>
    model.findMatches(`\\b${word.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}\\b`, false, true, true, null, false)
      .map((match) => ({ range: match.range, options: { inlineClassName: "hover-target" } }))));
}

function hoverKeyAt(line: string, offset: number): string | null {
  for (const key of sortedHoverKeys) {
    let start = line.indexOf(key);
    while (start !== -1) {
      const end = start + key.length;
      if (offset >= start && offset < end && hasIdentifierBoundaries(line, start, end)) return key;
      start = line.indexOf(key, start + 1);
    }
  }
  return null;
}

function bindCodeSourceLinks(): void {
  document.addEventListener("click", async (event) => {
    const link = (event.target as HTMLElement).closest<HTMLElement>("[data-code-source]");
    if (!link) return;
    event.preventDefault();
    if (link.dataset.codeSource !== "item-setup") return;

    try {
      const response = await fetch("/item-setup.cs");
      if (response.ok) setPanel(await response.text(), "item-setup.cs");
    } catch {
      // e
    }
  });
}

// Hovers!

function hasIdentifierBoundaries(text: string, start: number, end: number): boolean {
  const isIdentifierChar = (value: string | undefined) => value !== undefined && /[A-Za-z0-9_]/.test(value);
  return !isIdentifierChar(text[start - 1]) && !isIdentifierChar(text[end]);
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
  hoverPopover.style.left = `${Math.max(margin, Math.min(clientX + 16, window.innerWidth - rect.width - margin))}px`;
  hoverPopover.style.top = `${Math.max(margin, Math.min(clientY + 16, window.innerHeight - rect.height - margin))}px`;
}

function hideHoverPopover(): void {
  hoverPopover.classList.remove("alt-text-popover");
  hoverPopover.hidden = true;
}

function queueAltTextPopover(image: HTMLImageElement, clientX: number, clientY: number): void {
  pendingAltTextX = clientX;
  pendingAltTextY = clientY;
  if (pendingAltTextImage === image && altTextHoverTimer !== undefined) return;

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

function markLessonHoverTerms(): void {
  lessonContent.querySelectorAll<HTMLElement>("pre code").forEach((block) => {
    applyHoverTokens(block, (node) => {
      const token = node.parentElement?.closest("span");
      if (!token || !block.contains(token)) return true;
      const text = token.textContent ?? "";
      return !(text.trimStart().startsWith("//") || text.trimStart().startsWith("/*") || text.trimEnd().endsWith("*/"));
    });
  });
  lessonContent.querySelectorAll<HTMLElement>(".inline-code").forEach((element) => applyHoverTokens(element, () => true));
}

function applyHoverTokens(container: HTMLElement, shouldProcessNode: (node: Text) => boolean): void {
  const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      const parent = node.parentElement;
      if (!parent || parent.closest(".lesson-hover-token")) return NodeFilter.FILTER_REJECT;
      if (!shouldProcessNode(node as Text)) return NodeFilter.FILTER_REJECT;
      return findTextHoverMatches(node.textContent ?? "").length > 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
    }
  });

  const textNodes: Text[] = [];
  while (walker.nextNode()) textNodes.push(walker.currentNode as Text);

  textNodes.forEach((node) => {
    const text = node.textContent ?? "";
    const fragment = document.createDocumentFragment();
    let cursor = 0;
    findTextHoverMatches(text).forEach((match) => {
      if (match.start > cursor) fragment.append(document.createTextNode(text.slice(cursor, match.start)));
      const token = document.createElement("span");
      token.className = "hover-target lesson-hover-token";
      token.dataset.hoverKey = match.key;
      token.textContent = match.key;
      fragment.append(token);
      cursor = match.end;
    });
    if (cursor < text.length) fragment.append(document.createTextNode(text.slice(cursor)));
    node.replaceWith(fragment);
  });
}

function findTextHoverMatches(text: string): Array<{ key: string; start: number; end: number }> {
  const matches: Array<{ key: string; start: number; end: number }> = [];
  let cursor = 0;

  while (cursor < text.length) {
    let best: { key: string; start: number; end: number } | null = null;
    for (const key of sortedHoverKeys) {
      const start = text.indexOf(key, cursor);
      if (start === -1) continue;
      const end = start + key.length;
      if (!hasIdentifierBoundaries(text, start, end)) continue;
      if (!best || start < best.start || (start === best.start && key.length > best.key.length)) best = { key, start, end };
    }
    if (!best) break;
    matches.push(best);
    cursor = best.end;
  }

  return matches;
}

// Images

function applyScaledScreenshots(): void {
  lessonContent.querySelectorAll<HTMLImageElement>("img.screenshot[scale]").forEach((image) => {
    const scale = Number(image.getAttribute("scale"));
    if (!Number.isFinite(scale) || scale <= 0) return;
    image.style.width = `${Math.min(scale, 1) * 100}%`;
    image.style.height = "auto";
  });
}

function bindImageViewer(): void {
  lessonContent.addEventListener("click", (event) => {
    const image = (event.target as HTMLElement).closest<HTMLImageElement>("img.screenshot");
    if (!image) return;
    event.preventDefault();
    imageViewerImage.src = image.currentSrc || image.src;
    imageViewerImage.alt = image.alt;
    imageViewer.hidden = false;
  });

  const close = () => {
    imageViewer.hidden = true;
    imageViewerImage.removeAttribute("src");
  };
  imageViewer.addEventListener("click", (event) => {
    if (event.target === imageViewer || (event.target as HTMLElement).closest(".image-viewer-close")) close();
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && !imageViewer.hidden) close();
  });
}

// Search

async function loadSearchIndex(): Promise<SearchEntry[]> {
  if (!searchIndex) {
    try {
      const response = await fetch("/search-index.json");
      searchIndex = response.ok ? await response.json() : [];
    } catch {
      searchIndex = [];
    }
  }
  return searchIndex ?? [];
}

function splitSearchWords(value: string): string[] {
  return value.split(/[^a-z0-9]+/).filter(Boolean);
}

function searchScore(entry: SearchEntry, queryText: string, terms: string[]): number {
  const label = entry.label.toLowerCase();
  const title = entry.title.toLowerCase();
  const id = entry.id.toLowerCase();
  const codeTitle = entry.codeTitle.toLowerCase();
  const crumb = entry.crumb.toLowerCase();
  const body = entry.body.toLowerCase();
  const snippet = entry.snippet.toLowerCase();
  const labelWords = splitSearchWords(label);
  const titleWords = splitSearchWords(title);
  const idWords = splitSearchWords(id);

  let score = 0;
  if (label === queryText) score += 10000;
  if (title === queryText) score += 9500;
  if (id === queryText) score += 9000;
  if (label.startsWith(queryText)) score += 7000;
  if (title.startsWith(queryText)) score += 6400;
  if (id.startsWith(queryText)) score += 6000;
  if (labelWords.includes(queryText)) score += 5600;
  if (titleWords.includes(queryText)) score += 5200;
  if (idWords.includes(queryText)) score += 5000;

  for (const term of terms) {
    let termScore = 0;
    if (labelWords.includes(term)) termScore = Math.max(termScore, 1600);
    else if (label.includes(term)) termScore = Math.max(termScore, 1200);
    if (titleWords.includes(term)) termScore = Math.max(termScore, 1400);
    else if (title.includes(term)) termScore = Math.max(termScore, 1000);
    if (idWords.includes(term)) termScore = Math.max(termScore, 1350);
    else if (id.includes(term)) termScore = Math.max(termScore, 950);
    if (codeTitle.includes(term)) termScore = Math.max(termScore, 420);
    if (crumb.includes(term)) termScore = Math.max(termScore, 220);
    if (body.includes(term)) termScore = Math.max(termScore, 120);
    if (snippet.includes(term)) termScore = Math.max(termScore, 80);
    if (termScore === 0) return 0;
    score += termScore;
  }

  return score;
}

async function renderSearchResults(queryText: string): Promise<void> {
  const normalized = queryText.trim().toLowerCase();
  if (normalized.length <= 2) {
    hideSearchResults();
    return;
  }

  const terms = normalized.split(/\s+/).filter(Boolean);
  const matches = (await loadSearchIndex())
    .map((entry) => ({ entry, score: searchScore(entry, normalized, terms) }))
    .filter((item) => item.score > 0)
    .sort((left, right) => right.score - left.score || left.entry.label.localeCompare(right.entry.label))
    .slice(0, 8)
    .map((item) => item.entry);

  if (searchInput.value.trim().toLowerCase() !== normalized) return;
  searchResults.hidden = false;
  searchResults.innerHTML = matches.length === 0
    ? `<div class="search-empty">No matching pages yet.</div>`
    : matches.map((entry) => `
      <a class="search-result" href="${entry.path}">
        <span class="search-result-crumb">${escapeHtml(entry.crumb)}</span>
        <strong>${escapeHtml(entry.label)}</strong>
        <span class="search-result-title">${escapeHtml(entry.title)}</span>
        <span class="search-result-code">${escapeHtml(entry.codeTitle)}</span>
      </a>
    `).join("");
}

function hideSearchResults(): void {
  searchResults.hidden = true;
  searchResults.innerHTML = "";
}

function rememberSearchJump(): void {
  const value = searchInput.value.trim();
  try {
    if (value.length > 2) sessionStorage.setItem(searchJumpKey, value);
  } catch {
  }
}

function bindSearch(): void {
  searchInput.addEventListener("input", () => void renderSearchResults(searchInput.value));
  searchInput.addEventListener("focus", () => {
    void loadSearchIndex();
    if (searchInput.value.trim()) void renderSearchResults(searchInput.value);
  });
  searchInput.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      hideSearchResults();
      searchInput.blur();
    } else if (event.key === "Enter") {
      const first = searchResults.querySelector<HTMLAnchorElement>("a.search-result");
      if (!first) return;
      event.preventDefault();
      rememberSearchJump();
      location.href = first.href;
    }
  });
  searchResults.addEventListener("click", (event) => {
    if ((event.target as HTMLElement).closest("a.search-result")) rememberSearchJump();
  });

  document.addEventListener("click", (event) => {
    const target = event.target as HTMLElement;
    if (!target.closest(".docs-search")) hideSearchResults();
    if (!target.closest("#api-menu")) {
      const menu = document.querySelector<HTMLDetailsElement>("#api-menu");
      if (menu) menu.open = false;
    }
  });
}

function jumpToSearchMatch(): boolean {
  let queryText: string | null = null;
  try {
    queryText = sessionStorage.getItem(searchJumpKey);
    sessionStorage.removeItem(searchJumpKey);
  } catch {
    return false;
  }

  const normalizedQuery = queryText?.toLowerCase().replace(/\s+/g, " ").trim();
  if (!normalizedQuery) return false;

  const walker = document.createTreeWalker(lessonContent, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      const parent = node.parentElement;
      if (!parent || parent.closest(".page-controls, script, style")) return NodeFilter.FILTER_REJECT;
      return (node.textContent ?? "").toLowerCase().includes(normalizedQuery) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
    }
  });

  const node = walker.nextNode() as Text | null;
  if (!node) return false;

  const start = (node.textContent ?? "").toLowerCase().indexOf(normalizedQuery);
  const range = document.createRange();
  range.setStart(node, start);
  range.setEnd(node, start + normalizedQuery.length);
  const mark = document.createElement("mark");
  mark.className = "search-jump-mark";
  range.surroundContents(mark);
  activeSearchJumpMark = mark;

  requestAnimationFrame(() => mark.scrollIntoView({ behavior: "smooth", block: "center", inline: "nearest" }));
  window.setTimeout(() => {
    const active = activeSearchJumpMark;
    if (active !== mark || !active.parentNode) return;
    const parent = active.parentNode;
    while (active.firstChild) parent.insertBefore(active.firstChild, active);
    parent.removeChild(active);
    parent.normalize();
    activeSearchJumpMark = null;
  }, 2200);
  return true;
}


function restoreScroll(): void {
  try {
    const stored = Number(localStorage.getItem(scrollKey));
    if (Number.isFinite(stored) && stored > 0) lessonScroller.scrollTop = stored;
  } catch {
  }
}

function storeScroll(): void {
  try {
    localStorage.setItem(scrollKey, String(lessonScroller.scrollTop));
  } catch {
  }
}

function persistDropdowns(): void {
  lessonContent.querySelectorAll<HTMLDetailsElement>("details").forEach((details, index) => {
    if (details.closest("[data-generator]")) return;
    const key = `cucorelib.docs.details.${location.pathname}.${index}`;
    try {
      const stored = localStorage.getItem(key);
      if (stored !== null) details.open = stored === "1";
    } catch {
    }
    details.addEventListener("toggle", () => {
      try {
        localStorage.setItem(key, details.open ? "1" : "0");
      } catch {
      }
    });
  });
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
    if (char === "\"") inString = true;
    else if (char === "'") inChar = true;
    output += char;
  }

  return output.split("\n").map((line) => line.replace(/[ \t]+$/g, "")).join("\n").replace(/\n{3,}/g, "\n\n").trim();
}

function bindCopy(): void {
  const copy = async (button: HTMLButtonElement, value: string) => {
    await navigator.clipboard.writeText(value);
    const original = button.textContent ?? "Copy";
    button.textContent = "Copied";
    window.setTimeout(() => (button.textContent = original), 1200);
  };
  query<HTMLButtonElement>("#copy-code").addEventListener("click", (event) => void copy(event.currentTarget as HTMLButtonElement, panelCode));
  query<HTMLButtonElement>("#copy-code-clean").addEventListener("click", (event) => void copy(event.currentTarget as HTMLButtonElement, stripCsharpComments(panelCode)));
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
    editor?.layout();
  });
  handle.addEventListener("pointerup", (event) => {
    dragging = false;
    handle.classList.remove("is-dragging");
    handle.releasePointerCapture(event.pointerId);
  });
}

// ---------- Generators (item / recipe pages) ----------

function syncRangeLabels(host: ParentNode): void {
  host.querySelectorAll<HTMLSpanElement>("[data-range-for]").forEach((label) => {
    const input = document.getElementById(label.dataset.rangeFor ?? "") as HTMLInputElement | null;
    if (input) label.textContent = input.value;
  });
}

function rerender(host: HTMLElement, html: string): void {
  const open = Array.from(host.querySelectorAll("details")).map((details) => details.open);
  host.innerHTML = html;
  host.querySelectorAll("details").forEach((details, index) => {
    if (open[index] !== undefined) details.open = open[index];
  });
}

function bindItemGenerator(host: HTMLElement): void {
  const state = { ...defaultItemState };
  const fields: Record<string, keyof typeof state> = {
    "item-id": "id", "item-name": "name", "item-description": "description", "item-category": "category", "item-sprite": "sprite",
    "item-weight": "weight", "item-value": "value", "item-spawn": "spawnFrequency", "item-decay": "decayMinutes",
    "item-recognition": "recognition", "item-tags": "tags", "item-sickness": "sickness", "item-limb-skin": "limbSkinHealth",
    "item-limb-muscle": "limbMuscleHealth", "item-limb-pain": "limbPain", "item-limb-temperature": "limbTemperature",
    "item-limb-chill": "limbChillSeconds", "item-eat": "eat", "item-drink": "drink", "item-happiness": "happiness", "item-sound": "sound"
  };

  host.addEventListener("input", (event) => {
    const target = event.target as HTMLInputElement;
    if (target.id === "item-usable" || target.id === "item-usable-limb") {
      state[target.id === "item-usable" ? "usable" : "usableOnLimb"] = target.checked;
      rerender(host, itemFormHtml(state));
    } else if (fields[target.id]) {
      (state as Record<string, unknown>)[fields[target.id]] = target.value;
      syncRangeLabels(host);
    }
    setPanel(itemCode(state));
  });
}

function bindRecipeGenerator(host: HTMLElement): void {
  const state = { ...defaultRecipeState };
  let ingredients: Ingredient[] = defaultIngredients.map((item) => ({ ...item }));
  const fields: Record<string, "resultId" | "category" | "intRequirement" | "resultAmount" | "resultCondition"> = {
    "recipe-result": "resultId", "recipe-category": "category", "recipe-int": "intRequirement",
    "recipe-amount": "resultAmount", "recipe-condition": "resultCondition"
  };
  const refresh = (rebuild: boolean) => {
    if (rebuild) rerender(host, recipeFormHtml(state, ingredients));
    setPanel(recipeCode(state, ingredients));
  };

  host.addEventListener("input", (event) => {
    const target = event.target as HTMLInputElement;
    const field = target.dataset.ingredientField as keyof Ingredient | undefined;
    const index = Number(target.dataset.index);
    if (field && ingredients[index]) {
      if (field === "isLiquid" || field === "destroyItem") ingredients[index][field] = target.checked;
      else (ingredients[index] as unknown as Record<string, string>)[field] = target.value;
      if (field === "amount") ingredients[index].amountWasEdited = true;
      refresh(false);
    } else if (target.id === "recipe-liquid-result") {
      state.isLiquidResult = target.checked;
      refresh(true);
    } else if (fields[target.id]) {
      state[fields[target.id]] = target.value;
      syncRangeLabels(host);
      refresh(false);
    }
  });

  host.addEventListener("click", (event) => {
    const target = event.target as HTMLElement;
    if (target.dataset.removeIngredient !== undefined) {
      ingredients.splice(Number(target.dataset.removeIngredient), 1);
      refresh(true);
    } else if (target.id === "add-ingredient") {
      ingredients.push({ mode: "specific", id: "woodscraps", amount: "1", amountWasEdited: false, isLiquid: false, destroyItem: true });
      refresh(true);
    } else if (target.id === "reset-ingredients") {
      ingredients = defaultIngredients.map((item) => ({ ...item }));
      refresh(true);
    }
  });
}

// init
applyScaledScreenshots();
markLessonHoverTerms();
bindLessonHoverPopover();
bindImageViewer();
bindSearch();
bindCopy();
bindResize();
bindCodeSourceLinks();
document.querySelectorAll<HTMLElement>("[data-generator]").forEach((host) => {
  switch (host.dataset.generator) {
    case "item": bindItemGenerator(host); break;
    case "recipe": bindRecipeGenerator(host); break;
    case "calculators": bindCalculators(host); break;
  }
});
persistDropdowns();
if (!jumpToSearchMatch()) restoreScroll();
lessonScroller.addEventListener("scroll", storeScroll, { passive: true });
void loadEditor();
