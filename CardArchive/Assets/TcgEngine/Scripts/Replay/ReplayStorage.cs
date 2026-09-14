using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TcgEngine.Replay
{
    public static class ReplayStorage
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Replays");
        public static string Encode(ReplayRecord record)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(record));
                    gzip.Write(bytes, 0, bytes.Length);
                }
                return Convert.ToBase64String(output.ToArray());
            }
        }
        public static ReplayRecord Decode(string payload)
        {
            using (var input = new MemoryStream(Convert.FromBase64String(payload)))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int n;
                while ((n = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + n > 128 * 1024 * 1024) throw new InvalidDataException("Replay too large");
                    output.Write(buffer, 0, n);
                }
                var record = JsonUtility.FromJson<ReplayRecord>(Encoding.UTF8.GetString(output.ToArray()));
                if (record == null || record.formatVersion != 1 || record.initialState == null || record.events == null)
                    throw new InvalidDataException("Unsupported replay format");
                return record;
            }
        }
        public static async void Save(ReplayRecord record, bool upload)
        {
            if (record == null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                var request = new ReplayUpload { matchId = record.matchId, players = record.players, payload = Encode(record) };
                // Durable completed-match outbox. Failed uploads can be retried from the tool.
                string name = BitConverter.ToString(System.Security.Cryptography.SHA256.Create()
                    .ComputeHash(Encoding.UTF8.GetBytes(record.matchId))).Replace("-", "");
                string path = Path.Combine(Folder, name + ".json");
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(request));
                if (File.Exists(path)) File.Delete(path);
                File.Move(path + ".tmp", path);
                if (upload)
                {
                    for (int attempt = 0; attempt < 3; attempt++)
                    {
                        if (await Upload(request)) { File.Delete(path); return; }
                        await Task.Delay(1000 * (attempt + 1));
                    }
                    Debug.LogWarning("Replay upload pending: " + path);
                }
            }
            catch (Exception e) { Debug.LogError("Replay save failed: " + e); }
        }
        public static async Task<bool> Upload(ReplayUpload request)
        {
            var response = await ApiClient.Get().SendPostRequest(ApiClient.ServerURL + "/replays", JsonUtility.ToJson(request));
            return response.success;
        }
    }
}
