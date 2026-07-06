using UnityEngine;

namespace CUCoreLib.Data
{
    [System.Serializable]
    public struct BodyAnimationCurveOverride
    {
        public CUCoreLib.Helpers.BodyAnimationCurveField Field;
        public string AssetName;
    }

    [CreateAssetMenu(fileName = "BodyAnimationCurveProfile", menuName = "CUCoreLib/Body Animation Curve Profile")]
    public sealed class BodyAnimationCurveProfileAsset : ScriptableObject
    {
        public BodyAnimationCurveOverride[] Overrides = new BodyAnimationCurveOverride[0];
    }
}
