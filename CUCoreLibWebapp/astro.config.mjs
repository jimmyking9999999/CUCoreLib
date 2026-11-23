import { defineConfig } from "astro/config";
import mdx from "@astrojs/mdx";
import sitemap from "@astrojs/sitemap";
import { remarkPanelCode } from "./src/plugins/remark-panel-code.mjs";
import { rehypeDocs } from "./src/plugins/rehype-docs.mjs";

export default defineConfig({
  site: "https://cucorelib.jimmyking.dev",
  trailingSlash: "always",
  integrations: [mdx(), sitemap({ filter: (page) => !page.endsWith("/404/") })],
  markdown: {
    shikiConfig: { theme: "dark-plus" },
    remarkPlugins: [remarkPanelCode],
    rehypePlugins: [rehypeDocs]
  },
  server: { host: "127.0.0.1", port: 5174 }
});
