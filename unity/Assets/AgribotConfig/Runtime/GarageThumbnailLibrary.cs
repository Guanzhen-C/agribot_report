using System;
using System.IO;
using UnityEngine;

namespace Agribot.VehicleConfiguration
{
    public static class GarageThumbnailLibrary
    {
        public const string ResourceFolder = "LocalGarageThumbnails";

        public static Texture2D LoadTexture(string id)
        {
            if (string.IsNullOrEmpty(id) || Array.Exists(id.ToCharArray(), c => !char.IsLetterOrDigit(c) && c != '_'))
                throw new ArgumentException("Invalid thumbnail identifier", nameof(id));
            var png = Resources.Load<TextAsset>(ResourceFolder + "/" + id);
            if (png == null) throw new FileNotFoundException("缺少缩略图资源，请刷新缩略图后重新构建：" + id);
            // The workstation's image importer produced channel-corrupted icons.
            // Decode the original PNG bytes without changing the model colours.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = id };
            try
            {
                if (!texture.LoadImage(png.bytes, true))
                    throw new InvalidDataException("缩略图 PNG 无法解码：" + id);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                return texture;
            }
            catch
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }
    }
}
