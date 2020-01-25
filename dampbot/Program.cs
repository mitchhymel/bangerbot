using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace dampbot
{
    public class Program
    {
        static SpotifyWebAPI Spotify;

        static DiscordClient Discord = new DiscordClient(new DiscordConfiguration
        {
            Token = Secrets.TOKEN,
            TokenType = TokenType.Bot
        });

        public static async Task Main(string[] args)
        {
            SetUpSpotify();

            await SetUpDiscord();

            // Spin forever
            await Task.Delay(-1);
        }

        private static void SetUpSpotify()
        {
             LoginToSpotify();
        }

        private static void LoginToSpotify()
        {
            AuthorizationCodeAuth auth = new AuthorizationCodeAuth(Secrets.SPOTIFY_CLIENT_ID, Secrets.SPOTIFY_SECRET, "http://localhost:8000", "http://localhost:8000",
               SpotifyAPI.Web.Enums.Scope.UserLibraryRead | SpotifyAPI.Web.Enums.Scope.PlaylistModifyPublic);

            auth.AuthReceived += OnSpotifyAuthReceived;

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

        private static async void OnSpotifyAuthReceived(object sender, AuthorizationCode payload)
        {
            AuthorizationCodeAuth auth = (AuthorizationCodeAuth)sender;
            auth.Stop();

            Token token = await auth.ExchangeCode(payload.Code);
            Spotify = new SpotifyWebAPI
            {
                AccessToken = token.AccessToken,
                TokenType = token.TokenType
            };

            if (Spotify == null)
            {
                Console.WriteLine("Spotify was null");
            }
            else
            {
                Console.WriteLine("Logged in successfully");
            }
        }

        public static async Task SetUpDiscord()
        {
            // Set up events
            Discord.MessageCreated += OnMessageCreated;

            // Connect
            await Discord.ConnectAsync();
        }

        private static async Task OnMessageCreated(MessageCreateEventArgs e)
        {
            if (e.Message.ChannelId != Secrets.BANGER_CHANNEL_ID && e.Message.ChannelId != Secrets.TEST_BANGER_CHANNEL_ID)
            {
                return;
            }

            bool bangerBotMentioned = false;
            foreach (var user in e.Message.MentionedUsers)
            {
                if (user.Id == Discord.CurrentUser.Id)
                {
                    bangerBotMentioned = true;
                    break;
                }
            }

            bool messageContainsSpotifyTrack = e.Message.Content.Contains("https://open.spotify.com/track/");
            if (messageContainsSpotifyTrack)
            {
                // Parse track id
                string trackIdPatternKey = "TRACK_ID";
                string pattern = $@"https:\/\/open\.spotify\.com\/track\/(?<{trackIdPatternKey}>[^?]+)";
                Regex regex = new Regex(pattern);
                MatchCollection matches = regex.Matches(e.Message.Content);
                List<string> tracksToAdd = new List<string>();
                foreach (Match match in matches)
                {
                    string trackId = match.Groups[trackIdPatternKey].Value;
                    tracksToAdd.Add(trackId);
                }

                await AddTracksToSpotifyPlaylist(e, tracksToAdd);
            }
            else if (bangerBotMentioned)
            {

                DiscordEmoji certified = DiscordEmoji.FromName(Discord, ":certified:");
                DiscordEmoji notCertified = DiscordEmoji.FromName(Discord, ":notcertified:");

                var messages = await GetAllDiscordMessages(e);
                Dictionary<string, CustomDiscordUser> map = new Dictionary<string, CustomDiscordUser>();
                foreach (DiscordMessage message in messages)
                {
                    if (message.Reactions.Count > 0)
                    {
                        int certifiedCount = 0;
                        int notCertifiedCount = 0;
                        foreach (DiscordReaction reaction in message.Reactions)
                        {
                            if (reaction.Emoji == certified)
                            {
                                certifiedCount += reaction.Count;
                            }
                            else if (reaction.Emoji == notCertified)
                            {
                                notCertifiedCount += reaction.Count;
                            }
                        }

                        DiscordUser author = message.Author;
                        CustomDiscordUser custom;
                        if (map.ContainsKey(author.Username))
                        {
                            custom = map[author.Username];
                        }
                        else
                        {
                            custom = new CustomDiscordUser(author);
                        }

                        custom.Certified += certifiedCount;
                        custom.NotCertified += notCertifiedCount;
                        map[author.Username] = custom;
                    }
                }

                if (map.Keys.Count > 0)
                {
                    string result = GetBangerClangerString(map);
                    await e.Message.RespondAsync(result);
                }
                else
                {
                    await e.Message.RespondAsync(":grimace: No bangers found");
                }
            }
        }

        private static async Task<List<DiscordMessage>> GetAllDiscordMessages(MessageCreateEventArgs e)
        {
            List<DiscordMessage> result = new List<DiscordMessage>();
            var channel = await Discord.GetChannelAsync(e.Message.ChannelId);

            bool keepLooping = true;
            ulong? before = null;
            while (keepLooping)
            {
                var messages = await channel.GetMessagesAsync(before: before);
                if (messages.Count == 0)
                {
                    keepLooping = false;
                    break;
                }

                result.AddRange(messages);
                if (messages.Count < 100)
                {
                    keepLooping = false;
                    break;
                }
                else
                {
                    before = messages[messages.Count - 1].Id;
                }
            }

            return result;
        }

        private static async Task AddTracksToSpotifyPlaylist(MessageCreateEventArgs e, List<string> trackIds)
        {
            if (Spotify == null)
            {
                await e.Message.RespondAsync("OOF, this is awkward, I can't talk to Spotify right now :grimacing:");
                return;
            }

            FullPlaylist playlist = await Spotify.GetPlaylistAsync(Secrets.SPOTIFY_PLAYLIST_ID);
            if (playlist == null)
            {
                await e.Message.RespondAsync("I tried real hard but can't find the playlist :grimacing:");
                return;
            }

            List<FullTrack> tracks = new List<FullTrack>();
            foreach (string trackId in trackIds)
            {
                FullTrack track = await Spotify.GetTrackAsync(trackId);
                tracks.Add(track);
            }

            List<string> trackUris = tracks.ConvertAll(x => x.Uri);
            var response = await Spotify.AddPlaylistTracksAsync(Secrets.SPOTIFY_PLAYLIST_ID, trackUris);
            if (response.HasError())
            {
                await e.Message.RespondAsync($"Something fucky happened: {response.Error.Message}");
                return;
            }
            else
            {
                await e.Message.RespondAsync("Banger Certification Testing begins!");
                return;
            }
        }

        private static string GetBangerClangerString(Dictionary<string, CustomDiscordUser> map)
        {
            string lineBreak = "+-----------------------------+\n";
            string result = "";

            result += "+-----BANGERS---RANKINGS-----+\n";
            List<CustomDiscordUser> certifiedSorted = new List<CustomDiscordUser>(map.Values);
            certifiedSorted.Sort((a, b) => b.Certified.CompareTo(a.Certified));
            foreach (CustomDiscordUser user in certifiedSorted)
            {
                result += $"{user.User.Username}\t{user.Certified}\n";
            }
            result += lineBreak;


            result += "+-----CLANGERS--RANKINGS-----+\n";
            List<CustomDiscordUser> notCertifiedSorted = new List<CustomDiscordUser>(map.Values);
            notCertifiedSorted.Sort((a, b) => b.NotCertified.CompareTo(a.NotCertified));
            foreach (CustomDiscordUser user in notCertifiedSorted)
            {
                result += $"{user.User.Username}\t{user.NotCertified}\n";
            }
            result += lineBreak;

            return result;
        }

        public class CustomDiscordUser
        {
            public DiscordUser User { get; set; }
            public int Certified { get; set; }
            public int NotCertified { get; set; }

            public CustomDiscordUser(DiscordUser user)
            {
                User = user;
            }
        }
    }
}
