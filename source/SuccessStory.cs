﻿using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using CommonPluginsShared;
using SuccessStory.Models;
using SuccessStory.Views;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommonPlayniteShared;
using SuccessStory.Services;
using System.Windows.Automation;
using CommonPluginsShared.PlayniteExtended;
using System.Windows.Media;
using CommonPluginsShared.Controls;
using SuccessStory.Controls;
using CommonPluginsShared.Models;
using CommonPlayniteShared.Common;
using System.Reflection;
using CommonPluginsShared.Extensions;
using System.Diagnostics;
using QuickSearch.SearchItems;

namespace SuccessStory
{
    public class SuccessStory : PluginExtended<SuccessStorySettingsViewModel, SuccessStoryDatabase>
    {
        public override Guid Id { get; } = Guid.Parse("cebe6d32-8c46-4459-b993-5a5189d60788");

        internal TopPanelItem topPanelItem;
        internal SuccessStoryViewSidebar successStoryViewSidebar;
        internal SuccessStoryViewRaSidebar successStoryViewRaSidebar;

        public static bool TaskIsPaused = false;
        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        public static bool IsFromMenu { get; set; } = false;

        private OldToNew oldToNew;


        public SuccessStory(IPlayniteAPI api) : base(api)
        {
            // Manual dll load
            try
            {
                string PluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string PathDLL = Path.Combine(PluginPath, "VirtualizingWrapPanel.dll");
                if (File.Exists(PathDLL))
                {
                    var DLL = Assembly.LoadFile(PathDLL);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }

            PluginDatabase.InitializeClient(this);

            // Old database
            oldToNew = new OldToNew(this.GetPluginUserDataPath());

            // Custom theme button
            EventManager.RegisterClassHandler(typeof(Button), Button.ClickEvent, new RoutedEventHandler(OnCustomThemeButtonClick));

            // Add Event for WindowBase for get the "WindowSettings".
            EventManager.RegisterClassHandler(typeof(Window), Window.LoadedEvent, new RoutedEventHandler(WindowBase_LoadedEvent));

            // Initialize top & side bar
            if (API.Instance.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                topPanelItem = new TopPanelItem()
                {
                    Icon = new TextBlock
                    {
                        Text = "\ue820",
                        FontSize = 22,
                        FontFamily = resources.GetResource("FontIcoFont") as FontFamily
                    },
                    Title = resources.GetString("LOCSuccessStoryViewGames"),
                    Activated = () =>
                    {
                        var ViewExtension = new SuccessView();

                        var windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true,
                            Width = 1280,
                            Height = 740
                        };

                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSuccessStory"), ViewExtension, windowOptions);
                        windowExtension.ResizeMode = ResizeMode.CanResize;
                        windowExtension.ShowDialog();
                        PluginDatabase.IsViewOpen = false;
                    },
                    Visible = PluginSettings.Settings.EnableIntegrationButtonHeader
                };

                successStoryViewSidebar = new SuccessStoryViewSidebar(this);
                successStoryViewRaSidebar = new SuccessStoryViewRaSidebar(this);
            }

            // Custom elements integration
            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                ElementList = new List<string> {
                    "PluginButton", "PluginViewItem", "PluginProgressBar", "PluginCompactList",
                    "PluginCompactLocked", "PluginCompactUnlocked", "PluginChart",
                    "PluginUserStats", "PluginList"
                },
                SourceName = PluginDatabase.PluginName
            });

            // Settings integration
            AddSettingsSupport(new AddSettingsSupportArgs
            {
                SourceName = PluginDatabase.PluginName,
                SettingsRoot = $"{nameof(PluginSettings)}.{nameof(PluginSettings.Settings)}"
            });
        }


        #region Custom event
        public void OnCustomThemeButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string ButtonName = ((Button)sender).Name;
                if (ButtonName == "PART_CustomScButton")
                {
                    Common.LogDebug(true, $"OnCustomThemeButtonClick()");

                    PluginDatabase.IsViewOpen = true;
                    dynamic ViewExtension = null;

                    var windowOptions = new WindowOptions
                    {
                        ShowMinimizeButton = false,
                        ShowMaximizeButton = true,
                        ShowCloseButton = true
                    };

                    if (PluginDatabase.PluginSettings.Settings.EnableOneGameView)
                    {
                        if (PluginDatabase.GameContext.Name.IsEqual("overwatch") && (PluginDatabase.GameContext.Source?.Name?.IsEqual("battle.net") ?? false))
                        {
                            ViewExtension = new SuccessStoryOverwatchView(PluginDatabase.GameContext);
                        }
                        else if (PluginSettings.Settings.EnableGenshinImpact && PluginDatabase.GameContext.Name.IsEqual("Genshin Impact"))
                        {
                            ViewExtension = new SuccessStoryGenshinImpactView(PluginDatabase.GameContext);
                        }
                        else
                        {
                            ViewExtension = new SuccessStoryOneGameView(PluginDatabase.GameContext);
                        }

                        windowOptions.ShowMaximizeButton = false;
                    }
                    else
                    {
                        windowOptions.Width = 1280;
                        windowOptions.Height = 740;

                        if (PluginDatabase.PluginSettings.Settings.EnableRetroAchievementsView && PlayniteTools.IsGameEmulated(PluginDatabase.GameContext))
                        {
                            ViewExtension = new SuccessView(true, PluginDatabase.GameContext);
                        }
                        else
                        {
                            ViewExtension = new SuccessView(false, PluginDatabase.GameContext);
                        }
                    }

 
                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSuccessStory"), ViewExtension, windowOptions);
                    if (windowOptions.ShowMaximizeButton)
                    {
                        windowExtension.ResizeMode = ResizeMode.CanResize;
                    }
                    windowExtension.ShowDialog();
                    PluginDatabase.IsViewOpen = false;
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        private void WindowBase_LoadedEvent(object sender, System.EventArgs e)
        {
            string WinIdProperty = string.Empty;
            try
            {
                WinIdProperty = ((Window)sender).GetValue(AutomationProperties.AutomationIdProperty).ToString();

                if (WinIdProperty == "WindowSettings" ||WinIdProperty == "WindowExtensions" || WinIdProperty == "WindowLibraryIntegrations")
                {
                    foreach (var achievementProvider in SuccessStoryDatabase.AchievementProviders.Values)
                    {
                        achievementProvider.ResetCachedConfigurationValidationResult();
                        achievementProvider.ResetCachedIsConnectedResult();
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, $"Error on WindowBase_LoadedEvent for {WinIdProperty}", true, PluginDatabase.PluginName);
            }
        }
        #endregion


        #region Theme integration
        // Button on top panel
        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return topPanelItem;
        }

        // List custom controls
        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            if (args.Name == "PluginButton")
            {
                return new PluginButton();
            }

            if (args.Name == "PluginViewItem")
            {
                return new PluginViewItem();
            }

            if (args.Name == "PluginProgressBar")
            {
                return new PluginProgressBar();
            }

            if (args.Name == "PluginCompactList")
            {
                return new PluginCompactList();
            }

            if (args.Name == "PluginCompactLocked")
            {
                return new PluginCompact { IsUnlocked = false };
            }

            if (args.Name == "PluginCompactUnlocked")
            {
                return new PluginCompact { IsUnlocked = true };
            }

            if (args.Name == "PluginChart")
            {
                return new PluginChart();
            }

            if (args.Name == "PluginUserStats")
            {
                return new PluginUserStats();
            }

            if (args.Name == "PluginList")
            {
                return new PluginList();
            }

            return null;
        }

        // SidebarItem
        public class SuccessStoryViewSidebar : SidebarItem
        {
            public SuccessStoryViewSidebar(SuccessStory plugin)
            {
                Type = SiderbarItemType.View;
                Title = resources.GetString("LOCSuccessStoryAchievements");
                Icon = new TextBlock
                {
                    Text = "\ue820",
                    FontFamily = resources.GetResource("FontIcoFont") as FontFamily
                };
                Opened = () =>
                {
                    SidebarItemControl sidebarItemControl = new SidebarItemControl(PluginDatabase.PlayniteApi);
                    sidebarItemControl.SetTitle(resources.GetString("LOCSuccessStoryAchievements"));
                    sidebarItemControl.AddContent(new SuccessView());

                    return sidebarItemControl;
                };
                Visible = plugin.PluginSettings.Settings.EnableIntegrationButtonSide;
            }
        }

        public class SuccessStoryViewRaSidebar : SidebarItem
        {
            public SuccessStoryViewRaSidebar(SuccessStory plugin)
            {
                Type = SiderbarItemType.View;
                Title = resources.GetString("LOCSuccessStoryRetroAchievements");
                Icon = new TextBlock
                {
                    Text = "\ue910",
                    FontFamily = resources.GetResource("CommonFont") as FontFamily
                };
                Opened = () =>
                {
                    SidebarItemControl sidebarItemControl = new SidebarItemControl(PluginDatabase.PlayniteApi);
                    sidebarItemControl.SetTitle(resources.GetString("LOCSuccessStoryRetroAchievements"));
                    sidebarItemControl.AddContent(new SuccessView(true));

                    return sidebarItemControl;
                };
                Visible = (plugin.PluginSettings.Settings.EnableIntegrationButtonSide && plugin.PluginSettings.Settings.EnableRetroAchievementsView);
            }
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            return new List<SidebarItem>
            {
                successStoryViewSidebar,
                successStoryViewRaSidebar
            };
        }
        #endregion


        #region Menus
        // To add new game menu items override GetGameMenuItems
        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Game GameMenu = args.Games.First();
            List<Guid> Ids = args.Games.Select(x => x.Id).ToList();

            // TODO: for multiple games, either check if any of them could have achievements, or just assume so
            var achievementSource = SuccessStoryDatabase.GetAchievementSource(PluginSettings.Settings, GameMenu);
            bool GameCouldHaveAchievements = achievementSource != SuccessStoryDatabase.AchievementSource.None;
            GameAchievements gameAchievements = PluginDatabase.Get(GameMenu, true);

            List<GameMenuItem> gameMenuItems = new List<GameMenuItem>();

            if (!gameAchievements.IsIgnored)
            {
                if (GameCouldHaveAchievements)
                {
                    if (!PluginSettings.Settings.EnableOneGameView || (PluginSettings.Settings.EnableOneGameView && gameAchievements.HasData))
                    {
                        // Show list achievements for the selected game
                        // TODO: disable when selecting multiple games?
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = resources.GetString("LOCSuccessStory"),
                            Description = resources.GetString("LOCSuccessStoryViewGame"),
                            Action = (gameMenuItem) =>
                            {
                                dynamic ViewExtension = null;
                                PluginDatabase.IsViewOpen = true;

                                var windowOptions = new WindowOptions
                                {
                                    ShowMinimizeButton = false,
                                    ShowMaximizeButton = true,
                                    ShowCloseButton = true
                                };

                                if (PluginDatabase.PluginSettings.Settings.EnableOneGameView)
                                {
                                    if (PluginDatabase.GameContext.Name.IsEqual("overwatch") && (PluginDatabase.GameContext.Source?.Name?.IsEqual("battle.net") ?? false))
                                    {
                                        ViewExtension = new SuccessStoryOverwatchView(GameMenu);
                                    }
                                    else if (PluginSettings.Settings.EnableGenshinImpact && GameMenu.Name.IsEqual("Genshin Impact"))
                                    {
                                        ViewExtension = new SuccessStoryGenshinImpactView(GameMenu);
                                    }
                                    else 
                                    {
                                        ViewExtension = new SuccessStoryOneGameView(GameMenu);
                                    }

                                    windowOptions.ShowMaximizeButton = false;
                                }
                                else
                                {
                                    windowOptions.Width = 1280;
                                    windowOptions.Height = 740;

                                    ViewExtension = new SuccessView(false, GameMenu);
                                }

                                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSuccessStory"), ViewExtension, windowOptions);
                                if (windowOptions.ShowMaximizeButton)
                                {
                                    windowExtension.ResizeMode = ResizeMode.CanResize;
                                }
                                windowExtension.ShowDialog();
                                PluginDatabase.IsViewOpen = false;
                            }
                        });

                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = resources.GetString("LOCSuccessStory"),
                            Description = "-"
                        });
                    }

                    if (!gameAchievements.IsManual || (gameAchievements.IsManual && gameAchievements.HasData))
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = resources.GetString("LOCSuccessStory"),
                            Description = resources.GetString("LOCCommonRefreshGameData"),
                            Action = (gameMenuItem) =>
                            {
                                IsFromMenu = true;

                                if (Ids.Count == 1)
                                {
                                    PluginDatabase.Refresh(GameMenu.Id);
                                }
                                else
                                {
                                    PluginDatabase.Refresh(Ids);
                                }
                            }
                        });
                    }

                    if (!gameAchievements.IsManual)
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = resources.GetString("LOCSuccessStory"),
                            Description = resources.GetString("LOCSuccessStoryIgnored"),
                            Action = (mainMenuItem) =>
                            {
                                PluginDatabase.SetIgnored(gameAchievements);
                            }
                        });
                    }
                }

                if (PluginSettings.Settings.EnableManual && !GameMenu.Name.IsEqual("Genshin Impact"))
                {
                    if (!gameAchievements.HasData)
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = resources.GetString("LOCSuccessStory"),
                            Description = resources.GetString("LOCAddTitle"),
                            Action = (mainMenuItem) =>
                            {
                                PluginDatabase.Remove(GameMenu);
                                PluginDatabase.GetManual(GameMenu);
                            }
                        });
                    }
                    else if (gameAchievements.IsManual)
                    {
                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = resources.GetString("LOCSuccessStory"),
                            Description = resources.GetString("LOCEditGame"),
                            Action = (mainMenuItem) =>
                            {
                                var ViewExtension = new SuccessStoryEditManual(GameMenu);
                                Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSuccessStory"), ViewExtension);
                                windowExtension.ShowDialog();
                            }
                        });

                        gameMenuItems.Add(new GameMenuItem
                        {
                            MenuSection = resources.GetString("LOCSuccessStory"),
                            Description = resources.GetString("LOCRemoveTitle"),
                            Action = (gameMenuItem) =>
                            {
                                var TaskIntegrationUI = Task.Run(() =>
                                {
                                    PluginDatabase.Remove(GameMenu);
                                });
                            }
                        });
                    }
                }

                if (GameMenu.Name.IsEqual("Genshin Impact"))
                {
                    if (PluginSettings.Settings.EnableGenshinImpact)
                    {
                        if (!gameAchievements.HasData)
                        {
                            gameMenuItems.Add(new GameMenuItem
                            {
                                MenuSection = resources.GetString("LOCSuccessStory"),
                                Description = resources.GetString("LOCAddGenshinImpact"),
                                Action = (mainMenuItem) =>
                                {
                                    PluginDatabase.Remove(GameMenu);
                                    PluginDatabase.GetGenshinImpact(GameMenu);
                                }
                            });
                        }
                        else
                        {
                            gameMenuItems.Add(new GameMenuItem
                            {
                                MenuSection = resources.GetString("LOCSuccessStory"),
                                Description = resources.GetString("LOCEditGame"),
                                Action = (mainMenuItem) =>
                                {
                                    var ViewExtension = new SuccessStoryEditManual(GameMenu);
                                    Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSuccessStory"), ViewExtension);
                                    windowExtension.ShowDialog();
                                }
                            });

                            gameMenuItems.Add(new GameMenuItem
                            {
                                MenuSection = resources.GetString("LOCSuccessStory"),
                                Description = resources.GetString("LOCRemoveTitle"),
                                Action = (gameMenuItem) =>
                                {
                                    var TaskIntegrationUI = Task.Run(() =>
                                    {
                                        PluginDatabase.Remove(GameMenu);
                                    });
                                }
                            });
                        }
                    }
                }

                if (achievementSource == SuccessStoryDatabase.AchievementSource.Local && gameAchievements.HasData && !gameAchievements.IsManual)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = resources.GetString("LOCSuccessStory"),
                        Description = resources.GetString("LOCCommonDeleteGameData"),
                        Action = (gameMenuItem) =>
                        {
                            var TaskIntegrationUI = Task.Run(() =>
                            {
                                PluginDatabase.Remove(GameMenu.Id);
                            });
                        }
                    });
                }
            }
            else
            {
                if (GameCouldHaveAchievements)
                {
                    gameMenuItems.Add(new GameMenuItem
                    {
                        MenuSection = resources.GetString("LOCSuccessStory"),
                        Description = resources.GetString("LOCSuccessStoryNotIgnored"),
                        Action = (mainMenuItem) =>
                        {
                            PluginDatabase.SetIgnored(gameAchievements);
                        }
                    });
                }
            }

#if DEBUG
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = resources.GetString("LOCSuccessStory"),
                Description = "-"
            });
            gameMenuItems.Add(new GameMenuItem
            {
                MenuSection = resources.GetString("LOCSuccessStory"),
                Description = "Test",
                Action = (mainMenuItem) => 
                {
                    
                }
            });
#endif
            return gameMenuItems;
        }

        // To add new main menu items override GetMainMenuItems
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            string MenuInExtensions = string.Empty;
            if (PluginSettings.Settings.MenuInExtensions)
            {
                MenuInExtensions = "@";
            }

            List<MainMenuItem> mainMenuItems = new List<MainMenuItem>
            {
                // Show list achievements for all games
                new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = resources.GetString("LOCSuccessStoryViewGames"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.IsViewOpen = true;
                        var ViewExtension = new SuccessView();

                        var windowOptions = new WindowOptions
                        {
                            ShowMinimizeButton = false,
                            ShowMaximizeButton = true,
                            ShowCloseButton = true,
                            Width = 1280,
                            Height = 740
                        };

                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSuccessStory"), ViewExtension, windowOptions);
                        windowExtension.ResizeMode = ResizeMode.CanResize;
                        windowExtension.ShowDialog();
                        PluginDatabase.IsViewOpen = false;
                    }
                }
            };

            if (PluginSettings.Settings.EnableRetroAchievementsView && PluginSettings.Settings.EnableRetroAchievements)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = resources.GetString("LOCSuccessStoryViewGames") + " - RetroAchievements",
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.IsViewOpen = true;
                        SuccessView ViewExtension = null;
                        if (PluginSettings.Settings.EnableRetroAchievementsView && PlayniteTools.IsGameEmulated(PluginDatabase.GameContext))
                        {
                            ViewExtension = new SuccessView(true, PluginDatabase.GameContext);
                        }
                        else
                        {
                            ViewExtension = new SuccessView(false, PluginDatabase.GameContext);
                        }
                        ViewExtension.Width = 1280;
                        ViewExtension.Height = 740;

                        Window windowExtension = PlayniteUiHelper.CreateExtensionWindow(PlayniteApi, resources.GetString("LOCSuccessStory"), ViewExtension);
                        windowExtension.ShowDialog();
                        PluginDatabase.IsViewOpen = false;
                    }
                });
            }

            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                Description = "-"
            });

            // Download missing data for all game in database
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                Description = resources.GetString("LOCCommonDownloadPluginData"),
                Action = (mainMenuItem) =>
                {
                    IsFromMenu = true;
                    PluginDatabase.GetSelectData();
                    IsFromMenu = false;
                }
            });

            if (PluginDatabase.PluginSettings.Settings.EnableManual)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = "-"
                });

                // Refresh rarity data for manual achievements
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = resources.GetString("LOCSsRefreshRaretyManual"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.RefreshRarety();
                    }
                });

                // Refresh estimate time data for manual achievements
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = resources.GetString("LOCSsRefreshEstimateTimeManual"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.RefreshEstimateTime();
                    }
                });
            }

            if (PluginDatabase.PluginSettings.Settings.EnableTag)
            {
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = "-"
                });

                // Add tag for selected game in database if data exists
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = resources.GetString("LOCCommonAddTPlugin"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.AddTagSelectData();
                    }
                });
                // Add tag for all games
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = resources.GetString("LOCCommonAddAllTags"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.AddTagAllGame();
                    }
                });
                // Remove tag for all game in database
                mainMenuItems.Add(new MainMenuItem
                {
                    MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                    Description = resources.GetString("LOCCommonRemoveAllTags"),
                    Action = (mainMenuItem) =>
                    {
                        PluginDatabase.RemoveTagAllGame();
                    }
                });
            }


#if DEBUG
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                Description = "-"
            });
            mainMenuItems.Add(new MainMenuItem
            {
                MenuSection = MenuInExtensions + resources.GetString("LOCSuccessStory"),
                Description = "Test",
                Action = (mainMenuItem) => 
                {

                }
            });
#endif

            return mainMenuItems;
        }
        #endregion


        #region Game event
        public override void OnGameSelected(OnGameSelectedEventArgs args)
        {
            // TODO Old database
            if (oldToNew.IsOld)
            {
                oldToNew.ConvertDB(PlayniteApi);
            }

            // TODO Sourcelink
            var sourceLinkNull = PluginDatabase.Database?.Select(x => x)
                                    .Where(x => x.SourcesLink == null && x.IsManual && x.HasAchievements && PlayniteApi.Database.Games.Get(x.Id) != null);
            if (sourceLinkNull?.Count() > 0)
            {
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                    $"{PluginDatabase.PluginName} - Database migration",
                    false
                );
                globalProgressOptions.IsIndeterminate = true;

                PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                {
                    CommonPluginsStores.SteamApi steamApi = new CommonPluginsStores.SteamApi();

                    foreach (GameAchievements gameAchievements in sourceLinkNull)
                    {
                        try
                        {
                            Game game = PlayniteApi.Database.Games.Get(gameAchievements.Id);
                            if (game == null)
                            {
                                break;
                            }

                            string SourceName = PlayniteTools.GetSourceName(game);

                            if (gameAchievements.IsManual)
                            {                                
                                int AppId = steamApi.GetSteamId(gameAchievements.Name);

                                if (AppId != 0)
                                {
                                    gameAchievements.SourcesLink = new SourceLink
                                    {
                                        GameName = steamApi.GetGameName(AppId),
                                        Name = "Steam",
                                        Url = $"https://steamcommunity.com/stats/{AppId}/achievements"
                                    };
                                }
                                else
                                {
                                    gameAchievements.SourcesLink = null;
                                }
                            }
                            else
                            {
                                // TODO Refresh by user ?
                            }

                            PluginDatabase.AddOrUpdate(gameAchievements);
                        }
                        catch (Exception ex)
                        {
                            Common.LogError(ex, false, true, PluginDatabase.PluginName);
                        }
                    }
                }, globalProgressOptions);
            }

            // TODO Moving cache
            if (Directory.Exists(Path.Combine(PlaynitePaths.ImagesCachePath, PluginDatabase.PluginName)))
            {
                GlobalProgressOptions globalProgressOptions = new GlobalProgressOptions(
                    $"{PluginDatabase.PluginName} - Folder migration",
                    false
                );
                globalProgressOptions.IsIndeterminate = true;

                PlayniteApi.Dialogs.ActivateGlobalProgress((activateGlobalProgress) =>
                {
                    try
                    {
                        FileSystem.DeleteDirectory(PluginDatabase.Paths.PluginCachePath);
                        Directory.Move(Path.Combine(PlaynitePaths.ImagesCachePath, PluginDatabase.PluginName), PluginDatabase.Paths.PluginCachePath);
                    }
                    catch (Exception ex)
                    {
                        Common.LogError(ex, false, true, PluginDatabase.PluginName);
                    }
                }, globalProgressOptions);
            }

            try
            {
                if (args.NewValue?.Count == 1 && PluginDatabase.IsLoaded)
                {
                    PluginDatabase.GameContext = args.NewValue[0];
                    PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                }
                else
                {
                    Task.Run(() =>
                    {
                        System.Threading.SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);

                        Application.Current.Dispatcher.BeginInvoke((Action)delegate
                        {
                            if (args.NewValue?.Count == 1)
                            {
                                PluginDatabase.GameContext = args.NewValue[0];
                                PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                            }
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, true, PluginDatabase.PluginName);
            }
        }

        // Add code to be executed when game is finished installing.
        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {

        }

        // Add code to be executed when game is uninstalled.
        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            TaskIsPaused = true;
        }

        // Add code to be executed when game is started running.
        public override void OnGameStarted(OnGameStartedEventArgs args)
        {

        }

        // Add code to be executed when game is preparing to be started.
        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            TaskIsPaused = false;

            // Refresh Achievements database for game played.
            var TaskGameStopped = Task.Run(() =>
            {
                string SourceName = PlayniteTools.GetSourceName(args.Game);
                string GameName = args.Game.Name;
                bool VerifToAddOrShow = SuccessStoryDatabase.VerifToAddOrShow(this, PlayniteApi, PluginSettings.Settings, args.Game);
                GameAchievements gameAchievements = PluginDatabase.Get(args.Game, true);

                IsFromMenu = false;

                if (!gameAchievements.IsIgnored)
                {
                    if (VerifToAddOrShow)
                    {
                        if (!gameAchievements.IsManual)
                        {
                            PluginDatabase.RefreshNoLoader(args.Game.Id);


                            // Set to Beaten
                            if (PluginSettings.Settings.Auto100PercentCompleted && PluginSettings.Settings.CompletionStatus100Percent != null)
                            {
                                gameAchievements = PluginDatabase.Get(args.Game, true);

                                if (gameAchievements.Is100Percent)
                                {
                                    args.Game.CompletionStatusId = PluginSettings.Settings.CompletionStatus100Percent.Id;
                                    PlayniteApi.Database.Games.Update(args.Game);
                                }
                            }
                        }
                    }
                }


                // refresh themes resources
                if (args.Game.Id == PluginDatabase.GameContext.Id)
                {
                    PluginDatabase.SetThemesResources(PluginDatabase.GameContext);
                }
            });
        }
        #endregion  


        #region Application event
        // Add code to be executed when Playnite is initialized.
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Cache images
            if (PluginSettings.Settings.EnableImageCache)
            {
                CancellationToken ct = tokenSource.Token;
                var TaskCacheImage = Task.Run(() =>
                {
                    // Wait Playnite & extension database are loaded
                    System.Threading.SpinWait.SpinUntil(() => PlayniteApi.Database.IsOpen, -1);
                    System.Threading.SpinWait.SpinUntil(() => PluginDatabase.IsLoaded, -1);

                    var db = PluginDatabase.Database.Where(x => x.HasAchievements && !x.ImageIsCached);
#if DEBUG
                    Common.LogDebug(true, $"TaskCacheImage - {db.Count()} - Start");
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
#endif
                    
                    foreach (GameAchievements gameAchievements in db)
                    {
                        Common.LogDebug(true, $"TaskCacheImage - {gameAchievements.Name} - {gameAchievements.Items.Count}");
                        foreach (var achievement in gameAchievements.Items)
                        {
                            while (TaskIsPaused)
                            {
                                Thread.Sleep(100); 
                            }

                            try
                            {
                                if (achievement.UrlUnlocked.Contains("rpcs3", StringComparison.InvariantCultureIgnoreCase) && PlayniteTools.GetCacheFile(achievement.CacheUnlocked, PluginDatabase.PluginName).IsNullOrEmpty())
                                {
                                    string PathFile = Path.Combine(PluginDatabase.Paths.PluginUserDataPath, achievement.UrlUnlocked);
                                    string PathImageFileName = Path.Combine(PlaynitePaths.DataCachePath, PluginDatabase.PluginName, achievement.CacheUnlocked);
                                    if (File.Exists(PathFile))
                                    {
                                        FileSystem.CopyFile(PathFile, PathImageFileName);
                                    }
                                }

                                if (!achievement.UrlLocked.IsNullOrEmpty() && PlayniteTools.GetCacheFile(achievement.CacheLocked, PluginDatabase.PluginName).IsNullOrEmpty())
                                {
                                    Common.LogDebug(true, $"TaskCacheImage.DownloadFileImage - {gameAchievements.Name} - GetCacheFile({achievement.Name}" + "_Locked)");
                                    Web.DownloadFileImage(achievement.CacheLocked, achievement.UrlLocked, PlaynitePaths.DataCachePath, PluginDatabase.PluginName).GetAwaiter().GetResult();
                                }

                                if (ct.IsCancellationRequested)
                                {
                                    break;
                                }
                                
                                if (PlayniteTools.GetCacheFile(achievement.CacheUnlocked, PluginDatabase.PluginName).IsNullOrEmpty())
                                {
                                    Common.LogDebug(true, $"TaskCacheImage.DownloadFileImage - {gameAchievements.Name} - GetCacheFile({achievement.Name}" + "_Unlocked)");
                                    Web.DownloadFileImage(achievement.CacheUnlocked, achievement.UrlUnlocked, PlaynitePaths.DataCachePath, PluginDatabase.PluginName).GetAwaiter().GetResult();
                                }

                                if (ct.IsCancellationRequested)
                                {
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Common.LogError(ex, true, $"Error on TaskCacheImage");
                            }
                        }

                        if (ct.IsCancellationRequested)
                        {
                            logger.Info($"TaskCacheImage - IsCancellationRequested");
                            break;
                        }
                    }

#if DEBUG
                    stopwatch.Stop();
                    TimeSpan ts = stopwatch.Elapsed;
                    Common.LogDebug(true, $"TaskCacheImage() - End - {string.Format("{0:00}:{1:00}.{2:00}", ts.Minutes, ts.Seconds, ts.Milliseconds / 10)}");
#endif
                }, tokenSource.Token);
            }


            // QuickSearch support
            try
            {
                string icon = Path.Combine(PluginDatabase.Paths.PluginPath, "Resources", "star.png");

                var SsSubItemsAction = new SubItemsAction() { Action = () => { }, Name = "", CloseAfterExecute = false, SubItemSource = new QuickSearchItemSource() };
                var SsCommand = new CommandItem(PluginDatabase.PluginName, new List<CommandAction>(), ResourceProvider.GetString("LOCSsQuickSearchDescription"), icon);
                SsCommand.Keys.Add(new CommandItemKey() { Key = "ss", Weight = 1 });
                SsCommand.Actions.Add(SsSubItemsAction);
                QuickSearch.QuickSearchSDK.AddCommand(SsCommand);
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false);
            }
        }

        // Add code to be executed when Playnite is shutting down.
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            tokenSource.Cancel();
        }
        #endregion


        // Add code to be executed when library is updated.
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (PluginSettings.Settings.AutoImport)
            {
                var PlayniteDb = PlayniteApi.Database.Games
                        .Where(x => x.Added != null && x.Added > PluginSettings.Settings.LastAutoLibUpdateAssetsDownload)
                        .Select(x => x.Id).ToList();

                PluginDatabase.Refresh(PlayniteDb);

                PluginSettings.Settings.LastAutoLibUpdateAssetsDownload = DateTime.Now;
                SavePluginSettings(PluginSettings.Settings);
            }
        }


        #region Settings
        public override ISettings GetSettings(bool firstRunSettings)
        {
            return PluginSettings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SuccessStorySettingsView(this);
        }
        #endregion
    }
}
