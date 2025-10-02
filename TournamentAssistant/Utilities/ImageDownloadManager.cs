using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TournamentAssistant.Utilities
{
    public static class ImageDownloadManager
    {
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(4);

        /// <summary>
        /// Enqueues a texture download. Limits concurrent downloads.
        /// </summary>
        public static async Task<Texture2D> DownloadTexture(string url)
        {
            await semaphore.WaitAsync();
            try
            {
                return await LoadTexture(url);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static async Task<Texture2D> LoadTexture(string url)
        {
            using var request = UnityWebRequestTexture.GetTexture(url);
            var asyncOp = request.SendWebRequest();

            while (!asyncOp.isDone)
            {
                await Task.Yield();
            }

            // #if UNITY_2020_1_OR_NEWER
#if true
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError(request.error);
                return null;
            }

            return DownloadHandlerTexture.GetContent(request);
        }
    }
}