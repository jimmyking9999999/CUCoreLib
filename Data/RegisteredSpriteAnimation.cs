using UnityEngine;

namespace CUCoreLib.Data
{
    public sealed class RegisteredSpriteAnimation
    {
        public string Id;
        public Sprite[] Frames;
        public float FramesPerSecond = 12f;
        public bool Loop = true;
    }
}
