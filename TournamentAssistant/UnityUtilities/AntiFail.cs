using HarmonyLib;
using IPA.Utilities;
using System.Linq;
using UnityEngine;
using TournamentAssistantShared.Utilities;
using Logger = TournamentAssistantShared.Logger;

/**
 * Created by Moon on 6/13/2020
 * ...and then subsequently left empty until 6/16/2020, 2:40 AM
 */

namespace TournamentAssistant.UnityUtilities
{
    public class AntiFail
    {
        static readonly Harmony _harmony = new("TA:AntiFail");

        static bool _allowFail = true;
        static bool _wouldHaveFailed = false;
        static bool _forceFail = false;

        public static bool AllowFail
        {
            get { return _allowFail; }
            set
            {
                if (value == _allowFail)
                {
                    return;
                }

                if (value)
                {
                    Logger.Info($"Harmony unpatching {nameof(StandardLevelGameplayManager)}.HandleSongDidFinish");
                    _harmony.Unpatch(
                        AccessTools.Method(typeof(StandardLevelGameplayManager), "HandleSongDidFinish"),
                        AccessTools.Method(typeof(AntiFail), nameof(SongDidFinishEvent))
                    );

                    Logger.Info($"Harmony unpatching {nameof(StandardLevelGameplayManager)}.HandleGameEnergyDidReach0");
                    _harmony.Unpatch(
                        AccessTools.Method(typeof(StandardLevelGameplayManager), "HandleGameEnergyDidReach0"),
                        AccessTools.Method(typeof(AntiFail), nameof(HandleGameEnergyDidReach0))
                    );
                }
                else
                {
                    _wouldHaveFailed = false;

                    Logger.Info($"Harmony patching {nameof(StandardLevelGameplayManager)}.HandleSongDidFinish");
                    _harmony.Patch(
                        AccessTools.Method(typeof(StandardLevelGameplayManager), "HandleSongDidFinish"),
                        new HarmonyMethod(AccessTools.Method(typeof(AntiFail), nameof(SongDidFinishEvent)))
                    );

                    Logger.Info($"Harmony patching {nameof(StandardLevelGameplayManager)}.HandleGameEnergyDidReach0");
                    _harmony.Patch(
                        AccessTools.Method(typeof(StandardLevelGameplayManager), "HandleGameEnergyDidReach0"),
                        new HarmonyMethod(AccessTools.Method(typeof(AntiFail), nameof(HandleGameEnergyDidReach0)))
                    );
                }
                _allowFail = value;
            }
        }

        static bool HandleGameEnergyDidReach0()
        {
            _wouldHaveFailed = true;
            Logger.Debug($"HandleGameEnergyDidReach0: {AllowFail}");
            return AllowFail || _forceFail;
        }

        static bool SongDidFinishEvent()
        {
            Logger.Debug($"SongDidFinishEvent: {_wouldHaveFailed}");

            if (_wouldHaveFailed)
            {
                _forceFail = true;
                try
                {
                    var standardLevelGameplayManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();
                    standardLevelGameplayManager.InvokeMethod("HandleGameEnergyDidReach0");
                }
                finally
                {
                    _forceFail = false;
                }
            }

            return !_wouldHaveFailed;
        }
    }
}