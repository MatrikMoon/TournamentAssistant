using TournamentAssistantShared.SimpleJSON;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace TournamentAssistantCore.Discord.Services
{
    public class PictureService
    {
        public enum NekoType
        {
            Neko,
            NekoLewd,
            NekoGif,
            NekoLewdGif,
            Hentai,
            HentaiGif,
            HentaiSmall
        }

        private readonly HttpClient _http;

        public PictureService(HttpClient http) => _http = http;

        public async Task<Stream> GetCatPictureAsync()
        {
            var resp = await _http.GetAsync("https://cataas.com/cat");
            return await resp.Content.ReadAsStreamAsync();
        }

        public async Task<string> GetNekoPictureAsync()
        {
            return await GetStringFromNekosApi("https://nekos.life/api/v2/img/neko");
        }

        public async Task<string> GetNekoLewdPictureAsync()
        {
            return await GetStringFromNekosApi("https://nekos.life/api/v2/img/lewd");
        }

        public async Task<string> GetNekoGifAsync()
        {
            return await GetStringFromNekosApi("https://nekos.life/api/v2/img/ngif");
        }

        public async Task<string> GetNekoLewdGifAsync()
        {
            return await GetStringFromNekosApi("https://nekos.life/api/v2/img/nsfw_neko_gif");
        }

        public async Task<string> GetLewdPictureAsync()
        {
            return await GetStringFromNekosApi("https://nekos.life/api/v2/img/classic");
        }

        public async Task<string> GetLewdGifAsync()
        {
            return await GetStringFromNekosApi("https://nekos.life/api/v2/img/Random_hentai_gif");
        }

        public async Task<string> GetStringFromNekosApi(string apiCall)
        {
            var resp = await _http.GetAsync(apiCall);
            var stringResp = await resp.Content.ReadAsStringAsync();

            JSONNode node = JSON.Parse(WebUtility.UrlDecode(stringResp));

            return node["url"];
        }

        public async Task<Stream> GetNekoStreamAsync(NekoType type)
        {
            string url = null;

            switch (type)
            {
                case NekoType.Neko:
                    url = await GetNekoPictureAsync();
                    break;
                case NekoType.NekoLewd:
                    url = await GetNekoLewdPictureAsync();
                    break;
                case NekoType.NekoGif:
                    url = await GetNekoGifAsync();
                    break;
                case NekoType.NekoLewdGif:
                    url = await GetNekoLewdGifAsync();
                    break;
                case NekoType.Hentai:
                    url = await GetLewdPictureAsync();
                    break;
                case NekoType.HentaiGif:
                    url = await GetLewdGifAsync();
                    break;
            }

            var pic = await _http.GetAsync(url);
            return await pic.Content.ReadAsStreamAsync();
        }
    }
}
