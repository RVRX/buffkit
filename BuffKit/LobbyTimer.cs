using System;
using System.Collections;
using MuseBase.Multiplayer.Unity;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace BuffKit
{
    public class LobbyTimer : MonoBehaviour
    {
        private static readonly int count = 8;
        private static readonly int period = 30;
        private static readonly int overtimeDuration = 60;
        private int _secondsLeft = count * period;
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

                if (!FirstStart && _active)
                {
                    MuseWorldClient.Instance.ChatHandler.TrySendMessage("TIMER: Timer restarting!", "match");
                }

                if (FirstStart && _active)
                {
                    MuseWorldClient.Instance.ChatHandler.TrySendMessage("TIMER: Timer starting!", "match");
                    FirstStart = false;
                }
            }
        }

        public void Run()
        {
            StartCoroutine(Count());
        }


        private IEnumerator Count()
        {
            while (_secondsLeft >= 0)
            {
                if (!Active)
                {
                    if (!FirstStart)
                    {
                        MuseWorldClient.Instance.ChatHandler.TrySendMessage("TIMER: Timer paused!", "match");
                    }

                    yield return new WaitUntil(() => Active);
                }

                if (_secondsLeft % period == 0)
                {
                    var t = TimeSpan.FromSeconds(_secondsLeft);
                    var f = $"{t.Minutes:D1}:{t.Seconds:D2}";
                    MuseWorldClient.Instance.ChatHandler.TrySendMessage($"TIMER: {f} remaining", "match");
                }

                if (_secondsLeft == 0 && !Overtime)
                {
                    MuseWorldClient.Instance.ChatHandler.TrySendMessage(
                        "TIMER: Timer is over, please ready up or request overtime", "match");
                    Overtime = true;
                    _active = false;
                    FirstStart = true;
                    BuffKit.CoolerFooterButtons();
                    yield return new WaitUntil(() => Active);
                    _secondsLeft = overtimeDuration + 1;
                }

                if (_secondsLeft == 0 && Overtime)
                {
                    MuseWorldClient.Instance.ChatHandler.TrySendMessage("TIMER: Overtime is over, please ready up",
                        "match");
                    _active = false;
                    Overtime = false;
                    FirstStart = true;
                    BuffKit.CoolerFooterButtons();
                    yield break;
                }

                _secondsLeft--;
                yield return new WaitForSeconds(1);
            }
        }
    }
}