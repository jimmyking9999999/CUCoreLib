// Browser side of the tools page calculators.
import { colorOutputs, layerCount, layerOutputs, lootOutputs, outputsHtml } from "../tools.ts";

function readLayers(host: ParentNode): boolean[] {
  const layers = Array.from({ length: layerCount }, () => false);
  host.querySelectorAll<HTMLInputElement>("input[data-layer]").forEach((input) => {
    layers[Number(input.dataset.layer) - 1] = input.checked;
  });
  return layers;
}

export function bindCalculators(host: HTMLElement): void {
  const value = (id: string) => (host.querySelector<HTMLInputElement>(`#${id}`)?.value ?? "");
  const render = (id: string, html: string) => {
    const element = host.querySelector<HTMLElement>(`#${id}`);
    if (element) element.innerHTML = html;
  };

  const update = () => {
    render("calc-color-out", outputsHtml(colorOutputs(value("calc-color"))));
    render("calc-layer-out", outputsHtml(layerOutputs(readLayers(host))));
    render("calc-loot-out", outputsHtml(lootOutputs(value("calc-pool"), value("calc-weight"))));
  };

  host.addEventListener("input", update);
  host.addEventListener("click", async (event) => {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>("button[data-copy]");
    if (!button) return;
    await navigator.clipboard.writeText(button.dataset.copy ?? "");
    button.textContent = "Copied";
    window.setTimeout(() => (button.textContent = "Copy"), 1200);
  });
  update();
}
