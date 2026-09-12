using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.ContentReload;

namespace CUCoreLib.Registries
{
    public static class ConsoleCommandRegistry
    {
        private static readonly List<Command> RegisteredCommands = new List<Command>();

        public static void Register(string name, string description, Command.Action action,
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
            Register(command);
        }

        public static void Register(Command command)
        {
            ContentReloadSession.AssertNotActive("ConsoleCommandRegistry.Register()",
                "Console command registration is excluded from strict content reload.");

            if (command == null)
            {
                CUCoreLibPlugin.Log.LogWarning(
                    "Ignored console command registration because the command object was null.");
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

            RegisteredCommands.Add(command);

            if (ConsoleScript.Commands != null && ConsoleScript.Commands.Count > 0) InjectSingle(command);
        }

        internal static void InjectRegisteredCommands()
        {
            foreach (var command in RegisteredCommands) InjectSingle(command);
        }

        // 允许内置命令在语言重载后重新注册并刷新本地化描述。
        // 仅从注册表与游戏命令列表中移除，不影响其他 mod 注册的命令。
        internal static void Unregister(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var trimmedName = name.Trim();
            RegisteredCommands.RemoveAll(command =>
                command != null && command.name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));

            if (ConsoleScript.Commands == null) return;
            ConsoleScript.Commands.RemoveAll(command =>
                command != null && command.name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
        }

        private static void InjectSingle(Command command)
        {
            if (ConsoleScript.Commands.Any(c => c.name.Equals(command.name, StringComparison.OrdinalIgnoreCase)))
                return;

            ConsoleScript.Commands.Add(command);
        }
    }
}