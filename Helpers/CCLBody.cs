using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CUCoreLib.Data;
using CUCoreLib.Patches;

namespace CUCoreLib.Helpers
{
    public static class CCLBody
    {
        [ThreadStatic] private static Body _scopedBody;

        public static float BloodPressure
        {
            get => GetValue(data => data.BloodPressure);
            set => SetValue(value, data => data.BloodPressure);
        }

        public static float HeartRate
        {
            get => GetValue(data => data.HeartRate);
            set => SetValue(value, data => data.HeartRate);
        }

        public static float RespiratoryRate
        {
            get => GetValue(data => data.RespiratoryRate);
            set => SetValue(value, data => data.RespiratoryRate);
        }

        public static float MaxEncumberance
        {
            get => GetValue(data => data.MaxEncumberance);
            set => SetValue(value, data => data.MaxEncumberance);
        }

        public static float TotalEncumberance
        {
            get => GetValue(data => data.TotalEncumberance);
            set => SetValue(value, data => data.TotalEncumberance);
        }

        public static float Immunity
        {
            get => GetValue(data => data.Immunity);
            set => SetValue(value, data => data.Immunity);
        }

        public static float JumpSpeed
        {
            get => GetValue(data => data.JumpSpeed);
            set
            {
                SetValue(value, data => data.JumpSpeed);
                BodyFormulaPatches.ApplyJumpSpeedContribution(GetBody());
            }
        }

        public static float AveragePain
        {
            get => GetValue(data => data.AveragePain);
            set => SetValue(value, data => data.AveragePain);
        }

        public static BodyScope Use(Body body)
        {
            return new BodyScope(body);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetValue(Func<BodyFormulaData, Dictionary<string, float>> selector)
        {
            var data = GetData();
            if (data == null) return 0f;

            var contributions = selector(data);
            var callerKey = ResolveCallerKey();
            return contributions.TryGetValue(callerKey, out var value) ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetValue(float value, Func<BodyFormulaData, Dictionary<string, float>> selector)
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
            return PlayerCamera.main != null ? PlayerCamera.main.body : null;
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

                return assembly.GetName().Name ?? assembly.FullName ?? "external";
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