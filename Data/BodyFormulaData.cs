using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace CUCoreLib.Data
{
    [StatusOptions(Key = "cucorelib.bodyFormulaData", SaveEnabled = true)]
    internal sealed class BodyFormulaData : BodyStatus
    {
        public Dictionary<string, float> BloodPressure = new Dictionary<string, float>();
        public Dictionary<string, float> HeartRate = new Dictionary<string, float>();
        public Dictionary<string, float> RespiratoryRate = new Dictionary<string, float>();
        public Dictionary<string, float> MaxEncumberance = new Dictionary<string, float>();
        public Dictionary<string, float> TotalEncumberance = new Dictionary<string, float>();
        public Dictionary<string, float> Immunity = new Dictionary<string, float>();
        public Dictionary<string, float> JumpSpeed = new Dictionary<string, float>();
        public Dictionary<string, float> AveragePain = new Dictionary<string, float>();

        [JsonIgnore]
        public float AppliedJumpSpeedContribution;

        [JsonIgnore]
        public float AppliedAveragePainContribution;

        [JsonIgnore]
        public bool HasAnyFormulaEdits =>
            HasContributions(BloodPressure) ||
            HasContributions(HeartRate) ||
            HasContributions(RespiratoryRate) ||
            HasContributions(MaxEncumberance) ||
            HasContributions(TotalEncumberance) ||
            HasContributions(Immunity) ||
            HasContributions(JumpSpeed) ||
            HasContributions(AveragePain);

        internal static float Sum(Dictionary<string, float> contributions)
        {
            if (contributions == null || contributions.Count == 0)
            {
                return 0f;
            }

            return contributions.Values.Sum();
        }

        private static bool HasContributions(Dictionary<string, float> contributions)
        {
            return contributions != null && contributions.Values.Any(value => value != 0f);
        }
    }
}
