// Evil mdx parser
const passthrough = new Set(["mdxjsEsm", "mdxFlowExpression"]);
const isBlank = (node) => node.type === "text" && !node.value.trim();
const isElement = (node, tag) => node.type === "element" && (!tag || node.tagName === tag);

export function rehypeDocs() {
  return (tree) => {
    decorate(tree, null);
    wrapCards(tree);
  };
}

function wrapCards(tree) {
  const head = [];
  const chunks = [[]];
  for (const node of tree.children) {
    if (passthrough.has(node.type)) head.push(node);
    else if (isElement(node, "hr")) chunks.push([]);
    else chunks.at(-1).push(node);
  }

  const body = [];
  for (const chunk of chunks) {
    const content = chunk.filter((node) => !isBlank(node));
    if (content.length === 0) continue;
    const standalone = content.length === 1 && (isElement(content[0], "details") || content[0].type === "mdxJsxFlowElement");
    if (standalone) body.push(...content);
    else body.push({ type: "element", tagName: "section", properties: { className: ["lesson-card"] }, children: chunk });
  }

  tree.children = [...head, ...body];
}

function decorate(node, parent) {
  if (!node.children) return;

  for (let i = 0; i < node.children.length; i++) {
    const child = node.children[i];
    if (isElement(child, "table")) {
      addClass(child, "field-table");
      if (!(isElement(node, "div") && node.properties?.className?.includes("table-wrap"))) {
        node.children[i] = { type: "element", tagName: "div", properties: { className: ["table-wrap"] }, children: [child] };
      }
    } else if (isElement(child, "code") && !isElement(node, "pre")) {
      addClass(child, "inline-code");
    } else if (isElement(child, "p") && child.children[0]?.type === "text" && child.children[0].value.startsWith("Note:")) {
      addClass(child, "note");
      child.children[0].value = child.children[0].value.replace(/^Note:\s*/, "");
      child.children.unshift(noteIcon());
    } else if (isElement(child, "a") && /^https?:\/\//.test(String(child.properties?.href ?? ""))) {
      child.properties.target = "_blank";
      child.properties.rel = ["noopener", "noreferrer"];
    }

    decorate(child, node);
  }
}

// Note: 
function noteIcon() {
  return {
    type: "element",
    tagName: "svg",
    properties: { className: ["note-icon"], viewBox: "0 0 24 24", width: 18, height: 18, fill: "currentColor", ariaLabel: "Note", role: "img" },
    children: [{ type: "element", tagName: "path", properties: { d: "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z" }, children: [] }]
  };
}

function addClass(node, className) {
  const current = node.properties.className ?? [];
  node.properties.className = [...(Array.isArray(current) ? current : String(current).split(" ")), className];
}
