import { generatorCode } from "./generators.ts";
import type { DocEntry } from "./docs.ts";

const panelFence = /(`{3,})(\w+) panel title="([^"]*)"\n([\s\S]*?)\n\1/;

export function panelOf(entry: DocEntry): { title: string; code: string } {
  if (entry.data.codeGenerator) {
    return { title: entry.data.codeTitle ?? "Example.cs", code: generatorCode(entry.data.codeGenerator) };
  }
  const match = (entry.body ?? "").match(panelFence);
  return { title: match?.[3] ?? "", code: match?.[4] ?? "" };
}

export function markdownOf(entry: DocEntry): string {
  return (entry.body ?? "")
    .replace(panelFence, "")
    .replace(/^import .*$/gm, "")
    .replace(/\{\/\*[\s\S]*?\*\/\}/g, "")
    .replace(/<(ItemGenerator|RecipeGenerator) \/>/g, "")
    .replace(/&#123;/g, "{").replace(/&#125;/g, "}").replace(/&lt;/g, "<")
    .replace(/\n{3,}/g, "\n\n")
    .trim();
}

export function plainTextOf(entry: DocEntry): string {
  return markdownOf(entry)
    .replace(/<[^>]+>/g, " ")
    .replace(/[`*#|>\[\]()_-]+/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}
