using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Web;
using TournamentAssistantServer.Database.Contexts;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Sockets;

namespace TournamentAssistantServer
{
    class AuthorizationService
    {
        private UserDatabaseContext _userDatabaseContext;
        private X509Certificate2 _serverCert;
        private X509Certificate2 _pluginCert;

        public AuthorizationService(UserDatabaseContext userDatabaseContext, X509Certificate2 serverCert, X509Certificate2 pluginCert)
        {
            _userDatabaseContext = userDatabaseContext;
            _serverCert = serverCert;
            _pluginCert = pluginCert;
        }

        public string SignString(string targetString)
        {
            using var rsa = _serverCert.GetRSAPrivateKey();
            var signedBytes = rsa.SignData(Encoding.UTF8.GetBytes(targetString), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{targetString},{HttpUtility.UrlEncode(Convert.ToBase64String(signedBytes))}";
        }

        public bool VerifyString(string targetString, string signature)
        {
            // Convert the signed string from Base64 to bytes
            var messageBytes = Encoding.UTF8.GetBytes(targetString);
            var signatureBytes = Convert.FromBase64String(signature);

            using var rsa = _serverCert.GetRSAPublicKey();

            return rsa.VerifyData(messageBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        public string GenerateWebsocketToken(User user)
        {
            if (user.discord_info == null)
            {
                throw new ArgumentException("User must have had their Discord info populated before having a token generated");
            }

            // Create the signing credentials with the certificate
            var signingCredentials = new X509SigningCredentials(_serverCert);

            var expClaim = new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(100).ToUnixTimeSeconds().ToString());

            if (user.discord_info.UserId == "331280652320112641" || user.discord_info.UserId == "229408465787944970")
            {
                expClaim = new Claim("exp", DateTimeOffset.UtcNow.AddMonths(1).ToUnixTimeSeconds().ToString());
            }

            // Create a list of claims for the token payload
            var claims = new[]
            {
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                expClaim,
                new Claim("ta:discord_id", user.discord_info.UserId),
                new Claim("ta:discord_name", user.discord_info.Username),
                new Claim("ta:discord_avatar", user.discord_info.AvatarUrl),
            };

            // Create the JWT token with the claims and signing credentials
            var token = new JwtSecurityToken(
                issuer: "ta_server",
                audience: "ta_users",
                claims: claims,
                signingCredentials: signingCredentials
            );

            // Create a JWT token handler and serialize the token to a string
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public bool VerifyUser(string token, ConnectedUser socketUser, out User user)
        {
            //Empty tokens are definitely not valid
            if (string.IsNullOrWhiteSpace(token))
            {
                user = null;
                return false;
            }

            var eitherSucceeded = VerifyAsPlayer(token, socketUser, out user) || VerifyAsWebsocket(token, socketUser, out user);

            if (!eitherSucceeded)
            {
                Logger.Error($"Both validation methods failed.");
            }

            return eitherSucceeded;
        }

        private bool VerifyAsWebsocket(string token, ConnectedUser socketUser, out User user)
        {
            try
            {
                // Create a token validation parameters object with the signing credentials
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "ta_server",
                    ValidAudience = "ta_users",
                    IssuerSigningKey = new X509SecurityKey(_serverCert),
                };

                // Verify the token and extract the claims
                IdentityModelEventSource.ShowPII = true;
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                user = new User
                {
                    Guid = socketUser.id.ToString(),
                    ClientType = User.ClientTypes.WebsocketConnection,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = claims.First(x => x.Type == "ta:discord_id").Value,
                        Username = claims.First(x => x.Type == "ta:discord_name").Value,
                        AvatarUrl = $"https://cdn.discordapp.com/avatars/{claims.First(x => x.Type == "ta:discord_id").Value}/{claims.First(x => x.Type == "ta:discord_avatar").Value}.png"
                    }
                };

                return true;
            }
            catch (Exception e)
            {
                //Logger.Error($"Failed to validate token as websocket:");
                //Logger.Error(e.Message);
            }

            user = null;
            return false;
        }

        private bool VerifyAsPlayer(string token, ConnectedUser socketUser, out User user)
        {
            try
            {
                // Create a token validation parameters object with the signing credentials
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "ta_plugin",
                    ValidAudience = "ta_users",
                    IssuerSigningKey = new X509SecurityKey(_pluginCert),
                };

                // Verify the token and extract the claims
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                user = new User
                {
                    Guid = socketUser.id.ToString(),
                    Name = claims.First(x => x.Type == "ta:platform_username").Value,
                    PlatformId = claims.First(x => x.Type == "ta:platform_id").Value,
                    ClientType = User.ClientTypes.Player,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = claims.First(x => x.Type == "ta:discord_id").Value,
                        Username = claims.First(x => x.Type == "ta:discord_name").Value,
                        AvatarUrl = $"https://cdn.discordapp.com/avatars/{claims.First(x => x.Type == "ta:discord_id").Value}/{claims.First(x => x.Type == "ta:discord_avatar").Value}.png"
                    }
                };

                return true;
            }
            catch (Exception e)
            {
                //Logger.Error($"Failed to validate token as player:");
                //Logger.Error(e.Message);
            }

            user = null;
            return false;
        }
    }
}
