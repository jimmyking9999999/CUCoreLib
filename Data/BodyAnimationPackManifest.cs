using System;

namespace CUCoreLib.Data
{
    public sealed class BodyAnimationPackManifest
    {
        public BodyAnimationPackEntry[] Animations = Array.Empty<BodyAnimationPackEntry>();
        public string PackId;
    }

    // I shouldn't need xml comments here, I hope ;p
    public sealed class BodyAnimationPackEntry
    {
        public string AnimationId;
        public string ArmsClipAssetName;
        public string BodyClipAssetName;
        public bool Loop;
        public float Speed = 1f;
    }
}