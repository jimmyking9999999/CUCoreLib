using System.Collections.Generic;

namespace CUCoreLib.Data
{
    public class CustomRecipe
    {
        public Recipes.RecipeCategory category;

        // No need for simpleName because the game calculates it automatically fyi
        public int INT;
        public List<RecipeItem> items = new List<RecipeItem>();
        public CustomRecipeResult result;
    }

    public class CustomRecipeResult
    {
        public int amount = 1;
        public bool dontDrainResultLiquid;
        public string id;
        public bool isLiquid;
        public float resultCondition = 1f;
    }
}