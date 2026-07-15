namespace CUCoreLib.Data
{
    public sealed class BodyAnimationPackManifest
    {
        public string PackId;
        public BodyAnimationPackEntry[] Animations = new BodyAnimationPackEntry[0];
    }

    // I shouldn't need xml comments here, I hope ;p
    public sealed class BodyAnimationPackEntry
    {
        public string AnimationId;
        public string BodyClipAssetName;
        public string ArmsClipAssetName;
        public bool Loop;
        public float Speed = 1f;
    }
}
