using System;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using SandBox.GauntletUI;
using SandBox.View.Map;
using SandBox.ViewModelCollection.Nameplate;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

// ReSharper disable InconsistentNaming

namespace MapFind
{
    public class Mod : MBSubModuleBase
    {
        private readonly Harmony harmony = new Harmony("ca.gnivler.bannerlord.MapFind");
        private static GauntletEncyclopediaScreenManager gauntletEncyclopediaScreenManager;
        private static bool shouldClose;

        protected override void OnSubModuleLoad()
        {
            //Harmony.DEBUG = true;
            Log("Startup " + DateTime.Now.ToShortTimeString());
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            //Harmony.DEBUG = false;
        }

        private static void Log(object input)
        {
            //FileLog.Log($"[MapFind] {input ?? "null"}");
        }

        private static void SetCameraToSettlement(string stringID)
        {
            Log($"SetCameraToSettlement({stringID})");
            Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
            gauntletEncyclopediaScreenManager.CloseEncyclopedia();
            MapScreen.Instance.SetMapCameraPosition(Settlement.Find(stringID).Position2D);
            Traverse.Create(MapScreen.Instance).Method("UpdateMapCamera").GetValue();
        }

        private static string ConvertLinkToSettlementName(string linkString)
        {
            var match = Regex.Match(linkString, @"Settlement-(.*)""><b>", RegexOptions.CultureInvariant);
            var settlementName = match.Groups[1].Value;
            return settlementName;
        }


        [HarmonyPatch(typeof(GauntletEncyclopediaScreenManager), "OnTick")]
        public static class GauntletEncyclopediaScreenManagerOnTickPatch
        {
            static void Postfix()
            {
                try
                {
                    if (shouldClose)
                    {
                        gauntletEncyclopediaScreenManager.CloseEncyclopedia();
                        shouldClose = false;
                    }
                }
                catch (Exception e)
                {
                    Log(e);
                }
            }
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
                        string linkName = "";
                        if (target is Settlement settlement)
                        {
                            linkName = settlement.StringId;
                        }
                        
                        if (target is Hero hero)
                        {
                            linkName = hero.LastSeenPlace.StringId;
                            //linkName = ((Hero) target).LastSeenPlace.StringId;
                        }

                        if (target is Kingdom kingdom)
                        {
                            linkName = kingdom.Leader.LastSeenPlace.StringId;
                        }

                        if (target is Clan clan)
                        {
                            linkName = clan.Leader.LastSeenPlace.StringId;
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

        [HarmonyPatch(typeof(GauntletEncyclopediaScreenManager), MethodType.Constructor)]
        public static class GauntletEncyclopediaScreenManagerCtorPatch
        {
            private static void Postfix(GauntletEncyclopediaScreenManager __instance)
            {
                try
                {
                    gauntletEncyclopediaScreenManager = __instance;
                }
                catch (Exception e)
                {
                    Log(e);
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
                    if (Traverse.Create(MapScreen.Instance).Field("_mapState").GetValue<MapState>().AtMenu)
                    {
                        return true;
                    }

                    var shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
                    if (shift)
                    {
                        var ctrl = Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
                        MobileParty.MainParty.SetMoveGoToSettlement(__instance.Settlement);
                        Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppableFastForward;
                        MapScreen.Instance.CameraFollowMode = !ctrl;
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

        //[HarmonyPatch(typeof(MapScreen), "HandleLeftMouseButtonClick")]
        //public static class MapScreenHandleLeftMouseButtonClickPatch
        //{
        //    private static void Postfix()
        //    {
        //        try
        //        {
        //            // avoids time jumping ahead a tick if a menu is open
        //            //if (MobileParty.MainParty.CurrentSettlement == null)
        //            var shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
        //            if (
        //                Campaign.Current.TimeControlMode != CampaignTimeControlMode.StoppableFastForward &&
        //                !shift)
        //            {
        //                Campaign.Current.TimeControlMode = CampaignTimeControlMode.StoppableFastForward;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            Log(ex);
        //        }
        //    }
        //}
    }
}
