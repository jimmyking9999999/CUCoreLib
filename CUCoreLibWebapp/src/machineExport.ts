import type { Ingredient, ItemState, PageId, RecipeState } from "./types.ts";

export const machineExportEnabledPageIds = [
  "welcome",
  "unity-csharp",
  "setup",
  "harmony0",
  "tutorial-first-mod",
  "assets",
  "audio",
  "item",
  "advanced-item",
  "recipe",
  "liquids",
  "player",
  "statuses",
  "moodles",
  "building-entities",
  "advanced-building-entities",
  "minigames",
  "tiles",
  "traps",
  "debug-testing",
  "utils",
  "console",
  "tools",
  "settings",
  "locale",
  "saving",
  "multi-mod-compatibility"
] as const satisfies readonly PageId[];

export const machineExportDefaultIngredients: Ingredient[] = [
  { mode: "specific", id: "glass", amount: "1", isLiquid: false, destroyItem: true },
  { mode: "specific", id: "glass", amount: "1", isLiquid: false, destroyItem: true },
  { mode: "specific", id: "glass", amount: "1", isLiquid: false, destroyItem: true },
  { mode: "quality", id: "heatsource", amount: "10", isLiquid: false, destroyItem: false },
  { mode: "quality", id: "water", amount: "50", isLiquid: true, destroyItem: true }
];

export const machineExportDefaultItemState: ItemState = {
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

export const machineExportDefaultRecipeState: RecipeState = {
  resultId: "conicalFlask",
  category: "Tools",
  intRequirement: "2",
  resultAmount: "1",
  resultCondition: "1",
  isLiquidResult: false
};
