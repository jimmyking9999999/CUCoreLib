import type { APIRoute } from "astro";
import { getNavOrder, pagePath } from "../docs.ts";
import { panelOf, plainTextOf } from "../textExport.ts";

export const GET: APIRoute = async () => {
  const entries = (await getNavOrder())
    .filter((entry) => entry.id !== "tutorial-first-mod")
    .map((entry) => {
      const panel = panelOf(entry);
      return {
        id: entry.id,
        path: pagePath(entry.id),
        label: entry.data.label,
        crumb: entry.data.crumb,
        title: entry.data.title,
        codeTitle: panel.title,
        body: `${entry.data.lead} ${plainTextOf(entry)}`,
        snippet: panel.code
      };
    });
  return new Response(JSON.stringify(entries), { headers: { "Content-Type": "application/json" } });
};
