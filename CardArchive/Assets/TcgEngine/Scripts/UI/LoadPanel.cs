using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// Global loading panel that blocks input on top of every scene while something is pending
    /// (connecting to a match, waiting for the other player, checking decks...)
    /// Created from Resources on first use and kept between scenes.
    /// Callers request it with a key: it stays visible while any key is still requested,
    /// so one caller hiding its own request never releases another one's block.
    /// </summary>

    public class LoadPanel : UIPanel
    {
        public Text load_txt;

        private const string prefab_path = "UI/LoadPanel";

        private static LoadPanel instance;

        //Active requests in request order, the latest one's text is displayed
        private static readonly List<KeyValuePair<string, string>> requests = new List<KeyValuePair<string, string>>();

        protected override void Awake()
        {
            base.Awake();

            //Get() only creates the panel when none exists, an extra one means it was placed in a scene
            if (instance != null && instance != this)
            {
                Debug.LogWarning("LoadPanel already exists, remove the one placed in " + gameObject.scene.name);
                return;
            }

            instance = this;
            if (load_txt != null)
                load_txt.text = "";
        }

        protected override void Start()
        {
            base.Start();
            if (instance == this)
                Refresh();
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        //Add or update the request for this key, the text is optional
        public static void Show(string key, string text = "")
        {
            if (string.IsNullOrEmpty(key))
                return;

            int index = requests.FindIndex(r => r.Key == key);
            if (index >= 0)
            {
                if (requests[index].Value == text)
                    return; //Nothing changed, safe to call every frame
                requests[index] = new KeyValuePair<string, string>(key, text);
            }
            else
            {
                requests.Add(new KeyValuePair<string, string>(key, text));
            }

            Get()?.Refresh();
        }

        //Release the request for this key, the panel hides once no request is left
        public static void Hide(string key)
        {
            int removed = requests.RemoveAll(r => r.Key == key);
            if (removed > 0)
                instance?.Refresh(); //Never create the panel just to hide it
        }

        public static bool IsRequested(string key)
        {
            return requests.Exists(r => r.Key == key);
        }

        public static bool IsBlocking()
        {
            return requests.Count > 0;
        }

        private void Refresh()
        {
            if (requests.Count > 0)
            {
                if (load_txt != null)
                    load_txt.text = requests[requests.Count - 1].Value;
                if (!IsVisible())
                    Show(true); //Block input right away
            }
            else if (IsVisible())
            {
                Hide(); //Fade out
            }
        }

        public static LoadPanel Get()
        {
            if (instance == null && !Application.isBatchMode && Application.isPlaying)
            {
                GameObject prefab = Resources.Load<GameObject>(prefab_path);
                if (prefab == null)
                {
                    Debug.LogError("LoadPanel prefab not found in Resources: " + prefab_path);
                    return null;
                }

                GameObject obj = Instantiate(prefab);
                obj.name = prefab.name;
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }
}
