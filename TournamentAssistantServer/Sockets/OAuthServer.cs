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
using System.Web;

namespace TournamentAssistantServer.Sockets
{
    class OAuthServer
    {
        public event Func<User.DiscordInfo, string, Task> AuthorizeRecieved;

        private string _oauthUrl;
        private string _clientId;
        private string _clientSecret;
        private int _oauthPort;
        private HttpListener _httpListener = new HttpListener();
        private CancellationTokenSource _cancellationToken = new CancellationTokenSource();
        private AuthorizationService _authorizationManager;

        public OAuthServer(AuthorizationService authorizationManager, string serverAddress, int port, string clientId, string clientSecret)
        {
            _authorizationManager = authorizationManager;
            _oauthPort = port;

            _httpListener.Prefixes.Add($"http://*:{port}/");

            _clientId = clientId;
            _clientSecret = clientSecret;
            _oauthUrl = $"https://discord.com/api/oauth2/authorize?client_id={clientId}&redirect_uri=http%3A%2F%2F{serverAddress}%3A{port}&response_type=code&scope=identify";
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
            return $"{_oauthUrl}&state={_authorizationManager.SignString(userId)}";
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

                    Logger.Success($"REQUEST: {httpListenerContext.Request.RawUrl}");
                    Logger.Success($"CODE: {code}");
                    Logger.Success($"STATE: {state}");

                    var parameters = HttpUtility.ParseQueryString(string.Empty);
                    parameters["client_id"] = _clientId;
                    parameters["client_secret"] = _clientSecret;
                    parameters["code"] = code;
                    parameters["grant_type"] = "authorization_code";
                    parameters["redirect_uri"] = $"http://{Constants.MASTER_SERVER}:{_oauthPort}";
                    parameters["scope"] = "identify";

                    using (var client = new HttpClient())
                    {
                        var content = new StringContent(parameters.ToString());
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                        
                        var postResponse = await client.PostAsync("https://discord.com/api/oauth2/token", content, _cancellationToken.Token);

                        var responseJson = JSON.Parse(await postResponse.Content.ReadAsStringAsync());

                        Logger.Success($"PostAuthTokenResponse: {responseJson}");

                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(responseJson["token_type"], responseJson["access_token"]);

                        var getResponse = await client.GetAsync("https://discord.com/api/users/@me", _cancellationToken.Token);
                        responseJson = JSON.Parse(await getResponse.Content.ReadAsStringAsync());

                        Logger.Success($"GetMeResponse: {responseJson}");

                        var userGuid = state.Split(",")[0];
                        var userGuidSignature = state.Split(",")[1];

                        if (!_authorizationManager.VerifyString(userGuid, userGuidSignature))
                        {
                            throw new Exception("Failed to verify userGuid signature");
                        }

                        if (AuthorizeRecieved != null) await AuthorizeRecieved.Invoke(new User.DiscordInfo
                        {
                            UserId = responseJson["id"],
                            Username = $"{responseJson["username"].Value}",
                            AvatarUrl = responseJson["avatar"],
                        }, userGuid);
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
