using System.Collections.Generic;
using System.Linq;
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

            Sprite newSprite = GenerateFadedSprite(uiImage.sprite, fadePercentage, directions);
            if (newSprite != null)
            {
                uiImage.sprite = newSprite;
            }
        }

        public static void ApplyFade(SpriteRenderer renderer, float fadePercentage, params string[] directions)
        {
            if (renderer == null || renderer.sprite == null) return;

            Sprite newSprite = GenerateFadedSprite(renderer.sprite, fadePercentage, directions);
            if (newSprite != null)
            {
                renderer.sprite = newSprite;
            }
        }

        
        public static Sprite GenerateFadedSprite(Sprite sourceSprite, float fadePercentage, IEnumerable<string> directions)
        {
            if (sourceSprite == null) return null;

            HashSet<string> dirSet = new HashSet<string>();
            if (directions != null)
            {
                foreach (var d in directions)
                {
                    var splits = d.Split(new char[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var s in splits) dirSet.Add(s.ToLowerInvariant());
                }
            }

            // copy, should be fine
            Texture2D originalTexture = DuplicateTexture(sourceSprite.texture);

            int width = originalTexture.width;
            int height = originalTexture.height;

            Texture2D newTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            newTexture.filterMode = originalTexture.filterMode;

            // tresholds
            float fadeThresholdY = height * fadePercentage;
            float fadeThresholdX = width * fadePercentage;

            Color[] pixels = originalTexture.GetPixels();
            Color[] newPixels = new Color[pixels.Length];

            bool doUp = dirSet.Contains("up");
            bool doDown = dirSet.Contains("down");
            bool doLeft = dirSet.Contains("left");
            bool doRight = dirSet.Contains("right");

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color col = pixels[index];

                    float alphaFactor = 1.0f;

                    // DOWN
                    if (doDown && y < fadeThresholdY)
                    {
                        float f = (float)y / fadeThresholdY;
                        if (f < alphaFactor) alphaFactor = f;
                    }

                    // UP
                    if (doUp && y > (height - fadeThresholdY))
                    {
                        float distFromTop = height - y;
                        float f = distFromTop / fadeThresholdY;
                        if (f < alphaFactor) alphaFactor = f;
                    }

                    // LEFT
                    if (doLeft && x < fadeThresholdX)
                    {
                        float f = (float)x / fadeThresholdX;
                        if (f < alphaFactor) alphaFactor = f;
                    }

                    // RIGHT
                    if (doRight && x > (width - fadeThresholdX))
                    {
                        float distFromRight = width - x;
                        float f = distFromRight / fadeThresholdX;
                        if (f < alphaFactor) alphaFactor = f;
                    }

                    col.a *= alphaFactor;
                    newPixels[index] = col;
                }
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
            RenderTexture renderTex = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);

            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            readableText.filterMode = source.filterMode;

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableText;
        }

        public static Texture2D ResizeTexture(Texture2D source, int mulX, int mulY)
        {
            int sourceWidth = source.width;
            int sourceHeight = source.height;
            int targetWidth = sourceWidth * mulX;
            int targetHeight = sourceHeight * mulY;

            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);

            Color[] sourcePixels = source.GetPixels();
            Color[] targetPixels = new Color[targetWidth * targetHeight];

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int srcX = x / mulX;
                    int srcY = y / mulY;

                    int sourceIndex = srcY * sourceWidth + srcX;
                    int targetIndex = y * targetWidth + x;

                    targetPixels[targetIndex] = sourcePixels[sourceIndex];
                }
            }

            result.SetPixels(targetPixels);
            result.Apply();
            return result;
        }
    
    }
}
