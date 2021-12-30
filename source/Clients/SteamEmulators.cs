﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CommonPluginsShared;
using CommonPluginsStores;
using Playnite.SDK.Models;
using Playnite.SDK.Data;
using SuccessStory.Models;
using CommonPluginsShared.Models;
using CommonPluginsShared.Extensions;

namespace SuccessStory.Clients
{
    class SteamEmulators : GenericAchievements
    {
        protected static SteamApi _steamApi;
        internal static SteamApi steamApi
        {
            get
            {
                if (_steamApi == null)
                {
                    _steamApi = new SteamApi();
                }
                return _steamApi;
            }

            set
            {
                _steamApi = value;
            }
        }

        private List<string> AchievementsDirectories = new List<string>();
        private int SteamId { get; set; } = 0;


        public SteamEmulators(List<Folder> LocalFolders) : base("SteamEmulators")
        {
            AchievementsDirectories.Add("%PUBLIC%\\Documents\\Steam\\CODEX");
            AchievementsDirectories.Add("%appdata%\\Steam\\CODEX");

            AchievementsDirectories.Add("%ProgramData%\\Steam");
            AchievementsDirectories.Add("%localappdata%\\SKIDROW");
            AchievementsDirectories.Add("%DOCUMENTS%\\SKIDROW");

            foreach (Folder folder in LocalFolders)
            {
                AchievementsDirectories.Add(folder.FolderPath);
            }
        }


        public override GameAchievements GetAchievements(Game game)
        {
            throw new NotImplementedException();
        }


        #region Configuration
        public override bool ValidateConfiguration()
        {
            // The authentification is only for localised achievement
            return true;
        }

        public override bool EnabledInSettings()
        {
            // No necessary activation
            return true;
        }
        #endregion


        public int GetSteamId()
        {
            return SteamId;
        }


        #region SteamEmulator
        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public GameAchievements GetAchievementsLocal(Game game, string apiKey, int SteamId = 0, bool IsManual = false)
        {
            GameAchievements gameAchievements = SuccessStory.PluginDatabase.GetDefault(game);
            GameAchievements gameAchievementsCached = SuccessStory.PluginDatabase.Get(game, true);

            if (SteamId != 0)
            {
                this.SteamId = SteamId;
            }
            else
            {
                this.SteamId = steamApi.GetSteamId(game.Name);
            }


            var data = Get(game, this.SteamId, apiKey, IsManual); 

            if (gameAchievementsCached == null)
            {
                gameAchievements.Items = data.Achievements;
                gameAchievements.ItemsStats = data.Stats;
                return gameAchievements;
            }
            else
            {
                if (gameAchievementsCached.Items.Count != data.Achievements.Count)
                {
                    gameAchievements.Items = data.Achievements;
                    gameAchievements.ItemsStats = data.Stats;
                    return gameAchievements;
                }

                gameAchievementsCached.Items.ForEach(x =>
                {
                    var finded = data.Achievements.Find(y => x.ApiName == y.ApiName);
                    if (finded != null)
                    {
                        x.Name = finded.Name;
                        if (x.DateUnlocked == null || x.DateUnlocked == default(DateTime))
                        {
                            x.DateUnlocked = finded.DateUnlocked;
                        }
                    }
                });
                gameAchievementsCached.ItemsStats = data.Stats;
                return gameAchievementsCached;
            }
        }



        private List<GameStats> ReadStatsINI(string pathFile, List<GameStats> gameStats)
        {
            try
            {
                string line;
                string Name = string.Empty;
                double Value = 0;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    // Achievement name
                    if (!line.IsEqual("[Stats]"))
                    {
                        var data = line.Split('=');
                        if (data.Count() > 1 && !data[0].IsNullOrEmpty() && !data[0].IsEqual("STACount"))
                        {
                            Name = data[0];
                            try
                            {
                                Value = BitConverter.ToInt32(StringToByteArray(data[1]), 0);
                            }
                            catch
                            {
                                double.TryParse(data[1], out Value);
                            }

                            gameStats.Add(new GameStats
                            {
                                Name = Name,
                                Value = Value
                            });

                            Name = string.Empty;
                            Value = 0;
                        }
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "SuccessStory");
            }

            return gameStats;
        }

        private List<Achievements> ReadAchievementsINI(string pathFile, List<Achievements> ReturnAchievements)
        {
            bool isType2 = false;

            try
            {
                string line;
                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    if (line.IsEqual("[Time]"))
                    {
                        isType2 = true;
                        break;
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "SuccessStory");
            }

            if (!isType2)
            {
                return ReadAchievementsINI_type1(pathFile, ReturnAchievements);
            }
            else
            {
                return ReadAchievementsINI_type2(pathFile, ReturnAchievements);
            }
        }

        private List<Achievements> ReadAchievementsINI_type1(string pathFile, List<Achievements> ReturnAchievements)
        {
            try
            {
                string line;

                string Name = string.Empty;
                bool State = false;
                string sTimeUnlock = string.Empty;
                int timeUnlock = 0;
                DateTime? DateUnlocked = null;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    // Achievement name
                    if (line.IndexOf("[") > -1)
                    {
                        Name = line.Replace("[", string.Empty).Replace("]", string.Empty).Trim();
                        State = false;
                        timeUnlock = 0;
                        DateUnlocked = null;
                    }

                    if (Name != "Steam")
                    {
                        // State
                        if (line.IndexOf("State") > -1 && line.ToLower() != "state = 0000000000")
                        {
                            State = true;
                        }

                        // Unlock
                        if (line.IndexOf("Time") > -1 && line.ToLower() != "time = 0000000000")
                        {
                            if (line.Contains("Time = "))
                            {
                                sTimeUnlock = line.Replace("Time = ", string.Empty);
                                timeUnlock = BitConverter.ToInt32(StringToByteArray(line.Replace("Time = ", string.Empty)), 0);
                            }
                            if (line.Contains("Time="))
                            {
                                sTimeUnlock = line.Replace("Time=", string.Empty);
                                sTimeUnlock = sTimeUnlock.Substring(0, sTimeUnlock.Length - 2);

                                char[] ca = sTimeUnlock.ToCharArray();
                                StringBuilder sb = new StringBuilder(sTimeUnlock.Length);
                                for (int i = 0; i < sTimeUnlock.Length; i += 2)
                                {
                                    sb.Insert(0, ca, i, 2);
                                }
                                sTimeUnlock = sb.ToString();

                                timeUnlock = int.Parse(sTimeUnlock, System.Globalization.NumberStyles.HexNumber);
                            }
                        }
                        if (line.IndexOf("CurProgress") > -1 && line.ToLower() != "curprogress = 0000000000")
                        {
                            sTimeUnlock = line.Replace("CurProgress = ", string.Empty);
                            timeUnlock = BitConverter.ToInt32(StringToByteArray(line.Replace("CurProgress = ", string.Empty)), 0);
                        }

                        DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeUnlock).ToLocalTime();

                        // End Achievement
                        if (timeUnlock != 0 && State)
                        {
                            ReturnAchievements.Add(new Achievements
                            {
                                ApiName = Name,
                                Name = string.Empty,
                                Description = string.Empty,
                                UrlUnlocked = string.Empty,
                                UrlLocked = string.Empty,
                                DateUnlocked = DateUnlocked
                            });

                            Name = string.Empty;
                            State = false;
                            timeUnlock = 0;
                            DateUnlocked = null;
                        }
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "SuccessStory");
            }

            return ReturnAchievements;
        }

        private List<Achievements> ReadAchievementsINI_type2(string pathFile, List<Achievements> ReturnAchievements)
        {
            try
            {
                string line;
                bool startAchievement = false;

                string Name = string.Empty;
                string sTimeUnlock = string.Empty;
                int timeUnlock = 0;
                DateTime? DateUnlocked = null;

                StreamReader file = new StreamReader(pathFile);
                while ((line = file.ReadLine()) != null)
                {
                    if (line.IsEqual("[Time]"))
                    {
                        startAchievement = true;
                    }
                    else if (startAchievement)
                    {
                        var data = line.Split('=');
                        Name = data[0];
                        sTimeUnlock = data[1];
                        timeUnlock = BitConverter.ToInt32(StringToByteArray(sTimeUnlock), 0);
                        DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeUnlock).ToLocalTime();

                        if (timeUnlock != 0)
                        {
                            ReturnAchievements.Add(new Achievements
                            {
                                ApiName = Name,
                                Name = string.Empty,
                                Description = string.Empty,
                                UrlUnlocked = string.Empty,
                                UrlLocked = string.Empty,
                                DateUnlocked = DateUnlocked
                            });

                            Name = string.Empty;
                            timeUnlock = 0;
                            DateUnlocked = null;
                        }
                    }
                }
                file.Close();
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, "SuccessStory");
            }

            return ReturnAchievements;
        }


        private SteamEmulatorData Get(Game game, int SteamId, string apiKey, bool IsManual)
        {
            List<Achievements> ReturnAchievements = new List<Achievements>();
            List<GameStats> ReturnStats = new List<GameStats>();

            if (!IsManual)
            {
                // Search data local
                foreach (string DirAchivements in AchievementsDirectories)
                {
                    switch (DirAchivements.ToLower())
                    {
                        case ("%public%\\documents\\steam\\codex"):
                        case ("%appdata%\\steam\\codex"):
                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{SteamId}\\achievements.ini"))
                            {
                                string line;

                                string Name = string.Empty;
                                DateTime? DateUnlocked = null;

                                StreamReader file = new StreamReader(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{SteamId}\\achievements.ini");
                                while ((line = file.ReadLine()) != null)
                                {
                                    // Achievement name
                                    if (line.IndexOf("[") > -1)
                                    {
                                        Name = line.Replace("[", string.Empty).Replace("]", string.Empty).Trim();
                                    }
                                    // Achievement UnlockTime
                                    if (line.IndexOf("UnlockTime") > -1 && line.ToLower() != "unlocktime=0")
                                    {
                                        DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(int.Parse(line.Replace("UnlockTime=", string.Empty))).ToLocalTime();
                                    }

                                    // End Achievement
                                    if (line.Trim() == string.Empty && DateUnlocked != null)
                                    {
                                        ReturnAchievements.Add(new Achievements
                                        {
                                            ApiName = Name,
                                            Name = string.Empty,
                                            Description = string.Empty,
                                            UrlUnlocked = string.Empty,
                                            UrlLocked = string.Empty,
                                            DateUnlocked = DateUnlocked
                                        });

                                        Name = string.Empty;
                                        DateUnlocked = null;
                                    }
                                }
                                file.Close();
                            }

                            if (File.Exists(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{SteamId}\\stats.ini"))
                            {
                                ReturnStats = ReadStatsINI(Environment.ExpandEnvironmentVariables(DirAchivements) + $"\\{SteamId}\\stats.ini", ReturnStats);
                            }

                            break;

                        case "%programdata%\\steam":
                            if (Directory.Exists(Environment.ExpandEnvironmentVariables("%ProgramData%\\Steam")))
                            {
                                string[] dirsUsers = Directory.GetDirectories(Environment.ExpandEnvironmentVariables("%ProgramData%\\Steam"));
                                foreach (string dirUser in dirsUsers)
                                {
                                    if (File.Exists(dirUser + $"\\{SteamId}\\stats\\achievements.ini"))
                                    {
                                        ReturnAchievements = ReadAchievementsINI(dirUser + $"\\{SteamId}\\stats\\achievements.ini", ReturnAchievements);
                                    }

                                    if (File.Exists(dirUser + $"\\{SteamId}\\stats\\stats.ini"))
                                    {
                                        ReturnStats = ReadStatsINI(dirUser + $"\\{SteamId}\\stats\\stats.ini", ReturnStats);
                                    }
                                }
                            }

                            break;

                        case "%localappdata%\\skidrow":
                            logger.Warn($"No treatment for {DirAchivements}");
                            break;

                        case "%documents%\\skidrow":
                            logger.Warn($"No treatment for {DirAchivements}");
                            break;

                        default:
                            if (ReturnAchievements.Count == 0)
                            {
                                var finded = PluginDatabase.PluginSettings.Settings.LocalPath.Find(x => x.FolderPath.IsEqual(DirAchivements));
                                Guid.TryParse(finded?.GameId, out Guid GameId);

                                if (!DirAchivements.Contains("steamemu", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    if (File.Exists(DirAchivements + $"\\{SteamId}\\stats\\achievements.ini"))
                                    {
                                        ReturnAchievements = ReadAchievementsINI(DirAchivements + $"\\{SteamId}\\stats\\achievements.ini", ReturnAchievements);

                                        if (File.Exists(DirAchivements + $"\\{SteamId}\\stats\\stats.ini"))
                                        {
                                            ReturnStats = ReadStatsINI(DirAchivements + $"\\{SteamId}\\stats\\stats.ini", ReturnStats);
                                        }

                                    }
                                    else if (GameId != default(Guid) && GameId == game.Id && (finded?.HasGame ?? false))
                                    {
                                        if (File.Exists(DirAchivements + $"\\stats\\achievements.ini"))
                                        {
                                            ReturnAchievements = ReadAchievementsINI(DirAchivements + $"\\stats\\achievements.ini", ReturnAchievements);

                                            if (File.Exists(DirAchivements + $"\\stats\\stats.ini"))
                                            {
                                                ReturnStats = ReadStatsINI(DirAchivements + $"\\stats\\stats.ini", ReturnStats);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ReturnAchievements = GetSteamEmu(DirAchivements + $"\\{SteamId}\\SteamEmu");
                                    }
                                }
                                else
                                {
                                    var DataPath = DirAchivements.Split('\\').ToList();
                                    int index = DataPath.FindIndex(x => x.IsEqual("steamemu"));
                                    string GameName = DataPath[index - 1];

                                    SteamApi steamApi = new SteamApi();
                                    int TempSteamId = steamApi.GetSteamId(GameName);

                                    if (TempSteamId == SteamId)
                                    {
                                        ReturnAchievements = GetSteamEmu(DirAchivements);
                                    }
                                }
                            }
                            break;
                    }
                }

                Common.LogDebug(true, $"{Serialization.ToJson(ReturnAchievements)}");

                if (ReturnAchievements == new List<Achievements>())
                {
                    logger.Warn($"No data for {SteamId}");
                    return new SteamEmulatorData { Achievements = new List<Achievements>(), Stats = new List<GameStats>() };
                }
            }
            
            #region Get details achievements & stats
            // List details acheviements
            string lang = CodeLang.GetSteamLang(PluginDatabase.PlayniteApi.ApplicationSettings.Language);
            string url = string.Format(@"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={0}&appid={1}&l={2}", apiKey, SteamId, lang);

            string ResultWeb = string.Empty;
            try
            {
                ResultWeb = Web.DownloadStringData(url).GetAwaiter().GetResult();
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError && ex.Response != null)
                {
                    var resp = (HttpWebResponse)ex.Response;
                    switch (resp.StatusCode)
                    {
                        case HttpStatusCode.BadRequest: // HTTP 400
                            break;
                        case HttpStatusCode.ServiceUnavailable: // HTTP 503
                            break;
                        default:
                            Common.LogError(ex, false, $"Failed to load from {url}", true, "SuccessStory");
                            break;
                    }
                    return new SteamEmulatorData { Achievements = new List<Achievements>(), Stats = new List<GameStats>() };
                }
            }

            if (ResultWeb != string.Empty && ResultWeb.Length > 50)
            {
                dynamic resultObj = Serialization.FromJson<dynamic>(ResultWeb);
                dynamic resultItems = null;
                dynamic resultItemsStats = null;

                try
                {
                    resultItems = resultObj["game"]?["availableGameStats"]?["achievements"];
                    resultItemsStats = resultObj["game"]?["availableGameStats"]?["stats"];

                    for (int i = 0; i < resultItems?.Count; i++)
                    {
                        bool isFind = false;
                        for (int j = 0; j < ReturnAchievements.Count; j++)
                        {
                            if (ReturnAchievements[j].ApiName.IsEqual(((string)resultItems[i]["name"])))
                            {
                                Achievements temp = new Achievements
                                {
                                    ApiName = (string)resultItems[i]["name"],
                                    Name = (string)resultItems[i]["displayName"],
                                    Description = (string)resultItems[i]["description"],
                                    UrlUnlocked = (string)resultItems[i]["icon"],
                                    UrlLocked = (string)resultItems[i]["icongray"],
                                    DateUnlocked = ReturnAchievements[j].DateUnlocked
                                };

                                isFind = true;
                                ReturnAchievements[j] = temp;
                                j = ReturnAchievements.Count;
                            }
                        }

                        if (!isFind)
                        {
                            ReturnAchievements.Add(new Achievements
                            {
                                ApiName = (string)resultItems[i]["name"],
                                Name = (string)resultItems[i]["displayName"],
                                Description = (string)resultItems[i]["description"],
                                UrlUnlocked = (string)resultItems[i]["icon"],
                                UrlLocked = (string)resultItems[i]["icongray"],
                                DateUnlocked = default(DateTime)
                            });
                        }
                    }

                    if (ReturnStats.Count > 0)
                    {
                        for (int i = 0; i < resultItemsStats?.Count; i++)
                        {
                            bool isFind = false;
                            for (int j = 0; j < ReturnStats.Count; j++)
                            {
                                if (ReturnStats[j].Name.IsEqual(((string)resultItemsStats[i]["name"])))
                                {
                                    GameStats temp = new GameStats
                                    {
                                        Name = (string)resultItemsStats[i]["name"],
                                        Value = ReturnStats[j].Value
                                    };

                                    isFind = true;
                                    ReturnStats[j] = temp;
                                    j = ReturnStats.Count;
                                }
                            }

                            if (!isFind)
                            {
                                ReturnStats.Add(new GameStats
                                {
                                    Name = (string)resultItemsStats[i]["name"],
                                    Value = 0
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, true, $"Failed to parse");
                    return new SteamEmulatorData { Achievements = new List<Achievements>(), Stats = new List<GameStats>() };
                }
            }
            #endregion


            // Delete empty (SteamEmu)
            ReturnAchievements = ReturnAchievements.Select(x => x).Where(x => !string.IsNullOrEmpty(x.UrlLocked)).ToList();

            return new SteamEmulatorData { Achievements = ReturnAchievements, Stats = ReturnStats };
        }

        private List<Achievements> GetSteamEmu(string DirAchivements)
        {
            List<Achievements> ReturnAchievements = new List<Achievements>();

            if (File.Exists(DirAchivements + $"\\stats.ini"))
            {
                bool IsGoodSection = false;
                string line;

                string Name = string.Empty;
                DateTime? DateUnlocked = null;

                StreamReader file = new StreamReader(DirAchivements + $"\\stats.ini");
                while ((line = file.ReadLine()) != null)
                {
                    if (IsGoodSection)
                    {
                        // End list achievements unlocked
                        if (line.IndexOf("[Achievements]") > -1)
                        {
                            IsGoodSection = false;
                        }
                        else
                        {
                            var data = line.Split('=');

                            DateUnlocked = new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(int.Parse(data[1])).ToLocalTime();
                            Name = data[0];

                            ReturnAchievements.Add(new Achievements
                            {
                                ApiName = Name,
                                Name = string.Empty,
                                Description = string.Empty,
                                UrlUnlocked = string.Empty,
                                UrlLocked = string.Empty,
                                DateUnlocked = DateUnlocked
                            });
                        }
                    }

                    // Start list achievements unlocked
                    if (line.IndexOf("[AchievementsUnlockTimes]") > -1)
                    {
                        IsGoodSection = true;
                    }
                }
                file.Close();
            }

            return ReturnAchievements;
        }
        #endregion
    }


    public class SteamEmulatorData
    {
        public List<Achievements> Achievements { get; set; }
        public List<GameStats> Stats { get; set; }
    }
}
