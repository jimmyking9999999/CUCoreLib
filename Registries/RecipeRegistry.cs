using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CUCoreLib.ContentReload;
using UnityEngine;

namespace CUCoreLib.Registries;

public static class RecipeRegistry
{
    internal static List<Recipe> RegisteredRecipes = [];

    private static readonly HashSet<string> RegisteredRecipeKeys = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> RecipeOwners = new(StringComparer.OrdinalIgnoreCase);

    private static List<Recipe> LastRecipeList;

    private static readonly HashSet<string> InjectedRecipeKeys = new(StringComparer.OrdinalIgnoreCase);

    private static string ActiveOwnerId;

    public static void Register(Recipe recipe)
    {
        if (recipe?.result == null || string.IsNullOrWhiteSpace(recipe.result.id))
        {
            CUCoreLibPlugin.Log.LogError("Recipe registration failed: Result ID is missing.");
            return;
        }

        ValidateRecipeReferences(recipe);

        var key = BuildRecipeKey(recipe);
        if (!RegisteredRecipeKeys.Add(key))
        {
            CUCoreLibPlugin.Log.LogWarning(
                $"Recipe registration ignored duplicate recipe for '{recipe.result.id}'.");
            return;
        }

        RegisteredRecipes.Add(recipe);
        var ownerId = !string.IsNullOrWhiteSpace(ActiveOwnerId)
            ? ActiveOwnerId
            : ContentReloadSession.ResolveAmbientOwnerId();
        if (!string.IsNullOrWhiteSpace(ownerId)) RecipeOwners[key] = ownerId;

        if (Recipes.recipes != null) InjectRegisteredRecipes();
    }

    public static IDisposable BeginOwnerRegistration(string ownerId)
    {
        return new OwnerScope(ownerId);
    }

    internal static string BuildRecipeKey(Recipe recipe)
    {
        if (recipe?.result == null) return string.Empty;

        var ingredientKey = BuildIngredientKey(recipe.items);
        return
            $"{recipe.result.id}|{recipe.result.amount}|{recipe.result.resultCondition}|{recipe.result.isLiquid}|{ingredientKey}";
    }

    internal static bool InjectSingleRecipe(Recipe recipe)
    {
        if (Recipes.recipes == null || recipe?.result == null) return false;
        EnsureCurrentRecipeList();

        var recipeKey = BuildRecipeKey(recipe);
        if (InjectedRecipeKeys.Contains(recipeKey)) return false;

        if (Recipes.recipes.Any(existing =>
                BuildRecipeKey(existing).Equals(recipeKey, StringComparison.OrdinalIgnoreCase)))
        {
            InjectedRecipeKeys.Add(recipeKey);
            return false;
        }

        if (recipe.items != null)
            foreach (var item in recipe.items)
            {
                if (!string.IsNullOrEmpty(item.specificId))
                {
                    NormalizeSpecificIngredientDefaults(item);
                    item.specific = true;
                }

                item.ignoredId = recipe.isRepair ? string.Empty : recipe.result.id;
            }

        recipe.index = Recipes.recipes.Count;
        Recipes.recipes.Add(recipe);
        InjectedRecipeKeys.Add(recipeKey);
        return true;
    }

    internal static int InjectRegisteredRecipes()
    {
        if (Recipes.recipes == null) return 0;

        EnsureCurrentRecipeList();

        var added = RegisteredRecipes.Count(InjectSingleRecipe);

        if (added > 0) CUCoreLibPlugin.Log.LogInfo($"Recipes: Added {added} recipes.");

        return added;
    }

    internal static List<Recipe> CaptureOwnerEntries(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return [];

        var normalizedOwnerId = ownerId.Trim();
        return RegisteredRecipes
            .Where(recipe =>
            {
                var key = BuildRecipeKey(recipe);
                return RecipeOwners.TryGetValue(key, out var owner) &&
                       string.Equals(owner, normalizedOwnerId, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();
    }

    internal static void RestoreOwnerEntries(string ownerId, IEnumerable<Recipe> recipes)
    {
        if (recipes == null) return;

        foreach (var recipe in recipes) Register(recipe);
    }

    internal static void ClearOwnerEntries(string ownerId, ContentReloadResult result)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;

        var normalizedOwnerId = ownerId.Trim();
        var ownedKeys = RecipeOwners
            .Where(entry => string.Equals(entry.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
            .ToArray();

        if (ownedKeys.Length == 0) return;

        RegisteredRecipes.RemoveAll(recipe =>
        {
            var key = BuildRecipeKey(recipe);
            return RecipeOwners.TryGetValue(key, out var owner) &&
                   string.Equals(owner, normalizedOwnerId, StringComparison.OrdinalIgnoreCase);
        });

        foreach (var key in ownedKeys)
        {
            RegisteredRecipeKeys.Remove(key);
            RecipeOwners.Remove(key);
            InjectedRecipeKeys.Remove(key);
            var key1 = key;
            Recipes.recipes?.RemoveAll(recipe =>
                string.Equals(BuildRecipeKey(recipe), key1, StringComparison.OrdinalIgnoreCase));
        }

        result?.AddInfo("Cleared " + ownedKeys.Length + " recipes owned by '" + normalizedOwnerId + "'.");
    }

    private static void NormalizeSpecificIngredientDefaults(RecipeItem item)
    {
        if (item == null || item.specific) return;

        if (Mathf.Approximately(item.minimumCondition, 0.9f)) item.minimumCondition = 0f;
    }

    private static void EnsureCurrentRecipeList()
    {
        if (ReferenceEquals(LastRecipeList, Recipes.recipes)) return;
        LastRecipeList = Recipes.recipes;
        InjectedRecipeKeys.Clear();
    }

    private static void ValidateRecipeReferences(Recipe recipe)
    {
        if (recipe?.items == null) return;

        for (var i = 0; i < recipe.items.Count; i++)
        {
            var item = recipe.items[i];
            if (item == null)
            {
                CUCoreLibPlugin.Log?.LogWarning($"Recipe '{recipe.result.id}' has a null ingredient at index {i}.");
                continue;
            }

            if (!item.specific) continue;
            if (string.IsNullOrWhiteSpace(item.specificId))
            {
                CUCoreLibPlugin.Log?.LogWarning(
                    $"Recipe '{recipe.result.id}' has a specific ingredient without a specificId at index {i}.");
                continue;
            }

            var normalizedId = item.specificId.Trim();
            if (!TryResolveRecipeItemId(normalizedId, item.isLiquid))
                CUCoreLibPlugin.Log?.LogWarning(
                    $"Recipe '{recipe.result.id}' references unknown {(item.isLiquid ? "liquid" : "item")} '{normalizedId}' at ingredient index {i}.");
        }
    }

    private static bool TryResolveRecipeItemId(string id, bool isLiquid)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        if (isLiquid) return LiquidRegistry.TryGetCustomInfo(id, out _) || Liquids.Registry.ContainsKey(id);

        if (ItemRegistry.TryGetCustomInfo(id, out _)) return true;

        if (Item.GlobalItems == null)
            // Recipe registration commonly runs during plugin Awake, before vanilla item tables exist.
            // Defer vanilla-item validation until runtime instead of warning on valid base-game IDs.
            return true;

        return Item.GlobalItems.ContainsKey(id) || Resources.Load<GameObject>(id) != null;
    }

    private static string BuildIngredientKey(List<RecipeItem> items)
    {
        if (items == null || items.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (i > 0) builder.Append(';');

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
            builder.Append(item.quality != null
                ? item.quality.amount.ToString(CultureInfo.InvariantCulture)
                : string.Empty);
        }

        return builder.ToString();
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly string previousOwnerId;

        public OwnerScope(string ownerId)
        {
            previousOwnerId = ActiveOwnerId;
            ActiveOwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId.Trim();
        }

        public void Dispose()
        {
            ActiveOwnerId = previousOwnerId;
        }
    }
}