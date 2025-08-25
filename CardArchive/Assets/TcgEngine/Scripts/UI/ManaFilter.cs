using System.Collections.Generic;
using System.Linq;
using TcgEngine.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    public class ManaFilter : MonoBehaviour
    {
        public List<Mana> mana_list; // Index = 마나값 (0 ~ 10)
        public UnityAction onManaClicked;
        private HashSet<int> filteredManaValues;
        private bool isFiltering;

        void Start()
        {
            filteredManaValues = new HashSet<int>();

            foreach (Mana mana in mana_list)
            {
                mana.SetActive(true);
                filteredManaValues.Add(mana.GetValue());
            }

            isFiltering = false;
        }

        public void OnClickedMana(int mana)
        {
            if (isFiltering)
            {
                if (filteredManaValues.Contains(mana))
                    filteredManaValues.Remove(mana);
                else
                    filteredManaValues.Add(mana);
            }

            else
            {
                filteredManaValues.Clear();
                filteredManaValues.Add(mana);
                isFiltering = true;
            }


            if (filteredManaValues.Count == 0)
                Reset();
            else if (filteredManaValues.Count == mana_list.Count)
            {
                Reset();
                isFiltering = false;
            }
            else
            {
                foreach (Mana m in mana_list)
                {
                    if (filteredManaValues.Contains(m.GetValue()))
                        m.SetActive(true);
                    else
                        m.SetActive(false);
                }
            }


            onManaClicked?.Invoke();
        }

        public void Reset()
        {
            filteredManaValues.Clear();

            foreach (Mana mana in mana_list)
            {
                mana.SetActive(true);
                filteredManaValues.Add(mana.GetValue());
            }

            isFiltering = false;
        }

        public HashSet<int> GetFilteredMana()
        {
            return filteredManaValues;
        }
    }
}