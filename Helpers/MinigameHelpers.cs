using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CUCoreLib.Helpers;

public interface ICUCoreMinigameDefinition
{
    Minigame.HandSpriteType HandType(CUCoreMinigameSession session);

    string GuideLocaleKey(CUCoreMinigameSession session);

    bool NeedsItem(CUCoreMinigameSession session);

    float HandRotationOffset(CUCoreMinigameSession session);

    bool CanExit(CUCoreMinigameSession session);

    void Start(CUCoreMinigameSession session);

    void PhysicsUpdate(CUCoreMinigameSession session, float deltaTime);

    void Update(CUCoreMinigameSession session, List<RaycastResult> uiCasts);
}

public abstract class CUCoreMinigameDefinition : ICUCoreMinigameDefinition
{
    public virtual Minigame.HandSpriteType HandType(CUCoreMinigameSession session)
    {
        return Minigame.HandSpriteType.Grasp;
    }

    public virtual string GuideLocaleKey(CUCoreMinigameSession session)
    {
        return string.Empty;
    }

    public virtual bool NeedsItem(CUCoreMinigameSession session)
    {
        return true;
    }

    public virtual float HandRotationOffset(CUCoreMinigameSession session)
    {
        return 0f;
    }

    public virtual bool CanExit(CUCoreMinigameSession session)
    {
        return true;
    }

    public abstract void Start(CUCoreMinigameSession session);

    public virtual void PhysicsUpdate(CUCoreMinigameSession session, float deltaTime)
    {
    }

    public abstract void Update(CUCoreMinigameSession session, List<RaycastResult> uiCasts);
}

public sealed class CUCoreMinigameSession
{
    private static readonly FieldInfo HandSpriteField =
        typeof(MinigameBase).GetField("handSprite", BindingFlags.Instance | BindingFlags.NonPublic);

    internal CUCoreMinigameSession(MinigameBase game)
    {
        Game = game;
    }

    public MinigameBase Game { get; }

    public Body Body => Game != null ? Game.body : null;

    public Item CurrentItem => Game != null ? Game.currentItem : null;

    public Minigame CurrentMinigame => Game != null ? Game.currentMinigame : null;

    public GameObject SpawnedMiniGame => Game != null ? Game.spawnedMiniGame?.gameObject : null;

    public Transform SpawnedMiniGameTransform => Game != null ? Game.spawnedMiniGame : null;

    public RectTransform MinigameScreen => Game != null ? Game.minigameScreen : null;

    public RectTransform HandTransform => Game != null ? Game.handTransform : null;

    public Sprite[] HandSprites => Game != null ? Game.handSprites : null;

    public Vector2 HandPosition
    {
        get => Game != null ? Game.handPos : default;
        set
        {
            if (Game != null) Game.handPos = value;
        }
    }

    public Vector2 HandVelocity
    {
        get => Game != null ? Game.handVelocity : default;
        set
        {
            if (Game != null) Game.handVelocity = value;
        }
    }

    public bool HandClicking => Game != null && Game.handClicking;

    public bool HandStartedClicking => Game != null && Game.handStartedClicking;

    public bool HandStoppedClicking => Game != null && Game.handStoppedClicking;

    public float HandShakeForce
    {
        get => Game != null ? Game.handShakeForce : 1f;
        set
        {
            if (Game != null) Game.handShakeForce = value;
        }
    }

    public bool IsActive => Game != null && Game.currentMinigame != null;

    public bool TryCreateScreen(string resourceId)
    {
        if (Game == null || string.IsNullOrWhiteSpace(resourceId)) return false;

        Game.CreateScreen(resourceId);
        return true;
    }

    public void End()
    {
        if (Game != null) Game.EndMinigame();
    }

    public bool TryGetUiCasts(Vector3 screenPosition, out List<RaycastResult> uiCasts)
    {
        uiCasts = null;
        if (Game == null) return false;

        uiCasts = UIUtil.GetEventSystemRaycastResults(screenPosition);
        return true;
    }

    public bool TryGetSpawnedMiniGameChild(int index, out Transform child)
    {
        child = null;
        if (Game == null || Game.spawnedMiniGame == null || index < 0) return false;

        var root = Game.spawnedMiniGame;
        if (index >= root.childCount) return false;

        child = root.GetChild(index);
        return child != null;
    }

    public bool TryGetSpawnedMiniGameObject(int index, out GameObject gameObject)
    {
        gameObject = null;
        if (!TryGetSpawnedMiniGameChild(index, out var child)) return false;

        gameObject = child.gameObject;
        return gameObject != null;
    }

    public bool TryGetSpawnedMiniGameComponent<T>(int index, out T component)
        where T : Component
    {
        component = null;
        if (!TryGetSpawnedMiniGameChild(index, out var child)) return false;

        return child.TryGetComponent(out component) && component != null;
    }

    public bool TryGetSpawnedMiniGameComponentInChildren<T>(int index, out T component)
        where T : Component
    {
        component = null;
        if (!TryGetSpawnedMiniGameChild(index, out var child)) return false;

        component = child.GetComponentInChildren<T>();
        return component != null;
    }

    public bool TrySetHandSprite(Sprite sprite)
    {
        var image = GetHandSpriteImage();
        if (image == null || sprite == null) return false;

        image.sprite = sprite;
        return true;
    }

    public bool TrySetHandSprite(int index)
    {
        if (Game == null || Game.handSprites == null) return false;

        if (index < 0 || index >= Game.handSprites.Length) return false;

        return TrySetHandSprite(Game.handSprites[index]);
    }

    public bool TrySetHandSpriteSlot(int index, Sprite sprite)
    {
        if (Game == null || Game.handSprites == null || sprite == null) return false;

        if (index < 0 || index >= Game.handSprites.Length) return false;

        Game.handSprites[index] = sprite;
        return true;
    }

    public bool TrySetHandSpriteSlots(params (int index, Sprite sprite)[] sprites)
    {
        if (sprites == null) return false;

        var updated = false;
        for (var i = 0; i < sprites.Length; i++)
            updated |= TrySetHandSpriteSlot(sprites[i].index, sprites[i].sprite);

        return updated;
    }

    public Image GetHandSpriteImage()
    {
        if (Game == null) return null;

        return HandSpriteField?.GetValue(Game) as Image;
    }

    public bool TryRefreshHandSprite()
    {
        if (Game == null) return false;

        Game.UpdateHandSprite(true);
        return true;
    }
}

public static class CUCoreMinigames
{
    public static MinigameBase Game => MinigameBase.main;

    public static CUCoreMinigameSession CurrentSession => Game != null ? new CUCoreMinigameSession(Game) : null;

    public static bool IsBusy()
    {
        return Game != null && Game.currentMinigame != null;
    }

    public static bool TryStart(Minigame minigame, Item item = null)
    {
        if (minigame == null || Game == null || Game.currentMinigame != null) return false;

        Game.StartMinigame(minigame, item);
        return true;
    }

    public static bool TryStart<TMinigame>(Func<TMinigame> factory, Item item = null)
        where TMinigame : Minigame
    {
        if (factory == null) return false;

        return TryStart(factory(), item);
    }

    public static bool TryStartDefinition(ICUCoreMinigameDefinition definition, Item item = null)
    {
        return definition != null && TryStart(new CUCoreDefinitionMinigame(definition), item);
    }

    public static bool TryStartDefinition<TDefinition>(Func<TDefinition> factory, Item item = null)
        where TDefinition : ICUCoreMinigameDefinition
    {
        if (factory == null) return false;

        return TryStartDefinition(factory(), item);
    }

    public static bool TryCreateScreen(string resourceId)
    {
        return CurrentSession != null && CurrentSession.TryCreateScreen(resourceId);
    }

    public static void EndActiveMinigame()
    {
        CurrentSession?.End();
    }

    public static bool TryGetBody(out Body body)
    {
        body = CurrentSession?.Body;
        return body != null;
    }

    public static bool TryGetCurrentItem(out Item item)
    {
        item = CurrentSession?.CurrentItem;
        return item != null;
    }

    public static bool TryGetCurrentMinigame(out Minigame minigame)
    {
        minigame = CurrentSession?.CurrentMinigame;
        return minigame != null;
    }

    public static bool TryGetUiCasts(Vector3 screenPosition, out List<RaycastResult> uiCasts)
    {
        if (CurrentSession == null)
        {
            uiCasts = null;
            return false;
        }

        return CurrentSession.TryGetUiCasts(screenPosition, out uiCasts);
    }

    public static bool TryGetSpawnedMiniGameChild(int index, out Transform child)
    {
        if (CurrentSession == null)
        {
            child = null;
            return false;
        }

        return CurrentSession.TryGetSpawnedMiniGameChild(index, out child);
    }

    public static bool TryGetSpawnedMiniGameObject(int index, out GameObject gameObject)
    {
        if (CurrentSession == null)
        {
            gameObject = null;
            return false;
        }

        return CurrentSession.TryGetSpawnedMiniGameObject(index, out gameObject);
    }

    public static bool TryGetSpawnedMiniGameComponent<T>(int index, out T component)
        where T : Component
    {
        if (CurrentSession == null)
        {
            component = null;
            return false;
        }

        return CurrentSession.TryGetSpawnedMiniGameComponent(index, out component);
    }

    public static bool TryGetSpawnedMiniGameComponentInChildren<T>(int index, out T component)
        where T : Component
    {
        if (CurrentSession == null)
        {
            component = null;
            return false;
        }

        return CurrentSession.TryGetSpawnedMiniGameComponentInChildren(index, out component);
    }

    public static bool TrySetHandSprite(Sprite sprite)
    {
        return CurrentSession != null && CurrentSession.TrySetHandSprite(sprite);
    }

    public static bool TrySetHandSprite(int index)
    {
        return CurrentSession != null && CurrentSession.TrySetHandSprite(index);
    }

    public static bool TrySetHandSpriteSlot(int index, Sprite sprite)
    {
        return CurrentSession != null && CurrentSession.TrySetHandSpriteSlot(index, sprite);
    }

    public static bool TrySetHandSpriteSlots(params (int index, Sprite sprite)[] sprites)
    {
        return CurrentSession != null && CurrentSession.TrySetHandSpriteSlots(sprites);
    }

    public static Image GetHandSpriteImage()
    {
        return CurrentSession?.GetHandSpriteImage();
    }

    public static bool TryRefreshHandSprite()
    {
        return CurrentSession != null && CurrentSession.TryRefreshHandSprite();
    }
}

internal sealed class CUCoreDefinitionMinigame(ICUCoreMinigameDefinition definition) : Minigame
{
    private readonly ICUCoreMinigameDefinition definition = definition ?? throw new ArgumentNullException(nameof(definition));
    private CUCoreMinigameSession session;

    private CUCoreMinigameSession Session => session ??= CUCoreMinigames.CurrentSession;

    public override HandSpriteType HandType()
    {
        return definition.HandType(Session);
    }

    public override string GuideLocaleString()
    {
        return definition.GuideLocaleKey(Session);
    }

    public override bool NeedsItem()
    {
        return definition.NeedsItem(Session);
    }

    public override float HandRotOffset()
    {
        return definition.HandRotationOffset(Session);
    }

    public override bool CanExit()
    {
        return definition.CanExit(Session);
    }

    public override void Start()
    {
        session = CUCoreMinigames.CurrentSession;
        definition.Start(Session);
    }

    public override void PhysicsUpdate(float deltaTime)
    {
        definition.PhysicsUpdate(Session, deltaTime);
    }

    public override void Update(List<RaycastResult> uiCasts)
    {
        definition.Update(Session, uiCasts);
    }
}

public abstract class CUCoreMinigame : Minigame
{
    protected MinigameBase Game => CUCoreMinigames.Game;

    public Body Body => Game != null ? Game.body : null;

    public Item CurrentItem => Game != null ? Game.currentItem : null;

    public GameObject SpawnedMiniGame => Game != null ? Game.spawnedMiniGame?.gameObject : null;

    public RectTransform MinigameScreen => Game != null ? Game.minigameScreen : null;

    protected bool HasActiveMinigame => Game != null && Game.currentMinigame != null;

    public static bool IsBusy()
    {
        return CUCoreMinigames.IsBusy();
    }

    public static bool TryStart(Minigame minigame, Item item = null)
    {
        return CUCoreMinigames.TryStart(minigame, item);
    }

    public static bool TryStart<TMinigame>(Func<TMinigame> factory, Item item = null)
        where TMinigame : Minigame
    {
        return CUCoreMinigames.TryStart(factory, item);
    }

    public static bool TryStartDefinition(ICUCoreMinigameDefinition definition, Item item = null)
    {
        return CUCoreMinigames.TryStartDefinition(definition, item);
    }

    public static bool TryStartDefinition<TDefinition>(Func<TDefinition> factory, Item item = null)
        where TDefinition : ICUCoreMinigameDefinition
    {
        return CUCoreMinigames.TryStartDefinition(factory, item);
    }

    public static bool TryCreateScreen(string resourceId)
    {
        return CUCoreMinigames.TryCreateScreen(resourceId);
    }

    public static void EndActiveMinigame()
    {
        CUCoreMinigames.EndActiveMinigame();
    }

    public static bool TryGetBody(out Body body)
    {
        return CUCoreMinigames.TryGetBody(out body);
    }

    public static bool TryGetCurrentItem(out Item item)
    {
        return CUCoreMinigames.TryGetCurrentItem(out item);
    }

    public static bool TryGetCurrentMinigame(out Minigame minigame)
    {
        return CUCoreMinigames.TryGetCurrentMinigame(out minigame);
    }

    public static bool TryGetUiCasts(Vector3 screenPosition, out List<RaycastResult> uiCasts)
    {
        return CUCoreMinigames.TryGetUiCasts(screenPosition, out uiCasts);
    }

    public static bool TryGetSpawnedMiniGameChild(int index, out Transform child)
    {
        return CUCoreMinigames.TryGetSpawnedMiniGameChild(index, out child);
    }

    public static bool TryGetSpawnedMiniGameObject(int index, out GameObject gameObject)
    {
        return CUCoreMinigames.TryGetSpawnedMiniGameObject(index, out gameObject);
    }

    public static bool TryGetSpawnedMiniGameComponent<T>(int index, out T component)
        where T : Component
    {
        return CUCoreMinigames.TryGetSpawnedMiniGameComponent(index, out component);
    }

    public static bool TryGetSpawnedMiniGameComponentInChildren<T>(int index, out T component)
        where T : Component
    {
        return CUCoreMinigames.TryGetSpawnedMiniGameComponentInChildren(index, out component);
    }

    public static bool TrySetHandSprite(Sprite sprite)
    {
        return CUCoreMinigames.TrySetHandSprite(sprite);
    }

    public static bool TrySetHandSprite(int index)
    {
        return CUCoreMinigames.TrySetHandSprite(index);
    }

    public static bool TrySetHandSpriteSlot(int index, Sprite sprite)
    {
        return CUCoreMinigames.TrySetHandSpriteSlot(index, sprite);
    }

    public static bool TrySetHandSpriteSlots(params (int index, Sprite sprite)[] sprites)
    {
        return CUCoreMinigames.TrySetHandSpriteSlots(sprites);
    }

    public static Image GetHandSpriteImage()
    {
        return CUCoreMinigames.GetHandSpriteImage();
    }

    public static bool TryRefreshHandSprite()
    {
        return CUCoreMinigames.TryRefreshHandSprite();
    }

    public virtual string GetGuideKey()
    {
        return string.Empty;
    }

    public virtual string GetScreenResourceId()
    {
        return string.Empty;
    }

    public override string GuideLocaleString()
    {
        return GetGuideKey();
    }

    protected bool CreateDefaultScreen()
    {
        var resourceId = GetScreenResourceId();
        return !string.IsNullOrWhiteSpace(resourceId) && TryCreateScreen(resourceId);
    }
}