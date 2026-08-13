using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using CUCoreLib.Data;
using CUCoreLib.Patches;
using CUCoreLib.Registries;
using TMPro;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Object = UnityEngine.Object;
using UnityEngine.EventSystems;

namespace CUCoreLib.Helpers
{
    public static class CUCoreUtils
    {
        public sealed class FriendlyKeybind
        {
            private readonly FriendlyKeybindEntry entry;

            internal FriendlyKeybind(FriendlyKeybindEntry entry)
            {
                this.entry = entry;
            }

            public bool disableInInputFields
            {
                get => entry.DisableInInputFields;
                set => entry.DisableInInputFields = value;
            }

            public bool disableInMainMenu
            {
                get => entry.DisableInMainMenu;
                set => entry.DisableInMainMenu = value;
            }

            public bool disableInHealthPanel
            {
                get => entry.DisableInHealthPanel;
                set => entry.DisableInHealthPanel = value;
            }

            public bool disableInInventory
            {
                get => entry.DisableInInventory;
                set => entry.DisableInInventory = value;
            }

            public KeyCode KeyCode => ResolveKeyCode(entry);

            public string KeyName => GetFriendlyKeyName(KeyCode);

            public bool IsPressed()
            {
                if (!Input.GetKeyDown(KeyCode))
                    return false;
                return !ShouldDisableFriendlyKeybind(entry);
            }

            public static implicit operator KeyCode(FriendlyKeybind keybind)
            {
                return keybind != null ? keybind.KeyCode : KeyCode.None;
            }
        }

        internal sealed class FriendlyKeybindEntry
        {
            public string ActionId;
            public string FriendlyName;
            public KeyCode DefaultKey;
            public KeyCode CurrentKey;
            public string Description;
            public bool RebindRegistered;
            public bool DisableInInputFields;
            public bool DisableInMainMenu;
            public bool DisableInHealthPanel;
            public bool DisableInInventory;
            public FriendlyKeybind Handle;
        }

        private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<KeyCode, Sprite> KeySpriteCache = new Dictionary<KeyCode, Sprite>();
        private static readonly Dictionary<string, FriendlyKeybindEntry> FriendlyKeybindsByActionId =
            new Dictionary<string, FriendlyKeybindEntry>(StringComparer.Ordinal);
        private static readonly int ItemLayerMask = LayerMask.GetMask("Item");

        private static Talker ElectronicTalkerProxy;
        private static FieldInfo KrokMpChatFocusedField;
        private static bool KrokMpChatFocusFieldResolved;

        /// <summary>
        /// Invoked after the <c>heal</c> console command heals a player.
        /// </summary>
        public static event Action OnHeal;

        /// <summary>
        /// Invoked when the player successfully enters last stand.
        /// </summary>
        public static event Action OnLastStand;

        /// <summary>
        /// The player whose event is currently invoking a CUCoreUtils player-event callback.
        /// </summary>
        public static Body EventPlayer { get; internal set; }

        // TODO allow for keybind support with FriendyKeyNames as a relay
        private static readonly Dictionary<KeyCode, string> FriendlyKeyNames = new Dictionary<KeyCode, string>
        {
            { KeyCode.Mouse0, "Left Click" },
            { KeyCode.Mouse1, "Right Click" },
            { KeyCode.Mouse2, "Middle Click" },
            { KeyCode.Alpha0, "0" },
            { KeyCode.Alpha1, "1" },
            { KeyCode.Alpha2, "2" },
            { KeyCode.Alpha3, "3" },
            { KeyCode.Alpha4, "4" },
            { KeyCode.Alpha5, "5" },
            { KeyCode.Alpha6, "6" },
            { KeyCode.Alpha7, "7" },
            { KeyCode.Alpha8, "8" },
            { KeyCode.Alpha9, "9" },
            { KeyCode.Return, "Enter" },
            { KeyCode.Escape, "Esc" },
            { KeyCode.BackQuote, "~" }
        };

        public static Coroutine StartCoroutine(IEnumerator routine)
        {
            return routine == null 
                ? null 
                : CoroutineRunner.Instance.StartCoroutine(routine);
        }

        internal static void RaiseOnHeal(Body player = null)
        {
            RaisePlayerEvent(OnHeal, player);
        }

        internal static void RaiseOnLastStand(Body player = null)
        {
            RaisePlayerEvent(OnLastStand, player);
        }

        private static void RaisePlayerEvent(Action callback, Body player)
        {
            var previousPlayer = EventPlayer;
            EventPlayer = player;
            try
            {
                callback?.Invoke();
            }
            finally
            {
                EventPlayer = previousPlayer;
            }
        }

        public static Coroutine StartCoroutine(Func<IEnumerator> routineFactory)
        {
            return routineFactory == null
                ? null
                : StartCoroutine(routineFactory());
        }

        public static Coroutine DelayCall(float delaySeconds, Action action)
        {
            return action == null
                ? null
                : StartCoroutine(DelayCallRoutine(delaySeconds, action));
        }

        public static Coroutine CallWhen(Func<bool> condition, Action action, float checkRepeatTimeSeconds = 0f)
        {
            if (condition == null || action == null) return null;

            return StartCoroutine(CallWhenRoutine(condition, action, checkRepeatTimeSeconds));
        }

        public static IEnumerator AwaitMainMenu(float checkRepeatTimeSeconds = 0f)
        {
            while (!IsMainMenuReady())
                if (checkRepeatTimeSeconds <= 0f)
                    yield return null;
                else
                    yield return new WaitForSecondsRealtime(checkRepeatTimeSeconds);
        }

        // Camelcase aliases go hard. Shouldn't affect VS code, but tell me if it does for your own other IDE
        public static IEnumerator awaitMainMenu(float checkRepeatTimeSeconds = 0f)
        {
            return AwaitMainMenu(checkRepeatTimeSeconds);
        }

        public static IEnumerator AwaitWorldGeneration(float checkRepeatTimeSeconds = 0f)
        {
            while (!IsWorldGenerationReady())
                if (checkRepeatTimeSeconds <= 0f)
                    yield return null;
                else
                    yield return new WaitForSecondsRealtime(checkRepeatTimeSeconds);
        }

        public static IEnumerator awaitWorldGeneration(float checkRepeatTimeSeconds = 0f)
        {
            return AwaitWorldGeneration(checkRepeatTimeSeconds);
        }

        private static IEnumerator DelayCallRoutine(float delaySeconds, Action action)
        {
            if (delaySeconds > 0f) yield return new WaitForSecondsRealtime(delaySeconds);

            action();
        }

        private static IEnumerator CallWhenRoutine(Func<bool> condition, Action action, float checkRepeatTimeSeconds)
        {
            if (checkRepeatTimeSeconds <= 0f)
                while (!condition())
                    yield return null;
            else
                while (!condition())
                    yield return new WaitForSecondsRealtime(checkRepeatTimeSeconds);

            action();
        }

        public static bool IsMainMenuReady()
        {
            if (WorldGeneration.world != null) return false;

            return Object.FindObjectOfType<PreRunScript>() != null;
        }

        public static bool IsWorldGenerationReady()
        {
            var world = WorldGeneration.world;
            if (world == null) return false;

            return world.worldExists && !world.generatingWorld;
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        }

        public static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
        }

        public static bool getBool(string key, bool defaultValue = false)
        {
            return GetBool(key, defaultValue);
        }

        public static void setBool(string key, bool value)
        {
            SetBool(key, value);
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
        }

        public static float getFloat(string key, float defaultValue = 0f)
        {
            return GetFloat(key, defaultValue);
        }

        public static void setFloat(string key, float value)
        {
            SetFloat(key, value);
        }

        public static string GetString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public static void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value ?? string.Empty);
        }

        public static string getString(string key, string defaultValue = "")
        {
            return GetString(key, defaultValue);
        }

        public static void setString(string key, string value)
        {
            SetString(key, value);
        }

        /// <summary>
        /// Creates a vanilla <see cref="CraftingQuality"/> and optionally registers a fallback locale label for the quality ID.
        /// Only one fallbackname is needed per ID, all other fallbacknames for this function are disregarded.
        /// </summary>
        /// <param name="id">Stable crafting-quality id used for recipe matching and locale lookup.</param>
        /// <param name="amount">Minimum quality amount required.</param>
        /// <param name="fallbackName">Optional fallback label used when no locale entry exists for this quality id.</param>
        public static CraftingQuality CreateCraftingQuality(string id, float amount = 1f, string fallbackName = null)
        {
            LocaleRegistry.RegisterCraftingQuality(id, fallbackName);
            return new CraftingQuality(id, amount);
        }

        /// <summary>
        /// Creates a vanilla <see cref="CraftingQuality"/> with an amount of <c>1f</c> and optionally registers a fallback locale label for the quality ID.
        /// Only one fallbackname is needed per ID, all other fallbacknames for this function are disregarded.
        /// </summary>
        /// <param name="id">Stable crafting-quality id used for recipe matching and locale lookup.</param>
        /// <param name="fallbackName">Optional fallback label used when no locale entry exists for this quality id.</param>
        public static CraftingQuality CreateCraftingQuality(string id, string fallbackName)
        {
            LocaleRegistry.RegisterCraftingQuality(id, fallbackName);
            return new CraftingQuality(id);
        }

        /// <summary>
        /// Applies an edit to a vanilla item definition. The edit is queued automatically when
        /// called before vanilla item setup and reapplied whenever the vanilla table is rebuilt.
        /// </summary>
        /// <param name="itemId">Vanilla item ID, such as <c>flimsyknife</c>.</param>
        /// <param name="edit">Mutation to apply to the vanilla <see cref="ItemInfo"/>.</param>
        public static void EditVanillaItem(string itemId, Action<ItemInfo> edit)
        {
            ItemRegistry.QueueVanillaItemEdit(itemId, edit);
        }

        public static void editVanillaItem(string itemId, Action<ItemInfo> edit)
        {
            EditVanillaItem(itemId, edit);
        }

        public static bool IsInWorld()
        {
            return IsWorldGenerationReady() && PlayerCamera.main != null && PlayerCamera.main.body != null;
        }

        public static bool isInWorld()
        {
            return IsInWorld();
        }

        public static bool TryGetHeldItem(out Item item)
        {
            item = null;
            if (!IsInWorld()) return false;

            var body = PlayerCamera.main.body;
            if (body == null || !body.HoldingItem(body.handSlot)) return false;

            item = body.GetItem(body.handSlot);
            return item != null;
        }

        public static bool tryGetHeldItem(out Item item)
        {
            return TryGetHeldItem(out item);
        }

        public static bool HasEquipped(string id)
        {
            if (!IsInWorld() || string.IsNullOrWhiteSpace(id)) return false;

            var body = PlayerCamera.main.body;
            return body != null && body.HoldingItem(id.Trim());
        }

        public static bool hasEquipped(string id)
        {
            return HasEquipped(id);
        }

        public static bool TryGetBody(out Body body)
        {
            body = null;
            if (PlayerCamera.main == null) return false;

            body = PlayerCamera.main.body;
            return body != null;
        }

        public static bool TryGetCamera(out PlayerCamera camera)
        {
            camera = PlayerCamera.main;
            return camera != null;
        }

        public static bool TryGetHoveredItem(out Item item)
        {
            item = null;
            if (!TryGetBody(out var body)) return false;

            var uiCasts = UIUtil.GetEventSystemRaycastResults();
            for (var i = 0; i < uiCasts.Count; i++)
            {
                var gameObject = uiCasts[i].gameObject;
                if (gameObject == null) continue;

                if (!gameObject.TryGetComponent(out ItemLabel label) || label == null ||
                    label.refItem == null) continue;
                item = label.refItem;
                return true;
            }

            // maybe System.NullReferenceException
            var collider = Physics2D.OverlapPoint(
                Camera.main.ScreenToWorldPoint(Input.mousePosition),    // maybe null
                ItemLayerMask);

            if (collider == null) return false;

            return collider.TryGetComponent(out item) && item != null;
        }

        public static bool tryGetBody(out Body body)
        {
            return TryGetBody(out body);
        }

        public static bool tryGetCamera(out PlayerCamera camera)
        {
            return TryGetCamera(out camera);
        }

        public static bool tryGetHoveredItem(out Item item)
        {
            return TryGetHoveredItem(out item);
        }

        public static Vector2 GetMousePosition()
        {
            if (PlayerCamera.main != null)
                return PlayerCamera.main.body != null
                    ? (Vector2)PlayerCamera.main.body.targetLookPos
                    : (Vector2)Input.mousePosition;

            return Input.mousePosition;
        }

        public static Vector2 getMousePosition()
        {
            return GetMousePosition();
        }

        public static void ShowAlert(string text, bool? important = false)
        {
            if (PlayerCamera.main == null || string.IsNullOrEmpty(text)) return;

            PlayerCamera.main.DoAlert(text, important == true);
        }

        public static void showAlert(string text, bool? important = false)
        {
            ShowAlert(text, important);
        }

        public static void Alert(string text, bool important, float delay = 0f)
        {
            if (string.IsNullOrWhiteSpace(text) || PlayerCamera.main == null) return;

            if (delay <= 0f)
                PlayerCamera.main.DoAlert(text, important);
            else
                StartCoroutine(AlertDelayedRoutine(text, important, delay));
        }

        public static void alert(string text, bool important, float delay = 0f)
        {
            Alert(text, important, delay);
        }

        private static IEnumerator AlertDelayedRoutine(string text, bool important, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (PlayerCamera.main != null)
                PlayerCamera.main.DoAlert(text, important);
        }

        public static void GiveItem(string id, int count)
        {
            var body = EventPlayer != null ? EventPlayer : PlayerCamera.main?.body;
            if (!IsWorldGenerationReady() || string.IsNullOrWhiteSpace(id) || count <= 0 || body == null) return;

            var normalizedId = id.Trim();
            for (var i = 0; i < count; i++)
            {
                var spawned = Utils.Create(normalizedId, body.transform.position, 0f);
                var spawnedItem = spawned != null ? spawned.GetComponent<Item>() : null;
                if (spawnedItem == null)
                {
                    if (spawned != null) Object.Destroy(spawned);

                    return;
                }

                KrokMpCompatibilityPatches.EnsureItemNetworkRegistered(spawned);
                body.AutoPickUpItem(spawnedItem);
            }
        }

        public static void giveItem(string id, int count)
        {
            GiveItem(id, count);
        }
        
        public static void GiveItemSlot(string id, int slot, int count)
        {
            if (!IsInWorld() || string.IsNullOrWhiteSpace(id) || count <= 0) return;

            var body = PlayerCamera.main.body;
            if (body == null) return;

            var normalizedId = id.Trim();
            for (var i = 0; i < count; i++)
            {
                var spawned = Utils.Create(normalizedId, body.transform.position, 0f);
                var spawnedItem = spawned != null ? spawned.GetComponent<Item>() : null;
                if (spawnedItem == null)
                {
                    if (spawned != null) Object.Destroy(spawned);

                    return;
                }

                body.PickUpItem(spawnedItem, slot);
            }
        }

        public static void giveItemSlot(string id, int slot, int count)
        {
            GiveItemSlot(id, slot, count);
        }

        public static bool TryGetCustomItemInfo(string id, out CustomItemInfo info)
        {
            return ItemRegistry.TryGetCustomInfo(id, out info);
        }

        /// <summary>
        /// Converts a vanilla item definition to <see cref="CustomItemInfo"/> so CUCoreLib-only fields can be configured.
        /// </summary>
        /// <param name="info">The item definition to convert. May be <c>null</c>.</param>
        /// <returns>The original definition when it is already custom; otherwise a shallow custom copy.</returns>
        public static CustomItemInfo ToCustomItemInfo(ItemInfo info)
        {
            return ItemRegistry.ToCustomItemInfo(info);
        }

        public static bool tryGetCustomItemInfo(string id, out CustomItemInfo info)
        {
            return TryGetCustomItemInfo(id, out info);
        }

        /// <summary>
        /// Updates the registered worn sprite for a custom item and refreshes every live instance with the same item ID.
        /// Pass <c>null</c> to clear the custom worn sprite and fall back to the normal icon while worn.
        /// </summary>
        /// <param name="itemId">Registered custom item id to update.</param>
        /// <param name="wornSprite">Sprite to use while the item is worn.</param>
        public static bool SetWornSprite(string itemId, Sprite wornSprite)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;

            var normalizedId = itemId.Trim();
            if (!ItemRegistry.TryGetCustomInfo(normalizedId, out var info) || info == null) return false;

            info.WornSprite = wornSprite;
            ItemRegistry.Register(normalizedId, info);
            ItemRegistryPatches.RefreshLiveInstances(new[] { normalizedId });
            return true;
        }

        /// <summary>
        /// Updates the registered worn sprite for the item's ID and refreshes every live instance with that same item ID.
        /// Pass <c>null</c> to clear the custom worn sprite and fall back to the normal icon while worn.
        /// </summary>
        /// <param name="item">Any live item instance whose registered custom item should be updated.</param>
        /// <param name="wornSprite">Sprite to use while the item is worn.</param>
        public static bool SetWornSprite(Item item, Sprite wornSprite)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.id) && SetWornSprite(item.id, wornSprite);
        }

        /// <summary>
        /// Updates or clears one registered multi-worn sprite entry for a custom item, then refreshes every live instance with the same item ID.
        /// Pass <c>null</c> for <paramref name="wornSprite"/> to remove the multi-worn sprite for that limb.
        /// </summary>
        /// <param name="itemId">Registered custom item id to update.</param>
        /// <param name="limbName">Vanilla limb name used by <see cref="CustomItemInfo.MultiWornSprites"/>.</param>
        /// <param name="wornSprite">Sprite to use for that secondary worn limb, or <c>null</c> to remove it.</param>
        public static bool SetMultiWornSprite(string itemId, string limbName, Sprite wornSprite)
        {
            if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(limbName)) return false;

            var normalizedId = itemId.Trim();
            var normalizedLimbName = limbName.Trim();
            if (!ItemRegistry.TryGetCustomInfo(normalizedId, out var info) || info == null) return false;

            if (info.MultiWornSprites == null)
                info.MultiWornSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

            if (wornSprite == null)
            {
                info.MultiWornSprites.Remove(normalizedLimbName);
                info.MultiWornSpriteOffsets?.Remove(normalizedLimbName);
            }
            else
                info.MultiWornSprites[normalizedLimbName] = wornSprite;

            ItemRegistry.Register(normalizedId, info);
            ItemRegistryPatches.RefreshLiveInstances(new[] { normalizedId });
            return true;
        }

        /// <summary>
        /// Updates or clears one registered multi-worn sprite entry for the item's ID, then refreshes matching live instances.
        /// Pass <c>null</c> for <paramref name="wornSprite"/> to remove the multi-worn sprite for that limb.
        /// </summary>
        /// <param name="item">Any live item instance whose registered custom item should be updated.</param>
        /// <param name="limbName">Vanilla limb name used by <see cref="CustomItemInfo.MultiWornSprites"/>.</param>
        /// <param name="wornSprite">Sprite to use for that secondary worn limb, or <c>null</c> to remove it.</param>
        public static bool SetMultiWornSprite(Item item, string limbName, Sprite wornSprite)
        {
            return item != null &&
                   !string.IsNullOrWhiteSpace(item.id) &&
                   SetMultiWornSprite(item.id, limbName, wornSprite);
        }

        public static bool setWornSprite(string itemId, Sprite wornSprite)
        {
            return SetWornSprite(itemId, wornSprite);
        }

        public static bool setWornSprite(Item item, Sprite wornSprite)
        {
            return SetWornSprite(item, wornSprite);
        }

        public static bool setMultiWornSprite(string itemId, string limbName, Sprite wornSprite)
        {
            return SetMultiWornSprite(itemId, limbName, wornSprite);
        }

        public static bool setMultiWornSprite(Item item, string limbName, Sprite wornSprite)
        {
            return SetMultiWornSprite(item, limbName, wornSprite);
        }

        /// <summary>
        /// Updates the inventory/world icon sprite for one live item instance without touching its worn sprite or the shared item registration.
        /// Pass <c>null</c> to clear the override and fall back to the registered item icon.
        /// This is useful for per-instance state visuals such as battery-inserted versus battery-empty variants.
        /// </summary>
        /// <param name="item">Live item instance to update.</param>
        /// <param name="iconSprite">Sprite to use for the item's normal icon/world sprite.</param>
        public static bool SetItemSprite(Item item, Sprite iconSprite)
        {
            if (item == null) return false;
            if (!ItemRegistry.TryGetCustomInfo(item, out _)) return false;

            ItemRegistryPatches.SetRuntimeIconOverride(item, iconSprite);
            return true;
        }

        public static bool setItemSprite(Item item, Sprite iconSprite)
        {
            return SetItemSprite(item, iconSprite);
        }

        public static void DoAmputate(Item item, Limb limb)
        {
            if (item == null || limb == null) return;

            Item.DoAmputate(item, limb);
        }

        public static void doAmputate(Item item, Limb limb)
        {
            DoAmputate(item, limb);
        }

        public static void Talk(string dialogue)
        {
            if (string.IsNullOrWhiteSpace(dialogue)) return;
            if (PlayerCamera.main == null || PlayerCamera.main.body == null || PlayerCamera.main.body.talker == null) return;

            PlayerCamera.main.body.talker.Talk(dialogue);
        }

        public static void talk(string dialogue)
        {
            Talk(dialogue);
        }

        public static void TalkElectronic(string dialogue, Item item = null)
        {
            if (string.IsNullOrWhiteSpace(dialogue)) return;

            bool createdProxy;
            Talker talker = GetElectronicTalker(item, out createdProxy);
            if (talker == null) return;

            if (createdProxy)
            {
                DelayCall(0f, () => talker.Talk(dialogue));
                return;
            }

            talker.Talk(dialogue);
        }

        public static void talkElectronic(string dialogue, Item item = null)
        {
            TalkElectronic(dialogue, item);
        }

        public static AudioSource PlaySoundAt(AudioClip clip, Vector2? pos = null)
        {
            return PlaySoundAt(clip, null, null, pos, null);
        }

        public static AudioSource PlaySoundAt(AudioClip clip, float? volume = null, float? delay = null, Vector2? position = null, float? pitch = null)
        {
            if (clip == null) return null;

            var playPos = position ?? (PlayerCamera.main != null && PlayerCamera.main.body != null
                ? (Vector2)PlayerCamera.main.body.transform.position
                : Vector2.zero);

            float resolvedVolume = volume ?? 1f;
            float resolvedPitch = pitch ?? 1f;
            bool usePitchShift = !pitch.HasValue;

            if (delay.HasValue && delay.Value > 0f)
            {
                DelayCall(delay.Value, () => Sound.Play(clip, playPos, volume: resolvedVolume, pitch: resolvedPitch, pitchShift: usePitchShift));
                return null;
            }

            return Sound.Play(clip, playPos, volume: resolvedVolume, pitch: resolvedPitch, pitchShift: usePitchShift);
        }

        public static AudioSource playSoundAt(AudioClip clip, Vector2? pos = null)
        {
            return PlaySoundAt(clip, pos);
        }

        public static AudioSource playSoundAt(AudioClip clip, float? volume = null, float? delay = null, Vector2? position = null, float? pitch = null)
        {
            return PlaySoundAt(clip, volume, delay, position, pitch);
        }

        public static bool IsModdedItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;

            var normalizedId = itemId.Trim();
            if (ItemRegistry.TryGetCustomInfo(normalizedId, out _)) return true;

            return normalizedId.StartsWith("glassworks.", StringComparison.OrdinalIgnoreCase) ||
                   normalizedId.StartsWith("cucorelib.", StringComparison.OrdinalIgnoreCase);
        }

        public static bool isModdedItem(string itemId)
        {
            return IsModdedItem(itemId);
        }

        private static Talker GetElectronicTalker(Item item, out bool createdProxy)
        {
            createdProxy = false;

            if (item != null)
            {
                Talker attachedTalker = item.GetComponent<Talker>();
                if (attachedTalker != null)
                {
                    return attachedTalker;
                }
            }

            Vector3 fallbackPosition = PlayerCamera.main != null && PlayerCamera.main.body != null
                ? PlayerCamera.main.body.transform.position
                : Vector3.zero;

            Talker templateTalker = GetWatchTalkerTemplate();
            if (templateTalker == null)
            {
                return null;
            }

            if (ElectronicTalkerProxy == null)
            {
                GameObject target = new GameObject("CUCoreUtils_ElectronicTalker");
                Object.DontDestroyOnLoad(target);
                ElectronicTalkerProxy = target.AddComponent<Talker>();
                InitializeElectronicTalker(ElectronicTalkerProxy, templateTalker);
                createdProxy = true;
            }
            else if (!ElectronicTalkerProxy)
            {
                ElectronicTalkerProxy = null;
                return GetElectronicTalker(item, out createdProxy);
            }

            Transform proxyTransform = ElectronicTalkerProxy.transform;
            if (item != null)
            {
                proxyTransform.SetParent(item.transform, false);
                proxyTransform.position = item.transform.position;
            }
            else
            {
                proxyTransform.SetParent(null);
                proxyTransform.position = fallbackPosition;
            }

            return ElectronicTalkerProxy;
        }

        private static Talker GetWatchTalkerTemplate()
        {
            GameObject watchPrefab = Resources.Load<GameObject>("watch");
            if (watchPrefab == null)
            {
                return null;
            }

            return watchPrefab.GetComponent<Talker>();
        }

        private static void InitializeElectronicTalker(Talker talker, Talker templateTalker)
        {
            if (talker == null || templateTalker == null)
            {
                return;
            }

            talker.textPrefab = templateTalker.textPrefab;
            talker.talkSoundCustom = templateTalker.talkSoundCustom;
            talker.talkTimeMult = templateTalker.talkTimeMult;
            talker.trader = templateTalker.trader;
            talker.body = null;

            if (talker.text == null && talker.textPrefab != null)
            {
                GameObject textObject = Object.Instantiate(talker.textPrefab, talker.transform.position, Quaternion.identity);
                talker.text = textObject.GetComponent<TextMeshPro>();
            }
        }

        public static MethodInfo GetMethod(object target, string methodName)
        {
            if (target == null || string.IsNullOrEmpty(methodName)) return null;

            return target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.FlattenHierarchy
            );
        }

        public static void InvokeMethod(object target, string methodName, params object[] args)
        {
            var method = GetCachedMethod(target, methodName);
            if (method == null) return;

            method.Invoke(target, args);
        }

        // Did you know that bepinex has a publiciser and it bypasses private methods?
        // todo remove these :(
        public static void ConsoleLog(ConsoleScript instance, string message)
        {
            InvokeMethod(instance, "LogToConsole", message);
        }
        
        // try this?
        // _consoleScript are ConsoleScript Instance
        // public static void LogToConsole(string text)
        // {
        //     if (_consoleScript == null)
        //         return;
        //
        //     _consoleScript.logs.Add(
        //         $"[<alpha=#55>{TimeSpan.FromSeconds(Time.realtimeSinceStartup):mm\\:ss}<alpha=#FF>] {text}");
        //     if (_consoleScript.logs.Count > MaxLogCount)
        //         _consoleScript.logs.RemoveAt(0);
        //     if (!_consoleScript.active)
        //         return;
        //     if (_consoleScript.logText == null) return;
        //     _consoleScript.logText.text = string.Join("\n", _consoleScript.logs);
        // }

        public static void ConsoleRunCommand(ConsoleScript instance, string commandString)
        {
            InvokeMethod(instance, "RunCommandString", commandString);
        }

        public static void ConsoleCheckForWorld(ConsoleScript instance)
        {
            InvokeMethod(instance, "CheckForWorld");
        }

        private static MethodInfo GetCachedMethod(object target, string methodName)
        {
            if (target == null || string.IsNullOrEmpty(methodName)) return null;

            var targetType = target.GetType();
            var cacheKey = targetType.FullName + "::" + methodName;
            if (MethodCache.TryGetValue(cacheKey, out var cached)) return cached;

            var method = targetType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.FlattenHierarchy
            );

            MethodCache[cacheKey] = method;
            return method;
        }

        public static bool IsFinite(this float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinite(this Vector2 value)
        {
            return value.x.IsFinite() && value.y.IsFinite();
        }

        public static Sprite GetKeySprite(KeyCode key, string spritePrefix = "Key_")
        {
            if (KeySpriteCache.TryGetValue(key, out var cached)) return cached;

            var spriteName = spritePrefix + key;
            var found = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == spriteName);
            if (found != null) KeySpriteCache[key] = found;

            return found;
        }

        public static string GetFriendlyKeyName(KeyCode key)
        {
            return FriendlyKeyNames.TryGetValue(key, out var friendly) ? friendly : key.ToString();
        }

        public static string GetFriendlyKeyName(string friendlyKeybindName)
        {
            return GetFriendlyKeyBind(friendlyKeybindName).KeyName;
        }

        public static FriendlyKeybind GetFriendlyKeyBind(string friendlyKeybindName)
        {
            if (!TryGetOrCreateFriendlyKeybind(friendlyKeybindName, out var entry))
                return null;

            return entry.Handle;
        }

        public static bool AllowKeybindRebind(string friendlyKeybindName, string descriptionToShowInSettingsKeybindMenu)
        {
            if (!TryGetOrCreateFriendlyKeybind(friendlyKeybindName, out var entry))
                return false;

            if (!string.IsNullOrWhiteSpace(descriptionToShowInSettingsKeybindMenu))
                entry.Description = descriptionToShowInSettingsKeybindMenu.Trim();

            if (entry.RebindRegistered)
                return true;

            var label = string.IsNullOrWhiteSpace(entry.Description) ? entry.FriendlyName : entry.Description;
            var option = ModOptionDefinition.Keybind(
                entry.ActionId,
                label,
                label,
                Setting.SettingCategory.Input,
                entry.DefaultKey,
                value => entry.CurrentKey = value);

            if (!ModOptionsRegistry.Register(option))
                return false;

            entry.RebindRegistered = true;
            return true;
        }

        public static void SetFriendlyKeyName(KeyCode key, string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                FriendlyKeyNames.Remove(key);
                return;
            }

            FriendlyKeyNames[key] = displayName;
        }

        internal static bool TryGetFriendlyBindDisplayName(string actionId, out string displayName)
        {
            displayName = null;
            if (string.IsNullOrWhiteSpace(actionId)) return false;

            if (!FriendlyKeybindsByActionId.TryGetValue(actionId, out var entry))
                return false;

            var setting = Settings.Get<SettingKeybind>(actionId);
            if (setting != null)
                entry.CurrentKey = setting.value;

            displayName = GetFriendlyKeyName(entry.CurrentKey);
            return true;
        }

        internal static bool TryGetFriendlyBindKeyCode(string actionId, out KeyCode keyCode)
        {
            keyCode = KeyCode.None;
            if (string.IsNullOrWhiteSpace(actionId)) return false;

            if (!FriendlyKeybindsByActionId.TryGetValue(actionId, out var entry))
                return false;

            keyCode = ResolveKeyCode(entry);
            return true;
        }

        private static bool TryGetOrCreateFriendlyKeybind(string friendlyKeybindName, out FriendlyKeybindEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(friendlyKeybindName)) return false;

            var normalizedFriendlyName = friendlyKeybindName.Trim();
            if (!TryParseFriendlyKeyCode(normalizedFriendlyName, out var defaultKey)) return false;

            var actionId = BuildFriendlyKeybindActionId(normalizedFriendlyName);
            if (string.IsNullOrWhiteSpace(actionId)) return false;

            if (FriendlyKeybindsByActionId.TryGetValue(actionId, out entry)) return true;

            entry = new FriendlyKeybindEntry
            {
                ActionId = actionId,
                FriendlyName = normalizedFriendlyName,
                DefaultKey = defaultKey,
                CurrentKey = defaultKey
            };

            entry.Handle = new FriendlyKeybind(entry);

            FriendlyKeybindsByActionId[actionId] = entry;
            return true;
        }

        private static KeyCode ResolveKeyCode(FriendlyKeybindEntry entry)
        {
            if (entry == null) return KeyCode.None;

            var liveSetting = Settings.Get<SettingKeybind>(entry.ActionId);
            if (liveSetting != null)
                entry.CurrentKey = liveSetting.value;

            if (ShouldDisableFriendlyKeybind(entry))
                return KeyCode.None;

            return entry.CurrentKey;
        }

        private static bool ShouldDisableFriendlyKeybind(FriendlyKeybindEntry entry)
        {
            if (entry == null) return false;

            if (entry.DisableInMainMenu && IsFriendlyKeybindMainMenuBlocked())
                return true;

            if (entry.DisableInHealthPanel && IsFriendlyKeybindHealthPanelBlocked())
                return true;

            if (entry.DisableInInventory && IsFriendlyKeybindInventoryBlocked())
                return true;

            return entry.DisableInInputFields && IsFriendlyKeybindInputFieldBlocked();
        }

        private static bool IsFriendlyKeybindMainMenuBlocked()
        {
            return PreRunScript.instance != null && WorldGeneration.world == null;
        }

        private static bool IsFriendlyKeybindHealthPanelBlocked()
        {
            return PlayerCamera.main != null &&
                   PlayerCamera.main.woundView != null &&
                   PlayerCamera.main.woundView.activeSelf;
        }

        private static bool IsFriendlyKeybindInventoryBlocked()
        {
            return PlayerCamera.main != null &&
                   PlayerCamera.main.craftingPanel != null &&
                   PlayerCamera.main.craftingPanel.activeSelf;
        }

        private static bool IsFriendlyKeybindInputFieldBlocked()
        {
            if (ConsoleScript.instance != null &&
                ConsoleScript.instance.input != null &&
                ConsoleScript.instance.input.isFocused)
                return true;

            if (PlayerCamera.main != null &&
                PlayerCamera.main.recipeFilterField != null &&
                PlayerCamera.main.recipeFilterField.isFocused)
                return true;

            if (IsKrokMpChatFocused())
                return true;

            var inputField = EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>();
            return inputField != null && inputField.isFocused;
        }

        private static bool IsKrokMpChatFocused()
        {
            var focusedField = GetKrokMpChatFocusedField();
            if (focusedField == null) return false;

            return (bool)focusedField.GetValue(null);
        }

        private static FieldInfo GetKrokMpChatFocusedField()
        {
            if (KrokMpChatFocusFieldResolved) return KrokMpChatFocusedField;

            KrokMpChatFocusFieldResolved = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type chatType;
                try
                {
                    chatType = assembly.GetType("KrokoshaCasualtiesMP.Chat", false);
                }
                catch
                {
                    chatType = null;
                }

                if (chatType == null) continue;

                var focusedField = chatType.GetField("CHAT_textbox_input_focused",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (focusedField == null || focusedField.FieldType != typeof(bool)) return null;

                KrokMpChatFocusedField = focusedField;
                return KrokMpChatFocusedField;
            }

            return null;
        }

        private static string BuildFriendlyKeybindActionId(string friendlyKeybindName)
        {
            var sourceAssembly = ContentReload.ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();
            var assemblyName = sourceAssembly?.GetName().Name;
            var normalizedAssembly = NormalizeFriendlyKeybindToken(assemblyName);
            var normalizedKeybind = NormalizeFriendlyKeybindToken(friendlyKeybindName);
            if (string.IsNullOrWhiteSpace(normalizedAssembly) || string.IsNullOrWhiteSpace(normalizedKeybind))
                return null;

            return normalizedAssembly + ".keybind." + normalizedKeybind;
        }

        private static string NormalizeFriendlyKeybindToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var builder = new StringBuilder(value.Length);
            var lastWasSeparator = false;
            foreach (var c in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    lastWasSeparator = false;
                    continue;
                }

                if (lastWasSeparator) continue;

                builder.Append('-');
                lastWasSeparator = true;
            }

            return builder.ToString().Trim('-');
        }

        private static bool TryParseFriendlyKeyCode(string rawValue, out KeyCode keyCode)
        {
            keyCode = KeyCode.None;
            if (string.IsNullOrWhiteSpace(rawValue)) return false;

            var trimmed = rawValue.Trim();

            if (trimmed.Length == 1)
            {
                var single = trimmed[0];
                if (single >= 'A' && single <= 'Z')
                    return Enum.TryParse(single.ToString(), out keyCode);
                if (single >= 'a' && single <= 'z')
                    return Enum.TryParse(char.ToUpperInvariant(single).ToString(), out keyCode);
                if (single >= '0' && single <= '9')
                    return Enum.TryParse("Alpha" + single, out keyCode);
            }

            if (FriendlyKeyNames.FirstOrDefault(pair =>
                    string.Equals(pair.Value, trimmed, StringComparison.OrdinalIgnoreCase)).Key is var friendlyMatch &&
                friendlyMatch != KeyCode.None)
            {
                keyCode = friendlyMatch;
                return true;
            }

            return Enum.TryParse(trimmed, true, out keyCode);
        }

        public static Sprite LoadEmbeddedSprite(string resourcePath, float pixelsPerUnit = AssetLoader.PPU_UI,
            Assembly sourceAssembly = null)
        {
            return AssetLoader.LoadEmbeddedSprite(resourcePath, pixelsPerUnit, sourceAssembly);
        }

        /// <summary>
        /// Splits an equal-cell sprite sheet into independent sprites. Frames are ordered from the top-left cell,
        /// left-to-right across each row, then top-to-bottom.
        /// </summary>
        /// <param name="spriteSheet">Sprite covering the full sprite-sheet area to split.</param>
        /// <param name="columns">Number of equal-width cells in the sheet.</param>
        /// <param name="rows">Number of equal-height cells in the sheet.</param>
        /// <returns>One sprite per cell, or an empty array when the sheet is invalid or cannot be divided evenly.</returns>
        public static Sprite[] SplitSpriteSheet(Sprite spriteSheet, int columns, int rows)
        {
            if (spriteSheet == null || columns <= 0 || rows <= 0) return Array.Empty<Sprite>();

            Texture2D texture = spriteSheet.texture;
            Rect sheetRect = spriteSheet.rect;
            int sheetWidth = Mathf.RoundToInt(sheetRect.width);
            int sheetHeight = Mathf.RoundToInt(sheetRect.height);
            float pixelsPerUnit = spriteSheet.pixelsPerUnit;
            if (texture == null || sheetWidth <= 0 || sheetHeight <= 0 ||
                !Mathf.Approximately(sheetRect.width, sheetWidth) ||
                !Mathf.Approximately(sheetRect.height, sheetHeight) ||
                sheetRect.xMin < 0f || sheetRect.yMin < 0f || sheetRect.xMax > texture.width ||
                sheetRect.yMax > texture.height || sheetWidth % columns != 0 || sheetHeight % rows != 0 ||
                pixelsPerUnit <= 0f || float.IsNaN(pixelsPerUnit) || float.IsInfinity(pixelsPerUnit))
                return Array.Empty<Sprite>();

            int cellWidth = sheetWidth / columns;
            int cellHeight = sheetHeight / rows;
            Vector2 pivot = new Vector2(spriteSheet.pivot.x / sheetRect.width,
                spriteSheet.pivot.y / sheetRect.height);
            var frames = new Sprite[columns * rows];

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                var frameRect = new Rect(sheetRect.x + column * cellWidth,
                    sheetRect.y + (rows - row - 1) * cellHeight, cellWidth, cellHeight);
                var frame = Sprite.Create(texture, frameRect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                frame.name = string.IsNullOrEmpty(spriteSheet.name)
                    ? "sprite_" + row + "_" + column
                    : spriteSheet.name + "_" + row + "_" + column;
                frames[row * columns + column] = frame;
            }

            return frames;
        }

        /// <summary>
        /// Opens a readable stream for an embedded resource resolved from the calling assembly or an explicit assembly override.
        /// The caller owns the returned stream and should dispose it when finished.
        /// </summary>
        /// <param name="resourcePath">Full or suffix resource name to resolve.</param>
        /// <param name="sourceAssembly">Optional assembly override. Defaults to CUCoreLib's content-reload override or the calling assembly.</param>
        public static Stream LoadEmbeddedStream(string resourcePath, Assembly sourceAssembly = null)
        {
            return AssetLoader.LoadEmbeddedStream(resourcePath, sourceAssembly);
        }

        public static byte[] CompressGZip(byte[] data)
        {
            if (data == null) return null;

            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
                {
                    gzip.Write(data, 0, data.Length);
                }

                return output.ToArray();
            }
        }

        public static byte[] DecompressGZip(byte[] compressedData)
        {
            if (compressedData == null) return null;

            using (var input = new MemoryStream(compressedData))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return output.ToArray();
            }
        }

        public static byte[] CompressDeflate(byte[] data)
        {
            if (data == null) return null;

            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal))
                {
                    deflate.Write(data, 0, data.Length);
                }

                return output.ToArray();
            }
        }

        public static byte[] DecompressDeflate(byte[] compressedData)
        {
            if (compressedData == null) return null;

            using (var input = new MemoryStream(compressedData))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                deflate.CopyTo(output);
                return output.ToArray();
            }
        }

        private sealed class CoroutineRunner : MonoBehaviour
        {
            private static CoroutineRunner _instance;

            public static CoroutineRunner Instance
            {
                get
                {
                    if (_instance != null) return _instance;
                    var obj = new GameObject("CUCoreUtils_CoroutineRunner");
                    DontDestroyOnLoad(obj);
                    obj.hideFlags = HideFlags.HideAndDontSave;
                    _instance = obj.AddComponent<CoroutineRunner>();

                    return _instance;
                }
            }
        }
    }
}
