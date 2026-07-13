using System.Collections.Generic;

namespace CUCoreLib.Data
{
    /// <summary>
    ///     Defines a custom crafting recipe registered through <c>RecipeRegistry.Register</c>.
    /// </summary>
    public class CustomRecipe
    {
        /// <summary>
        ///     Vanilla recipe category used to group the recipe in crafting menus.
        /// </summary>
        public Recipes.RecipeCategory category;

        // No need for simpleName because the game calculates it automatically fyi

        /// <summary>
        ///     Minimum intelligence required to craft the recipe (note that -3 INT can still attempt to craft, but with a
        ///     penalty).
        /// </summary>
        public int INT;

        /// <summary>
        ///     Ingredient list required by the recipe.
        ///     Use with new RecipeItem(float? conditionreq) { id = "item_id", amount = 1 } for each ingredient.
        /// </summary>
        public List<RecipeItem> items = new List<RecipeItem>();

        /// <summary>
        ///     Output produced when the recipe completes.
        ///     Use with result = new RecipeResult { id = "item_id", amount = 1, ... },
        /// </summary>
        public CustomRecipeResult result;
    }

    /// <summary>
    ///     Describes the result produced by a <see cref="CustomRecipe" />.
    /// </summary>
    public class CustomRecipeResult
    {
        /// <summary>
        ///     Number of result units produced per craft.
        /// </summary>
        public int amount = 1;

        /// <summary>
        ///     Prevents the output liquid container from being drained when used as a liquid result.
        /// </summary>
        public bool dontDrainResultLiquid;

        /// <summary>
        ///     Item or liquid id produced by the recipe.
        /// </summary>
        public string id;

        /// <summary>
        ///     Whether <see cref="id" /> points to a liquid registration instead of an item.
        /// </summary>
        public bool isLiquid;

        /// <summary>
        ///     Initial condition applied to the crafted result.
        /// </summary>
        public float resultCondition = 1f;
    }
}