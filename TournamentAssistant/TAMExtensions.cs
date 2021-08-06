using IPA.Utilities.Async;
using System;
using System.Threading.Tasks;

namespace TournamentAssistant
{
    internal static class TAMExtensions
    {
        public static void RunMainHeadless(this Task task)
        {
            Task.Run(() =>
            {
                try
                {
                    return task;
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                    return Task.CompletedTask;
                }
            });
        }

        public static async Task RunMain(this Task task)
        {
            Exception? asyncException = null;
            var mainTask = await UnityMainThreadTaskScheduler.Factory.StartNew(async () =>
            {
                try
                {
                    await task;
                }
                catch (Exception e)
                {
                    asyncException = e;
                }
            });
            if (asyncException is not null)
                throw asyncException;
        }

        public static async Task<T> RunMain<T>(this Task<T> task)
        {
            T? value = default;
            Exception? asyncException = null;
            var mainTask = await UnityMainThreadTaskScheduler.Factory.StartNew(async () =>
            {
                try
                {
                    value = await task;
                }
                catch (Exception e)
                {
                    asyncException = e;
                }
            });
            if (asyncException is not null)
                throw asyncException;
            return value!;
        }
    }
}