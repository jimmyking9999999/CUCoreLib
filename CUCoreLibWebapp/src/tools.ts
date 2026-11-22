import { escapeHtml } from "./generators.ts";

export const layerCount = 12;

export function colorLiteral(hex: string): string {
  const match = /^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hex.trim());
  const channels = match ? [1, 2, 3].map((index) => parseInt(match[index], 16) / 255) : [1, 1, 1];
  return `new Color(${channels.map((value) => `${Number(value.toFixed(2))}f`).join(", ")})`;
}


export function layerCheckboxes(prefix: string, layers: boolean[]): string {
  return `<div class="layer-grid">${layers.map((checked, index) =>
    `<label class="layer-toggle"><input type="checkbox" data-layer="${index + 1}" id="${prefix}-layer-${index + 1}"${checked ? " checked" : ""}><span>${index + 1}</span></label>`
  ).join("")}</div>`;
}

export function layerExpression(registry: string, layers: boolean[]): string | null {
  const selected = layers.map((on, index) => (on ? index + 1 : 0)).filter(Boolean);
  if (selected.length === 0 || selected.length === layers.length) return null;
  const excluded = layers.map((on, index) => (on ? 0 : index + 1)).filter(Boolean);
  return excluded.length < selected.length
    ? `${registry}.AllLayersExcept(${excluded.join(", ")})`
    : `${registry}.LayersToMask(${selected.join(", ")})`;
}

export function layerMask(layers: boolean[]): number {
  if (!layers.some(Boolean) || layers.every(Boolean)) return -1;
  return layers.reduce((mask, on, index) => (on ? mask | (1 << index) : mask), 0);
}


export function calculatorsHtml(): string {
  return `
    <details open>
      <summary>Color to Unity color</summary>
      <div class="details-body">
        <div class="form-grid">
          <label>Pick a color<input id="calc-color" type="color" value="#7fe0a0"></label>
        </div>
        <div class="tool-outputs" id="calc-color-out"></div>
      </div>
    </details>
    <details open>
      <summary>Spawn layer mask</summary>
      <div class="details-body">
        <p class="hint">1-indexed. Layer 1 is the first layer of a run. The full game has ${layerCount-1}.</p>
        ${layerCheckboxes("calc", Array.from({ length: layerCount-1 }, () => false))}
        <div class="tool-outputs" id="calc-layer-out"></div>
      </div>
    </details>
    <details open>
      <summary>Loot pool odds</summary>
      <div class="details-body">
        <div class="form-grid">
          <label>Other entries in the pool<input id="calc-pool" type="number" min="0" step="1" value="40"><span class="hint">Count them in the decompiled loot table.</span></label>
          <label>Your spawn weight<input id="calc-weight" type="number" min="0" step="0.1" value="1"><span class="hint">The 4th argument of ItemRegistry.Register.</span></label>
        </div>
        <div class="tool-outputs" id="calc-loot-out"></div>
      </div>
    </details>
  `;
}

export interface ToolOutput {
  label: string;
  value: string;
  ok?: boolean;
  copy?: boolean;
}

export function outputsHtml(outputs: ToolOutput[]): string {
  return outputs.map((output) => `
    <div class="tool-output${output.ok === false ? " is-bad" : output.ok ? " is-good" : ""}">
      <span class="tool-output-label">${escapeHtml(output.label)}</span>
      <code>${escapeHtml(output.value)}</code>
      ${output.copy ? `<button class="mini-button" type="button" data-copy="${escapeHtml(output.value)}">Copy</button>` : ""}
    </div>`).join("");
}

export function colorOutputs(hex: string): ToolOutput[] {
  const match = /^#?([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(hex.trim());
  const bytes = match ? [1, 2, 3].map((index) => parseInt(match[index], 16)) : [255, 255, 255];
  return [
    { label: "Color", value: colorLiteral(hex), copy: true },
    { label: "Color32", value: `new Color32(${bytes.join(", ")}, 255)`, copy: true },
    { label: "Hex (for TMP <color=...>)", value: `#${bytes.map((byte) => byte.toString(16).padStart(2, "0")).join("")}`, copy: true }
  ];
}

export function layerOutputs(layers: boolean[]): ToolOutput[] {
  const mask = layerMask(layers);
  if (mask === -1) {
    return [{ label: "Every layer", value: "Leave SpawnLayers unset (that's the default)" }];
  }
  return [
    { label: "Buildings", value: layerExpression("BuildingEntityRegistry", layers) ?? "", copy: true },
    { label: "Tiles", value: layerExpression("TileRegistry", layers) ?? "", copy: true },
    { label: "Liquid tiles", value: layerExpression("LiquidTileRegistry", layers) ?? "", copy: true },
    { label: "Raw mask", value: String(mask), copy: true }
  ];
}

export function lootOutputs(poolText: string, weightText: string): ToolOutput[] {
  const pool = Math.max(0, Math.floor(Number(poolText) || 0));
  const weight = Math.max(0, Number(weightText) || 0);
  if (weight === 0) return [{ label: "Chance", value: "0%: a weight of 0 keeps it out of loot pools", ok: false }];

  const whole = Math.floor(weight);
  const fraction = weight - whole;
  const chanceFor = (entries: number) => (entries === 0 ? 0 : entries / (pool + entries));
  const percent = (value: number) => `${(value * 100).toFixed(value < 0.01 ? 2 : 1)}%`;
  // Fractional weights roll an extra entry once per pool per world
  const expected = (1 - fraction) * chanceFor(whole) + fraction * chanceFor(whole + 1);

  const outputs: ToolOutput[] = [
    { label: "Chance per pick", value: `${percent(expected)} (about 1 in ${expected > 0 ? Math.round(1 / expected) : "∞"})` }
  ];
  if (fraction > 0) {
    outputs.push({
      label: "Per world",
      value: `${percent(1 - fraction)} of worlds: ${whole} entr${whole === 1 ? "y" : "ies"} (${percent(chanceFor(whole))}), ${percent(fraction)}: ${whole + 1} (${percent(chanceFor(whole + 1))})`
    });
  }
  return outputs;
}
