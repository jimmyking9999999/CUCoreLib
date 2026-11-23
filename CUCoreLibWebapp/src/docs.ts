import { getCollection, type CollectionEntry } from "astro:content";

export const siteUrl = "https://cucorelib.jimmyking.dev";
export const siteName = "CUCoreLib Docs";

export const navGroups: Array<{ label: string; pages: string[] }> = [
  { label: "Introduction", pages: ["welcome", "unity-csharp", "setup", "harmony0", "full-game-transition"] },
  { label: "Items / Liquids", pages: ["assets", "audio", "item", "advanced-item", "custom-item-scripts", "recipe", "liquids", "liquid-tiles", "guns"] },
  { label: "Player", pages: ["player", "statuses", "limb-statuses", "moodles"] },
  { label: "World", pages: ["building-entities", "advanced-building-entities", "minigames", "tiles", "traps", "enemies", "multi-block-structures"] },
  { label: "Misc / API", pages: ["debug-testing", "utils", "console", "tools", "visuals", "shaders-vfx", "settings", "locale", "saving", "animations", "multi-mod-compatibility"] }
];

export type DocEntry = CollectionEntry<"docs">;

export function pagePath(id: string): string {
  if (id === "welcome") return "/";
  if (id === "tools") return "/tools/";
  return `/docs/${id}/`;
}

export async function getDocs(): Promise<DocEntry[]> {
  return getCollection("docs");
}

export async function getPublishedDocs(): Promise<DocEntry[]> {
  return (await getDocs()).filter((entry) => !entry.data.draft);
}

export async function getNavOrder(): Promise<DocEntry[]> {
  const published = new Map((await getPublishedDocs()).map((entry) => [entry.id, entry]));
  return navGroups.flatMap((group) => group.pages).map((id) => published.get(id)).filter((entry): entry is DocEntry => !!entry);
}
