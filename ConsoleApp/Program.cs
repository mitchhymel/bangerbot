using dampbot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConsoleApp
{
    class Program
    {
        static SpotifyHelper Spotify = new SpotifyHelper();

        static DiscordHelper Discord = new DiscordHelper();

        public static async Task Main(string[] args)
        {
            if (await Spotify.Login() == null)
            {
                Console.WriteLine("Error when logging into Spotify.");
                return;
            }

            if (await Discord.SetUpDiscord() == null)
            {
                Console.WriteLine("Error when setting up Discord client.");
                return;
            }

            Console.WriteLine("Starting...");
            await ParseChannelMessagesAndAddToSpotify();
            Console.WriteLine("Done");

            Console.ReadLine();
        }

        public static async Task ParseChannelMessagesAndAddToSpotify()
        {
            List<string> tracksToAdd = new List<string>();
            var messages = await Discord.GetAllDiscordMessages(Secrets.BANGER_CHANNEL_ID);
            foreach (var message in messages)
            {
                if (Spotify.MessageContainsSpotifyTrack(message.Content))
                {
                    List<string> tracks = Spotify.GetTrackIdsFromMessage(message.Content);
                    tracksToAdd.AddRange(tracks);
                }
            }

            await Spotify.AddTracksToSpotifyPlaylist(Secrets.SPOTIFY_PLAYLIST_ID, tracksToAdd);
        }
    }
}
