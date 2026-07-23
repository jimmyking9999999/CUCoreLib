using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using CUCoreLib.Data;

namespace CUCoreLib.Helpers
{
    public static class CCLBody
    {
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
            set => SetValue(value, data => data.JumpSpeed);
        }

        public static float AveragePain
        {
            get => GetValue(data => data.AveragePain);
            set => SetValue(value, data => data.AveragePain);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static float GetValue(System.Func<BodyFormulaData, Dictionary<string, float>> selector)
        {
            BodyFormulaData data = GetData();
            if (data == null)
            {
                return 0f;
            }

            Dictionary<string, float> contributions = selector(data);
            string callerKey = ResolveCallerKey();
            return contributions.TryGetValue(callerKey, out float value) ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetValue(float value, System.Func<BodyFormulaData, Dictionary<string, float>> selector)
        {
            BodyFormulaData data = GetData();
            if (data == null)
            {
                return;
            }

            Dictionary<string, float> contributions = selector(data);
            string callerKey = ResolveCallerKey();
            contributions[callerKey] = value;
        }

        private static BodyFormulaData GetData()
        {
            if (PlayerCamera.main == null || PlayerCamera.main.body == null)
            {
                return null;
            }

            return PlayerCamera.main.body.GetBodyFormulaData();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ResolveCallerKey()
        {
            Assembly currentAssembly = typeof(CCLBody).Assembly;
            StackTrace trace = new StackTrace();

            for (int i = 1; i < trace.FrameCount; i++)
            {
                MethodBase method = trace.GetFrame(i)?.GetMethod();
                Type declaringType = method?.DeclaringType;
                Assembly assembly = declaringType?.Assembly;
                if (assembly == null || assembly == currentAssembly)
                {
                    continue;
                }

                return assembly.GetName().Name ?? assembly.FullName ?? "external";
            }

            return currentAssembly.GetName().Name ?? "cucorelib";
        }
    }
}
