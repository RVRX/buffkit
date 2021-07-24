using System;
using System.Collections;
using UnityEngine;
using static BuffKit.Util;

namespace BuffKit
{
    public class LobbyTimer : MonoBehaviour
    {
        private const int Count = 8;
        private const int Period = 30;
        private const int OvertimeDuration = 60;
        private int _secondsLeft = Count * Period;
        private bool _active = false;
        public bool FirstStart = true;
        public bool Overtime = false;

        public bool Active
        {
            get => _active;
            set
            {
                if (_active && value) return;
                _active = value;
                if (!_active) return;

                if (FirstStart)
                {
                    TrySendMessage("TIMER: Timer starting!", "match");
                    FirstStart = false;
                    
                }
                else
                {
                    TrySendMessage("TIMER: Timer restarting!", "match");
                }
            }
        }

        public void Run()
        {
            StartCoroutine(RunTimer());
        }


        private IEnumerator RunTimer()
        {
            while (_secondsLeft >= 0)
            {
                if (!Active)
                {
                    if (!FirstStart)
                    {
                        TrySendMessage("TIMER: Timer paused!", "match");
                    }

                    yield return new WaitUntil(() => Active);
                }

                if (_secondsLeft % Period == 0)
                {
                    var t = TimeSpan.FromSeconds(_secondsLeft);
                    var f = $"{t.Minutes:D1}:{t.Seconds:D2}";
                    TrySendMessage($"TIMER: {f} remaining", "match");
                }

                if (_secondsLeft > 0)
                {
                    _secondsLeft--;
                    yield return new WaitForSeconds(1);
                    continue;
                }

                if (!Overtime)
                {
                    TrySendMessage("TIMER: Timer is over, please ready up or request overtime", "match");
                    
                    Overtime = true;
                    FirstStart = true;
                    _active = false;
                    
                    BuffKit.PaintFooterButtons();
                    
                    yield return new WaitUntil(() => Active);
                    
                    _secondsLeft = OvertimeDuration;
                    continue;
                }

                if (Overtime)
                {
                    TrySendMessage("TIMER: Overtime is over, please ready up", "match");
                    
                    _active = false;
                    Overtime = false;
                    FirstStart = true;
                    
                    BuffKit.PaintFooterButtons();
                    
                    yield break;
                }
            }
        }
    }
}