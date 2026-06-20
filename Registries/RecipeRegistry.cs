using System.Collections.Generic;
using UnityEngine;

namespace CUCoreLib.Registries
{
    public static class RecipeRegistry
    {
        internal static List<Recipe> RegisteredRecipes = new List<Recipe>();
        private static readonly HashSet<string> RegisteredRecipeKeys =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        private static List<Recipe> LastRecipeList;
        private static readonly HashSet<string> InjectedRecipeKeys =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public static void Register(Recipe recipe)
        {
            if (recipe == null || recipe.result == null || string.IsNullOrWhiteSpace(recipe.result.id))
            {
                CUCoreLibPlugin.Log.LogError("Recipe registration failed: Result ID is missing.");
                return;
            }

            ValidateRecipeReferences(recipe);

            string key = BuildRecipeKey(recipe);
            if (!RegisteredRecipeKeys.Add(key))
            {
                CUCoreLibPlugin.Log.LogWarning($"Recipe registration ignored duplicate recipe for '{recipe.result.id}'.");
                return;
            }

            RegisteredRecipes.Add(recipe);

            if (Recipes.recipes != null)
            {
                InjectRegisteredRecipes();
            }
        }

        internal static string BuildRecipeKey(Recipe recipe)
        {
            if (recipe == null || recipe.result == null) return string.Empty;

            string ingredientKey = BuildIngredientKey(recipe.items);
            return $"{recipe.result.id}|{recipe.result.amount}|{recipe.result.resultCondition}|{recipe.result.isLiquid}|{ingredientKey}";
        }

        internal static bool InjectSingleRecipe(Recipe recipe)
        {
            if (Recipes.recipes == null || recipe == null || recipe.result == null) return false;
            EnsureCurrentRecipeList();

            string recipeKey = BuildRecipeKey(recipe);
            if (InjectedRecipeKeys.Contains(recipeKey))
            {
                return false;
            }

            foreach (Recipe existing in Recipes.recipes)
            {
                if (BuildRecipeKey(existing).Equals(recipeKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    InjectedRecipeKeys.Add(recipeKey);
                    return false;
                }
            }

            if (recipe.items != null)
            {
                foreach (RecipeItem item in recipe.items)
                {
                    if (!string.IsNullOrEmpty(item.specificId))
                    {
                        NormalizeSpecificIngredientDefaults(item);
                        item.specific = true;
                    }

                    item.ignoredId = recipe.isRepair ? string.Empty : recipe.result.id;
                }
            }

            recipe.index = Recipes.recipes.Count;
            Recipes.recipes.Add(recipe);
            InjectedRecipeKeys.Add(recipeKey);
            return true;
        }

        internal static int InjectRegisteredRecipes()
        {
            if (Recipes.recipes == null)
            {
                return 0;
            }

            EnsureCurrentRecipeList();

            int added = 0;
            foreach (Recipe recipe in RegisteredRecipes)
            {
                if (InjectSingleRecipe(recipe))
                {
                    added++;
                }
            }

            if (added > 0)
            {
                CUCoreLibPlugin.Log.LogInfo($"Recipes: Added {added} recipes.");
            }

            return added;
        }

        private static void NormalizeSpecificIngredientDefaults(RecipeItem item)
        {
            if (item == null || item.specific)
            {
                return;
            }

            if (Mathf.Approximately(item.minimumCondition, 0.9f))
            {
                item.minimumCondition = 0f;
            }
        }

        private static void EnsureCurrentRecipeList()
        {
            if (!object.ReferenceEquals(LastRecipeList, Recipes.recipes))
            {
                LastRecipeList = Recipes.recipes;
                InjectedRecipeKeys.Clear();
            }
        }

        private static void ValidateRecipeReferences(Recipe recipe)
        {
            if (recipe?.items == null)
            {
                return;
            }

            for (int i = 0; i < recipe.items.Count; i++)
            {
                RecipeItem item = recipe.items[i];
                if (item == null)
                {
                    CUCoreLibPlugin.Log?.LogWarning($"Recipe '{recipe.result.id}' has a null ingredient at index {i}.");
                    continue;
                }

                if (item.specific)
                {
                    if (string.IsNullOrWhiteSpace(item.specificId))
                    {
                        CUCoreLibPlugin.Log?.LogWarning($"Recipe '{recipe.result.id}' has a specific ingredient without a specificId at index {i}.");
                        continue;
                    }

                    string normalizedId = item.specificId.Trim();
                    if (!TryResolveRecipeItemId(normalizedId, item.isLiquid))
                    {
                        CUCoreLibPlugin.Log?.LogWarning($"Recipe '{recipe.result.id}' references unknown {(item.isLiquid ? "liquid" : "item")} '{normalizedId}' at ingredient index {i}.");
                    }
                }
            }
        }

        private static bool TryResolveRecipeItemId(string id, bool isLiquid)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            if (isLiquid)
            {
                return LiquidRegistry.TryGetCustomInfo(id, out _) || Liquids.Registry.ContainsKey(id);
            }

            return ItemRegistry.TryGetItemInfo(id, out _) || Resources.Load<GameObject>(id) != null;
        }

        private static string BuildIngredientKey(List<RecipeItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                RecipeItem item = items[i];
                if (i > 0)
                {
                    builder.Append(';');
                }

                if (item == null)
                {
                    builder.Append("null");
                    continue;
                }

                builder.Append(item.minimumCondition);
                builder.Append('|');
                builder.Append(item.isLiquid ? 'L' : 'I');
                builder.Append('|');
                builder.Append(item.specific ? 'S' : 'A');
                builder.Append('|');
                builder.Append(string.IsNullOrWhiteSpace(item.specificId) ? string.Empty : item.specificId.Trim());
                builder.Append('|');
                builder.Append(item.destroyItem ? 'D' : 'K');
                builder.Append('|');
                builder.Append(item.quality != null ? item.quality.id : string.Empty);
                builder.Append('|');
                builder.Append(item.quality != null ? item.quality.amount.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty);
            }

            return builder.ToString();
        }
    }
}
