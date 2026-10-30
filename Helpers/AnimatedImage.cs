using CUCoreLib.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CUCoreLib.Helpers
{
    [DisallowMultipleComponent]
    public sealed class AnimatedImage : MonoBehaviour
    {
        private Image _image;
        private float _time;

        public string AnimationId { get; private set; }

        public RegisteredSpriteAnimation Animation { get; private set; }

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void Update()
        {
            if (!_image || Animation?.Frames == null || Animation.Frames.Length == 0)
                return;

            _time += Time.unscaledDeltaTime;
            ApplyCurrentFrame();
        }

        private void OnEnable()
        {
            _time = 0f;
            ApplyCurrentFrame();
        }

        public void SetAnimation(string animationId, RegisteredSpriteAnimation animation)
        {
            AnimationId = animationId;
            Animation = animation;
            _time = 0f;
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            if (!_image
                || Animation?.Frames == null
                || Animation.Frames.Length == 0) return;

            var frameIndex = ResolveFrameIndex();
            var frame = Animation.Frames[frameIndex];
            if (frame) _image.sprite = frame;
        }

        private int ResolveFrameIndex()
        {
            if (Animation?.Frames == null
                || Animation.Frames.Length == 0) return 0;

            if (Animation.Frames.Length == 1 || Animation.FramesPerSecond <= 0f) return 0;

            var frameCount = Animation.Frames.Length;
            var frameIndex = Mathf.FloorToInt(_time * Animation.FramesPerSecond);
            if (Animation.Loop)
                frameIndex %= frameCount;
            else
                frameIndex = Mathf.Min(frameIndex, frameCount - 1);

            return Mathf.Clamp(frameIndex, 0, frameCount - 1);
        }
    }
}
