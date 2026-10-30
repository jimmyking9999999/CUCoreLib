using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CUCoreLib.Data;
using CUCoreLib.Networking;

namespace CUCoreLib.Helpers
{
    /// <summary>
    ///     A Helper to assist in modifying the player that closely follows vanilla behavior so you don't have to write
    ///     transpilers, prefix overrides or postfixes with duplicated logic.
    /// </summary>
    /// <remarks>
    ///     <list type="bullet">
    ///         <item>
    ///             <term>Mod Clobbering</term>
    ///             <description>This Helper is per assembly, so you don't need to worry about clobbering another mod's values.</description>
    ///         </item>
    ///         <item>
    ///             <term>Handling CCLBody</term>
    ///             <description>
    ///                 If you have multiple objects that modify the same field in this class you'll need to have a helper
    ///                 function to handle changing the values.<br />
    ///                 Avoid writing multiple times with the same value, every call requires resolving the calling assembly.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public static class CCLBody
    {
        [ThreadStatic] private static Body _scopedBody;

        /// <summary>
        ///     Increases the target Blood Pressure.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::HandleCirculation.<br />
        ///     Vanilla clamps this from 0 to 250.
        /// </remarks>
        public static float BloodPressure
        {
            get => GetValue(data => data.BloodPressure);
            set => SetValue(value, data => data.BloodPressure);
        }

        /// <summary>
        ///     Increases the target Heart Rate.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::HandleCirculation.
        /// </remarks>
        public static float HeartRate
        {
            get => GetValue(data => data.HeartRate);
            set => SetValue(value, data => data.HeartRate);
        }

        /// <summary>
        ///     Increases the target Respiratory Rate.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::HandleCirculation.<br />
        ///     Vanilla clamps this from 0 to 100.
        /// </remarks>
        public static float RespiratoryRate
        {
            get => GetValue(data => data.RespiratoryRate);
            set => SetValue(value, data => data.RespiratoryRate);
        }

        /// <summary>
        ///     Increases the load the player can handle.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::HandlePeriodicChecks.
        /// </remarks>
        public static float MaxEncumberance
        {
            get => GetValue(data => data.MaxEncumberance);
            set => SetValue(value, data => data.MaxEncumberance);
        }

        /// <summary>
        ///     Increases the load the player is under.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::HandlePeriodicChecks.
        /// </remarks>
        public static float TotalEncumberance
        {
            get => GetValue(data => data.TotalEncumberance);
            set => SetValue(value, data => data.TotalEncumberance);
        }

        /// <summary>
        ///     Increases player immunity, ignores maximum clamp.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::HandlePeriodicChecks.
        /// </remarks>
        public static float Immunity
        {
            get => GetValue(data => data.Immunity);
            set => SetValue(value, data => data.Immunity);
        }

        /// <summary>
        ///     Increases the maximum speed the player can go, use this over Body::maxSpeed as that is permanent and will likely
        ///     stack across layers and loads if used improperly.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::actualMaxSpeed.
        /// </remarks>
        public static float MaxSpeed
        {
            get => GetValue(data => data.MaxSpeed);
            set => SetValue(value, data => data.MaxSpeed);
        }

        /// <summary>
        ///     Increases jump height and speed, use this over Body::jumpSpeed as that is permanent and will likely stack across
        ///     layers and loads if used improperly.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::actualJumpSpeed.
        /// </remarks>
        public static float JumpSpeed
        {
            get => GetValue(data => data.JumpSpeed);
            set => SetValue(value, data => data.JumpSpeed);
        }

        /// <summary>
        ///     Increases the average pain the player is in, won't show up on limbs.
        /// </summary>
        /// <remarks>
        ///     Transpiled into Body::HandleBody.
        /// </remarks>
        public static float AveragePain
        {
            get => GetValue(data => data.AveragePain);
            set => SetValue(value, data => data.AveragePain);
        }

        /// <summary>
        ///     Changes the targeted body.
        /// </summary>
        /// <value>
        ///     BodyScope that contains the previous body, use the Dispose function to change back to it.
        /// </value>
        public static BodyScope Use(Body body)
        {
            return new BodyScope(body);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetValue(
            Func<BodyFormulaData, Dictionary<string, float>> selector)
        {
            var data = GetData();
            if (data == null) return 0f;

            var contributions = selector(data);
            var callerKey = ResolveCallerKey();
            return contributions.TryGetValue(callerKey, out var value)
                ? value
                : 0f;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetValue(float value,
            Func<BodyFormulaData, Dictionary<string, float>> selector)
        {
            var data = GetData();
            if (data == null) return;

            var contributions = selector(data);
            var callerKey = ResolveCallerKey();
            contributions[callerKey] = value;
        }

        private static BodyFormulaData GetData()
        {
            var body = GetBody();
            if (body == null) return null;

            return body.GetBodyFormulaData();
        }

        private static Body GetBody()
        {
            if (_scopedBody != null) return _scopedBody;
            return MultiplayerApi.TryGetLocalBody(out var body)
                ? body
                : null;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ResolveCallerKey()
        {
            var currentAssembly = typeof(CCLBody).Assembly;
            var trace = new StackTrace();

            for (var i = 1; i < trace.FrameCount; i++)
            {
                var method = trace.GetFrame(i)?.GetMethod();
                var declaringType = method?.DeclaringType;
                var assembly = declaringType?.Assembly;
                if (assembly == null || assembly == currentAssembly) continue;

                return assembly.GetName().Name ?? assembly.FullName;
            }

            return currentAssembly.GetName().Name ?? "cucorelib";
        }

        public struct BodyScope : IDisposable
        {
            private readonly Body _previousBody;

            internal BodyScope(Body body)
            {
                _previousBody = _scopedBody;
                _scopedBody = body;
            }

            public void Dispose()
            {
                _scopedBody = _previousBody;
            }
        }
    }
}
