using System;
using System.Collections;
using UnityEngine;
using static BuffKit.Util;

namespace BuffKit
{
    public class LobbyTimer : MonoBehaviour
    {
        private const int Interval = 30;
        private const int MainDuration = 210;
        private const int OvertimeDuration = 60;
        private const int LoadoutSetupDuration = 30;
        private const int PreLockAnnouncementTime = 30;
        private const int LockAnnouncementTime = 0;
        private int _secondsLeft = MainDuration;
        private bool _active = false;
        public bool FirstStart = true;
        public State CurrentState = State.Main;
        
        public enum State
        {
            Main,
            LoadoutSetup,
            Overtime,
            OvertimeLoadoutSetup
        }

        public bool Active
        {
            get => _active;
            set
            {
                if (_active && value) return;
                _active = value;
                if (!_active) return;

                var t = TimeSpan.FromSeconds(_secondsLeft);
                var f = $"{t.Minutes:D1}:{t.Seconds:D2}";
                if (FirstStart)
                {
                    if (CurrentState == State.Main)
                        TrySendMessage($"TIMER: Timer starting, {f} remaining, you will have {LoadoutSetupDuration} seconds to set up your loadouts or request overtime after the timer ends!", "match");
                    else if (CurrentState == State.Overtime)
                        TrySendMessage($"TIMER: Overtime starting, {f} remaining, you will have {LoadoutSetupDuration} seconds to set up your loadouts after the timer ends!", "match");

                    FirstStart = false;
                }
                else
                {
                    TrySendMessage($"TIMER: Timer resuming, {f} remaining!", "match");
                }
            }
        }

        public void Run()
        {
            StartCoroutine(RunTimer());
        }

        public void StartOvertime()
        {
            CurrentState = State.Overtime;
            _secondsLeft = OvertimeDuration;
            FirstStart = true;
            //Dirty hack to make sure it's announced
            Active = false;
            Active = true;
        }


        private IEnumerator RunTimer()
        {
            while (_secondsLeft >= 0)
            {
                var t = TimeSpan.FromSeconds(_secondsLeft);
                var f = $"{t.Minutes:D1}:{t.Seconds:D2}";
                if (!Active)
                {
                    if (!FirstStart)
                    {
                        TrySendMessage($"TIMER: Timer paused, {f} remaining!!", "match");
                    }

                    yield return new WaitUntil(() => Active);
                }

                if (_secondsLeft > 0)
                {
                    if (CurrentState == State.Main && _secondsLeft == MainDuration) {}
                    else if (CurrentState == State.Overtime && _secondsLeft == OvertimeDuration) {}
                    else if (CurrentState == State.LoadoutSetup || CurrentState == State.OvertimeLoadoutSetup) {}
                    else if (_secondsLeft == PreLockAnnouncementTime)
                    {
                        TrySendMessage($"TIMER: {f} remaining, ships will be locked in {(PreLockAnnouncementTime - LockAnnouncementTime).ToString()} seconds", "match");
                    }
                    else if (_secondsLeft % Interval == 0)
                    {
                        TrySendMessage($"TIMER: {f} remaining", "match");
                    }
                    
                    _secondsLeft--;
                    yield return new WaitForSeconds(1);
                    continue;
                }
                //Timer ended, which state are we in?

                switch (CurrentState)
                {
                    case State.Main:
                        TrySendMessage($"TIMER: Ships are locked, you have {LoadoutSetupDuration} seconds to set up your loadouts or request overtime", "match");
                        
                        CurrentState = State.LoadoutSetup;
                        FirstStart = false;
                    
                        BuffKit.PaintFooterButtons();
                        _secondsLeft = LoadoutSetupDuration;
                        
                        continue;
                    case State.LoadoutSetup:
                        TrySendMessage("TIMER: Setup time is over, the referee will either start the game or add overtime now", "match");
                        
                        FirstStart = true;
                        _active = false;
                        
                        BuffKit.PaintFooterButtons();
                        yield return new WaitUntil(() => Active);
                        
                        continue;
                    case State.Overtime:
                        TrySendMessage($"TIMER: Overtime is over, ships are locked, you have {LoadoutSetupDuration} seconds to set up your loadouts and ready up", "match");
                    
                        CurrentState = State.OvertimeLoadoutSetup;
                        _secondsLeft = LoadoutSetupDuration;
                        BuffKit.PaintFooterButtons();
                        continue;
                    case State.OvertimeLoadoutSetup:
                        TrySendMessage("TIMER: Lobby time is over, the ref will start the game now", "match");
                        
                        FirstStart = false;
                        _active = false;
                        
                        BuffKit.PaintFooterButtons();
                        yield break;
                }
            }
        }
    }
}