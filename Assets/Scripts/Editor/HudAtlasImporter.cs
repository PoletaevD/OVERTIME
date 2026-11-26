using UnityEditor;
using UnityEngine;

namespace Overtime.Editor
{
    internal sealed class HudAtlasImporter : AssetPostprocessor
    {
        private const string TemporalGlyphs = "Assets/Resources/Temporal Glyphs.png";
        private const string ModulePlates = "Assets/Resources/Module Plates.png";

        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (assetPath == TemporalGlyphs)
            {
                // Allocate an alpha channel for the RGB glyph source.
                ((TextureImporter)assetImporter).alphaSource = TextureImporterAlphaSource.FromGrayScale;
            }
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            var modules = assetPath == ModulePlates;
            if (!modules && assetPath != TemporalGlyphs)
            {
                return;
            }

            var width = texture.width;
            var cellWidth = width / 5;
            var cellHeight = texture.height / 2;
            var border = Mathf.CeilToInt(cellWidth / 16f);
            var pixels = texture.GetPixels32();
            for (var index = 0; index < pixels.Length; index++)
            {
                var pixel = pixels[index];
                var light = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                var opacity = modules ? Mathf.InverseLerp(47f, 88f, light) : Mathf.InverseLerp(32f, 85f, light);
                if (modules)
                {
                    // The module atlas includes a frame at the edge of each cell.
                    var x = index % width % cellWidth;
                    var y = index / width % cellHeight;
                    if (x < border || x >= cellWidth - border || y < border || y >= cellHeight - border)
                    {
                        opacity = 0f;
                    }
                }

                pixel.a = (byte)Mathf.RoundToInt(opacity * (modules ? pixel.a : 255));
                pixels[index] = pixel;
            }

            texture.SetPixels32(pixels);
        }
    }
}

