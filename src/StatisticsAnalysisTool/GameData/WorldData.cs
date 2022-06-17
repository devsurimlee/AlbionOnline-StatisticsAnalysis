using log4net;
using StatisticsAnalysisTool.Common;
using StatisticsAnalysisTool.Models;
using StatisticsAnalysisTool.Properties;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StatisticsAnalysisTool.GameData
{
    public static class WorldData
    {
        public static ObservableCollection<WorldJsonObject> MapData;
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);

        public static string GetUniqueNameOrNull(string index)
        {
            return MapData?.FirstOrDefault(x => x?.Index == index)?.UniqueName;
        }

        public static string GetUniqueNameOrDefault(int index)
        {
            return GetUniqueNameOrDefault($"{index:0000}");
        }

        public static string GetUniqueNameOrDefault(string index)
        {
            var name = MapData?.FirstOrDefault(x => x.Index == index)?.UniqueName ?? index;
            var splitName = name?.Split(new[] { "@" }, StringSplitOptions.None);

            if (splitName is { Length: > 0 } && name.ToLower().Contains('@'))
            {
                return GetMapNameByMapType(GetMapType(splitName[1]));
            }

            return name;
        }

        public static Guid? GetMapGuid(string index)
        {
            // Cluster change event
            // @ISLAND@c640e642-5135-4203-89b5-0007e4215605
            // @RANDOMDUNGEON@fe968505-9771-4653-8ade-29a1bd6ddb56 
            // @HIDEOUT@2306@29c344b3-2138-421d-a97c-06e29d4759ec

            // Base vault info event
            // 4a82e5ed-8b64-40e3-926b-e0b020c4550a@@ISLAND@c640e642-5135-4203-89b5-0007e4215605
            // f56a368d-2f0b-4d01-a1ba-0079cf8b1fa9@4001

            try
            {
                var splitName = index.Split(new[] { "@" }, StringSplitOptions.RemoveEmptyEntries);

                var mapTypeIndex = Array.FindIndex(splitName, indexString => indexString is "ISLAND" or "HIDEOUT");

                if (index.Contains("ISLAND") && mapTypeIndex > -1 && splitName.Length >= mapTypeIndex + 1)
                {
                    var mapGuid = new Guid(splitName[mapTypeIndex + 1]);
                    return mapGuid;
                }

                if (index.Contains("HIDEOUT") && mapTypeIndex > -1 && splitName.Length >= mapTypeIndex + 2)
                {
                    var mapGuid = new Guid(splitName[mapTypeIndex + 2]);
                    return mapGuid;
                }

                if (splitName.Length > 1 && index.ToLower().Contains('@'))
                {
                    var mapType = GetMapType(splitName[0]);
                    if (mapType is MapType.RandomDungeon or MapType.CorruptedDungeon or MapType.HellGate or MapType.Expedition && !string.IsNullOrEmpty(splitName[1]))
                    {
                        var mapGuid = new Guid(splitName[1]);
                        return mapGuid;
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public static string GetMapNameByMapType(MapType mapType)
        {
            return mapType switch
            {
                MapType.HellGate => LanguageController.Translation("HELLGATE"),
                MapType.RandomDungeon => LanguageController.Translation("DUNGEON"),
                MapType.CorruptedDungeon => LanguageController.Translation("CORRUPTED_LAIR"),
                MapType.Island => LanguageController.Translation("ISLAND"),
                MapType.Hideout => LanguageController.Translation("HIDEOUT"),
                MapType.Expedition => LanguageController.Translation("EXPEDITION"),
                MapType.Arena => LanguageController.Translation("ARENA"),
                _ => LanguageController.Translation("UNKNOWN")
            };
        }

        public static MapType GetMapType(string index)
        {
            if (index.ToUpper().Contains("HELLCLUSTER")) return MapType.HellGate;

            if (index.ToUpper().Contains("RANDOMDUNGEON")) return MapType.RandomDungeon;

            if (index.ToUpper().Contains("CORRUPTEDDUNGEON")) return MapType.CorruptedDungeon;

            if (index.ToUpper().Contains("ISLAND")) return MapType.Island;

            if (index.ToUpper().Contains("HIDEOUT")) return MapType.Hideout;

            if (index.ToUpper().Contains("EXPEDITION")) return MapType.Expedition;

            if (index.ToUpper().Contains("ARENA")) return MapType.Arena;

            return MapType.Unknown;
        }

        public static async Task<bool> GetDataListFromJsonAsync()
        {
            var url = Settings.Default.WorldDataSourceUrl;
            var localFilePath = $"{AppDomain.CurrentDomain.BaseDirectory}{Settings.Default.WorldDataFileName}";

            if (string.IsNullOrEmpty(url))
            {
                Log.Warn($"{nameof(GetDataListFromJsonAsync)}: No WorldDataSourceUrl found.");
                return false;
            }

            if (File.Exists(localFilePath))
            {
                var fileDateTime = File.GetLastWriteTime(localFilePath);

                if (fileDateTime.AddDays(Settings.Default.UpdateWorldDataByDays) < DateTime.Now)
                {
                    if (await GetWorldListFromWebAsync(url).ConfigureAwait(false))
                    {
                        MapData = GetWorldDataFromLocal();
                    }

                    return MapData?.Count > 0;
                }

                MapData = GetWorldDataFromLocal();
                return MapData?.Count > 0;
            }

            if (await GetWorldListFromWebAsync(url).ConfigureAwait(false))
            {
                MapData = GetWorldDataFromLocal();
            }

            return MapData?.Count > 0;
        }

        public static string GetWorldJsonTypeByIndex(string index)
        {
            if (index == null)
            {
                return null;
            }

            var splitName = index.Split(new[] { "@" }, StringSplitOptions.RemoveEmptyEntries);
            if (index.ToLower().Contains('@') && splitName.Length > 0 && !string.IsNullOrEmpty(splitName[0]))
            {
                return splitName[0];
            }

            return MapData?.FirstOrDefault(x => x.Index == index)?.Type;
        }

        public static string GetFileByIndex(string index)
        {
            return MapData?.FirstOrDefault(x => x.Index == index)?.File;
        }

        private static ObservableCollection<WorldJsonObject> GetWorldDataFromLocal()
        {
            try
            {
                var options = new JsonSerializerOptions()
                {
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                var localItemString = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Settings.Default.GameFilesDirectoryName, Settings.Default.WorldDataFileName), Encoding.UTF8);
                return JsonSerializer.Deserialize<ObservableCollection<WorldJsonObject>>(localItemString, options);
            }
            catch (Exception e)
            {
                ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
                Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType, e);
                return new ObservableCollection<WorldJsonObject>();
            }
        }

        private static async Task<bool> GetWorldListFromWebAsync(string url)
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(300)
            };

            try
            {
                using var response = await client.GetAsync(url);
                using var content = response.Content;

                var fileString = await content.ReadAsStringAsync();
                await File.WriteAllTextAsync(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameFiles", Settings.Default.WorldDataFileName), fileString, Encoding.UTF8);
                return true;
            }
            catch (HttpRequestException e)
            {
                ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, e.InnerException);
                Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType, e.InnerException);
                return false;
            }
            catch (Exception e)
            {
                ConsoleManager.WriteLineForError(MethodBase.GetCurrentMethod()?.DeclaringType, e);
                Log.Error(MethodBase.GetCurrentMethod()?.DeclaringType, e);
                return false;
            }
        }
    }

    public enum MapType
    {
        Unknown,
        RandomDungeon,
        HellGate,
        CorruptedDungeon,
        Island,
        Hideout,
        Expedition,
        Arena
    }
}