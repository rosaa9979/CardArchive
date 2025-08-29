using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

namespace TcgEngine.UI
{
    public class AronaSelection : MonoBehaviour
    {
        [SerializeField] private GameObject arona_prefab;
        private List<IconButton> arona_list = new List<IconButton>();
        private List<string> all_arona_list = new List<string>();

        void Start()
        {
            foreach (CardData arona in CardData.GetAll())
            {
                if (!arona.IsArona())
                    continue;

                GameObject arona_object = Instantiate(arona_prefab);
                arona_object.transform.SetParent(this.transform, false);
                IconButton arona_icon = arona_object.GetComponent<IconButton>();

                if (arona_icon != null)
                {
                    arona_icon.SetValue(arona.id);
                    arona_icon.onClick += OnClickArona;
                    arona_list.Add(arona_icon);
                    all_arona_list.Add(arona_icon.value);
                }
            }
        }

        public void OnClickArona(IconButton button)
        {
            CollectionPanel.Get().SetDeckHero(null);

            foreach (string arona_id in all_arona_list)
            {
                if (arona_id == button.value)
                    CollectionPanel.Get().SetDeckHero(button.value);
            }
        }

        public void Refresh(UserCardData hero_info)
        {
            foreach (IconButton icon in arona_list)
            {
                icon.Deactivate();
            }

            foreach (IconButton icon in arona_list)
            {
                if (hero_info != null && hero_info.tid == icon.value)
                    icon.Activate();
            }
        }
    }
}
