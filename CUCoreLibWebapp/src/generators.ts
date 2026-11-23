import type { Ingredient, ItemState, RecipeState } from "./types.ts";

export const defaultIngredients: Ingredient[] = [
  { mode: "specific", id: "glass", amount: "0.9", amountWasEdited: false, isLiquid: false, destroyItem: true },
  { mode: "specific", id: "glass", amount: "0.9", amountWasEdited: false, isLiquid: false, destroyItem: true },
  { mode: "specific", id: "glass", amount: "0.9", amountWasEdited: false, isLiquid: false, destroyItem: true },
  { mode: "quality", id: "heatsource", amount: "1", amountWasEdited: false, isLiquid: false, destroyItem: false },
  { mode: "quality", id: "water", amount: "50", amountWasEdited: false, isLiquid: true, destroyItem: true }
];

export const defaultItemState: ItemState = {
  id: "sunpear",
  name: "Sunpear",
  description: "A pale yellow fruit. Probably edible.",
  category: "food",
  sprite: "sunpear.png",
  weight: "0.4",
  value: "4",
  spawnFrequency: "1",
  decayMinutes: "180",
  recognition: "2",
  tags: "cangetwet",
  usable: true,
  usableOnLimb: false,
  sickness: "0",
  limbSkinHealth: "0",
  limbMuscleHealth: "0",
  limbPain: "0",
  limbTemperature: "-1",
  limbChillSeconds: "100",
  eat: "12",
  drink: "4",
  happiness: "1",
  sound: "eatCrunch"
};

export const defaultRecipeState: RecipeState = {
  resultId: "conicalFlask",
  category: "Tools",
  intRequirement: "2",
  resultAmount: "1",
  resultCondition: "1",
  isLiquidResult: false
};

export function escapeHtml(value: string): string {
  return value.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function escapeCsharp(value: string): string {
  return value.replace(/\\/g, "\\\\").replace(/"/g, "\\\"");
}

function floatLiteral(value: string | number): string {
  const number = Number(value);
  const clean = Number.isFinite(number) ? String(Number(number.toFixed(4))) : "0";
  return `${clean}f`;
}

function intLiteral(value: string): string {
  const number = Number.parseInt(value, 10);
  return Number.isFinite(number) ? String(number) : "0";
}

function csharpIdentifier(value: string, fallback: string): string {
  const cleaned = (value || fallback).replace(/[^A-Za-z0-9_]/g, "_").replace(/^[^A-Za-z_]+/, "");
  return cleaned || fallback;
}

function textInput(id: string, label: string, value: string, hint = "", full = false): string {
  return `<label class="${full ? "full" : ""}">${label}<input id="${id}" value="${escapeHtml(value)}">${hint ? `<span class="hint">${hint}</span>` : ""}</label>`;
}

function textareaInput(id: string, label: string, value: string): string {
  return `<label class="full">${label}<textarea id="${id}">${escapeHtml(value)}</textarea></label>`;
}

function selectInput(id: string, label: string, options: string[], value: string): string {
  const optionHtml = options.map((option) => `<option value="${escapeHtml(option)}"${option === value ? " selected" : ""}>${option || "none"}</option>`).join("");
  return `<label>${label}<select id="${id}">${optionHtml}</select></label>`;
}

function rangeInput(id: string, label: string, min: string, max: string, step: string, value: string, hint = ""): string {
  return `<label>${label}<span class="range-row"><input id="${id}" type="range" min="${min}" max="${max}" step="${step}" value="${value}"><span class="range-value" data-range-for="${id}">${value}</span></span>${hint ? `<span class="hint">${hint}</span>` : ""}</label>`;
}

function checkboxInput(id: string, label: string, checked: boolean, full = false): string {
  return `<label class="checkbox-row ${full ? "full" : ""}"><input id="${id}" type="checkbox"${checked ? " checked" : ""}>${label}</label>`;
}

export function itemFormHtml(itemState: ItemState): string {
  return `
    <details closed>
      <summary>Identity</summary>
      <div class="details-body form-grid">
        ${textInput("item-id", "Item ID", itemState.id, "Lowercase IDs are easiest. No spaces.")}
        ${textInput("item-name", "Display Name", itemState.name)}
        ${textareaInput("item-description", "Description", itemState.description)}
        ${selectInput("item-category", "Category", ["food", "tool", "weapon", "medicine", "nospawn", "trash"], itemState.category)}
        ${textInput("item-sprite", "Embedded Sprite File", itemState.sprite, "Embed this PNG in your mod project.")}
      </div>
    </details>
    <details closed>
      <summary>Stats</summary>
      <div class="details-body form-grid">
        ${rangeInput("item-weight", "Weight", "0", "20", "0.1", itemState.weight)}
        ${rangeInput("item-value", "Value", "0", "200", "1", itemState.value)}
        <label>Spawn Frequency<input id="item-spawn" type="number" min="0" step="any" value="${escapeHtml(itemState.spawnFrequency)}"><span class="hint">0 disables pooled drops. Fractions reroll per pool each world generation.</span></label>
        ${rangeInput("item-decay", "Decay Minutes", "0", "1440", "10", itemState.decayMinutes)}
        ${rangeInput("item-recognition", "Recognition", "0", "15", "1", itemState.recognition, "Vanilla items usually set this explicitly.")}
        ${textInput("item-tags", "Tags", itemState.tags, "Comma-separated vanilla tags.", true)}
      </div>
    </details>
    <details closed>
      <summary>Use actions</summary>
      <div class="details-body form-grid">
        ${checkboxInput("item-usable", "Item can be used", itemState.usable, true)}
        ${checkboxInput("item-usable-limb", "Item can be used on limbs", itemState.usableOnLimb, true)}
        ${itemState.usable ? rangeInput("item-eat", "Eat Amount", "-20", "60", "1", itemState.eat) : ""}
        ${itemState.usable ? rangeInput("item-drink", "Drink Amount", "-20", "60", "1", itemState.drink) : ""}
        ${itemState.usable ? rangeInput("item-happiness", "Happiness", "-10", "10", "0.5", itemState.happiness) : ""}
        ${itemState.usable ? rangeInput("item-sickness", "Sickness", "-50", "50", "1", itemState.sickness) : ""}
        ${itemState.usable ? selectInput("item-sound", "Sound", ["eatCrunch", "eatFlesh", "drink", "combine", ""], itemState.sound) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-skin", "Skin Health", "-100", "100", "1", itemState.limbSkinHealth) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-muscle", "Muscle Health", "-100", "100", "1", itemState.limbMuscleHealth) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-pain", "Pain", "-100", "100", "1", itemState.limbPain) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-temperature", "Limb Temperature Change", "-5", "5", "0.5", itemState.limbTemperature) : ""}
        ${itemState.usableOnLimb ? rangeInput("item-limb-chill", "Chilled Limb Seconds", "0", "300", "10", itemState.limbChillSeconds) : ""}
      </div>
    </details>
  `;
}

function ingredientCards(ingredients: Ingredient[]): string {
  return ingredients
    .map(
      (ingredient, index) => `
        <section class="ingredient-card">
          <header>
            <h3>Ingredient ${index + 1}</h3>
            <button class="mini-button" type="button" data-remove-ingredient="${index}">Remove</button>
          </header>
          <div class="form-grid">
            <label>Match Type
              <select data-ingredient-field="mode" data-index="${index}">
                <option value="specific"${ingredient.mode === "specific" ? " selected" : ""}>Specific ID</option>
                <option value="quality"${ingredient.mode === "quality" ? " selected" : ""}>Crafting Quality</option>
              </select>
            </label>
            <label>ID Or Quality
              <input value="${escapeHtml(ingredient.id)}" data-ingredient-field="id" data-index="${index}">
            </label>
            <label>Condition / mL / Quality Level
              <input type="number" min="0" step="0.1" value="${escapeHtml(ingredient.amount)}" data-ingredient-field="amount" data-index="${index}">
            </label>
            <label class="checkbox-row">
              <input type="checkbox" ${ingredient.isLiquid ? "checked" : ""} data-ingredient-field="isLiquid" data-index="${index}">
              Liquid ingredient
            </label>
            <label class="checkbox-row full">
              <input type="checkbox" ${ingredient.destroyItem ? "checked" : ""} data-ingredient-field="destroyItem" data-index="${index}">
              Consume or destroy this ingredient
            </label>
          </div>
        </section>
      `
    )
    .join("");
}

export function recipeFormHtml(recipeState: RecipeState, ingredients: Ingredient[]): string {
  return `
    <details open>
      <summary>Result</summary>
      <div class="details-body form-grid">
        ${textInput("recipe-result", "Result Item ID", recipeState.resultId)}
        ${selectInput("recipe-category", "Category", ["Materials", "Tools", "Medicine", "Utilities", "Food"], recipeState.category)}
        ${rangeInput("recipe-int", "Intelligence Requirement", "0", "20", "1", recipeState.intRequirement)}
        ${rangeInput("recipe-amount", "Result Amount", "1", "10", "1", recipeState.resultAmount)}
        ${rangeInput("recipe-condition", "Result Condition", "0", recipeState.isLiquidResult ? "500" : "1", recipeState.isLiquidResult ? "1" : "0.05", recipeState.resultCondition, recipeState.isLiquidResult ? "Liquid results use mL." : "Item results use condition from 0 to 1.")}
        ${checkboxInput("recipe-liquid-result", "Result is liquid", recipeState.isLiquidResult)}
      </div>
    </details>
    <details open>
      <summary>Ingredients</summary>
      <div class="details-body">
        <div class="ingredient-list" id="ingredient-list">${ingredientCards(ingredients)}</div>
        <div class="actions">
          <button class="action-button primary" type="button" id="add-ingredient">Add Ingredient</button>
          <button class="action-button" type="button" id="reset-ingredients">Reset</button>
        </div>
      </div>
    </details>
  `;
}

export function itemCode(itemState: ItemState): string {
  const id = itemState.id.trim() || "myitem";
  const spriteVariable = `${csharpIdentifier(id, "item")}Sprite`;
  const eat = Number(itemState.eat);
  const drink = Number(itemState.drink);
  const happiness = Number(itemState.happiness);
  const sickness = Number(itemState.sickness);
  const limbSkinHealth = Number(itemState.limbSkinHealth);
  const limbMuscleHealth = Number(itemState.limbMuscleHealth);
  const limbPain = Number(itemState.limbPain);
  const limbTemperature = Number(itemState.limbTemperature);
  const limbChillSeconds = Number(itemState.limbChillSeconds);
  const useLines: string[] = [];
  const limbLines: string[] = [];

  if (eat !== 0) useLines.push(`        body.Eat(${floatLiteral(eat)}, 0.5f);`);
  if (drink !== 0) useLines.push(`        body.Drink(${floatLiteral(drink)});`);
  if (happiness !== 0) useLines.push(`        body.happiness += ${floatLiteral(happiness)};`);
  if (sickness !== 0) useLines.push(`        body.sicknessAmount = Mathf.Clamp(body.sicknessAmount + ${floatLiteral(sickness)}, 0f, 100f);`);
  useLines.push("        item.condition -= 1f;");
  if (itemState.sound) useLines.push(`        Sound.Play("${escapeCsharp(itemState.sound)}", body.transform.position);`);

  const useAction = itemState.usable
    ? `,
    useAction = (body, item) =>
    {
${useLines.join("\n")}
    }`
    : "";
  if (limbSkinHealth !== 0) {
    limbLines.push(`        limb.skinHealth = Mathf.Clamp(limb.skinHealth + ${floatLiteral(limbSkinHealth)}, 0f, 100f);`);
  }
  if (limbMuscleHealth !== 0) {
    limbLines.push(`        limb.muscleHealth = Mathf.Clamp(limb.muscleHealth + ${floatLiteral(limbMuscleHealth)}, 0f, 100f);`);
  }
  if (limbPain !== 0) {
    limbLines.push(`        limb.pain = Mathf.Clamp(limb.pain + ${floatLiteral(limbPain)}, 0f, 100f);`);
  }
  if (limbTemperature !== 0) {
    limbLines.push(`        limb.body.temperature += ${floatLiteral(limbTemperature)};`);
  }
  if (limbChillSeconds > 0) {
    limbLines.push(`        ChilledLimb chilled = limb.gameObject.GetComponent<ChilledLimb>() ?? limb.gameObject.AddComponent<ChilledLimb>();`);
    limbLines.push(`        chilled.timeLeft = ${floatLiteral(limbChillSeconds)};`);
    limbLines.push(`        chilled.maxTime = ${floatLiteral(limbChillSeconds)};`);
  }
  const useLimbAction = itemState.usableOnLimb
    ? `,
    usableOnLimb = true,
    useLimbAction = (limb, item) =>
    {
        if (item.condition < 0.9f) return;

        item.condition -= 1f;
${limbLines.join("\n")}
    }`
    : "";
  const spawnNumber = Number(itemState.spawnFrequency);
  const spawnFrequency = !Number.isFinite(spawnNumber) || spawnNumber < 0 ? "0"
    : Number.isInteger(spawnNumber) ? String(spawnNumber) : `${spawnNumber}f`;
  const spawnFrequencyArgument = spawnFrequency === "1" ? "" : `, ${spawnFrequency}`;
  const recognition = intLiteral(itemState.recognition);
  // Vanilla defaults (Recognition(2), not usable, no decay) are left out
  const recognitionLine = recognition === "2" ? "" : `,
    rec = new Recognition(${recognition})`;
  const usableLine = itemState.usable ? `,
    usable = true` : "";
  const decayLine = Number(itemState.decayMinutes) > 0 ? `,
    decayMinutes = ${floatLiteral(itemState.decayMinutes)}` : "";

  return `Sprite ${spriteVariable} = AssetLoader.LoadEmbeddedSprite("${escapeCsharp(itemState.sprite || `${id}.png`)}");

ItemRegistry.Register("${escapeCsharp(id)}", new ItemInfo
{
    fullName = "${escapeCsharp(itemState.name || "My Item")}",
    description = "${escapeCsharp(itemState.description)}",
    category = "${escapeCsharp(itemState.category)}",
    weight = ${floatLiteral(itemState.weight)},
    value = ${intLiteral(itemState.value)},
    tags = "${escapeCsharp(itemState.tags)}"${decayLine}${recognitionLine}${usableLine}${useAction}${useLimbAction}
}, ${spriteVariable}${spawnFrequencyArgument});`;
}

function ingredientCode(ingredient: Ingredient): string {
  const amount = floatLiteral(ingredient.amount);
  const props: string[] = [];
  let constructorArg: string;

  if (ingredient.mode === "specific") {
    props.push(`specificId = "${escapeCsharp(ingredient.id)}"`);
    // Minimum condition for items (vanilla default 0.9 needs no argument), mL for liquids
    constructorArg = !ingredient.isLiquid && Number(ingredient.amount) === 0.9 ? "" : `(${amount})`;
  } else {
    // The number is the required quality level; the constructor argument is minimum condition, so allow any
    // Level 1 (any item with the quality) is the default, so vanilla writes just the ID
    props.push(Number(ingredient.amount) === 1
      ? `quality = "${escapeCsharp(ingredient.id)}"`
      : `quality = new CraftingQuality("${escapeCsharp(ingredient.id)}", ${amount})`);
    constructorArg = "(0f)";
  }

  if (ingredient.isLiquid) props.push("isLiquid = true");
  if (!ingredient.destroyItem) props.push("destroyItem = false");

  return `new RecipeItem${constructorArg} { ${props.join(", ")} }`;
}

export function recipeCode(recipeState: RecipeState, ingredients: Ingredient[]): string {
  const ingredientLines = ingredients.map((ingredient) => `        ${ingredientCode(ingredient)}`).join(",\n");

  // Only write result fields that differ from vanilla's defaults (amount 1, condition 1, not liquid)
  const resultFields = [`id = "${escapeCsharp(recipeState.resultId || "myitem")}"`];
  if (intLiteral(recipeState.resultAmount) !== "1") resultFields.push(`amount = ${intLiteral(recipeState.resultAmount)}`);
  if (recipeState.isLiquidResult) resultFields.push("isLiquid = true");
  if (Number(recipeState.resultCondition) !== 1) resultFields.push(`resultCondition = ${floatLiteral(recipeState.resultCondition)}`);

  return `RecipeRegistry.Register(new Recipe
{
    INT = ${intLiteral(recipeState.intRequirement)},
    category = Recipes.RecipeCategory.${recipeState.category},
    result = new RecipeResult { ${resultFields.join(", ")} },
    items = new List<RecipeItem>
    {
${ingredientLines}
    }
});`;
}

export function generatorCode(kind: "item" | "recipe"): string {
  return kind === "item" ? itemCode(defaultItemState) : recipeCode(defaultRecipeState, defaultIngredients);
}
