
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WindowsShuffle
{
    public class MusicBrainzService
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Podmix/1.0 (https://github.com/ypd11/podmixamusic)");

            return client;
        }

        public async Task<JArray> SearchAlbumReleasesAsync(
            string artist,
            string title)
        {
            JArray results = new JArray();

            if (string.IsNullOrWhiteSpace(artist) ||
                string.IsNullOrWhiteSpace(title))
            {
                return results;
            }

            string query =
                "recording:\"" + EscapeLucenePhrase(title) +
                "\" AND artist:\"" + EscapeLucenePhrase(artist) + "\"";

            string searchUrl =
                "https://musicbrainz.org/ws/2/recording/?query=" +
                Uri.EscapeDataString(query) +
                "&fmt=json&limit=10";

            using (HttpResponseMessage response =
                await Client.GetAsync(searchUrl))
            {
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                JObject data = JObject.Parse(json);

                JArray recordings =
                    data["recordings"] as JArray ?? new JArray();

                HashSet<string> addedReleaseIds =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                int lookupCount = Math.Min(recordings.Count, 8);

                for (int i = 0; i < lookupCount; i++)
                {
                    JObject recording = recordings[i] as JObject;

                    if (recording == null || recording["id"] == null)
                        continue;

                    string recordingId = recording["id"].ToString();

                    string recordingTitle =
                        recording["title"] != null
                            ? recording["title"].ToString()
                            : title;

                    // Respect MusicBrainz rate limits.
                    await Task.Delay(1100);

                    string lookupUrl =
                        "https://musicbrainz.org/ws/2/recording/" +
                        Uri.EscapeDataString(recordingId) +
                        "?inc=releases&fmt=json";

                    using (HttpResponseMessage lookupResponse =
                        await Client.GetAsync(lookupUrl))
                    {
                        lookupResponse.EnsureSuccessStatusCode();

                        string lookupJson =
                            await lookupResponse.Content.ReadAsStringAsync();

                        JObject recordingDetails =
                            JObject.Parse(lookupJson);

                        JArray releases =
                            recordingDetails["releases"] as JArray;

                        if (releases == null)
                            continue;

                        foreach (JObject release in releases)
                        {
                            if (release["id"] == null)
                                continue;

                            string releaseId = release["id"].ToString();

                            if (addedReleaseIds.Contains(releaseId))
                                continue;

                            addedReleaseIds.Add(releaseId);

                            JObject releaseCopy =
                                (JObject)release.DeepClone();

                            releaseCopy["matched-recording-title"] =
                                recordingTitle;

                            results.Add(releaseCopy);
                        }
                    }
                }
            }

            return results;
        }


        public async Task<JObject> GetReleaseDetailsAsync(string releaseId)
        {
            if (string.IsNullOrWhiteSpace(releaseId))
                return null;

            string url =
                "https://musicbrainz.org/ws/2/release/" +
                Uri.EscapeDataString(releaseId) +
                "?inc=recordings+artists&fmt=json";

            using (HttpResponseMessage response = await Client.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                return JObject.Parse(json);
            }
        }


        private static string EscapeLucenePhrase(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}
