import { mkdir, readdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, "..");
const pagesRoot = path.join(projectRoot, "pages");

const docsPagesModule = await import(pathToFileURL(path.join(projectRoot, "src", "docsPages.ts")).href);
const machineExportModule = await import(pathToFileURL(path.join(projectRoot, "src", "machineExport.ts")).href);

const pages = docsPagesModule.pages;
const enabledPageIds = machineExportModule.machineExportEnabledPageIds;
const navGroups = [
  { label: "Introduction", pages: ["welcome", "unity-csharp", "setup", "harmony0"] },
  { label: "Items / Liquids", pages: ["assets", "audio", "item", "advanced-item", "custom-item-scripts", "recipe", "liquids", "liquid-tiles"] },
  { label: "Player", pages: ["player", "statuses", "moodles"] },
  { label: "World", pages: ["building-entities", "advanced-building-entities", "minigames", "tiles", "traps", "multi-block-structures"] },
  { label: "Misc / API", pages: ["debug-testing", "utils", "console", "tools", "settings", "locale", "saving", "animations", "multi-mod-compatibility"] }
];

await mkdir(pagesRoot, { recursive: true });

const docsRoot = path.join(pagesRoot, "docs");
await mkdir(docsRoot, { recursive: true });
const expectedPageIds = new Set(enabledPageIds.filter((pageId) => pageId !== "tools"));

for (const entry of await readdir(docsRoot, { withFileTypes: true })) {
  if (!entry.isDirectory() || expectedPageIds.has(entry.name)) {
    continue;
  }

  await rm(path.join(docsRoot, entry.name), { recursive: true, force: true });
}

for (const pageId of enabledPageIds) {
  if (pageId === "tools") {
    continue;
  }

  const page = pages.find((entry) => entry.id === pageId);
  if (!page) {
    continue;
  }

  const pageDir = path.join(docsRoot, pageId);
  await mkdir(pageDir, { recursive: true });
  await writeFile(path.join(pageDir, "index.html"), renderPage(page), "utf8");
}

function renderPage(page) {
  const title = `${page.title} | CUCoreLib Docs`;
  const description = page.lead.trim();
  const canonicalUrl = `https://cucorelib.jimmyking.dev/docs/${encodeURIComponent(page.id)}/`;
  const structuredData = renderStructuredData(page, description, canonicalUrl);
  return `<!doctype html>
<html lang="en" style="background: #111; color: #fff;">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="color-scheme" content="dark" />
    <meta name="description" content="${escapeHtml(description)}" />
    <meta name="robots" content="index,follow" />
    <link rel="canonical" href="${canonicalUrl}" />
    <meta property="og:type" content="website" />
    <meta property="og:title" content="${escapeHtml(title)}" />
    <meta property="og:description" content="${escapeHtml(description)}" />
    <meta property="og:url" content="${canonicalUrl}" />
    <meta property="og:site_name" content="CUCoreLib Docs" />
    <meta name="twitter:card" content="summary" />
    <meta name="twitter:title" content="${escapeHtml(title)}" />
    <meta name="twitter:description" content="${escapeHtml(description)}" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
    <link rel="alternate icon" type="image/png" href="/favicon.png" />
    ${structuredData}
    <title>${escapeHtml(title)}</title>
    <script>document.documentElement.classList.add("js")</script>
    <style>html.js #app > .seo-fallback { display: none; }</style>
  </head>
  <body style="margin: 0; background: #111; color: #fff;">
    <div id="app">
      <main class="seo-fallback">
        <h1>${escapeHtml(page.title)}</h1>
        <p>${escapeHtml(description)}</p>
        ${renderNavigation(page.id)}
        <p>This documentation page uses JavaScript for the interactive app interface.</p>
        <p>Machine-readable docs are available at <a href="/api/cucorelib-docs.v1.json">/api/cucorelib-docs.v1.json</a>.</p>
      </main>
    </div>
    <script type="module" src="/src/main.ts"></script>
  </body>
</html>
`;
}

function renderNavigation(currentPageId) {
  return `<nav aria-label="Documentation pages">
    <h2>Documentation</h2>
    ${navGroups.map((group) => {
      const links = group.pages
        .map((pageId) => pages.find((page) => page.id === pageId))
        .filter(Boolean)
        .map((page) => page.id === currentPageId
          ? `<li><strong>${escapeHtml(page.label)}</strong></li>`
          : `<li><a href="${pagePath(page.id)}">${escapeHtml(page.label)}</a></li>`)
        .join("");
      return links ? `<section><h3>${escapeHtml(group.label)}</h3><ul>${links}</ul></section>` : "";
    }).join("")}
  </nav>`;
}

function pagePath(pageId) {
  return pageId === "tools" ? "/tools/" : `/docs/${encodeURIComponent(pageId)}/`;
}

function renderStructuredData(page, description, canonicalUrl) {
  if (page.id !== "setup") {
    return "";
  }

  const payload = {
    "@context": "https://schema.org",
    "@type": "TechArticle",
    headline: page.title,
    description,
    url: canonicalUrl,
    about: ["CUCoreLib", "Casualties Unknown", "BepInEx"],
    isPartOf: "CUCoreLib Docs"
  };

  return `<script type="application/ld+json">${escapeScriptJson(JSON.stringify(payload))}</script>`;
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function escapeScriptJson(value) {
  return String(value).replace(/</g, "\\u003c");
}
