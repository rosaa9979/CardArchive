using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TcgEngine
{
    /// <summary>
    /// Use this to export all your cards to png images as they appear on the board (board art + frame + stats)
    /// Same flow as CardExporter, but renders the world-space BoardCard prefab instead of the full CardUI
    /// </summary>

    public class BoardCardExporter : MonoBehaviour
    {
        public string export_path = "C:/BoardCardsExport";
        public int width = 800;
        public int height = 896;
        public float capture_height = 2.24f; //World units captured vertically, captured width follows the texture aspect ratio
        public VariantData variant;

        [Header("References")]
        public Camera render_cam;
        public BoardCard board_card;

        private CardUI card_ui;
        private RenderTexture texture;
        private Texture2D export_texture;

        private Color32 font_color = new Color32(37, 44, 91, 255); //Same as BoardCard font_color

        void Start()
        {
            if (variant == null)
                variant = VariantData.GetDefault();

            card_ui = board_card.GetComponent<CardUI>();

            //BoardCard.Awake deactivates CanvasUI/StatUI until the game is ready, reactivate them to render
            foreach (Canvas canvas in board_card.GetComponentsInChildren<Canvas>(true))
                canvas.gameObject.SetActive(true);

            //Hide gameplay-only elements
            if (board_card.card_glow != null)
                board_card.card_glow.gameObject.SetActive(false);
            if (board_card.card_shadow != null)
                board_card.card_shadow.gameObject.SetActive(false);
            if (board_card.armor != null)
                board_card.armor.enabled = false;
            if (board_card.armor_icon != null)
                board_card.armor_icon.enabled = false;
            if (board_card.status_group != null)
                board_card.status_group.alpha = 0f;
            if (board_card.equipment != null)
                board_card.equipment.Hide();
            foreach (AbilityButton button in board_card.buttons)
                button.Hide();

            GenerateAll();
        }

        private async void GenerateAll()
        {
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1); //Set Max Quality level

            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            export_texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Point;
            export_texture.filterMode = FilterMode.Point;
            render_cam.targetTexture = texture;
            render_cam.orthographicSize = capture_height / 2f;

            List<CardData> cards = CardData.GetAll();
            for (int i = 0; i < cards.Count; i++)
            {
                CardData card = cards[i];
                if (card.deckbuilding && card.IsBoardCard())
                {
                    ShowText("Exporting: " + card.id);
                    GenerateCard(card);
                    await TimeTool.Delay(1);
                    ExportCard(card);
                    await TimeTool.Delay(2);
                }
            }
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            ShowText("Completed!");
        }

        private void GenerateCard(CardData card)
        {
            card_ui.SetCard(card, variant);

            //Match the in-game board look, BoardCard.Update applies these while playing
            card_ui.frame_image.sprite = board_card.ally_frame;
            card_ui.frame_image.color = Color.white;
            card_ui.attack_background.sprite = board_card.ally_attack_bg;
            card_ui.hp_background.sprite = board_card.ally_hp_bg;
            card_ui.attack.color = font_color;
            card_ui.hp.color = font_color;

            board_card.card_sprite.sprite = card.GetBoardArt(variant);

            render_cam.Render();
        }

        private void ExportCard(CardData card)
        {
            RenderTexture.active = texture;
            export_texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            byte[] bytes = export_texture.EncodeToPNG();
            export_path = Application.dataPath + "/TcgEngine/Resources/BoardCardImages";
            Directory.CreateDirectory(export_path);
            string file = card.id + ".png";
            File.WriteAllBytes(export_path + "/" + file, bytes);
            RenderTexture.active = null;
        }

        private void ShowText(string txt)
        {
            Debug.Log(txt);
        }
    }
}
