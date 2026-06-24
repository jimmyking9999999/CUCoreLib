using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.ContentReload;

namespace CUCoreLib.Registries
{
    public static class ConsoleCommandRegistry
    {
        private static readonly List<Command> RegisteredCommands = new List<Command>();

        public static void Register(string name, string description, Command.Action action, Dictionary<int, List<string>> argAutofill = null, params (string, string)[] argDescription)
        {
            ContentReloadSession.AssertNotActive("ConsoleCommandRegistry.Register()", "Console command registration is excluded from strict content reload.");

            if (string.IsNullOrWhiteSpace(name) || action == null)
            {
                // Probably better then allowing
                CUCoreLibPlugin.Log.LogWarning("Ignored console command registration with no action.");
                return;
            }

            string trimmedName = name.Trim();
            Command command = new Command(trimmedName, description ?? string.Empty, action, argAutofill, argDescription);
            Register(command);
        }

        public static void Register(Command command)
        {
            ContentReloadSession.AssertNotActive("ConsoleCommandRegistry.Register()", "Console command registration is excluded from strict content reload.");

            if (command == null || string.IsNullOrWhiteSpace(command.name))
            {
                CUCoreLibPlugin.Log.LogWarning("Ignored null console command registration.");
                return;
            }

            if (RegisteredCommands.Any(c => c.name.Equals(command.name, StringComparison.OrdinalIgnoreCase)))
            {
                CUCoreLibPlugin.Log.LogWarning($"Duplicate console command '{command.name}'!.");
                return;
            }

            RegisteredCommands.Add(command);

            if (ConsoleScript.Commands != null && ConsoleScript.Commands.Count > 0)
            {
                InjectSingle(command);
            }
        }

        internal static void InjectRegisteredCommands()
        {
            foreach (Command command in RegisteredCommands)
            {
                InjectSingle(command);
            }
        }

        private static void InjectSingle(Command command)
        {
            if (ConsoleScript.Commands.Any(c => c.name.Equals(command.name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            ConsoleScript.Commands.Add(command);
        }
    }
}
