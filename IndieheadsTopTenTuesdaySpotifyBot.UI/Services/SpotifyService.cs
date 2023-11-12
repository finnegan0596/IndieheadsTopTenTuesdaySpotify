using ExcelDataReader;
using IndieheadsTopTenTuesdaySpotifyBot.UI.Config;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IndieheadsTopTenTuesdaySpotifyBot.UI.Services
{
    // TODO refactor for proper separation of concerns. this is currently a god class
    public interface ISpotifyService
    {
        Task UpdatePlaylistAsync(string accessToken, string playlistId);
    }

    public class SpotifyService : ISpotifyService
    {
        private readonly HttpClient _client;
        private readonly IOptions<SpotifyConfig> _spotifyConfig;

        public SpotifyService(HttpClient client)
        {
            _client = client;
        }

        public async Task UpdatePlaylistAsync(string accessToken, string playlistId)
        {
            var spreadsheetSongs = ReadResultsFromExcelFile(_spotifyConfig.Value.LogFileLocation);
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var playlist = await GetPlaylistSongsAsync(playlistId);
            foreach (var spreadsheetSong in spreadsheetSongs)
            {
                if (!IsSongInPlaylist(playlist, spreadsheetSong.Artist, spreadsheetSong.Song))
                {
                    var uri = await SearchSongAsync(spreadsheetSong.Artist, spreadsheetSong.Album, spreadsheetSong.Song);
                    if (uri != null) // TODO we need to search the song again with the returned song info
                        await AddSongToPlaylistAsync(playlistId, uri);
                    else
                        using (StreamWriter writer = new StreamWriter("problemSongs.txt", true))
                        {
                            writer.WriteLine($"Artist: {spreadsheetSong.Artist}, Title: {spreadsheetSong.Song}, Album: {spreadsheetSong.Album}");
                        }
                }
            }
        }

        private async Task<string> SearchSongAsync(string artist, string album, string song)
        {
            //artist = ReplaceWordsWithApostrophe(artist);
            //album = ReplaceWordsWithApostrophe(album);
            song = ReplaceWordsWithApostrophe(song);
            string query = Uri.EscapeDataString($"artist:{artist} album:{album} track:{song}");
            string url = $"https://api.spotify.com/v1/search?q={query}&type=track";

            HttpResponseMessage response = await _client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string content = await response.Content.ReadAsStringAsync();
            JObject json = JObject.Parse(content);

            if (!json["tracks"]["items"].Any())
            {
                //query = $"artist:{artist} track:{song}";
                query = Uri.EscapeDataString($"artist:{artist} track:{song}");
                url = $"https://api.spotify.com/v1/search?q={query}&type=track";

                response = await _client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                content = await response.Content.ReadAsStringAsync();
                json = JObject.Parse(content);
            }


            if (!json["tracks"]["items"].Any())
                return null;

            string songUri = json["tracks"]["items"][0]?["uri"]?.ToString();
            return songUri;

        }

        private async Task AddSongToPlaylistAsync(string playlistId, string songUri)
        {
            string url = $"https://api.spotify.com/v1/playlists/{playlistId}/tracks?uris={songUri}";
            HttpResponseMessage response = await _client.PostAsync(url, null);
            response.EnsureSuccessStatusCode();
        }

        private bool IsSongInPlaylist(JArray playlist, string song, string artist)
        {
            foreach (var item in playlist)
            {
                JToken track = item["track"];
                string trackName = RemoveSpecialCharacters(track["name"].ToString());
                string trackArtist = RemoveSpecialCharacters(track["artists"][0]["name"].ToString());

                if (trackName.Contains(song, StringComparison.OrdinalIgnoreCase) &&
                    trackArtist.Contains(artist, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;

        }

        private async Task<JArray> GetPlaylistSongsAsync(string playlistId)
        {
            string url = $"https://api.spotify.com/v1/playlists/{playlistId}/tracks";
            JArray allTracks = new JArray();

            while (!string.IsNullOrEmpty(url))
            {
                HttpResponseMessage response = await _client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string content = await response.Content.ReadAsStringAsync();
                JObject json = JObject.Parse(content);
                JArray tracks = (JArray)json["items"];

                allTracks.Merge(tracks);

                url = url = json["next"]?.ToString();
            }

            return allTracks;
        }

        private ICollection<dynamic> ReadResultsFromExcelFile(string fileLocation, string sheetName = null)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var songs = new List<dynamic>();
            using (var stream = File.Open(fileLocation, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    do
                    {
                        if (sheetName == null || reader.Name == sheetName)
                        {
                            bool isBlank = true;
                            bool isNewArtist = true;
                            string artistName = string.Empty;

                            while (reader.Read()) // Iterating through rows of the current sheet
                            {
                                isBlank = String.IsNullOrWhiteSpace(reader.GetValue(0)?.ToString());

                                if (!isBlank && isNewArtist)
                                {
                                    artistName = reader.GetValue(0)?.ToString();
                                    isNewArtist = false;
                                    reader.Read(); // skip next line containing headers
                                }
                                else if (!isBlank && !isNewArtist)
                                {
                                    var songTitle = reader.GetValue(0)?.ToString();
                                    var albumTitle = reader.GetValue(2)?.ToString();
                                    songs.Add(new { Song = songTitle, Artist = artistName, Album = albumTitle });
                                }
                                else if (isBlank)
                                {
                                    isNewArtist = true;
                                }
                                // Process each row here
                            }
                        }
                    } while (reader.NextResult()); // Move to the next sheet
                }
            }
            return songs;
        }

        private string RemoveSpecialCharacters(string str)
        {
            return Regex.Replace(str, "[^a-zA-Z0-9]", "");
        }

        private string ReplaceWordsWithApostrophe(string input)
        {
            string[] words = input.Split(' ');

            if (words.Length > 1 && words[0].Contains("'"))
            {
                return String.Join(" ", words, 1, words.Length - 1);
            }

            return input;
        }

    }
}