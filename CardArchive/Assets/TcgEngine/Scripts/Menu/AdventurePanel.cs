using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// Four-tab adventure menu. Tabs (Tutorial / Adventure / Story / TotalAssault)
    /// are pre-placed; sub-entries are spawned dynamically into a horizontal
    /// ScrollRect when a tab is selected. Visible entries play a staggered
    /// slide+fade intro (DOTween); off-viewport entries snap to final state.
    /// Tab switch mid-animation cancels and restarts from the new category.
    /// </summary>
    public class AdventurePanel : UIPanel
    {
        [Header("Tabs")]
        public Button tutorial_tab;
        public Button adventure_tab;
        public Button story_tab;
        public Button assault_tab;

        [Header("ScrollView")]
        public ScrollRect scroll;
        public RectTransform content;
        public LevelUI entry_prefab;

        [Header("Intro Animation")]
        public float intro_offset_x = -40f;
        public float intro_duration = 0.22f;
        public float intro_stagger = 0.06f;
        public int intro_max_animated_count = 6;
        public Ease intro_ease = Ease.OutCubic;

        private AdventureCategory current_category;
        private readonly List<LevelUI> active_entries = new List<LevelUI>();
        private int intro_token = 0;
        private Tween unlock_call;

        private static AdventurePanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Start()
        {
            base.Start();

            if (tutorial_tab != null)
                tutorial_tab.onClick.AddListener(() => SelectCategory(AdventureCategory.Tutorial));
            if (adventure_tab != null)
                adventure_tab.onClick.AddListener(() => SelectCategory(AdventureCategory.Adventure));
            if (story_tab != null)
            {
                story_tab.interactable = false; //StoryData not implemented yet
                story_tab.onClick.AddListener(() => SelectCategory(AdventureCategory.Story));
            }
            if (assault_tab != null)
                assault_tab.onClick.AddListener(() => SelectCategory(AdventureCategory.TotalAssault));
        }

        public override void Show(bool instant = false)
        {
            base.Show(instant);
            SelectCategory(AdventureCategory.Tutorial);
        }

        public void SelectCategory(AdventureCategory cat)
        {
            //Bump token so any in-flight unlock call from a previous run is ignored
            intro_token++;
            int this_token = intro_token;

            //Cancel pending scroll-unlock from previous run
            if (unlock_call != null && unlock_call.IsActive())
                unlock_call.Kill();

            //Lock scroll for the duration of the new intro
            if (scroll != null)
                scroll.horizontal = false;

            //Tear down previous entries (no pooling)
            for (int i = 0; i < active_entries.Count; i++)
            {
                LevelUI entry = active_entries[i];
                if (entry == null)
                    continue;
                //Deactivate first so LayoutGroup ignores them this frame
                entry.gameObject.SetActive(false);
                Destroy(entry.gameObject);
            }
            active_entries.Clear();

            current_category = cat;

            //Spawn fresh entries
            List<IGameTypeView> views = GetEntries(cat);
            int animated_count = Mathf.Min(views.Count, intro_max_animated_count);

            for (int i = 0; i < views.Count; i++)
            {
                LevelUI entry = Instantiate(entry_prefab, content);
                entry.SetData(views[i]);
                active_entries.Add(entry);

                if (i < animated_count)
                {
                    entry.ResetIntroState(intro_offset_x);
                    entry.PlayIntro(intro_duration, i * intro_stagger, intro_ease);
                }
                else
                {
                    entry.SetFinalState();
                }
            }

            //Schedule scroll unlock when the last animated entry finishes
            float total_intro_time = animated_count > 0
                ? (animated_count - 1) * intro_stagger + intro_duration
                : 0f;

            unlock_call = DOVirtual.DelayedCall(total_intro_time, () =>
            {
                if (this_token != intro_token)
                    return; //A newer SelectCategory invalidated us
                if (scroll != null)
                    scroll.horizontal = true;
            });
        }

        private List<IGameTypeView> GetEntries(AdventureCategory cat)
        {
            List<IGameTypeView> list = new List<IGameTypeView>();
            switch (cat)
            {
                case AdventureCategory.Tutorial:
                    foreach (TutorialData t in TutorialData.GetAll())
                        list.Add(t);
                    break;
                case AdventureCategory.Adventure:
                    foreach (LevelData l in LevelData.GetAll())
                        list.Add(l);
                    break;
                case AdventureCategory.Story:
                    //StoryData not implemented yet
                    break;
                case AdventureCategory.TotalAssault:
                    foreach (TotalAssaultData a in TotalAssaultData.GetAll())
                        list.Add(a);
                    break;
            }
            return list;
        }

        public AdventureCategory GetCurrentCategory()
        {
            return current_category;
        }

        public static AdventurePanel Get()
        {
            return instance;
        }
    }
}
