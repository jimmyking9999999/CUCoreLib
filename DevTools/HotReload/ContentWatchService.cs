using System.Collections;
using CUCoreLib.Helpers;
using UnityEngine;

namespace CUCoreLib.ContentReload
{
    internal static class ContentWatchService
    {
        private static bool started;

        internal static void Initialize()
        {
            if (started) return;

            started = true;
            CUCoreUtils.StartCoroutine(WatchLoop());
        }

        private static IEnumerator WatchLoop()
        {
            while (true)
            {
                var intervalSeconds = ContentReloadManager.GetPollIntervalSeconds();
                if (intervalSeconds <= 0) intervalSeconds = 2;

                yield return new WaitForSecondsRealtime(intervalSeconds);
                ContentReloadManager.PollWatchers();
            }
        } // The iterator never returned
    }
}