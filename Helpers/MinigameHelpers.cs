using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CUCoreLib.Helpers
{
    public enum CUCoreMinigameEndReason
    {
        Completed,
        Cancelled,
        Failed,
        Interrupted
    }

    public sealed class CUCoreMinigameConfig
    {
        public Func<CUCoreMinigameSession, bool> CanExit = _ => true;

        public Func<CUCoreMinigameSession, string> GuideLocaleKey = _ => string.Empty;

        public Func<CUCoreMinigameSession, float> HandRotationOffset = _ => 0f;
        public Func<CUCoreMinigameSession, Minigame.HandSpriteType> HandType = _ => Minigame.HandSpriteType.Grasp;

        public Func<CUCoreMinigameSession, bool> NeedsItem = _ => true;
    }

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

        public virtual CUCoreMinigameConfig Configure(CUCoreMinigameSession session)
        {
            return new CUCoreMinigameConfig
            {
                HandType = HandType,
                GuideLocaleKey = GuideLocaleKey,
                NeedsItem = NeedsItem,
                HandRotationOffset = HandRotationOffset,
                CanExit = CanExit
            };
        }

        public virtual void End(CUCoreMinigameSession session, CUCoreMinigameEndReason reason)
        {
        }
    }

    public sealed class CUCoreMinigameTimer
    {
        public CUCoreMinigameTimer()
        {
        }

        public CUCoreMinigameTimer(float duration)
        {
            Restart(duration);
        }

        public float Duration { get; private set; }

        public float Elapsed { get; private set; }

        public float Remaining => Mathf.Max(0f, Duration - Elapsed);

        public float Progress => Duration <= 0f ? 1f : Mathf.Clamp01(Elapsed / Duration);

        public bool IsComplete => Elapsed >= Duration;

        public void Restart(float duration)
        {
            Duration = Mathf.Max(0f, duration);
            Elapsed = 0f;
        }

        public bool Tick(float deltaTime)
        {
            if (IsComplete) return true;

            Elapsed = Mathf.Min(Duration, Elapsed + Mathf.Max(0f, deltaTime));
            return IsComplete;
        }
    }

    public sealed class CUCoreMinigameProgress
    {
        public CUCoreMinigameProgress()
            : this(1f)
        {
        }

        public CUCoreMinigameProgress(float target)
        {
            Reset(target);
        }

        public float Target { get; private set; }

        public float Value { get; private set; }

        public float Normalized => Target <= 0f ? 1f : Mathf.Clamp01(Value / Target);

        public bool IsComplete => Value >= Target;

        public void Reset(float target)
        {
            Target = Mathf.Max(0f, target);
            Value = 0f;
        }

        public void Add(float amount)
        {
            Value = Mathf.Clamp(Value + amount, 0f, Target);
        }

        public void Set(float value)
        {
            Value = Mathf.Clamp(value, 0f, Target);
        }
    }

    public sealed class CUCoreMinigameSession
    {
        private static readonly FieldInfo HandSpriteField =
            typeof(MinigameBase).GetField("handSprite", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Minigame boundMinigame;
        private readonly Dictionary<Type, object> stateByType = new Dictionary<Type, object>();
        private ICUCoreMinigameLifecycleHost lifecycleHost;

        internal CUCoreMinigameSession(MinigameBase game, Minigame minigame,
            ICUCoreMinigameLifecycleHost lifecycleHost = null)
        {
            Game = game;
            boundMinigame = minigame;
            this.lifecycleHost = lifecycleHost;
        }

        public MinigameBase Game { get; }

        public Body Body => Game != null ? Game.body : null;

        public Item CurrentItem => Game != null ? Game.currentItem : null;

        public Minigame CurrentMinigame =>
            Game != null && Game.currentMinigame != null ? Game.currentMinigame : boundMinigame;

        public GameObject SpawnedMiniGame => Game != null ? Game.spawnedMiniGame?.gameObject : null;

        public Transform SpawnedMiniGameTransform => Game != null ? Game.spawnedMiniGame : null;

        public RectTransform MinigameScreen => Game != null ? Game.minigameScreen : null;

        public RectTransform HandTransform => Game != null ? Game.handTransform : null;

        public Sprite[] HandSprites => Game != null ? Game.handSprites : null;

        public CUCoreMinigameEndReason? RequestedEndReason => lifecycleHost?.RequestedEndReason;

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

        public bool IsActive
        {
            get
            {
                if (Game == null) return false;
                if (boundMinigame == null) return Game.currentMinigame != null;

                return ReferenceEquals(Game.currentMinigame, boundMinigame);
            }
        }

        public T GetOrCreateState<T>() where T : class, new()
        {
            return GetOrCreateState(() => new T());
        }

        public T GetOrCreateState<T>(Func<T> factory) where T : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            if (TryGetState(out T state)) return state;

            state = factory();
            if (state == null) throw new InvalidOperationException("Minigame state factory returned null.");

            stateByType[typeof(T)] = state;
            return state;
        }

        public bool TryGetState<T>(out T state) where T : class
        {
            if (stateByType.TryGetValue(typeof(T), out var boxed) && boxed is T typed)
            {
                state = typed;
                return true;
            }

            state = null;
            return false;
        }

        public void SetState<T>(T state) where T : class
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            stateByType[typeof(T)] = state;
        }

        public bool RemoveState<T>() where T : class
        {
            return stateByType.Remove(typeof(T));
        }

        public bool TryCreateScreen(string resourceId)
        {
            if (Game == null || string.IsNullOrWhiteSpace(resourceId)) return false;

            Game.CreateScreen(resourceId);
            return true;
        }

        public void End()
        {
            End(CUCoreMinigameEndReason.Cancelled);
        }

        public void End(CUCoreMinigameEndReason reason)
        {
            lifecycleHost?.RequestEnd(reason);
            if (Game != null) Game.EndMinigame();
        }

        public void Complete()
        {
            End(CUCoreMinigameEndReason.Completed);
        }

        public void Fail()
        {
            End(CUCoreMinigameEndReason.Failed);
        }

        public void Cancel()
        {
            End(CUCoreMinigameEndReason.Cancelled);
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

        public bool TryGetSpawnedMiniGameChild(string name, out Transform child)
        {
            child = null;
            if (string.IsNullOrWhiteSpace(name) || SpawnedMiniGameTransform == null) return false;

            child = FindChildByName(SpawnedMiniGameTransform, name);
            return child != null;
        }

        public bool TryGetSpawnedMiniGameObject(int index, out GameObject gameObject)
        {
            gameObject = null;
            if (!TryGetSpawnedMiniGameChild(index, out var child)) return false;

            gameObject = child.gameObject;
            return gameObject != null;
        }

        public bool TryGetSpawnedMiniGameObject(string name, out GameObject gameObject)
        {
            gameObject = null;
            if (!TryGetSpawnedMiniGameChild(name, out var child)) return false;

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

        public bool TryGetSpawnedMiniGameComponent<T>(string name, out T component)
            where T : Component
        {
            component = null;
            if (!TryGetSpawnedMiniGameChild(name, out var child)) return false;

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

        public bool TryGetSpawnedMiniGameComponentInChildren<T>(string name, out T component)
            where T : Component
        {
            component = null;
            if (!TryGetSpawnedMiniGameChild(name, out var child)) return false;

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

        internal void ClearState()
        {
            stateByType.Clear();
        }

        internal void AttachLifecycleHost(ICUCoreMinigameLifecycleHost host)
        {
            if (host != null) lifecycleHost = host;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindChildByName(root.GetChild(i), name);
                if (match != null) return match;
            }

            return null;
        }
    }

    public static class CUCoreMinigames
    {
        private static readonly Dictionary<Minigame, CUCoreMinigameSession> SessionByMinigame =
            new Dictionary<Minigame, CUCoreMinigameSession>();

        public static MinigameBase Game => MinigameBase.main;

        public static CUCoreMinigameSession CurrentSession => TryGetCurrentSession(out var session) ? session : null;

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

        public static bool TryGetSpawnedMiniGameChild(string name, out Transform child)
        {
            if (CurrentSession == null)
            {
                child = null;
                return false;
            }

            return CurrentSession.TryGetSpawnedMiniGameChild(name, out child);
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

        public static bool TryGetSpawnedMiniGameObject(string name, out GameObject gameObject)
        {
            if (CurrentSession == null)
            {
                gameObject = null;
                return false;
            }

            return CurrentSession.TryGetSpawnedMiniGameObject(name, out gameObject);
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

        public static bool TryGetSpawnedMiniGameComponent<T>(string name, out T component)
            where T : Component
        {
            if (CurrentSession == null)
            {
                component = null;
                return false;
            }

            return CurrentSession.TryGetSpawnedMiniGameComponent(name, out component);
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

        public static bool TryGetSpawnedMiniGameComponentInChildren<T>(string name, out T component)
            where T : Component
        {
            if (CurrentSession == null)
            {
                component = null;
                return false;
            }

            return CurrentSession.TryGetSpawnedMiniGameComponentInChildren(name, out component);
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

        internal static CUCoreMinigameSession GetOrCreateSession(MinigameBase game, Minigame minigame,
            ICUCoreMinigameLifecycleHost lifecycleHost = null)
        {
            if (game == null || minigame == null) return null;

            if (SessionByMinigame.TryGetValue(minigame, out var session))
            {
                session.AttachLifecycleHost(lifecycleHost);
                return session;
            }

            session = new CUCoreMinigameSession(game, minigame, lifecycleHost);
            SessionByMinigame[minigame] = session;
            return session;
        }

        internal static void NotifyMinigameEnded(MinigameBase game, Minigame minigame)
        {
            if (game == null || minigame == null) return;

            if (SessionByMinigame.TryGetValue(minigame, out var session))
            {
                session.ClearState();
                SessionByMinigame.Remove(minigame);
            }
        }

        private static bool TryGetCurrentSession(out CUCoreMinigameSession session)
        {
            var game = Game;
            var minigame = game?.currentMinigame;
            if (game == null || minigame == null)
            {
                session = null;
                return false;
            }

            session = GetOrCreateSession(game, minigame);
            return session != null;
        }
    }

    internal sealed class CUCoreDefinitionMinigame : Minigame, ICUCoreMinigameLifecycleHost
    {
        private readonly ICUCoreMinigameDefinition definition;
        private CUCoreMinigameConfig cachedConfig;
        private bool hasConfig;
        private bool hasEnded;
        private CUCoreMinigameEndReason? requestedEndReason;
        private CUCoreMinigameSession session;

        public CUCoreDefinitionMinigame(ICUCoreMinigameDefinition definition)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        private CUCoreMinigameSession Session =>
            session ?? (session = CUCoreMinigames.GetOrCreateSession(CUCoreMinigames.Game, this, this));

        public CUCoreMinigameEndReason? RequestedEndReason => requestedEndReason;

        public void RequestEnd(CUCoreMinigameEndReason reason)
        {
            if (!requestedEndReason.HasValue) requestedEndReason = reason;
        }

        public void NotifyEnded(CUCoreMinigameEndReason fallbackReason)
        {
            if (hasEnded) return;

            hasEnded = true;
            var finalReason = requestedEndReason ?? fallbackReason;
            if (definition is CUCoreMinigameDefinition definitionWithLifecycle)
                definitionWithLifecycle.End(Session, finalReason);
        }

        public override HandSpriteType HandType()
        {
            return ResolveConfig().HandType(Session);
        }

        public override string GuideLocaleString()
        {
            return ResolveConfig().GuideLocaleKey(Session);
        }

        public override bool NeedsItem()
        {
            return ResolveConfig().NeedsItem(Session);
        }

        public override float HandRotOffset()
        {
            return ResolveConfig().HandRotationOffset(Session);
        }

        public override bool CanExit()
        {
            return ResolveConfig().CanExit(Session);
        }

        public override void Start()
        {
            session = CUCoreMinigames.GetOrCreateSession(CUCoreMinigames.Game, this, this);
            ResolveConfig();
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

        private CUCoreMinigameConfig ResolveConfig()
        {
            if (hasConfig) return cachedConfig;

            if (definition is CUCoreMinigameDefinition configuredDefinition)
                cachedConfig = configuredDefinition.Configure(Session) ?? new CUCoreMinigameConfig();
            else
                cachedConfig = new CUCoreMinigameConfig
                {
                    HandType = definition.HandType,
                    GuideLocaleKey = definition.GuideLocaleKey,
                    NeedsItem = definition.NeedsItem,
                    HandRotationOffset = definition.HandRotationOffset,
                    CanExit = definition.CanExit
                };

            hasConfig = true;
            return cachedConfig;
        }
    }

    [HarmonyPatch(typeof(MinigameBase), nameof(MinigameBase.EndMinigame))]
    internal static class CUCoreMinigameEndPatch
    {
        private static void Prefix(MinigameBase __instance, out Minigame __state)
        {
            __state = __instance?.currentMinigame;
            if (__state is ICUCoreMinigameLifecycleHost lifecycleHost)
                lifecycleHost.NotifyEnded(CUCoreMinigameEndReason.Interrupted);
        }

        private static void Postfix(MinigameBase __instance, Minigame __state)
        {
            CUCoreMinigames.NotifyMinigameEnded(__instance, __state);
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

        public static bool TryGetSpawnedMiniGameChild(string name, out Transform child)
        {
            return CUCoreMinigames.TryGetSpawnedMiniGameChild(name, out child);
        }

        public static bool TryGetSpawnedMiniGameObject(int index, out GameObject gameObject)
        {
            return CUCoreMinigames.TryGetSpawnedMiniGameObject(index, out gameObject);
        }

        public static bool TryGetSpawnedMiniGameObject(string name, out GameObject gameObject)
        {
            return CUCoreMinigames.TryGetSpawnedMiniGameObject(name, out gameObject);
        }

        public static bool TryGetSpawnedMiniGameComponent<T>(int index, out T component)
            where T : Component
        {
            return CUCoreMinigames.TryGetSpawnedMiniGameComponent(index, out component);
        }

        public static bool TryGetSpawnedMiniGameComponent<T>(string name, out T component)
            where T : Component
        {
            return CUCoreMinigames.TryGetSpawnedMiniGameComponent(name, out component);
        }

        public static bool TryGetSpawnedMiniGameComponentInChildren<T>(int index, out T component)
            where T : Component
        {
            return CUCoreMinigames.TryGetSpawnedMiniGameComponentInChildren(index, out component);
        }

        public static bool TryGetSpawnedMiniGameComponentInChildren<T>(string name, out T component)
            where T : Component
        {
            return CUCoreMinigames.TryGetSpawnedMiniGameComponentInChildren(name, out component);
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

    internal interface ICUCoreMinigameLifecycleHost
    {
        CUCoreMinigameEndReason? RequestedEndReason { get; }

        void RequestEnd(CUCoreMinigameEndReason reason);

        void NotifyEnded(CUCoreMinigameEndReason fallbackReason);
    }
}