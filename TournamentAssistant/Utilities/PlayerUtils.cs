using Oculus.Platform;
using Oculus.Platform.Models;
using Steamworks;
using System;
using System.Linq;
using UnityEngine;

namespace TournamentAssistant.Utilities
{
    public class PlayerUtils
    {
        public static void GetPlatformUserData(Action<string, ulong> usernameResolved)
        {
            if (PersistentSingleton<VRPlatformHelper>.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.OpenVR || Environment.CommandLine.Contains("-vrmode oculus"))
            {
                GetSteamUser(usernameResolved);
            }
            else if (PersistentSingleton<VRPlatformHelper>.instance.vrPlatformSDK == VRPlatformHelper.VRPlatformSDK.Oculus)
            {
                GetOculusUser(usernameResolved);
            }
            else GetSteamUser(usernameResolved);
        }

        private static void GetSteamUser(Action<string, ulong> usernameResolved)
        {
            if (SteamManager.Initialized)
            {
                usernameResolved?.Invoke(SteamFriends.GetPersonaName(), Steamworks.SteamUser.GetSteamID().m_SteamID);
            }
        }

        private static void GetOculusUser(Action<string, ulong> usernameResolved)
        {
            Users.GetLoggedInUser().OnComplete((Message<User> msg) =>
            {
                if (!msg.IsError)
                {
                    usernameResolved?.Invoke(msg.Data.OculusID, msg.Data.ID);
                }
            });
        }

        public static void ReturnToMenu()
        {
            Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault()?.PopScenes(0.35f);
        }
    }
}
