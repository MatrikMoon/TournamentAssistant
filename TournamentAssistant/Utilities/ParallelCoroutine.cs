using System.Collections;
using System.Linq;
using TournamentAssistant.Behaviors;
using UnityEngine;

namespace TournamentAssistant.Utilities
{
    public class ParallelCoroutine
    {
        private int count;

        public IEnumerator ExecuteCoroutines(params IEnumerator[] coroutines)
        {
            count = coroutines.Length;
            coroutines.ToList().ForEach(x => SharedCoroutineStarter.instance.StartCoroutine(DoParallel(x)));
            yield return new WaitUntil(() => count == 0);
        }

        IEnumerator DoParallel(IEnumerator coroutine)
        {
            yield return SharedCoroutineStarter.instance.StartCoroutine(coroutine);
            count--;
        }
    }
}
