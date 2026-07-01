using System.Collections;
using UnityEngine;

namespace CUCoreLib.Util;

public static class AwaitUtils
{
    public static IEnumerator AwaitMainMenu(float checkRepeatTimeSeconds = 0f)
    {
        while (!CheckUtils.IsMainMenuReady())
            if (checkRepeatTimeSeconds <= 0f)
                yield return null;
            else
                yield return new WaitForSecondsRealtime(checkRepeatTimeSeconds);
    }

    public static IEnumerator AwaitWorldGeneration(float checkRepeatTimeSeconds = 0f)
    {
        while (!CheckUtils.IsWorldGenerationReady())
            if (checkRepeatTimeSeconds <= 0f)
                yield return null;
            else
                yield return new WaitForSecondsRealtime(checkRepeatTimeSeconds);
    }

    public static IEnumerator AwaitKey(KeyCode keyCode)
    {
        yield return new WaitUntil(() => Input.GetKeyDown(keyCode));
    }

    public static IEnumerator AwaitLeftClick()
    {
        yield return AwaitKey(KeyCode.Mouse0);
    }

    public static IEnumerator AwaitRightClick()
    {
        yield return AwaitKey(KeyCode.Mouse1);
    }
}