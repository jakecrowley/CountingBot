using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CountingBot
{
    class Program
    {
        static DiscordClient client;

        static MongoClient mongo;
        static IMongoDatabase database;

        static IMongoCollection<countingday> countingdays;
        static IMongoCollection<countinguser> countingusers;

        static void Main(string[] args)
        {
	    IConfigurationRoot config = new ConfigurationBuilder()
		.AddUserSecrets<Program>()
		.Build();

            RunBotAsync(config);
            Console.ReadLine();
        }

        private static async void RunBotAsync(IConfigurationRoot config)
        {
            mongo = new MongoClient(config["MongoURL"]);
            database = mongo.GetDatabase("bot929");

            client = new DiscordClient(new DiscordConfiguration()
            {
                Token = config["BotToken"],
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                MinimumLogLevel = LogLevel.Information
            });

            client.Ready += Client_Ready;
            client.MessageCreated += Client_MessageCreated;

            await client.ConnectAsync();
        }

        private static async Task Client_MessageCreated(DiscordClient sender, DSharpPlus.EventArgs.MessageCreateEventArgs e)
        {
            if (e.Message.ChannelId == 642566127804874755)
            {
                DiscordMessage lastMessage = (await e.Channel.GetMessagesAsync(2)).Last();
                var creation = e.Message.CreationTimestamp.AddHours(-5).Date.ToString("MM-dd-yyyy");

                int last, current;
                try { 
                    last = int.Parse(lastMessage.Content); 
                }
                catch(FormatException exc) 
                { 
                    LogMessage($"Could not parse \"{lastMessage.Content}\" to int.", LogLevel.Error); 
                    return; 
                }

                try 
                { 
                    current = int.Parse(e.Message.Content); 
                }
                catch (FormatException exc) 
                { 
                    await e.Message.DeleteAsync();
                    LogMessage($"Could not parse \"{e.Message.Content}\" to int.", LogLevel.Error); 
                    return; 
                }

                if (current != last + 1 || e.Message.Author.Id == lastMessage.Author.Id)
                {
                    await e.Message.DeleteAsync();
                    LogMessage($"Deleting bad counting by {e.Author.Username}#{e.Author.Discriminator} - " + e.Message.Content);
                }
                else
                {
                    var userfind = countingusers.Find($"{{_id: {e.Author.Id}}}");

                    countinguser user;
                    if (userfind.CountDocuments() < 1)
                    {
                        user = new countinguser(e.Author.Id);
                    }
                    else
                        user = userfind.First();

                    UpdateDefinition<countinguser> update = Builders<countinguser>.Update.Set("counted", user.counted + 1);
                    countingusers.UpdateOne(cu => (cu.id == user.id), update, new UpdateOptions { IsUpsert = true });

                    var dayfind = countingdays.Find($"{{_id: \"{creation}\"}}");

                    countingday day;
                    if (dayfind.CountDocuments() < 1)
                    {
                        day = new countingday(creation);
                    }
                    else
                        day = dayfind.First();

                    UpdateDefinition<countingday> dayupdate = Builders<countingday>.Update.Set("counted", day.counted + 1);
                    dayupdate = dayupdate.Set("day", day.day);
                    countingdays.UpdateOne(cd => (cd.id == day.id), dayupdate, new UpdateOptions { IsUpsert = true });
                }
            }
        }

        private static Task Client_Ready(DiscordClient sender, DSharpPlus.EventArgs.ReadyEventArgs e)
        {
            countingdays = database.GetCollection<countingday>("countingdays");
            countingusers = database.GetCollection<countinguser>("countingusers");
            LogMessage("Client is ready!");
            return Task.CompletedTask;
        }

        private static void LogMessage(string message, LogLevel logLevel = LogLevel.Information)
        {
            client.Logger.Log(logLevel, message);
        }
    }

    class countinguser
    {
        [BsonElement("_id")]
        public ulong id;

        [BsonElement("counted")]
        public int counted;

        public countinguser(ulong id)
        {
            this.id = id;
        }
    }

    class countingday
    {
        [BsonElement("_id")]
        public string id;

        [BsonElement("day")]
        public int day;

        [BsonElement("counted")]
        public int counted;

        public countingday(string date)
        {
            this.id = date;
        }
    }
}
