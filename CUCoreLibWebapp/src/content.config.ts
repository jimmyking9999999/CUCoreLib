import { defineCollection } from "astro:content";
import { glob } from "astro/loaders";
import { z } from "astro/zod";

const docs = defineCollection({
  loader: glob({ pattern: "**/*.mdx", base: "./src/content/docs" }),
  schema: z.object({
    title: z.string(),
    label: z.string(),
    crumb: z.string(),
    lead: z.string(),
    // Soon!
    draft: z.boolean().optional(),
    codeGenerator: z.enum(["item", "recipe"]).optional(),
    codeTitle: z.string().optional()
  })
});

export const collections = { docs };
