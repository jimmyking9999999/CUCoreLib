import type { APIRoute } from "astro";
import { getNavOrder, pagePath, siteUrl } from "../docs.ts";
import { markdownOf, panelOf } from "../textExport.ts";
// boop?
export const GET: APIRoute = async () => {
  const pages = (await getNavOrder()).map((entry) => {
    const panel = panelOf(entry);
    const example = panel.code ? `\n\n### Example: ${panel.title}\n\n\`\`\`${panel.title.endsWith(".json") ? "json" : "csharp"}\n${panel.code}\n\`\`\`` : "";
    return `# ${entry.data.title}\n\nSource: ${siteUrl}${pagePath(entry.id)}\n\n> ${entry.data.lead.trim()}\n\n${markdownOf(entry).replace(/^---$/gm, "")}${example}`;
  });
  return new Response(pages.join("\n\n\n"), { headers: { "Content-Type": "text/plain; charset=utf-8" } });
};
