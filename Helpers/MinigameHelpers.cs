using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CUCoreLib.Helpers
{
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
        private static readonly FieldInfo HandSpriteField = typeof(MinigameBase).GetField("handSprite", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly MinigameBase game;

        internal CUCoreMinigameSession(MinigameBase game)
        {
            this.game = game;
        }

        public MinigameBase Game => game;

        public Body Body => game != null ? game.body : null;

        public Item CurrentItem => game != null ? game.currentItem : null;

        public Minigame CurrentMinigame => game != null ? game.currentMinigame : null;

        public GameObject SpawnedMiniGame => game != null ? game.spawnedMiniGame?.gameObject : null;

        public Transform SpawnedMiniGameTransform => game != null ? game.spawnedMiniGame : null;

        public RectTransform MinigameScreen => game != null ? game.minigameScreen : null;

        public RectTransform HandTransform => game != null ? game.handTransform : null;

        public Sprite[] HandSprites => game != null ? game.handSprites : null;

        public Vector2 HandPosition
        {
            get => game != null ? game.handPos : default;
            set
            {
                if (game != null)
                {
                    game.handPos = value;
                }
            }
        }

        public Vector2 HandVelocity
        {
            get => game != null ? game.handVelocity : default;
            set
            {
                if (game != null)
                {
                    game.handVelocity = value;
                }
            }
        }

        public bool HandClicking => game != null && game.handClicking;

        public bool HandStartedClicking => game != null && game.handStartedClicking;

        public bool HandStoppedClicking => game != null && game.handStoppedClicking;

        public float HandShakeForce
        {
            get => game != null ? game.handShakeForce : 1f;
            set
            {
                if (game != null)
                {
                    game.handShakeForce = value;
                }
            }
        }

        public bool IsActive => game != null && game.currentMinigame != null;

        public bool TryCreateScreen(string resourceId)
        {
            if (game == null || string.IsNullOrWhiteSpace(resourceId))
            {
                return false;
            }

            game.CreateScreen(resourceId);
            return true;
        }

        public void End()
        {
            if (game != null)
            {
                game.EndMinigame();
            }
        }

        public bool TryGetUiCasts(Vector3 screenPosition, out List<RaycastResult> uiCasts)
        {
            uiCasts = null;
            if (game == null)
            {
                return false;
            }

            uiCasts = UIUtil.GetEventSystemRaycastResults(screenPosition);
            return true;
        }

        public bool TryGetSpawnedMiniGameChild(int index, out Transform child)
        {
            child = null;
            if (game == null || game.spawnedMiniGame == null || index < 0)
            {
                return false;
            }

            Transform root = game.spawnedMiniGame;
            if (index >= root.childCount)
            {
                return false;
            }

            child = root.GetChild(index);
            return child != null;
        }

        public bool TryGetSpawnedMiniGameObject(int index, out GameObject gameObject)
        {
            gameObject = null;
            if (!TryGetSpawnedMiniGameChild(index, out Transform child))
            {
                return false;
            }

            gameObject = child.gameObject;
            return gameObject != null;
        }

        public bool TryGetSpawnedMiniGameComponent<T>(int index, out T component)
            where T : Component
        {
            component = null;
            if (!TryGetSpawnedMiniGameChild(index, out Transform child))
            {
                return false;
            }

            return child.TryGetComponent(out component) && component != null;
        }

        public bool TryGetSpawnedMiniGameComponentInChildren<T>(int index, out T component)
            where T : Component
        {
            component = null;
            if (!TryGetSpawnedMiniGameChild(index, out Transform child))
            {
                return false;
            }

            component = child.GetComponentInChildren<T>();
            return component != null;
        }

        public bool TrySetHandSprite(Sprite sprite)
        {
            Image image = GetHandSpriteImage();
            if (image == null || sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            return true;
        }

        public bool TrySetHandSprite(int index)
        {
            if (game == null || game.handSprites == null)
            {
                return false;
            }

            if (index < 0 || index >= game.handSprites.Length)
            {
                return false;
            }

            return TrySetHandSprite(game.handSprites[index]);
        }

        public bool TrySetHandSpriteSlot(int index, Sprite sprite)
        {
            if (game == null || game.handSprites == null || sprite == null)
            {
                return false;
            }

            if (index < 0 || index >= game.handSprites.Length)
            {
                return false;
            }

            game.handSprites[index] = sprite;
            return true;
        }

        public bool TrySetHandSpriteSlots(params (int index, Sprite sprite)[] sprites)
        {
            if (sprites == null)
            {
                return false;
            }

            bool updated = false;
            for (int i = 0; i < sprites.Length; i++)
            {
                updated |= TrySetHandSpriteSlot(sprites[i].index, sprites[i].sprite);
            }

            return updated;
        }

        public Image GetHandSpriteImage()
        {
            if (game == null)
            {
                return null;
            }

            return HandSpriteField?.GetValue(game) as Image;
        }

        public bool TryRefreshHandSprite()
        {
            if (game == null)
            {
                return false;
            }

            game.UpdateHandSprite(reset: true);
            return true;
        }
    }

    public static class CUCoreMinigames
    {
        public static MinigameBase Game => MinigameBase.main;

        public static bool IsBusy()
        {
            return Game != null && Game.currentMinigame != null;
        }

        public static CUCoreMinigameSession CurrentSession => Game != null ? new CUCoreMinigameSession(Game) : null;

        public static bool TryStart(Minigame minigame, Item item = null)
        {
            if (minigame == null || Game == null || Game.currentMinigame != null)
            {
                return false;
            }

            Game.StartMinigame(minigame, item);
            return true;
        }

        public static bool TryStart<TMinigame>(Func<TMinigame> factory, Item item = null)
            where TMinigame : Minigame
        {
            if (factory == null)
            {
                return false;
            }

            return TryStart(factory(), item);
        }

        public static bool TryStartDefinition(ICUCoreMinigameDefinition definition, Item item = null)
        {
            return definition != null && TryStart(new CUCoreDefinitionMinigame(definition), item);
        }

        public static bool TryStartDefinition<TDefinition>(Func<TDefinition> factory, Item item = null)
            where TDefinition : ICUCoreMinigameDefinition
        {
            if (factory == null)
            {
                return false;
            }

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

    internal sealed class CUCoreDefinitionMinigame : Minigame
    {
        private readonly ICUCoreMinigameDefinition definition;
        private CUCoreMinigameSession session;

        public CUCoreDefinitionMinigame(ICUCoreMinigameDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        private CUCoreMinigameSession Session => session ?? (session = CUCoreMinigames.CurrentSession);

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

        public static bool TryStart<TMinigame>(System.Func<TMinigame> factory, Item item = null)
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
            string resourceId = GetScreenResourceId();
            return !string.IsNullOrWhiteSpace(resourceId) && TryCreateScreen(resourceId);
        }
    }
}
