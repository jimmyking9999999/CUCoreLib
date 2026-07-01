using System;

namespace CUCoreLib.Data;

public enum ContentReloadEntryStage
{
    LoadAssets = 0,
    RegisterText = 100,
    RegisterLocale = 200,
    RegisterLiquids = 300,
    RegisterItems = 400,
    RegisterBuildings = 450,
    RegisterRecipes = 500
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ContentReloadEntryAttribute(ContentReloadEntryStage stage) : Attribute
{
    public ContentReloadEntryStage Stage { get; } = stage;
    public int Order { get; set; }
}