using System;
using System.Collections;
using System.Linq;
using TournamentAssistant.Utilities;
using UnityEngine;

namespace TournamentAssistant.Behaviors
{
    class AntiPause : MonoBehaviour
    {
        public static AntiPause Instance { get; set; }

        private StandardLevelGameplayManager standardLevelGameplayManager;

        void Awake()
        {
            Instance = this;

            DontDestroyOnLoad(this); //Will actually be destroyed when the main game scene is loaded again, but unfortunately this 
                                     //object is created before the game scene loads, so we need to do this to prevent the game scene
                                     //load from destroying it

            standardLevelGameplayManager = Resources.FindObjectsOfTypeAll<StandardLevelGameplayManager>().First();

            StartCoroutine(DoOnLevelStart());
        }

        public IEnumerator DoOnLevelStart()
        {
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<StandardLevelGameplayManager.GameState>("_gameState") == StandardLevelGameplayManager.GameState.Playing);
            yield return new WaitUntil(() => standardLevelGameplayManager.GetField<PauseController>("_pauseController").GetProperty<bool>("canPause"));

            var pauseController = standardLevelGameplayManager.GetField<PauseController>("_pauseController");
            pauseController.canPauseEvent -= standardLevelGameplayManager.HandlePauseControllerCanPause;
            pauseController.canPauseEvent += HandlePauseControllerCanPause_AlwaysFalse;

            if (Plugin.UseSync)
            {
                new GameObject("SyncController").AddComponent<SyncHandler>();
                Plugin.UseSync = false;
            }
        }

        public static void HandlePauseControllerCanPause_AlwaysFalse(Action<bool> canPause)
        {
            canPause?.Invoke(false);
        }

        public static void Destroy() => Destroy(Instance);

        void OnDestroy() => Instance = null;
    }
}
