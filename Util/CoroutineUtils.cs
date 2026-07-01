using System;
using System.Collections;
using UnityEngine;

namespace CUCoreLib.Util;

public static class CoroutineUtils
{
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