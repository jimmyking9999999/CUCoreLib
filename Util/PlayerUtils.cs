using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CUCoreLib.Util;

public static class PlayerUtils
{
    public static bool TryGetBody(out Body body)
    {
        body = null;
        if (PlayerCamera.main == null) return false;
        body = PlayerCamera.main.body;
        return body != null;
    }

    public static Body GetBody()
    {
        TryGetBody(out var body);
        return body;
    }

    public static bool TryGetCamera(out PlayerCamera camera)
    {
        camera = PlayerCamera.main;
        return camera;
    }

    public static PlayerCamera GetCamera()
    {
        TryGetCamera(out var camera);
        return camera;
    }

    public static bool TryGetItemInSlot(int slot, out Item item)
    {
        item = null;
        if (!GetBody()) return false;

        item = GetBody().GetItem(slot);
        return item != null;
    }

    public static bool HasItemInSlot(int slot)
    {
        return TryGetItemInSlot(slot, out _);
    }

    public static Vector2 GetMousePosition()
    {
        if (PlayerCamera.main != null)
            return PlayerCamera.main.body != null
                ? PlayerCamera.main.body.targetLookPos
                : (Vector2)Input.mousePosition;

        return Input.mousePosition;
    }

    public static void Alert(string text, bool important = false, float delay = 0f)
    {
        if (PlayerCamera.main == null || string.IsNullOrEmpty(text)) return;

        if (delay <= 0f)
            PlayerCamera.main.DoAlert(text, important);
        else
            CoroutineUtils.StartCoroutine(AlertDelayedRoutine(text, important, delay));
    }

    private static IEnumerator AlertDelayedRoutine(string text, bool important, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (PlayerCamera.main != null)
            PlayerCamera.main.DoAlert(text, important);
    }

    public static void GiveItem(string id, int count)
    {
        if (!CheckUtils.IsInWorld() || string.IsNullOrWhiteSpace(id) || count <= 0) return;

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

    public static void GiveItemInSlot(string id, int slot, int count)
    {
        if (!CheckUtils.IsInWorld() || string.IsNullOrWhiteSpace(id) || count <= 0) return;

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

    public static void DoAmputate(Item item, Limb limb)
    {
        if (item == null || limb == null) return;

        Item.DoAmputate(item, limb);
    }

    public static AudioSource PlaySoundAt(AudioClip clip, Vector2? pos = null)
    {
        if (clip == null) return null;

        var playPos = pos ?? (PlayerCamera.main != null && PlayerCamera.main.body != null
            ? PlayerCamera.main.body.transform.position
            : Vector2.zero);

        return Sound.Play(clip, playPos);
    }
}
