using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Saving
{
    public interface ICustomSaveProvider
    {
        int GetVersion();
        JToken Capture();
        void Restore(JToken payload, int version, SaveRestoreContext context);
    }

    public interface IItemSaveProvider
    {
        int GetVersion();
        JToken Capture(Item item, string itemKey);
        void Restore(Item item, string itemKey, JToken payload, int version, SaveRestoreContext context);
    }

    public interface IBodySaveProvider
    {
        int GetVersion();
        JToken Capture(Body body);
        void Restore(Body body, JToken payload, int version, SaveRestoreContext context);
    }

    public interface ILimbSaveProvider
    {
        int GetVersion();
        JToken Capture(Limb limb, int limbIndex);
        void Restore(Limb limb, int limbIndex, JToken payload, int version, SaveRestoreContext context);
    }

    public interface IWorldSaveProvider
    {
        int GetVersion();
        JToken Capture(WorldSaveContext context);
        void Restore(WorldSaveContext context, JToken payload, int version, SaveRestoreContext contextForRestore);
    }

    public sealed class WorldSaveContext
    {
        public Body Body => PlayerCamera.main != null ? PlayerCamera.main.body : null;
        public PlayerCamera PlayerCamera => PlayerCamera.main;
        public WorldGeneration World => WorldGeneration.world;
    }

    public sealed class SaveRestoreContext
    {
        private readonly List<Action> _deferredActions = new List<Action>();

        internal SaveRestoreContext()
        {
        }

        public Body Body => PlayerCamera.main != null ? PlayerCamera.main.body : null;
        public PlayerCamera PlayerCamera => PlayerCamera.main;
        public WorldGeneration World => WorldGeneration.world;

        public void Defer(Action action)
        {
            if (action == null)
            {
                return;
            }

            _deferredActions.Add(action);
        }

        internal void ExecuteDeferred()
        {
            if (_deferredActions.Count == 0)
            {
                return;
            }

            List<Action> actions = new List<Action>(_deferredActions);
            _deferredActions.Clear();

            CUCoreLib.Helpers.CUCoreUtils.StartCoroutine(RunDeferred(actions));
        }

        private static System.Collections.IEnumerator RunDeferred(List<Action> actions)
        {
            yield return null;

            foreach (Action action in actions)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Save: Deferred restore action failed.\n" + ex);
                }
            }
        }
    }
}
