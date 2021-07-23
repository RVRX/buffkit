using System;
using System.Linq;
using MuseBase.Multiplayer.Unity;

namespace BuffKit
{
    public class UIModMenuState : UIManager.UINewHeaderState
    {
        public override UIManager.UIState BackState => UIManager.overlayBack;
        public static UIModMenuState instance = new UIModMenuState();
        private UIManager.UIState _previous;
        private bool _needRepaint = false;

        public override void Enter(UIManager.UIState previous, UIManager.UIContext uiContext)
        {
            base.Enter(previous, uiContext);
            _previous = previous;
            MuseLog.Info(previous.ToString());
            UIMatchManager.Deactivate();
            UITutorialManager.Deactivate();
            UITutorialManager.GameMode = false;
            UIPageFrame.Instance.ShowLobbyChat();
            PaintMenu(BackState, uiContext);
        }

        public override void Update(UIManager.UIContext uiContext)
        {
            base.Update(uiContext);
            if (_needRepaint)
            {
                PaintMenu(BackState, uiContext);
                _needRepaint = false;
            }
        }

        public override void Exit(UIManager.UIState next, UIManager.UIContext uiContext)
        {
            UIScoreboard.Activated = false;
            UIPageFrame.Instance.header.Deactivate();
            base.Exit(next, uiContext);
        }

        public override void UIEventExit()
        {
            if (UIScoreboard.Activated)
            {
                UIScoreboard.Activated = false;
            }
            else
                base.UIEventExit();
        }

        private void PaintMenu(UIManager.UIState state, UIManager.UIContext context)
        {
            var mlv = MatchLobbyView.Instance;
            var msv = MatchStateView.Instance;
            var dm = UIPageFrame.Instance.navigationMenu;
            dm.Clear();
            var actions = (from action in MatchModAction.RunningMatchActions
                where action.CanAct(NetworkedPlayer.Local, mlv)
                select action).ToList();

            foreach (var action in actions)
            {
                if (action is TimerModAction)
                {
                    if (msv.ModCountdown > 0.0)
                    {
                        if (msv.ModCountdownSwitch)
                        {
                            dm.AddButton("Pause timer", String.Empty, UIMenuItem.Size.Small, false, false, delegate
                            {
                                _needRepaint = true;
                                MatchActions.PauseCountdown();
                                MuseWorldClient.Instance.ChatHandler.TrySendMessage("GAME PAUSED", "match");
                                UIManager.TransitionToState(state);
                            });
                        }
                        else
                        {
                            dm.AddButton("Resume timer", String.Empty, UIMenuItem.Size.Small, false, false, delegate
                            {
                                _needRepaint = true;
                                MatchActions.ExtendCountdown(0);
                                MuseWorldClient.Instance.ChatHandler.TrySendMessage("GAME RESUMED", "match");

                                UIManager.TransitionToState(state);
                            });
                        }

                        dm.AddButton("Stop the timer", String.Empty, UIMenuItem.Size.Small, false, false,
                            delegate
                            {
                                _needRepaint = true;
                                MatchActions.StopCountdown();
                                UIManager.TransitionToState(state);
                            });
                    }
                    else
                    {
                        dm.AddButton("Start the timer", String.Empty, UIMenuItem.Size.Small, false, false,
                            delegate
                            {
                                _needRepaint = true;
                                MatchActions.StartCountdown(1200);
                                MuseWorldClient.Instance.ChatHandler.TrySendMessage("TIMER STARTED", "match");
                                UIManager.TransitionToState(state);
                            });
                        dm.AddButton("Add overtime", String.Empty, UIMenuItem.Size.Small, false, false,
                            delegate
                            {
                                _needRepaint = true;
                                MatchActions.StartCountdown(180);
                                MuseWorldClient.Instance.ChatHandler.TrySendMessage("OVERTIME STARTED", "match");
                                UIManager.TransitionToState(state);
                            });
                    }
                }
                else
                {
                    dm.AddButton(action.Name(mlv), String.Empty, UIMenuItem.Size.Small, false, false,
                        delegate { action.Act(mlv); });
                }
            }
        }
    }
}