import type { APIRoute } from "astro";
import { getNavOrder, navGroups, pagePath, siteUrl } from "../docs.ts";
// beep
export const GET: APIRoute = async () => {
  const pages = new Map((await getNavOrder()).map((entry) => [entry.id, entry]));
  const sections = navGroups.map((group) => {
    const links = group.pages
      .map((id) => pages.get(id))
      .filter((entry) => !!entry)
      .map((entry) => `- [${entry!.data.title}](${siteUrl}${pagePath(entry!.id)}): ${entry!.data.lead.trim()}`);
    return links.length ? `## ${group.label}\n\n${links.join("\n")}` : "";
  }).filter(Boolean);

  const text = `# CUCoreLib

> CUCoreLib is the BepInEx + Harmony core library for modding Casualties: Unknown. It provides registries for items, recipes, liquids, buildings, tiles, statuses, moodles, settings, saving, console commands and multiplayer sync.

Full text of every page: ${siteUrl}/llms-full.txt

${sections.join("\n\n")}
`;
  return new Response(text, { headers: { "Content-Type": "text/plain; charset=utf-8" } });
};
