using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace dampbot
{
    public class Program
    {
        static DiscordClient Discord = new DiscordClient(new DiscordConfiguration
        {
            Token = Secrets.TOKEN,
            TokenType = TokenType.Bot
        });

        public static async Task Main(string[] args)
        {

            // Set up events
            Discord.MessageCreated += OnMessageCreated;

            // Connect
            await Discord.ConnectAsync();

            // Spin forever
            await Task.Delay(-1);
        }

        private static async Task OnMessageCreated(MessageCreateEventArgs e)
        {
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
                foreach (Match match in matches)
                {
                    string trackId = match.Groups[trackIdPatternKey].Value;
                    await e.Message.RespondAsync(trackId);
                }
            }
            else if (bangerBotMentioned)
            {

                DiscordEmoji certified = DiscordEmoji.FromName(Discord, ":certified:");
                DiscordEmoji notCertified = DiscordEmoji.FromName(Discord, ":notcertified:");

                var channel = await Discord.GetChannelAsync(e.Message.ChannelId);
                var messages = await channel.GetMessagesAsync(); //TODO: get all messages
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


                string result = "User\tCertified\tNotCertified\n";
                foreach (CustomDiscordUser user in map.Values)
                {
                    result += $"{user.User.Username}\t{user.Certified}\t{user.NotCertified}\n";
                }

                await e.Message.RespondAsync(result);
            }
        }

        private static async Task AddTracksToSpotifyPlaylist(string[] trackIds, string playlistId)
        {

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
