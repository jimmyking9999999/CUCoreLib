using BepInEx;
using BepInEx.Logging;
using CUCoreLib.ContentReload;

namespace CUCoreLib.Helpers
{
    public abstract class CCLBase
    {
        protected CCLBase()
        {
        }

        protected CCLBase(BaseUnityPlugin plugin)
        {
            Plugin = plugin;
        }

        protected BaseUnityPlugin Plugin { get; private set; }

        protected ManualLogSource Logger => CUCoreLibPlugin.Log;

        public static void Initialize(BaseUnityPlugin plugin)
        {
            if (plugin == null) return;

            ContentHostRuntime.RegisterPlugin(plugin);
        }

        internal void AttachPlugin(BaseUnityPlugin plugin)
        {
            Plugin = plugin;
        }

        protected T GetPlugin<T>() where T : BaseUnityPlugin
        {
            return Plugin as T;
        }

        protected PluginInfo GetPluginInfo()
        {
            return Plugin != null ? Plugin.Info : null;
        }
    }
}