using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;


namespace TcgEngine
{
    public class CardImageLoader : MonoBehaviour
    {
        public static byte[] GetCardImageBytes(string cardId)
        {
            string filePath = GetCardImagePath(cardId);
            if (File.Exists(filePath))
            {
                return File.ReadAllBytes(filePath);
            }
            
            #if UNITY_EDITOR
            Texture2D texture = Resources.Load<Texture2D>($"CardImages/{cardId}");
            if (texture != null)
            {
                // 읽기 가능한 복사본 생성
                Texture2D readable = MakeReadableCopy(texture);
                byte[] bytes = readable.EncodeToPNG();
                Destroy(readable);
                return bytes;
            }
            #endif
            
            return null;
        }
        
        private static string GetCardImagePath(string cardId)
        {
            #if UNITY_EDITOR
            return Path.Combine(Application.dataPath, 
                "TcgEngine/Resources/CardImages", cardId + ".png");
            #else
            return Path.Combine(Application.persistentDataPath, 
                "CardImages", cardId + ".png");
            #endif
        }
        
        private static Texture2D MakeReadableCopy(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(
                source.width, source.height, 0);
            
            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;
            
            Texture2D readableText = new Texture2D(source.width, source.height);
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply();
            
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            
            return readableText;
        }
    }
}