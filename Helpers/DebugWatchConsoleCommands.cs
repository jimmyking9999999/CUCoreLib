using System;
using System.Linq;

namespace CUCoreLib.Helpers
{
    internal static class DebugWatchConsoleCommands
    {
        internal static void Run(ConsoleScript console, string[] args)
        {
            if (args == null || args.Length < 2)
                throw new Exception("Usage: debugwatch [add|remove|list|clear|show|hide] [Type.member]");

            var verb = args[1]?.Trim()?.ToLowerInvariant();
            switch (verb)
            {
                case "add":
                    HandleAdd(console, args);
                    return;
                case "remove":
                    HandleRemove(console, args);
                    return;
                case "list":
                    HandleList(console);
                    return;
                case "clear":
                    DebugWatchService.ClearWatches();
                    CUCoreUtils.ConsoleLog(console, "Cleared all debug watches.");
                    return;
                case "show":
                    DebugWatchService.SetOverlayVisible(true);
                    CUCoreUtils.ConsoleLog(console, "Debug watch overlay shown.");
                    return;
                case "hide":
                    DebugWatchService.SetOverlayVisible(false);
                    CUCoreUtils.ConsoleLog(console, "Debug watch overlay hidden.");
                    return;
                default:
                    throw new Exception($"Unknown debugwatch action '{args[1]}'.");
            }
        }

        private static void HandleAdd(ConsoleScript console, string[] args)
        {
            if (args.Length < 3)
                throw new Exception("Usage: debugwatch add [Type.member]");

            if (!DebugWatchService.AddWatch(args[2], out var message))
                throw new Exception(message);

            CUCoreUtils.ConsoleLog(console, message);
        }

        private static void HandleRemove(ConsoleScript console, string[] args)
        {
            if (args.Length < 3)
                throw new Exception("Usage: debugwatch remove [Type.member]");

            if (!DebugWatchService.RemoveWatch(args[2], out var message))
                throw new Exception(message);

            CUCoreUtils.ConsoleLog(console, message);
        }

        private static void HandleList(ConsoleScript console)
        {
            var activeLines = DebugWatchService.GetActiveWatchLines();
            if (activeLines.Count == 0)
            {
                CUCoreUtils.ConsoleLog(console, "No active debug watches.");
                return;
            }

            CUCoreUtils.ConsoleLog(console,
                $"Active debug watches ({activeLines.Count}) - overlay {(DebugWatchService.IsOverlayVisible() ? "shown" : "hidden")}:");
            foreach (var line in activeLines.ToList())
                CUCoreUtils.ConsoleLog(console, "  " + line);
        }
    }
}