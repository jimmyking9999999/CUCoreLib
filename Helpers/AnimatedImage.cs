using CUCoreLib.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CUCoreLib.Helpers
{
    [DisallowMultipleComponent]
    public sealed class AnimatedImage : MonoBehaviour
    {
        private Image _image;
        private RegisteredSpriteAnimation _animation;
        private float _time;

        public string AnimationId { get; private set; }

        private void Awake()
        {
            _image = GetComponent<Image>();
        }

        private void OnEnable()
        {
            _time = 0f;
            ApplyCurrentFrame();
        }

        private void Update()
        {
            if (_image == null || _animation == null || _animation.Frames == null || _animation.Frames.Length == 0)
            {
                return;
            }

            _time += Time.unscaledDeltaTime;
            ApplyCurrentFrame();
        }

        public void SetAnimation(string animationId, RegisteredSpriteAnimation animation)
        {
            AnimationId = animationId;
            _animation = animation;
            _time = 0f;
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            if (_image == null || _animation == null || _animation.Frames == null || _animation.Frames.Length == 0)
            {
                return;
            }

            int frameIndex = ResolveFrameIndex();
            Sprite frame = _animation.Frames[frameIndex];
            if (frame != null)
            {
                _image.sprite = frame;
            }
        }

        private int ResolveFrameIndex()
        {
            if (_animation == null || _animation.Frames == null || _animation.Frames.Length == 0)
            {
                return 0;
            }

            if (_animation.Frames.Length == 1 || _animation.FramesPerSecond <= 0f)
            {
                return 0;
            }

            int frameCount = _animation.Frames.Length;
            int frameIndex = Mathf.FloorToInt(_time * _animation.FramesPerSecond);
            if (_animation.Loop)
            {
                frameIndex %= frameCount;
            }
            else
            {
                frameIndex = Mathf.Min(frameIndex, frameCount - 1);
            }

            return Mathf.Clamp(frameIndex, 0, frameCount - 1);
        }
    }
}
