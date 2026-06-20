using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CUCoreLib.Helpers
{
    internal static class ModifyTextures
    {
        // This was part of the original mod I based this library on, but it's niche enough that someone out there might eventually find a use for it
        // Why remove if it it has no overhad and ain't broke? ;)
        public static void ApplyFade(Image uiImage, float fadePercentage, params string[] directions)
        {
            if (uiImage == null || uiImage.sprite == null) return;

            var newSprite = GenerateFadedSprite(uiImage.sprite, fadePercentage, directions);
            if (newSprite != null) uiImage.sprite = newSprite;
        }

        public static void ApplyFade(SpriteRenderer renderer, float fadePercentage, params string[] directions)
        {
            if (renderer == null || renderer.sprite == null) return;

            var newSprite = GenerateFadedSprite(renderer.sprite, fadePercentage, directions);
            if (newSprite != null) renderer.sprite = newSprite;
        }


        public static Sprite GenerateFadedSprite(Sprite sourceSprite, float fadePercentage,
            IEnumerable<string> directions)
        {
            if (sourceSprite == null) return null;

            var dirSet = new HashSet<string>();
            if (directions != null)
                foreach (var d in directions)
                {
                    var splits = d.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var s in splits) dirSet.Add(s.ToLowerInvariant());
                }

            // copy, should be fine
            var originalTexture = DuplicateTexture(sourceSprite.texture);

            var width = originalTexture.width;
            var height = originalTexture.height;

            var newTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = originalTexture.filterMode
            };

            // tresholds
            var fadeThresholdY = height * fadePercentage;
            var fadeThresholdX = width * fadePercentage;

            var pixels = originalTexture.GetPixels();
            var newPixels = new Color[pixels.Length];

            var doUp = dirSet.Contains("up");
            var doDown = dirSet.Contains("down");
            var doLeft = dirSet.Contains("left");
            var doRight = dirSet.Contains("right");

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var col = pixels[index];

                var alphaFactor = 1.0f;

                // DOWN
                if (doDown && y < fadeThresholdY)
                {
                    var f = y / fadeThresholdY;
                    if (f < alphaFactor) alphaFactor = f;
                }

                // UP
                if (doUp && y > height - fadeThresholdY)
                {
                    float distFromTop = height - y;
                    var f = distFromTop / fadeThresholdY;
                    if (f < alphaFactor) alphaFactor = f;
                }

                // LEFT
                if (doLeft && x < fadeThresholdX)
                {
                    var f = x / fadeThresholdX;
                    if (f < alphaFactor) alphaFactor = f;
                }

                // RIGHT
                if (doRight && x > width - fadeThresholdX)
                {
                    float distFromRight = width - x;
                    var f = distFromRight / fadeThresholdX;
                    if (f < alphaFactor) alphaFactor = f;
                }

                col.a *= alphaFactor;
                newPixels[index] = col;
            }

            newTexture.SetPixels(newPixels);
            newTexture.Apply();

            return Sprite.Create(
                newTexture,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f),
                sourceSprite.pixelsPerUnit
            );
        }

        private static Texture2D DuplicateTexture(Texture2D source)
        {
            var renderTex = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            var previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            var readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            readableText.filterMode = source.filterMode;

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }

        public static Texture2D ResizeTexture(Texture2D source, int mulX, int mulY)
        {
            var sourceWidth = source.width;
            var sourceHeight = source.height;
            var targetWidth = sourceWidth * mulX;
            var targetHeight = sourceHeight * mulY;

            var result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

            var sourcePixels = source.GetPixels();
            var targetPixels = new Color[targetWidth * targetHeight];

            for (var y = 0; y < targetHeight; y++)
            for (var x = 0; x < targetWidth; x++)
            {
                var srcX = x / mulX;
                var srcY = y / mulY;

                var sourceIndex = srcY * sourceWidth + srcX;
                var targetIndex = y * targetWidth + x;

                targetPixels[targetIndex] = sourcePixels[sourceIndex];
            }

            result.SetPixels(targetPixels);
            result.Apply();
            return result;
        }
    }
}