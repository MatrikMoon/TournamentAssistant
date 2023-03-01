using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.SimpleJSON;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;

namespace TournamentAssistantCore.Sockets
{
    class OAuthServer
    {
        private string oauthUrl;

        public event Func<User.DiscordInfo, string, Task> AuthorizeRecieved;

        private string clientId;
        private string clientSecret;
        private int oauthPort;
        private HttpListener _httpListener = new HttpListener();
        private CancellationTokenSource _cancellationToken = new CancellationTokenSource();

        public OAuthServer(string serverAddress, int port, string clientId, string clientSecret)
        {
            oauthPort = port;

            _httpListener.Prefixes.Add($"http://*:{port}/");

            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.oauthUrl = $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri=http%3A%2F%2F{serverAddress}%3A{port}&response_type=code&scope=identify";
        }

        public void Start()
        {
            _httpListener.Start();
            Task.Run(HttpAccept);
        }

        public void Stop()
        {
            _cancellationToken.Cancel();
        }

        public string GetOAuthUrl(string userId)
        {
            return $"{oauthUrl}&state={Convert.ToBase64String(Encoding.UTF8.GetBytes(userId))}";
        }

        private async Task HttpAccept()
        {
            while (!_cancellationToken.Token.IsCancellationRequested)
            {
                try
                {
                    var httpListenerContext = await _httpListener.GetContextAsync();
                    var code = httpListenerContext.Request.QueryString.Get("code");
                    var state = httpListenerContext.Request.QueryString.Get("state");

                    Logger.Success($"CODE: {code}");
                    Logger.Success($"STATE: {state}");
                    Logger.Success($"STATE STRING: {Encoding.UTF8.GetString(Convert.FromBase64String(state))}");

                    var parameters = new Dictionary<string, string>();
                    parameters["client_id"] = clientId;
                    parameters["client_secret"] = clientSecret;
                    parameters["code"] = code;
                    parameters["grant_type"] = "authorization_code";
                    parameters["redirect_uri"] = $"http://{Constants.MASTER_SERVER}:{oauthPort}";
                    parameters["scope"] = "identify";

                    var body = QueryHelpers.AddQueryString("", parameters)[1..];

                    using (var client = new HttpClient())
                    {
                        var content = new StringContent(body);
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                        var postResponse = await client.PostAsync("https://discord.com/api/oauth2/token", content, _cancellationToken.Token);

                        var responseJson = JSON.Parse(await postResponse.Content.ReadAsStringAsync());

                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(responseJson["token_type"], responseJson["access_token"]);

                        var getResponse = await client.GetAsync("https://discord.com/api/users/@me", _cancellationToken.Token);
                        responseJson = JSON.Parse(await getResponse.Content.ReadAsStringAsync());

                        Logger.Success(JSON.Parse(await getResponse.Content.ReadAsStringAsync()).ToString());

                        if (AuthorizeRecieved != null) await AuthorizeRecieved.Invoke(new User.DiscordInfo
                        {
                            UserId = responseJson["id"],
                            Username = $"{responseJson["username"].Value}#{responseJson["discriminator"].Value}",
                            AvatarUrl = responseJson["avatar"],
                        }, Encoding.UTF8.GetString(Convert.FromBase64String(state)));
                    }

                    var response = httpListenerContext.Response;
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentType = "text/html";
                    response.OutputStream.Write(Encoding.UTF8.GetBytes("<script>window.close()</script>\r\n"));
                    response.OutputStream.Close();
                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                    Logger.Error(e.StackTrace);
                }
            }
        }
    }
}
