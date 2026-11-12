using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CUCoreLib.ContentReload;
using HarmonyLib;

namespace CUCoreLib.Registries
{
    public static class ConsoleCommandRegistry
    {
        private static readonly List<Command> RegisteredCommands = new List<Command>();

        // Full game only: the Demo has no achievement guard, so cheat registration degrades to a normal command.
        private static readonly MethodInfo CheckForCheatsMethod =
            AccessTools.Method(typeof(ConsoleScript), "CheckForCheats");

        public static void Register(string name, string description, Command.Action action,
            Dictionary<int, List<string>> argAutofill = null, params (string, string)[] argDescription)
        {
            Register(name, description, action, false, argAutofill, argDescription);
        }

        public static void Register(string name, string description, Command.Action action, bool isCheat,
            Dictionary<int, List<string>> argAutofill = null, params (string, string)[] argDescription)
        {
            ContentReloadSession.AssertNotActive("ConsoleCommandRegistry.Register()",
                "Console command registration is excluded from strict content reload.");

            if (string.IsNullOrWhiteSpace(name))
            {
                CUCoreLibPlugin.Log.LogWarning(
                    "Ignored console command registration because the command name was null, empty, or whitespace.");
                return;
            }

            if (action == null)
            {
                CUCoreLibPlugin.Log.LogWarning(
                    "Ignored console command registration for '" + name.Trim() + "' because the action was null.");
                return;
            }

            var trimmedName = name.Trim();
            var command = new Command(trimmedName, description ?? string.Empty, action, argAutofill, argDescription);
            Register(command, isCheat);
        }

        public static void Register(Command command)
        {
            Register(command, false);
        }

        public static void Register(Command command, bool isCheat)
        {
            ContentReloadSession.AssertNotActive("ConsoleCommandRegistry.Register()",
                "Console command registration is excluded from strict content reload.");

            if (command == null)
            {
                CUCoreLibPlugin.Log.LogWarning("Ignored console command registration because the command object was null.");
                return;
            }

            if (string.IsNullOrWhiteSpace(command.name))
            {
                CUCoreLibPlugin.Log.LogWarning(
                    "Ignored console command registration because command.name was null, empty, or whitespace.");
                return;
            }

            if (RegisteredCommands.Any(c => c.name.Equals(command.name, StringComparison.OrdinalIgnoreCase)))
            {
                CUCoreLibPlugin.Log.LogWarning($"Ignored duplicate console command registration for '{command.name}'.");
                return;
            }

            if (isCheat) command.action = WithCheatGuard(command.action, command.name);

            RegisteredCommands.Add(command);

            if (ConsoleScript.Commands != null && ConsoleScript.Commands.Count > 0) InjectSingle(command);
        }

        private static Command.Action WithCheatGuard(Command.Action action, string name)
        {
            if (CheckForCheatsMethod == null)
            {
                CUCoreLibPlugin.Log.LogWarning(
                    $"Console command '{name}' was registered as a cheat, but this game build has no achievement guard. Achievements stay enabled.");
                return action;
            }

            return args =>
            {
                if (ConsoleScript.instance != null) CheckForCheatsMethod.Invoke(ConsoleScript.instance, null);
                action(args);
            };
        }

        internal static void InjectRegisteredCommands()
        {
            foreach (var command in RegisteredCommands) InjectSingle(command);
        }

        private static void InjectSingle(Command command)
        {
            if (ConsoleScript.Commands.Any(c => c.name.Equals(command.name, StringComparison.OrdinalIgnoreCase)))
                return;

            ConsoleScript.Commands.Add(command);
        }
    }
}
