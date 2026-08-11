import { mkdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const projectRoot = path.resolve(__dirname, "..");
const publicDir = path.join(projectRoot, "public");
const apiDir = path.join(publicDir, "api");
const topicsDir = path.join(apiDir, "topics");
const siteOrigin = "https://cucorelib.jimmyking.dev";

const docsPagesModule = await import(pathToFileURL(path.join(projectRoot, "src", "docsPages.ts")).href);
const codeSnippetsModule = await import(pathToFileURL(path.join(projectRoot, "src", "codeSnippets.ts")).href);
const machineExportModule = await import(pathToFileURL(path.join(projectRoot, "src", "machineExport.ts")).href);

const pages = docsPagesModule.pages;
const pageBody = docsPagesModule.pageBody;
const currentCode = codeSnippetsModule.currentCode;
const codeTitle = codeSnippetsModule.codeTitle;
const enabledPageIds = machineExportModule.machineExportEnabledPageIds;
const itemState = structuredClone(machineExportModule.machineExportDefaultItemState);
const recipeState = structuredClone(machineExportModule.machineExportDefaultRecipeState);
const ingredients = structuredClone(machineExportModule.machineExportDefaultIngredients);
const interactiveDefaults = buildInteractiveDefaults(itemState, recipeState, ingredients);

await rm(path.join(publicDir, "docs"), { recursive: true, force: true });
await rm(topicsDir, { recursive: true, force: true });
await mkdir(topicsDir, { recursive: true });

const generatedAt = new Date().toISOString();
const topicEntries = enabledPageIds
  .map((pageId) => {
    const page = pages.find((entry) => entry.id === pageId);
    if (!page) return null;

    const bodyHtml = pageBody(page.id, itemState, recipeState, ingredients);
    const bodyText = htmlToText(bodyHtml);
    const code = currentCode(page.id, itemState, recipeState, ingredients);
    const machineUrl = `/api/topics/${page.id}.json`;
    const appUrl = page.id === "tools" ? "/tools/" : `/docs/${page.id}/`;

    return {
      id: page.id,
      label: page.label,
      crumb: page.crumb,
      title: page.title,
      lead: page.lead,
      appUrl,
      machineUrl,
      bodyHtml,
      bodyText,
      codeTitle: codeTitle(page.id),
      codeLanguage: inferCodeLanguage(codeTitle(page.id)),
      code,
      interactiveDefaults: getInteractiveDefaultsForPage(page.id)
    };
  })
  .filter((entry) => entry !== null);

for (const topic of topicEntries) {
  await writeFile(
    path.join(topicsDir, `${topic.id}.json`),
    `${JSON.stringify(topic, null, 2)}\n`,
    "utf8"
  );
}

const canonicalIndex = {
  version: "1.0",
  library: "CUCoreLib",
  description: "Machine-readable CUCoreLib web docs generated from the interactive docs sources.",
  generatedAt,
  baseUrl: "/",
  topics: topicEntries.map((topic) => ({
    id: topic.id,
    label: topic.label,
    crumb: topic.crumb,
    title: topic.title,
    lead: topic.lead,
    appUrl: topic.appUrl,
    machineUrl: topic.machineUrl
  }))
};

await writeFile(
  path.join(apiDir, "cucorelib-docs.v1.json"),
  `${JSON.stringify(canonicalIndex, null, 2)}\n`,
  "utf8"
);

await writeFile(path.join(publicDir, "robots.txt"), renderRobots(), "utf8");
await writeFile(path.join(publicDir, "sitemap.xml"), renderSitemap(topicEntries), "utf8");

function htmlToText(value) {
  return value
    .replace(/<style[\s\S]*?<\/style>/gi, " ")
    .replace(/<script[\s\S]*?<\/script>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, "\"")
    .replace(/&#39;/g, "'")
    .replace(/\s+/g, " ")
    .trim();
}

function inferCodeLanguage(filename) {
  if (filename.endsWith(".json")) return "json";
  return "csharp";
}

function buildInteractiveDefaults(itemState, recipeState, ingredients) {
  return {
    item: {
      ...itemState,
      imagePath: normalizeImagePath(itemState.sprite)
    },
    recipe: {
      ...recipeState
    },
    ingredients: ingredients.map((ingredient) => ({ ...ingredient }))
  };
}

function getInteractiveDefaultsForPage(pageId) {
  if (pageId === "item" || pageId === "advanced-item" || pageId === "tools") {
    return interactiveDefaults;
  }

  if (pageId === "recipe") {
    return {
      recipe: interactiveDefaults.recipe,
      ingredients: interactiveDefaults.ingredients
    };
  }

  return null;
}

function normalizeImagePath(sprite) {
  const trimmed = String(sprite ?? "").trim();
  if (!trimmed) return null;

  if (/^(?:https?:)?\/\//i.test(trimmed) || trimmed.startsWith("/")) {
    return trimmed;
  }

  return `/images/${trimmed.replace(/^\.?\/+/, "")}`;
}

function renderRobots() {
  return `User-agent: *
Allow: /

Sitemap: ${siteOrigin}/sitemap.xml
`;
}

function renderSitemap(topicEntries) {
  const urls = [
    `${siteOrigin}/`,
    ...topicEntries
      .filter((topic) => topic.id !== "tools")
      .map((topic) => `${siteOrigin}${topic.appUrl}`)
  ];

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls.map((url) => `  <url><loc>${escapeXml(url)}</loc></url>`).join("\n")}
</urlset>
`;
}

function escapeXml(value) {
  return String(value)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}
