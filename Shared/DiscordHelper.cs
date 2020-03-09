using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace dampbot
{
    public class DiscordHelper
    {

        DiscordClient Discord = new DiscordClient(new DiscordConfiguration
        {
            Token = Secrets.TOKEN,
            TokenType = TokenType.Bot
        });

        public DiscordHelper()
        {

        }
        public async Task<DiscordClient> SetUpDiscord()
        {
            // Connect
            await Discord.ConnectAsync();

            return Discord;
        }

        public async Task<List<DiscordMessage>> GetAllDiscordMessages(ulong channelId)
        {
            List<DiscordMessage> result = new List<DiscordMessage>();
            var channel = await Discord.GetChannelAsync(channelId);

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
    }
}
