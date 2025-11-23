using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace REPO_SteamInput;

public class SpriteFactory {
    
        // ReSharper disable once ShaderLabShaderReferenceNotResolved
	public static TMP_SpriteAsset BuildFromFiles(string[] filePaths, int padding = 10, float pixelsPerUnit = 100f) {
        List<Texture2D> textures = new List<Texture2D>();

        foreach (var path in filePaths) {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(fileData);
            tex.filterMode = FilterMode.Point;
            textures.Add(tex);
        }

        Texture2D atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
        atlas.filterMode = FilterMode.Point;
        Rect[] rects = atlas.PackTextures(textures.ToArray(), padding, 2048);

        TMP_SpriteAsset spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.name = "DynamicSpriteAsset";
        spriteAsset.spriteSheet = atlas;
        Material material = new Material(Shader.Find("TextMeshPro/Sprite"));
        material.mainTexture = atlas;
        spriteAsset.material = material;
        
        List<TMP_Sprite> sprites = new List<TMP_Sprite>();
        
        for (int i = 0; i < rects.Length; i++) {
            Rect rect = rects[i];
            Rect pixelRect = new Rect(
                rect.x * atlas.width,
                rect.y * atlas.height,
                rect.width * atlas.width,
                rect.height * atlas.height
            );
            
            TMP_Sprite sprite = new TMP_Sprite();

            sprite.name = Path.GetFileNameWithoutExtension(filePaths[i]);
            sprite.sprite = Sprite.Create(textures[i], rect, Vector2.zero, pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(padding, padding, padding, padding));
            sprite.unicode = 0;
            sprite.scale = 1f;
            sprite.x = rect.x * atlas.width;
            sprite.y = rect.y * atlas.height;
            sprite.width = rect.width * atlas.width;
            sprite.height = rect.height * atlas.height;
            sprite.yOffset = 80f;
            sprite.xAdvance = rect.width * atlas.width;
            sprites.Add(sprite);
        }
        
        spriteAsset.spriteInfoList = sprites;
        spriteAsset.UpdateLookupTables();
        
        for (int i = 0; i < spriteAsset.spriteGlyphTable.Count; i++) {
            spriteAsset.spriteCharacterTable[i].glyphIndex = spriteAsset.spriteGlyphTable[i].index;
        }
        spriteAsset.UpdateLookupTables();
      
        return spriteAsset;
    }
}