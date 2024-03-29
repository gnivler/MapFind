﻿using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using SandBox.GauntletUI;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Engine.Screens;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem;

// ReSharper disable UnusedMember.Global    
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedType.Local  
// ReSharper disable InconsistentNaming

namespace MapFind
{
    public class SubModule : MBSubModuleBase
    {
        private static MapEncyclopediaView mapEncyclopediaView;
        private static GauntletClanScreen gauntletClanScreen;
        private readonly Harmony harmony = new Harmony("ca.gnivler.bannerlord.MapFind");

        protected override void OnSubModuleLoad()
        {
            Log("Startup " + DateTime.Now.ToShortTimeString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private static void Log(object input)
        {
            //FileLog.Log($"[MapFind] {input ?? "null"}");
        }

        private static void SetCameraToSettlement(string stringID)
        {
            try
            {
                Log($"SetCameraToSettlement({stringID})");
                CloseAnyOpenWindows();
                MapScreen.Instance.FastMoveCameraToPosition(Settlement.Find(stringID).Position2D);
                Traverse.Create(MapScreen.Instance).Method("UpdateMapCamera").GetValue();
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        private static void CloseAnyOpenWindows()
        {
            if (mapEncyclopediaView.IsEncyclopediaOpen)
            {
                mapEncyclopediaView.CloseEncyclopedia();
            }

            if (gauntletClanScreen != null && gauntletClanScreen.IsActive)
            {
                Traverse.Create(gauntletClanScreen).Method("CloseClanScreen").GetValue();
            }
        }

        private static string ConvertLinkToSettlementName(string linkString)
        {
            var match = Regex.Match(linkString, @"Settlement-(.*)""><b>", RegexOptions.CultureInvariant);

            var settlementName = match.Groups[1].Value;
            return settlementName;
        }


        [HarmonyPatch(typeof(EncyclopediaNavigatorVM), "ExecuteLink")]
        public static class EncyclopediaConceptPageVMExecuteLinkPatch
        {
            private static bool Prefix(object target)
            {
                try
                {
                    Log($"EncyclopediaNavigatorVM.ExecuteLink({target})");
                    var shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
                    if (shift)
                    {
                        string linkName = default;
                        var type = target.GetType().Name;
                        switch (type)
                        {
                            case "Settlement":
                            {
                                linkName = ((Settlement)target).StringId;
                                break;
                            }
                            case "Hero":
                            {
                                linkName = ((Hero)target).LastSeenPlace.StringId;
                                break;
                            }
                            case "Kingdom":
                            {
                                linkName = ((Kingdom)target).Leader.LastSeenPlace.StringId;
                                break;
                            }
                            case "Clan":
                            {
                                linkName = ((Clan)target).Leader.LastSeenPlace.StringId;
                                break;
                            }
                        }

                        SetCameraToSettlement(linkName);
                        return false;
                    }
                }

                catch (Exception ex)
                {
                    Log(ex);
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(MapEncyclopediaView), MethodType.Constructor)]
        public static class MapEncyclopediaViewCtorPatch
        {
            private static void Postfix(MapEncyclopediaView __instance)
            {
                try
                {
                    Log("MapEncyclopediaView");
                    mapEncyclopediaView = __instance;
                }
                catch (Exception ex)
                {
                    Log(ex);
                }
            }
        }

        [HarmonyPatch(typeof(GauntletClanScreen), MethodType.Constructor, typeof(ClanState))]
        public static class GauntletClanScreenCtorPatch
        {
            private static void Postfix(GauntletClanScreen __instance)
            {
                try
                {
                    Log("GauntletClanScreenCtorPatch");
                    gauntletClanScreen = __instance;
                }
                catch (Exception ex)
                {
                    Log(ex);
                }
            }
        }

        // shift click settlement to move there and camera-follow
        // ctrl-shift click to move
        // unmodified click is "centre camera" (return true)
        [HarmonyPatch(typeof(SettlementNameplateVM), "ExecuteSetCameraPosition")]
        public static class SettlementNameplateVMExecuteSetCameraPositionPatch
        {
            public static bool Prefix(SettlementNameplateVM __instance)
            {
                try
                {
                    if (Traverse.Create(MapScreen.Instance).Field("_mapState").GetValue<MapState>().AtMenu) return true;

                    var shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
                    if (shift)
                    {
                        var ctrl = Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
                        MobileParty.MainParty.SetMoveGoToSettlement(__instance.Settlement);
                        Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppableFastForward;

                        if (ctrl)
                        {
                            MapScreen.Instance.CurrentCameraFollowMode = MapScreen.CameraFollowMode.Free;
                        }
                        else
                        {
                            MapScreen.Instance.CurrentCameraFollowMode = MapScreen.CameraFollowMode.FollowParty;
                        }

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log(ex);
                }

                return true;
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            // thanks Cheyron for the fix
            if (!(ScreenManager.TopScreen is MapScreen))
            {
                return;
            }

            if ((Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl))
                &&
                Input.IsKeyPressed(InputKey.F12))
            {
                var textInquiryData = new TextInquiryData(
                    new TextObject("Enter settlement name").ToString(),
                    "",
                    true,
                    true,
                    "Find",
                    "Cancel",
                    GoToSettlementByString,
                    null);
                InformationManager.ShowTextInquiry(textInquiryData, true);
            }
        }

        private static void GoToSettlementByString(string name)
        {
            var matches = Settlement.FindAll(x =>
                x.Name.ToString().ToLower().StartsWith(name)).ToList();
            if (matches.Count == 0)
            {
                return;
            }

            // prefer an exact match over a substring
            var settlement = matches.Any(x => x.ToString().Length == name.Length)
                ? matches.First(x => x.ToString().Length == name.Length)
                : matches.OrderByDescending(x => x.ToString().Length).First();

            SetCameraToSettlement(settlement.StringId);
        }
    }
}
