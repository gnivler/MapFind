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

// ReSharper disable UnusedMember.Global    
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedType.Local  
// ReSharper disable InconsistentNaming

namespace MapFind
{
    public class Mod : MBSubModuleBase
    {
        private const LogLevel level = LogLevel.Disabled;
        private static GauntletEncyclopediaScreenManager gauntletEncyclopediaScreenManager;
        private static GauntletClanScreen gauntletClanScreen;
        private readonly Harmony harmony = new Harmony("ca.gnivler.bannerlord.MapFind");

        protected override void OnSubModuleLoad()
        {
            //Harmony.DEBUG = true;
            Log("Startup " + DateTime.Now.ToShortTimeString(), LogLevel.Info);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            //Harmony.DEBUG = false;
        }

        private static void Log(object input, LogLevel logLevel)
        {
            if (level <= logLevel) FileLog.Log($"[MapFind] {input ?? "null"}");
        }

        private static void SetCameraToSettlement(string stringID)
        {
            try
            {
                Log($"SetCameraToSettlement({stringID})", LogLevel.Debug);
                if (gauntletEncyclopediaScreenManager.IsEncyclopediaOpen) gauntletEncyclopediaScreenManager.CloseEncyclopedia();

                if (gauntletClanScreen.IsActive) Traverse.Create(gauntletClanScreen).Method("CloseClanScreen").GetValue();

                MapScreen.Instance.SetMapCameraPosition(Settlement.Find(stringID).Position2D);
                Traverse.Create(MapScreen.Instance).Method("UpdateMapCamera").GetValue();
            }
            catch (Exception ex)
            {
                Log(ex, LogLevel.Error);
            }
        }

        private static string ConvertLinkToSettlementName(string linkString)
        {
            var match = Regex.Match(linkString, @"Settlement-(.*)""><b>", RegexOptions.CultureInvariant);

            var settlementName = match.Groups[1].Value;
            return settlementName;
        }

        private enum LogLevel
        {
            Debug,
            Error,
            Info,
            Disabled
        }

        [HarmonyPatch(typeof(EncyclopediaNavigatorVM), "ExecuteLink")]
        public static class EncyclopediaConceptPageVMExecuteLinkPatch
        {
            private static bool Prefix(object target)
            {
                try
                {
                    Log($"EncyclopediaNavigatorVM.ExecuteLink({target})", LogLevel.Debug);
                    var shift = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
                    if (shift)
                    {
                        string linkName = default;
                        var type = target.GetType().Name;
                        switch (type)
                        {
                            case "Settlement":
                            {
                                linkName = ((Settlement) target).StringId;
                                break;
                            }
                            case "Hero":
                            {
                                linkName = ((Hero) target).LastSeenPlace.StringId;
                                break;
                            }
                            case "Kingdom":
                            {
                                linkName = ((Kingdom) target).Leader.LastSeenPlace.StringId;
                                break;
                            }
                            case "Clan":
                            {
                                linkName = ((Clan) target).Leader.LastSeenPlace.StringId;
                                break;
                            }
                        }

                        SetCameraToSettlement(linkName);
                        return false;
                    }
                }

                catch (Exception ex)
                {
                    Log(ex, LogLevel.Error);
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
                    Log("GauntletEncyclopediaScreenManagerCtorPatch", LogLevel.Debug);
                    gauntletEncyclopediaScreenManager = __instance;
                }
                catch (Exception ex)
                {
                    Log(ex, LogLevel.Error);
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
                    Log("GauntletClanScreenCtorPatch", LogLevel.Debug);
                    gauntletClanScreen = __instance;
                }
                catch (Exception ex)
                {
                    Log(ex, LogLevel.Error);
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
                        MapScreen.Instance.CameraFollowMode = !ctrl;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Log(ex, LogLevel.Error);
                }

                return true;
            }
        }
    }
}
