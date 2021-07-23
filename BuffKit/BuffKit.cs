using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Muse.Common;
using Muse.Goi2.Entity;
using static UIManager;

namespace BuffKit
{
    [BepInPlugin("me.trgk.buffkit", "Buff Kit", "0.0.1")]
    public class BuffKit : BaseUnityPlugin
    {
        public static LobbyTimer _lobbyTimer;

        void Awake()
        {
            var log = BepInEx.Logging.Logger.CreateLogSource("buffkit");
            var harmony = new Harmony("BuffKit");

            var asm = Assembly.GetAssembly(typeof(UIManager));

            var mmsType = asm.GetTypes().First(t => t.Name.Equals("UIMatchMenuState"));
            var mlsType = asm.GetTypes().First(t => t.Name.Equals("UINewMatchLobbyState"));

            var modFeaturesOriginal = mmsType.GetMethods().First(m => m.Name.Equals("ModFeatures"));
            var modFeaturesPatch = typeof(BuffKit).GetMethods().First(m => m.Name.Equals("UINewMod"));
            harmony.Patch(modFeaturesOriginal, new HarmonyMethod(modFeaturesPatch));

            var paintButtonsOriginal = mlsType.GetMethods().First(m => m.Name.Equals("PaintFooterButtons"));
            var paintButtonsPatch = typeof(BuffKit).GetMethods().First(m => m.Name.Equals("CoolerFooterButtons"));
            harmony.Patch(paintButtonsOriginal, new HarmonyMethod(paintButtonsPatch));

            log.LogInfo("Buff applied!");
        }

        public static bool UINewMod()
        {
            TransitionToState(UIModMenuState.instance);
            return false;
        }

        public static bool CoolerFooterButtons()
        {
            var mlv = MatchLobbyView.Instance;
            if (mlv != null && NetworkedPlayer.Local != null)
            {
                var footer = UIPageFrame.Instance.footer;
                footer.ClearButtons();
                var a = "START";
                if (mlv.Moderated && NetworkedPlayer.Local.Privilege >= UserPrivilege.Referee ||
                    NetworkedPlayer.Local.Privilege.IsModerator())
                {
                    if (_lobbyTimer == null || _lobbyTimer.FirstStart)
                    {
                        a = "START";
                    }
                    else if (_lobbyTimer.Active)
                    {
                        a = "PAUSE";
                    }
                    else if (!_lobbyTimer.Active)
                    {
                        a = "RESUME";
                    }

                    var b = _lobbyTimer == null ? "TIMER" : (_lobbyTimer.Overtime ? "OVERTIME" : "TIMER");
                    footer.AddButton($"{a} {b}", delegate
                    {
                        if (_lobbyTimer == null)
                        {
                            _lobbyTimer = UIPageFrame.Instance.footer.gameObject.AddComponent<LobbyTimer>();
                            _lobbyTimer.Run();
                        }

                        _lobbyTimer.Active = !_lobbyTimer.Active;
                        CoolerFooterButtons();
                    });

                    footer.AddButton("CHANGE MAP", delegate { NewMapAct(); });

                    footer.AddButton("MOD MATCH", delegate { UINewMatchLobbyState.instance.ModFeatures(); });
                }
            }

            return false;
        }

        private static bool NewMapAct()
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

        public static bool Nop()
        {
            return false;
        }
    }
}