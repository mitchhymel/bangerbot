using DSharpPlus.EventArgs;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace dampbot
{
    public class SpotifyHelper
    {
        public SpotifyWebAPI API;


        public SpotifyHelper()
        {
        }

        public async Task<SpotifyWebAPI> Login()
        {
            var tcs = new TaskCompletionSource<SpotifyWebAPI>();
            LoginToSpotify(tcs);
            return await tcs.Task;
        }

        private void LoginToSpotify(TaskCompletionSource<SpotifyWebAPI> tcs)
        {
            AuthorizationCodeAuth auth = new AuthorizationCodeAuth(Secrets.SPOTIFY_CLIENT_ID, Secrets.SPOTIFY_SECRET, "http://localhost:8000", "http://localhost:8000",
               SpotifyAPI.Web.Enums.Scope.UserLibraryRead | SpotifyAPI.Web.Enums.Scope.PlaylistModifyPublic);

            auth.AuthReceived += async (sender, payload) =>
            {
                AuthorizationCodeAuth auth = (AuthorizationCodeAuth)sender;
                auth.Stop();

                Token token = await auth.ExchangeCode(payload.Code);
                API = new SpotifyWebAPI
                {
                    AccessToken = token.AccessToken,
                    TokenType = token.TokenType
                };

                if (API == null)
                {
                    Console.WriteLine("Spotify was null");
                }
                else
                {
                    Console.WriteLine("Logged in successfully");
                }

                tcs.SetResult(API);
            };

            try
            {
                auth.Start();
                auth.OpenBrowser();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task AddTracksToSpotifyPlaylist(MessageCreateEventArgs e, string playlistId, List<string> trackIds)
        {
            string message = await AddTracksToSpotifyPlaylist(playlistId, trackIds);
            await e.Message.RespondAsync(message);
        }

        public async Task<string> AddTracksToSpotifyPlaylist(string playlistId, List<string> trackIds)
        {
            if (API == null)
            {
                return "OOF, this is awkward, I can't talk to Spotify right now :grimacing:";
            }

            FullPlaylist playlist = await API.GetPlaylistAsync(playlistId);
            if (playlist == null)
            {
                return "I tried real hard but can't find the playlist :grimacing:";
            }

            // get all trackIds of songs in playlist to filter out dupes
            List<PlaylistTrack> playlistTracks = await GetAllTracksInPlaylist(playlist.Id);
            List<string> strPlaylistTrackIds = playlistTracks.ConvertAll(x => x.Track.Id);
            trackIds.RemoveAll(x => strPlaylistTrackIds.Contains(x));

            List<FullTrack> tracks = new List<FullTrack>();
            foreach (string trackId in trackIds)
            {
                FullTrack track = await API.GetTrackAsync(trackId);
                tracks.Add(track);
            }

            List<string> trackUris = tracks.ConvertAll(x => x.Uri);
            var response = await API.AddPlaylistTracksAsync(playlistId, trackUris);
            if (response.HasError())
            {
                return $"Something fucky happened: {response.Error.Message}";
            }
            else
            {
                return "Banger Certification Testing begins!";
            }
        }

        private async Task<List<PlaylistTrack>> GetAllTracksInPlaylist(string id)
        {
            List<PlaylistTrack> playlistTracks = new List<PlaylistTrack>();
            Paging<PlaylistTrack> resp = await API.GetPlaylistTracksAsync(id);
            resp.Items.ForEach(t => playlistTracks.Add(t));
            while (resp.HasNextPage())
            {
                resp = await API.GetPlaylistTracksAsync(id, offset: resp.Offset + 1);
                resp.Items.ForEach(t => playlistTracks.Add(t));
            }

            return playlistTracks;
        }

        public bool MessageContainsSpotifyTrack(string message)
        {
            return message.Contains("https://open.spotify.com/track/");
        }

        public List<string> GetTrackIdsFromMessage(string message)
        {
            // Parse track id
            string trackIdPatternKey = "TRACK_ID";
            string pattern = $@"https:\/\/open\.spotify\.com\/track\/(?<{trackIdPatternKey}>[^?]+)";
            Regex regex = new Regex(pattern);
            MatchCollection matches = regex.Matches(message);
            List<string> tracksToAdd = new List<string>();
            foreach (Match match in matches)
            {
                string trackId = match.Groups[trackIdPatternKey].Value;
                tracksToAdd.Add(trackId);
            }

            return tracksToAdd;
        }

        public async Task ClearSpotifyPlaylist(string playlistId)
        {
            var emptyList = new List<string>();
            var response =  await API.ReplacePlaylistTracksAsync(playlistId, emptyList);
            if (response.HasError())
            {
                Console.WriteLine($"Error while clearing playlist: {response.Error}");
            }
        }
    }
}
