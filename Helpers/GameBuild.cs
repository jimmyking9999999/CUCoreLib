using System;

namespace CUCoreLib.Helpers
{
    /// <summary>
    /// Which Casualties Unknown build is running
    /// </summary>
    [Flags]
    public enum GameBuild
    {
        Demo = 1,
        Full = 2,
        Both = Demo | Full
    }

    public static class GameBuildInfo
    {
        public static GameBuild Current => CUCoreUtils.IsDemo() ? GameBuild.Demo : GameBuild.Full;

        public static bool IsFullGame => !CUCoreUtils.IsDemo();

        public static bool Matches(GameBuild target)
        {
            return (target & Current) != 0;
        }
    }

    /// <summary>
    /// Restricts a <c>[HarmonyPatch]</c> family to (a) specific build(s)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class AppliesToBuildAttribute : Attribute
    {
        public GameBuild Build { get; }

        public AppliesToBuildAttribute(GameBuild build)
        {
            Build = build;
        }
    }
}
