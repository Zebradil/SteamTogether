using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SteamTogether.Utils;

namespace SteamTogether
{
    class Program
    {
        private static IConfiguration _config;

        static async Task Main()
        {
            Console.WriteLine("Loading..");
            LoadConfiguration();

            try
            {
                ValidateConfiguration();

                var steamApiKey = _config["SteamDevKey"];
                if (string.IsNullOrEmpty(steamApiKey))
                {
                    throw new ArgumentException("SteamDevKey should be set");
                }

                var client = new Client(steamApiKey);
                var steamIds = _config
                    .GetSection("Users").Get<List<long>>()
                    .Distinct()
                    .ToList();

                var filterCount = _config.GetSection("fullEqual").Get<bool>()
                    ? steamIds.Count
                    : _config.GetSection("FilterCount").Get<int>();

                Console.WriteLine("Getting data..");

                var players = await client.GetUsersInfo(steamIds);
                var games = players
                    .SelectMany(x => x.Value.OwnedGames)
                    .Distinct(new GameComparator())
                    .Select(
                        x =>
                        {
                            var names = players.Select(o =>
                                {
                                    return new
                                    {
                                        Name = o.Value.Info.Nickname,
                                        AppIds = o.Value.OwnedGames.Select(x => x.AppId)
                                    };
                                })
                                .Where(y => y.AppIds.Contains(x.AppId))
                                .Select(z => z.Name);

                            return new
                            {
                                x.Name,
                                NickNames = names
                            };
                        })
                    .Where(x => x.NickNames.Count() >= filterCount)
                    .OrderByDescending(x => x.NickNames.Count());

                if (!games.Any())
                {
                    Console.WriteLine("There are no intersections among owned games");
                } else {
                    var nameHeader = "Name";
                    var countHeader = "Count";
                    var playersHeader = "Players";

                    var nameLength = nameHeader.Length;
                    var countLength = countHeader.Length;
                    var playersLength = playersHeader.Length;

                    foreach (var game in games)
                    {
                        nameLength = Math.Max(nameLength, game.Name.Length);
                        playersLength = Math.Max(playersLength, String.Join(", ", game.NickNames).Length);
                    }

                    var templateString = $"| {{0,-{nameLength}}} | {{1,{countLength}}} | {{2,-{playersLength}}} |";
                    var tableBorder = $"+{new String('=', nameLength + 2)}+{new String('=', countLength + 2)}+{new String('=', playersLength + 2)}+";
                    Console.WriteLine(tableBorder);
                    Console.WriteLine(templateString, nameHeader, countHeader, playersHeader);
                    Console.WriteLine(tableBorder);
                    foreach (var game in games)
                    {
                        Console.WriteLine(templateString, game.Name, game.NickNames.Count(), String.Join(", ", game.NickNames));
                    }
                    Console.WriteLine(tableBorder);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Something went wrong");
                Console.WriteLine(e.Message);
                Console.ReadLine();
            }
        }

        private static void LoadConfiguration()
        {
            var appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            if (!File.Exists(appSettingsPath))
            {
                throw new ArgumentException("Set up the configuration file appsettings.json");
            }

            _config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .SetBasePath(Directory.GetCurrentDirectory())
                .Build();
        }

        private static void ValidateConfiguration()
        {
            if (string.IsNullOrEmpty(_config["SteamDevKey"]))
            {
                throw new ArgumentException("key[SteamDevKey] should be set");
            }

            var usersCount = _config.GetSection("Users").Get<List<long>>().Count;
            if (usersCount < 2)
            {
                throw new ArgumentException("key[Users] -> set at least 2 user ids");
            }

            var filterCount = _config.GetSection("FilterCount").Get<int>();
            if (usersCount < filterCount)
            {
                throw new ArgumentException("key[FilterCount] -> Filter count should be > than amount of user ids");
            }
        }
    }
}
