using System;
using System.Collections.Generic;
using System.Linq;

namespace CUCoreLib.Registries.Modules
{
    internal static class RecipeVanillaEditQueue
    {
        private static readonly List<QueuedVanillaRecipeEdit> QueuedVanillaRecipeEdits =
            new List<QueuedVanillaRecipeEdit>();

        internal static int QueueVanillaRecipeEdit(string resultId, Action<Recipe> edit)
        {
            return Enqueue(QueuedVanillaRecipeEdit.ForResultId(resultId, edit));
        }

        internal static int QueueVanillaRecipeEdit(Func<Recipe, bool> match, Action<Recipe> edit)
        {
            return Enqueue(QueuedVanillaRecipeEdit.ForPredicate(match, edit));
        }

        private static int Enqueue(QueuedVanillaRecipeEdit edit)
        {
            if (edit == null) return 0;

            QueuedVanillaRecipeEdits.Add(edit);
            CUCoreLibPlugin.Log?.LogInfo($"Recipes: Queued vanilla recipe {edit.Describe()}.");

            if (Recipes.recipes == null) return 0;

            var mutations = ApplyVanillaRecipeEdits();
            if (mutations > 0) Patches.RecipeRegistryPatches.RefreshCraftingUi();

            return mutations;
        }

        internal static int ApplyVanillaRecipeEdits()
        {
            if (Recipes.recipes == null || QueuedVanillaRecipeEdits.Count == 0) return 0;

            int mutations = 0;

            foreach (Recipe recipe in Recipes.recipes.ToArray())
            {
                if (IsLibraryRegisteredRecipe(recipe)) continue;

                foreach (QueuedVanillaRecipeEdit edit in QueuedVanillaRecipeEdits)
                    mutations += edit.TryApply(recipe) ? 1 : 0;
            }

            if (mutations > 0)
                CUCoreLibPlugin.Log?.LogInfo($"Recipes: Applied edits to {mutations} vanilla recipe(s).");

            return mutations;
        }

        private static bool IsLibraryRegisteredRecipe(Recipe recipe)
        {
            if (recipe?.result == null) return false;

            return RecipeRegistry.RegisteredRecipeKeys.Contains(RecipeRegistry.BuildRecipeKey(recipe));
        }

        private abstract class QueuedVanillaRecipeEdit
        {
            private readonly Action<Recipe> Edit;
            private readonly HashSet<Recipe> AppliedTo = new HashSet<Recipe>();

            private QueuedVanillaRecipeEdit(Action<Recipe> edit)
            {
                Edit = edit;
            }

            internal static QueuedVanillaRecipeEdit ForResultId(string resultId, Action<Recipe> edit)
            {
                if (string.IsNullOrWhiteSpace(resultId) || edit == null) return null;
                return new ResultIdEdit(resultId.Trim(), edit);
            }

            internal static QueuedVanillaRecipeEdit ForPredicate(Func<Recipe, bool> match, Action<Recipe> edit)
            {
                if (match == null || edit == null) return null;
                return new PredicateEdit(match, edit);
            }

            // Fills the queue-log sentence: "Queued vanilla recipe <Describe()>."
            internal abstract string Describe();

            internal string LabelFor(Recipe recipe)
            {
                return recipe.result != null ? recipe.result.id : "<no result>";
            }

            internal abstract bool Matches(Recipe recipe);

            internal bool TryApply(Recipe recipe)
            {
                if (recipe == null || AppliedTo.Contains(recipe)) return false;
                if (!Matches(recipe)) return false;

                try
                {
                    Edit(recipe);
                }
                catch (Exception ex)
                {
                    CUCoreLibPlugin.Log?.LogError(
                        $"Recipes: Vanilla recipe edit threw for '{LabelFor(recipe)}' and stays queued: {ex}");
                    return false;
                }

                RecipeRegistry.NormalizeRecipeIngredients(recipe);
                AppliedTo.Add(recipe);
                return true;
            }

            private sealed class ResultIdEdit : QueuedVanillaRecipeEdit
            {
                private readonly string ResultId;

                internal ResultIdEdit(string resultId, Action<Recipe> edit)
                    : base(edit)
                {
                    ResultId = resultId;
                }

                internal override string Describe()
                {
                    return $"edit for '{ResultId}'";
                }

                internal override bool Matches(Recipe recipe)
                {
                    var resultId = recipe?.result?.id;
                    return !string.IsNullOrWhiteSpace(resultId) &&
                           string.Equals(resultId.Trim(), ResultId, StringComparison.OrdinalIgnoreCase);
                }
            }

            private sealed class PredicateEdit : QueuedVanillaRecipeEdit
            {
                private readonly Func<Recipe, bool> Match;

                internal PredicateEdit(Func<Recipe, bool> match, Action<Recipe> edit)
                    : base(edit)
                {
                    Match = match;
                }

                internal override string Describe()
                {
                    return "predicate edit";
                }

                internal override bool Matches(Recipe recipe)
                {
                    try
                    {
                        return Match(recipe);
                    }
                    catch (Exception ex)
                    {
                        CUCoreLibPlugin.Log?.LogWarning(
                            $"Recipes: Vanilla recipe edit predicate threw for '{LabelFor(recipe)}': {ex.Message}");
                        return false;
                    }
                }
            }
        }
    }
}
