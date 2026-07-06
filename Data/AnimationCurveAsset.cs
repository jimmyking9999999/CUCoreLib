using UnityEngine;

namespace CUCoreLib.Data
{
    [CreateAssetMenu(fileName = "AnimationCurveAsset", menuName = "CUCoreLib/Animation Curve Asset")]
    public sealed class AnimationCurveAsset : ScriptableObject
    {
        public AnimationCurve Curve;
    }
}
