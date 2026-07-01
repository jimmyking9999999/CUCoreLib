using System;
using System.Collections.Generic;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Registries;

public static class MoodleRegistry
{
    private const float DefaultHoldSeconds = 0.75f;
    private static readonly List<IBodyMoodleContributor> BodyContributors = [];
    private static readonly List<ILimbMoodleContributor> LimbContributors = [];

    private static readonly Dictionary<string, QueuedMoodle> QueuedMoodles = new(StringComparer.Ordinal);

    private static readonly Dictionary<int, Sprite> UiSpriteCache = new();

    private static readonly Dictionary<string, string> ActiveAnimationIdsByIconKey = new(StringComparer.Ordinal);

    private static readonly HashSet<string> WarnedInvalidIconIds = new(StringComparer.Ordinal);

    public static void RegisterBody<TStatus>(Func<Body, TStatus, StatusMoodleDefinition> buildMoodle)
        where TStatus : BodyStatus, new()
    {
        ContentReloadSession.AssertNotActive("MoodleRegistry.RegisterBody()",
            "Status and moodle registration are excluded from strict content reload.");

        if (buildMoodle == null)
        {
            CUCoreLibPlugin.Log?.LogWarning("Ignored body moodle registration because the contributor was null.");
            return;
        }

        BodyContributors.Add(new BodyMoodleContributor<TStatus>(buildMoodle));
    }

    public static void RegisterLimb<TStatus>(Func<Limb, TStatus, StatusMoodleDefinition> buildMoodle)
        where TStatus : LimbStatus, new()
    {
        ContentReloadSession.AssertNotActive("MoodleRegistry.RegisterLimb()",
            "Status and moodle registration are excluded from strict content reload.");

        if (buildMoodle == null)
        {
            CUCoreLibPlugin.Log?.LogWarning("Ignored limb moodle registration because the contributor was null.");
            return;
        }

        LimbContributors.Add(new LimbMoodleContributor<TStatus>(buildMoodle));
    }

    public static void AddMoodle(int intensity, Sprite icon, string name, string description, bool critical = false,
        bool chippedOnly = false, bool important = true, string key = null, float holdSeconds = DefaultHoldSeconds)
    {
        QueueMoodle(
            key,
            new StatusMoodleDefinition
            {
                Intensity = intensity,
                Name = name,
                Description = description,
                Critical = critical,
                ChippedOnly = chippedOnly,
                Important = important
            },
            icon,
            null,
            holdSeconds
        );
    }

    public static void AddMoodle(int intensity, string iconId, string name, string description,
        bool critical = false, bool chippedOnly = false, bool important = true, string key = null,
        float holdSeconds = DefaultHoldSeconds)
    {
        QueueMoodle(
            key,
            new StatusMoodleDefinition
            {
                Intensity = intensity,
                Icon = iconId,
                Name = name,
                Description = description,
                Critical = critical,
                ChippedOnly = chippedOnly,
                Important = important
            },
            null,
            iconId,
            holdSeconds
        );
    }

    public static void AddAnimatedMoodle(int intensity, string animationId, string name, string description,
        bool critical = false, bool chippedOnly = false, bool important = true, string key = null,
        float holdSeconds = DefaultHoldSeconds)
    {
        var animation = AssetLoader.GetCachedSpriteAnimation(animationId);
        if (animation?.Frames == null || animation.Frames.Length == 0) return;

        QueueMoodle(
            key,
            new StatusMoodleDefinition
            {
                Intensity = intensity,
                Name = name,
                Description = description,
                Critical = critical,
                ChippedOnly = chippedOnly,
                Important = important
            },
            animation.Frames[0],
            null,
            holdSeconds,
            animationId
        );
    }

    internal static void AddBodyMoodles(MoodleManager manager, Body body, bool important)
    {
        if (manager == null || body == null) return;

        foreach (var contributor in BodyContributors) AddMoodle(manager, contributor.Build(body), important);
    }

    internal static void AddLimbMoodles(MoodleManager manager, Limb limb, bool important)
    {
        if (manager == null || limb == null) return;

        foreach (var contributor in LimbContributors) AddMoodle(manager, contributor.Build(limb), important);
    }

    internal static void AddQueuedMoodles(MoodleManager manager, bool important)
    {
        if (manager == null) return;

        List<string> expiredKeys = null;
        foreach (var entry in QueuedMoodles)
        {
            if (entry.Value.ExpiresAt < Time.unscaledTime)
            {
                expiredKeys ??= [];

                expiredKeys.Add(entry.Key);
                continue;
            }

            var moodle = entry.Value.Definition;
            if (moodle == null || moodle.Important != important) continue;

            if (entry.Value.IconSprite != null)
            {
                var iconKey = "cucorelib.dynamic." + entry.Key;
                manager.icons[iconKey] = entry.Value.IconSprite;
                if (string.IsNullOrWhiteSpace(entry.Value.AnimationId))
                    ActiveAnimationIdsByIconKey.Remove(iconKey);
                else
                    ActiveAnimationIdsByIconKey[iconKey] = entry.Value.AnimationId;
                moodle.Icon = iconKey;
            }
            else
            {
                ActiveAnimationIdsByIconKey.Remove(entry.Value.IconId ?? string.Empty);
                moodle.Icon = entry.Value.IconId;
            }

            AddMoodle(manager, moodle, important);
        }

        if (expiredKeys == null) return;

        foreach (var key in expiredKeys) QueuedMoodles.Remove(key);
    }

    internal static JObject CaptureNetworkSnapshot()
    {
        var root = new JObject();
        var queued = new JArray();

        foreach (var entry in QueuedMoodles)
        {
            var moodle = entry.Value;
            if (moodle?.Definition == null) continue;

            queued.Add(new JObject
            {
                ["key"] = entry.Key,
                ["intensity"] = moodle.Definition.Intensity,
                ["name"] = moodle.Definition.Name ?? string.Empty,
                ["description"] = moodle.Definition.Description ?? string.Empty,
                ["critical"] = moodle.Definition.Critical,
                ["chippedOnly"] = moodle.Definition.ChippedOnly,
                ["important"] = moodle.Definition.Important,
                ["iconId"] = moodle.IconSprite != null ? moodle.IconSprite.name : moodle.IconId ?? string.Empty,
                ["holdSeconds"] = Mathf.Max(0.05f, moodle.ExpiresAt - Time.unscaledTime)
            });
        }

        root["queued"] = queued;
        return root;
    }

    internal static void ApplyNetworkSnapshot(JObject snapshot)
    {
        if (snapshot?["queued"] is not JArray queued) return;

        QueuedMoodles.Clear();
        foreach (var token in queued)
        {
            if (token is not JObject obj) continue;

            var key = obj.Value<string>("key");
            var iconId = obj.Value<string>("iconId");
            var name = obj.Value<string>("name");
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(iconId) ||
                string.IsNullOrWhiteSpace(name)) continue;

            QueueMoodle(
                key,
                new StatusMoodleDefinition
                {
                    Intensity = obj.Value<int?>("intensity") ?? 0,
                    Name = name,
                    Description = obj.Value<string>("description") ?? string.Empty,
                    Critical = obj.Value<bool?>("critical") ?? false,
                    ChippedOnly = obj.Value<bool?>("chippedOnly") ?? false,
                    Important = obj.Value<bool?>("important") ?? true
                },
                null,
                iconId,
                obj.Value<float?>("holdSeconds") ?? DefaultHoldSeconds);
        }
    }

    internal static bool TryGetAnimationId(string iconKey, out string animationId)
    {
        return ActiveAnimationIdsByIconKey.TryGetValue(iconKey ?? string.Empty, out animationId);
    }

    private static void QueueMoodle(string key, StatusMoodleDefinition definition, Sprite iconSprite, string iconId,
        float holdSeconds, string animationId = null)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.Name)) return;

        if (iconSprite == null && string.IsNullOrWhiteSpace(iconId)) return;

        var resolvedKey = string.IsNullOrWhiteSpace(key)
            ? BuildQueueKey(definition, iconSprite, iconId)
            : key.Trim();

        QueuedMoodles[resolvedKey] = new QueuedMoodle
        {
            Definition = definition,
            IconSprite = NormalizeIconSprite(iconSprite),
            IconId = iconId,
            ExpiresAt = Time.unscaledTime + Mathf.Max(holdSeconds, 0.05f),
            AnimationId = animationId
        };
    }

    private static string BuildQueueKey(StatusMoodleDefinition definition, Sprite iconSprite, string iconId)
    {
        var iconKey = iconSprite != null ? iconSprite.name : iconId ?? "icon";
        return iconKey + "|" + definition.Name + "|" + definition.Intensity;
    }

    private static void AddMoodle(MoodleManager manager, StatusMoodleDefinition moodle, bool important)
    {
        if (manager == null || moodle == null || string.IsNullOrWhiteSpace(moodle.Icon) ||
            string.IsNullOrWhiteSpace(moodle.Name)) return;

        if (!manager.icons.ContainsKey(moodle.Icon))
        {
            if (WarnedInvalidIconIds.Add(moodle.Icon))
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Moodles: Skipping moodle '" + moodle.Name +
                                                "' because icon '" + moodle.Icon +
                                                "' is not registered in MoodleManager.icons.");

            return;
        }

        var originalSideMoodles = manager.sideMoodles;
        manager.sideMoodles = !important;
        try
        {
            manager.AddMoodle(
                moodle.Intensity,
                moodle.Icon,
                moodle.Name,
                moodle.Description ?? string.Empty,
                moodle.Critical,
                moodle.ChippedOnly
            );
        }
        finally
        {
            manager.sideMoodles = originalSideMoodles;
        }
    }

    private static Sprite NormalizeIconSprite(Sprite iconSprite)
    {
        if (iconSprite == null) return null;

        if (Mathf.Approximately(iconSprite.pixelsPerUnit, AssetLoader.PPU_UI)) return iconSprite;

        var instanceId = iconSprite.GetInstanceID();
        if (UiSpriteCache.TryGetValue(instanceId, out var cachedSprite) && cachedSprite != null)
            return cachedSprite;

        var normalizedSprite = Sprite.Create(
            iconSprite.texture,
            iconSprite.rect,
            iconSprite.pivot / iconSprite.rect.size,
            AssetLoader.PPU_UI,
            0,
            SpriteMeshType.FullRect,
            iconSprite.border);

        normalizedSprite.name = iconSprite.name;
        UiSpriteCache[instanceId] = normalizedSprite;
        return normalizedSprite;
    }

    private interface IBodyMoodleContributor
    {
        StatusMoodleDefinition Build(Body body);
    }

    private interface ILimbMoodleContributor
    {
        StatusMoodleDefinition Build(Limb limb);
    }

    private sealed class BodyMoodleContributor<TStatus>(Func<Body, TStatus, StatusMoodleDefinition> buildMoodle)
        : IBodyMoodleContributor
        where TStatus : BodyStatus, new()
    {
        public StatusMoodleDefinition Build(Body body)
        {
            return buildMoodle(body, body.GetStatus<TStatus>());
        }
    }

    private sealed class LimbMoodleContributor<TStatus>(Func<Limb, TStatus, StatusMoodleDefinition> buildMoodle)
        : ILimbMoodleContributor
        where TStatus : LimbStatus, new()
    {
        public StatusMoodleDefinition Build(Limb limb)
        {
            return buildMoodle(limb, limb.GetStatus<TStatus>());
        }
    }

    private sealed class QueuedMoodle
    {
        public string AnimationId;
        public StatusMoodleDefinition Definition;
        public float ExpiresAt;
        public string IconId;
        public Sprite IconSprite;
    }
}