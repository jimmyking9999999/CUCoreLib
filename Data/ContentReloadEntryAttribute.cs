using System;

namespace CUCoreLib.Data
{
    public enum ContentReloadEntryStage
    {
        LoadAssets = 0,
        RegisterText = 100,
        RegisterLocale = 200,
        RegisterLiquids = 300,
        RegisterItems = 400,
        RegisterRecipes = 500
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ContentReloadEntryAttribute : Attribute
    {
        public ContentReloadEntryStage Stage { get; }
        public int Order { get; set; }

        public ContentReloadEntryAttribute(ContentReloadEntryStage stage)
        {
            Stage = stage;
        }
    }
}
