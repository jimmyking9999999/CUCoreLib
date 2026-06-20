using System.Collections.Generic;
using UnityEngine;

namespace CUCoreLib.Data
{
    public class CustomRecipe
    {
        // No need for simpleName because the game calculates it automatically fyi
        public int INT;
        public CustomRecipeResult result;
        public List<RecipeItem> items = new List<RecipeItem>();
        public Recipes.RecipeCategory category;
    }

    public class CustomRecipeResult
    {
        public string id;
        public float resultCondition = 1f;
        public int amount = 1;
        public bool isLiquid;
        public bool dontDrainResultLiquid;
    }
}