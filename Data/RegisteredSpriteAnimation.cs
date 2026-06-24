using UnityEngine;

namespace CUCoreLib.Data
{
    public sealed class RegisteredSpriteAnimation
    {
        public Sprite[] Frames;
        public float FramesPerSecond = 12f;
        public string Id;
        public bool Loop = true;
    }
}