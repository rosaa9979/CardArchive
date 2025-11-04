using UnityEngine;


namespace TcgEngine
{
    public static class CardImageLoader // 클래스 자체를 static으로 변경
    {
        private const string BasePath = "CardImages/";

        /// <summary>
        /// Resources/CardImages/ 폴더에서 카드 ID에 해당하는 Texture2D를 로드하여 반환합니다.
        /// </summary>

        public static Texture2D LoadCardImage(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                Debug.LogError("[Load Failed] Card ID is null or empty.");
                return null;
            }

            string resourcePath = BasePath + cardId;

            Texture2D loadedTexture = Resources.Load<Texture2D>(resourcePath);

            if (loadedTexture != null)
                return loadedTexture;
            else
                return null;
        }
    }
}