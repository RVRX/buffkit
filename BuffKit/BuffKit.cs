using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Muse.Common;
using Muse.Goi2.Entity;
using static BuffKit.Util;
using static UIManager;

namespace BuffKit
{
    [BepInPlugin("me.trgk.buffkit", "Buff Kit", "0.0.1")]
    public class BuffKit : BaseUnityPlugin
    {
        void Awake()
        {
            var log = BepInEx.Logging.Logger.CreateLogSource("buffkit");
            var harmony = new Harmony("BuffKit");

            var asm = Assembly.GetAssembly(typeof(UIManager));

            var mmsType = asm.GetTypes().First(t => t.Name.Equals("UIMatchMenuState"));
            var mlsType = asm.GetTypes().First(t => t.Name.Equals("UINewMatchLobbyState"));

            var modFeaturesOriginal = mmsType.GetMethods().First(m => m.Name.Equals("ModFeatures"));
            var modFeaturesPatch = typeof(BuffKit).GetMethods().First(m => m.Name.Equals("ModFeatures"));
            harmony.Patch(modFeaturesOriginal, new HarmonyMethod(modFeaturesPatch));

            var paintButtonsOriginal = mlsType.GetMethods().First(m => m.Name.Equals("PaintFooterButtons"));
            var paintButtonsPatch = typeof(BuffKit).GetMethods().First(m => m.Name.Equals("PaintFooterButtons"));
            harmony.Patch(paintButtonsOriginal, new HarmonyMethod(paintButtonsPatch));

            log.LogInfo("Buff applied!");
        }

        public static bool ModFeatures()
        {
            TransitionToState(UIModMenuState.Instance);
            return false;
        }

        public static bool PaintFooterButtons()
        {
            var mlv = MatchLobbyView.Instance;
            if (mlv == null || NetworkedPlayer.Local == null) return false;
            
            var footer = UIPageFrame.Instance.footer;
            footer.ClearButtons();

            if (!HasModPrivilege(mlv)) return false;
            
            var lobbyTimer = mlv.gameObject.GetComponent<LobbyTimer>();
                    
            var a = "START";
            if (lobbyTimer != null && !lobbyTimer.FirstStart) a = lobbyTimer.Active ? "PAUSE" : "RESUME";

            var b = "TIMER";
            if (lobbyTimer != null && lobbyTimer.Overtime) b = "OVERTIME";
                    
            footer.AddButton($"{a} {b}", delegate
            {
                if (lobbyTimer == null)
                {
                    lobbyTimer = mlv.gameObject.AddComponent<LobbyTimer>();
                    lobbyTimer.Run();
                }

                lobbyTimer.Active = !lobbyTimer.Active;
                PaintFooterButtons();
            });
            footer.AddButton("CHANGE MAP", delegate { PaintMapPicker(); });
            footer.AddButton("MOD MATCH", delegate { UINewMatchLobbyState.instance.ModFeatures(); });

            return false;
        }

        private static bool PaintMapPicker()
        {
            var comparer = new IntArrayEqualityComparer();
            var mlv = MatchLobbyView.Instance;
            var maps = CachedRepository
                .Instance
                .GetBy(
                    (Region r) =>
                        r.Public && r.GameMode.GetGameType() == mlv.Map.GameMode.GetGameType() &&
                        comparer.Equals(r.NonEmptyTeamSize, mlv.Map.NonEmptyTeamSize)
                )
                .OrderBy(m => m.GetLocalizedName())
                .Where(m => m.GameMode == RegionGameMode.TEAM_MELEE)
                .ToArray();

            UINewModalDialog.Select("Select Map", "Current: " + mlv.Map.GetLocalizedName(),
                UINewModalDialog.DropdownSetting.CreateSetting(maps,
                    r => "{0} ({1})".F(r.GetLocalizedName(), r.GameMode.GetString())), delegate(int index)
                {
                    if (index >= 0)
                    {
                        LobbyActions.ChangeMap(maps[index].Id);
                    }
                });
            
            return false;
        }
    }
}