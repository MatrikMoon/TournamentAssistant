using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using TournamentAssistantServer.Database.Contexts;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistantServer
{
    class AuthorizationManager
    {
        private UserDatabaseContext _userDatabaseContext;
        private X509Certificate2 _serverCert;
        private X509Certificate2 _pluginCert;

        public AuthorizationManager(UserDatabaseContext userDatabaseContext, X509Certificate2 serverCert, X509Certificate2 pluginCert)
        {
            _userDatabaseContext = userDatabaseContext;
            _serverCert = serverCert;
            _pluginCert = pluginCert;
        }

        public string GenerateToken(User user)
        {
            if (user.discord_info == null)
            {
                throw new ArgumentException("User must have had their Discord info populated before having a token generated");
            }

            // Create the signing credentials with the certificate
            var signingCredentials = new X509SigningCredentials(_serverCert);

            // Create a list of claims for the token payload
            var claims = new[]
            {
                new Claim("sub", user.Guid),
                new Claim("name", user.Name),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                new Claim("exp", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString()),
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

        public bool VerifyUser(string token, out User user)
        {
            //Empty tokens are definitely not valid
            if (string.IsNullOrWhiteSpace(token))
            {
                user = null;
                return false;
            }

            var eitherSucceeded = VerifyAsPlayer(token, out user) || VerifyAsWebsocket(token, out user);

            if (!eitherSucceeded)
            {
                Logger.Error($"Both validation methods failed.");
            }

            return eitherSucceeded;
        }

        private bool VerifyAsWebsocket(string token, out User user)
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
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                user = new User
                {
                    Guid = claims.First(x => x.Type == "sub").Value,
                    Name = claims.First(x => x.Type == "name").Value,
                    ClientType = User.ClientTypes.WebsocketConnection,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = claims.First(x => x.Type == "ta:discord_id").Value,
                        Username = claims.First(x => x.Type == "ta:discord_name").Value,
                        AvatarUrl = claims.First(x => x.Type == "ta:discord_avatar").Value
                    }
                };

                return true;
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to validate token as websocket:");
                Logger.Error(e.Message);
            }

            user = null;
            return false;
        }

        private bool VerifyAsPlayer(string token, out User user)
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
                    Guid = claims.First(x => x.Type == "sub").Value,
                    Name = claims.First(x => x.Type == "name").Value,
                    ClientType = User.ClientTypes.Player,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = claims.First(x => x.Type == "ta:discord_id").Value,
                        Username = claims.First(x => x.Type == "ta:discord_name").Value,
                        AvatarUrl = claims.First(x => x.Type == "ta:discord_avatar").Value
                    }
                };

                return true;
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to validate token as player:");
                Logger.Error(e.Message);
            }

            user = null;
            return false;
        }
    }
}
