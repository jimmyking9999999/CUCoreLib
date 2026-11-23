
export function remarkPanelCode() {
  return (tree, file) => {
    const index = tree.children.findIndex((node) => node.type === "code" && /(^|\s)panel(\s|$)/.test(node.meta ?? ""));
    if (index === -1) return;

    const node = tree.children[index];
    tree.children.splice(index, 1);
    const frontmatter = (file.data.astro ??= {}).frontmatter ??= {};
    frontmatter.panelCode = node.value;
    frontmatter.panelLang = node.lang ?? "csharp";
    frontmatter.panelTitle = node.meta.match(/title="([^"]*)"/)?.[1] ?? "Example.cs";
  };
}
