using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using UnityEngine;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Object = UnityEngine.Object;

namespace CUCoreLib.Helpers
{
    public static class CUCoreUtils
    {
        private static readonly Dictionary<string, MethodInfo> MethodCache = new Dictionary<string, MethodInfo>();
        private static readonly Dictionary<KeyCode, Sprite> KeySpriteCache = new Dictionary<KeyCode, Sprite>();

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

            foreach (var uiCast in UIUtil.GetEventSystemRaycastResults()
                         .Where(uiCast => uiCast.gameObject != null))
            {
                if (!uiCast.gameObject.TryGetComponent(out ItemLabel label) || label == null ||
                    label.refItem == null) continue;
                item = label.refItem;
                return true;
            }

            // maybe System.NullReferenceException
            var collider = Physics2D.OverlapPoint(
                Camera.main.ScreenToWorldPoint(Input.mousePosition),    // maybe null
                LayerMask.GetMask("Item"));

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

        public static bool tryGetCustomItemInfo(string id, out CustomItemInfo info)
        {
            return TryGetCustomItemInfo(id, out info);
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

        public static AudioSource PlaySoundAt(AudioClip clip, Vector2? pos = null)
        {
            if (clip == null) return null;

            var playPos = pos ?? (PlayerCamera.main != null && PlayerCamera.main.body != null
                ? (Vector2)PlayerCamera.main.body.transform.position
                : Vector2.zero);

            return Sound.Play(clip, playPos);
        }

        public static AudioSource playSoundAt(AudioClip clip, Vector2? pos = null)
        {
            return PlaySoundAt(clip, pos);
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

        public static void SetFriendlyKeyName(KeyCode key, string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                FriendlyKeyNames.Remove(key);
                return;
            }

            FriendlyKeyNames[key] = displayName;
        }

        public static Sprite LoadEmbeddedSprite(string resourcePath, float pixelsPerUnit = AssetLoader.PPU_UI,
            Assembly sourceAssembly = null)
        {
            return AssetLoader.LoadEmbeddedSprite(resourcePath, pixelsPerUnit, sourceAssembly);
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