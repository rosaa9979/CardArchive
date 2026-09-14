using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace TcgEngine.Replay
{
    public class ReplayTool : MonoBehaviour
    {
        string username = "";
        string url = "";
        string configuredUrl = "";
        string status = "Select a user, then a completed recording.";
        string[] ids = Array.Empty<string>();
        string[] local = Array.Empty<string>();
        Vector2 scroll;
        bool busy;
        void Start()
        {
            if (TcgNetwork.Get() != null && TcgNetwork.Get().IsActive()) TcgNetwork.Get().Disconnect();
            Time.timeScale = 1;
            var data = Resources.LoadAll<NetworkData>("");
            if (data.Length > 0) url = (data[0].api_https ? "https://" : "http://") + data[0].api_url;
            configuredUrl = url;
            var api = ApiClient.Get();
            if (api != null) username = api.Username;
            RefreshLocal();
        }
        void RefreshLocal() => local = Directory.Exists(ReplayStorage.Folder)
            ? Directory.GetFiles(ReplayStorage.Folder, "*.json") : Array.Empty<string>();
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(850, Screen.width - 40), Screen.height - 40), GUI.skin.box);
            GUILayout.Label("Card Archive — Replay Tool");
            GUILayout.Label("Viewpoint username (required)");
            username = GUILayout.TextField(username);
            GUILayout.Label("API URL");
            url = GUILayout.TextField(url);
            GUILayout.Label("Enter a viewpoint username to load recordings. No login required.");
            GUI.enabled = !busy && !string.IsNullOrWhiteSpace(username);
            if (GUILayout.Button("Load user's latest recordings")) StartCoroutine(Fetch("/replays/user/" + Uri.EscapeDataString(username), false));
            GUI.enabled = !busy;
            GUILayout.Label(status);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (string id in ids)
                if (GUILayout.Button("DB: " + id)) StartCoroutine(Fetch("/replays/" + Uri.EscapeDataString(id), true));
            GUILayout.Space(12);
            GUILayout.Label("Local completed recordings / pending server uploads");
            if (GUILayout.Button("Refresh local files")) RefreshLocal();
            foreach (string path in local)
            {
                ReplayUpload upload;
                try { upload = JsonUtility.FromJson<ReplayUpload>(File.ReadAllText(path)); }
                catch { continue; }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(upload.matchId + " — " + string.Join(" / ", upload.players))) Open(upload);
                if (GUILayout.Button("Upload", GUILayout.Width(80))) StartCoroutine(Push(upload));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUI.enabled = true;
            GUILayout.EndArea();
        }
        IEnumerator Push(ReplayUpload upload)
        {
            busy = true;
            using (var request = new UnityWebRequest(url.TrimEnd('/') + "/replays", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(upload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                Authorize(request);
                yield return request.SendWebRequest();
                status = request.result == UnityWebRequest.Result.Success ? "Uploaded." : request.downloadHandler.text;
            }
            busy = false;
        }
        IEnumerator Fetch(string path, bool open)
        {
            busy = true;
            using (var request = UnityWebRequest.Get(url.TrimEnd('/') + path))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) status = request.responseCode + ": " + request.downloadHandler.text;
                else if (open) Open(JsonUtility.FromJson<ReplayUpload>(request.downloadHandler.text));
                else
                {
                    ids = JsonUtility.FromJson<ReplayList>(request.downloadHandler.text).ids ?? Array.Empty<string>();
                    status = ids.Length + " recordings";
                }
            }
            busy = false;
        }
        void Open(ReplayUpload upload)
        {
            try { ReplaySession.Open(ReplayStorage.Decode(upload.payload), username); }
            catch (Exception e) { status = e.Message; }
        }
        void Authorize(UnityWebRequest request)
        {
#if UNITY_EDITOR
            request.SetRequestHeader("X-CardArchive-Replay-Tool", "editor");
#endif
            // Only send the existing login to the configured API, never an arbitrary edited URL.
            if (!string.Equals(url.TrimEnd('/'), configuredUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return;
            var api = ApiClient.Get();
            string accessToken = api != null ? api.AccessToken : PlayerPrefs.GetString("tcg_access_token", "");
            if (!string.IsNullOrEmpty(accessToken)) request.SetRequestHeader("Authorization", accessToken);
        }
    }
}
