export type PageId = string;
export type IngredientMode = "specific" | "quality";

export interface Page {
  id: PageId;
  label: string;
  crumb: string;
  title: string;
  lead: string;
}

export interface Ingredient {
  mode: IngredientMode;
  id: string;
  amount: string;
  isLiquid: boolean;
  destroyItem: boolean;
}

export interface ItemState {
  id: string;
  name: string;
  description: string;
  category: string;
  sprite: string;
  weight: string;
  value: string;
  spawnFrequency: string;
  decayMinutes: string;
  recognition: string;
  tags: string;
  usable: boolean;
  usableOnLimb: boolean;
  sickness: string;
  limbSkinHealth: string;
  limbMuscleHealth: string;
  limbPain: string;
  limbTemperature: string;
  limbChillSeconds: string;
  eat: string;
  drink: string;
  happiness: string;
  sound: string;
}

export interface RecipeState {
  resultId: string;
  category: string;
  intRequirement: string;
  resultAmount: string;
  resultCondition: string;
  isLiquidResult: boolean;
}

export interface HoverPanel {
  title: string;
  signature?: string;
  body: string;
  items?: string[];
}
