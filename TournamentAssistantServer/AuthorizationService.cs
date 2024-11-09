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
using TournamentAssistantServer.Database;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Sockets;

namespace TournamentAssistantServer
{
    public class AuthorizationService
    {
        private DatabaseService _databaseService;
        private X509Certificate2 _serverCert;
        private RsaSecurityKey _beatKhanaPublicKey;
        private X509Certificate2 _pluginCert;
        private X509Certificate2 _mockCert;

        public AuthorizationService(DatabaseService databaseService, X509Certificate2 serverCert, RsaSecurityKey beatKhanaPublicKey, X509Certificate2 pluginCert, X509Certificate2 mockCert)
        {
            _databaseService = databaseService;
            _serverCert = serverCert;
            _beatKhanaPublicKey = beatKhanaPublicKey;
            _pluginCert = pluginCert;
            _mockCert = mockCert;
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

        public string GenerateWebsocketToken(User user, bool isBotToken = false)
        {
            if (user.discord_info == null)
            {
                throw new ArgumentException("User must have had their Discord info populated before having a token generated");
            }

            // Create the signing credentials with the certificate
            var signingCredentials = new X509SigningCredentials(_serverCert);

            var expClaim = new Claim("exp", DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds().ToString());

            if (isBotToken)
            {
                expClaim = new Claim("exp", DateTimeOffset.UtcNow.AddYears(10).ToUnixTimeSeconds().ToString());
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
            // Empty tokens are definitely not valid
            if (string.IsNullOrWhiteSpace(token))
            {
                user = null;
                return false;
            }

            var eitherSucceeded = VerifyAsPlayer(token, socketUser, out user) || VerifyAsWebsocket(token, socketUser, out user) || VerifyBotTokenAsWebsocket(token, socketUser, out user) || VerifyBeatKhanaTokenAsWebsocket(token, socketUser, out user) || VerifyAsMockPlayer(token, socketUser, out user);

            if (!eitherSucceeded)
            {
                Logger.Error($"All validation methods failed.");
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
#if DEBUG
                    ClockSkew = TimeSpan.Zero
#endif
                };

                // Verify the token and extract the claims
                IdentityModelEventSource.ShowPII = true;
                IdentityModelEventSource.LogCompleteSecurityArtifact = true;
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                var discordId = claims.First(x => x.Type == "ta:discord_id").Value;
                var discordUsername = claims.First(x => x.Type == "ta:discord_name").Value;
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{claims.First(x => x.Type == "ta:discord_id").Value}/{claims.First(x => x.Type == "ta:discord_avatar").Value}.png";

                // If the discordId is a guid, this is a bot token and should be checked as such
                if (Guid.TryParse(discordId, out var _)) {
                    user = null;
                    return false;
                }

                if (!string.IsNullOrEmpty(discordId))
                {
                    avatarUrl = null;
                }

                user = new User
                {
                    Guid = socketUser.id.ToString(),
                    ClientType = User.ClientTypes.WebsocketConnection,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = discordId,
                        Username = discordUsername,
                        AvatarUrl = avatarUrl
                    }
                };

                return true;
            }
            catch (Exception)
            {
                //Logger.Error($"Failed to validate token as websocket:");
                //Logger.Error(e.Message);
            }

            user = null;
            return false;
        }

        private bool VerifyBotTokenAsWebsocket(string token, ConnectedUser socketUser, out User user)
        {
            // This validation will look very similar to the above, with the addition of checking
            // the bot token database to ensure this exact token exists and has not been revoked
            try
            {
                // Check that the token is in the token database
                var userDatabase = _databaseService.NewUserDatabaseContext();

                if (!userDatabase.TokenExists(token))
                {
                    user = null;
                    return false;
                }

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
#if DEBUG
                    ClockSkew = TimeSpan.Zero
#endif
                };

                // Verify the token and extract the claims
                IdentityModelEventSource.ShowPII = true;
                IdentityModelEventSource.LogCompleteSecurityArtifact = true;
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                var discordId = claims.First(x => x.Type == "ta:discord_id").Value;
                var discordUsername = claims.First(x => x.Type == "ta:discord_name").Value;

                user = new User
                {
                    Guid = socketUser.id.ToString(),
                    ClientType = User.ClientTypes.WebsocketConnection,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = discordId,
                        Username = discordUsername,
                    }
                };

                return true;
            }
            catch (Exception)
            {
                //Logger.Error($"Failed to validate token as websocket:");
                //Logger.Error(e.Message);
            }

            user = null;
            return false;
        }

        private bool VerifyBeatKhanaTokenAsWebsocket(string token, ConnectedUser socketUser, out User user)
        {
            try
            {
                // Create a token validation parameters object with the signing credentials
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false, // BK does not provide these fields
                    ValidateAudience = false, // BK does not provide these fields
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _beatKhanaPublicKey,
#if DEBUG
                    ClockSkew = TimeSpan.Zero
#endif
                };

                // Verify the token and extract the claims
                IdentityModelEventSource.ShowPII = true;
                IdentityModelEventSource.LogCompleteSecurityArtifact = true;
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                var discordId = claims.First(x => x.Type == "id").Value;
                var discordUsername = claims.First(x => x.Type == "username").Value;
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{claims.First(x => x.Type == "id").Value}/{claims.First(x => x.Type == "avatar").Value}.png";

                if (!string.IsNullOrEmpty(discordId))
                {
                    avatarUrl = null;
                }

                user = new User
                {
                    Guid = socketUser.id.ToString(),
                    ClientType = User.ClientTypes.WebsocketConnection,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = discordId,
                        Username = discordUsername,
                        AvatarUrl = avatarUrl
                    }
                };

                return true;
            }
            catch (Exception)
            {
                // Logger.Error($"Failed to validate token as websocket:");
                // Logger.Error(e.Message);
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
                IdentityModelEventSource.ShowPII = true;
                IdentityModelEventSource.LogCompleteSecurityArtifact = true;
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                var discordId = claims.First(x => x.Type == "ta:discord_id").Value;
                var discordUsername = claims.First(x => x.Type == "ta:discord_name").Value;
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{claims.First(x => x.Type == "ta:discord_id").Value}/{claims.First(x => x.Type == "ta:discord_avatar").Value}.png";

                if (!string.IsNullOrEmpty(discordId))
                {
                    avatarUrl = null;
                }

                user = new User
                {
                    Guid = socketUser.id.ToString(),
                    Name = claims.First(x => x.Type == "ta:platform_username").Value,
                    PlatformId = claims.First(x => x.Type == "ta:platform_id").Value,
                    ClientType = User.ClientTypes.Player,
                    discord_info = new User.DiscordInfo
                    {
                        UserId = discordId,
                        Username = discordUsername,
                        AvatarUrl = avatarUrl
                    }
                };

                return true;
            }
            catch (Exception)
            {
                //Logger.Error($"Failed to validate token as player:");
                //Logger.Error(e.Message);
            }

            user = null;
            return false;
        }

#if DEBUG
        private bool VerifyAsMockPlayer(string token, ConnectedUser socketUser, out User user)
        {
            try
            {
                // Create a token validation parameters object with the signing credentials
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = false,
                    ValidIssuer = "ta_plugin_mock",
                    ValidAudience = "ta_users",
                    IssuerSigningKey = new X509SecurityKey(_mockCert),
                };

                // Verify the token and extract the claims
                IdentityModelEventSource.ShowPII = true;
                IdentityModelEventSource.LogCompleteSecurityArtifact = true;
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
                        UserId = null,
                        Username = null,
                        AvatarUrl = null
                    }
                };

                return true;
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to validate token as mock player:");
                Logger.Error(e.Message);
            }

            user = null;
            return false;
        }
#else
        private bool VerifyAsMockPlayer(string token, ConnectedUser socketUser, out User user)
        {
            user = null;
            return false;
        }
#endif
    }
}
