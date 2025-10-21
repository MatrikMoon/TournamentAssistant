using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;
using UnityEngine;
using UnityEngine.Networking;

namespace TournamentAssistant.Utilities
{
    public static class ImageDownloadManager
    {
        private static readonly string CACHE_FOLDER = $"{Environment.CurrentDirectory}/UserData/{Constants.NAME}/";
        private static readonly SemaphoreSlim semaphore = new SemaphoreSlim(4);

        /// <summary>
        /// Enqueues a texture download. Limits concurrent downloads.
        /// </summary>
        public static async Task<Texture2D> DownloadTexture(string url, string cacheKey)
        {
            await semaphore.WaitAsync();
            try
            {
                var cached = GetCached(cacheKey);
                if (cached != null)
                {
                    return cached;
                }

                return await LoadTexture(url, cacheKey);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static async Task<Texture2D> LoadTexture(string url, string cacheKey)
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

            var resultTexture = DownloadHandlerTexture.GetContent(request);

            CacheTexture(cacheKey, resultTexture);

            return DownloadHandlerTexture.GetContent(request);
        }

        private static void CacheTexture(string name, Texture2D texture)
        {
            // Make cache directory if it didn't exist
            Directory.CreateDirectory(CACHE_FOLDER);

            File.WriteAllBytes($"{CACHE_FOLDER}/{name}.png", texture.EncodeToPNG());
        }

        public static bool IsCached(string name)
        {
            return File.Exists($"{CACHE_FOLDER}/{name}.png");
        }

        public static Texture2D GetCached(string name)
        {
            var cached = IsCached(name);
            if (cached)
            {
                var rawData = System.IO.File.ReadAllBytes($"{CACHE_FOLDER}/{name}.png");
                var loaded = new Texture2D(2, 2);
                loaded.LoadImage(rawData);
                return loaded;
            }
            return null;
        }
    }
}