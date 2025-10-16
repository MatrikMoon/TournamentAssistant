using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
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
                // Bot tokens are signed by the player cert because they need a long lifetime.
                // Besides, in theory the player cert is safe too... Right?
                signingCredentials = new X509SigningCredentials(_pluginCert);
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

        // Right now, we're just accepting a websocket token user and converting it into a REST one
        // There may be more interesting things we can do with this, but as I've written elsewhere,
        // I'm currently in the middle of the tedious work involved with adding ASP.NET support,
        // so for now we get the cheap version.
        // Yes, it's just valid for 5 days for now.
        // And yes, right now at least, a rest user is only differentiated by a lack of SocketUser
        // and a claim
        public string GenerateRestToken(User user)
        {
            if (user.discord_info == null)
            {
                throw new ArgumentException("User must have had their Discord info populated before having a token generated");
            }

            // Create the signing credentials with the certificate
            var signingCredentials = new X509SigningCredentials(_serverCert);

            var expClaim = new Claim("exp", DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds().ToString());

            // Create a list of claims for the token payload
            var claims = new[]
            {
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                expClaim,
                new Claim("ta:discord_id", user.discord_info.UserId),
                new Claim("ta:discord_name", user.discord_info.Username),
                new Claim("ta:discord_avatar", user.discord_info.AvatarUrl),
                new Claim("ta:is_rest", "true"),
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

        // Note: allowSocketlessWebsocket is specifically for validating a websocket token before
        // converting it to a REST token
        public bool VerifyUser(string token, ConnectedUser socketUser, out User user, bool allowSocketlessWebsocket = false)
        {
            // Empty tokens are definitely not valid
            if (string.IsNullOrWhiteSpace(token))
            {
                user = null;
                return false;
            }

            var anySucceeded =
                VerifyAsPlayer(token, socketUser, out user) ||
                VerifyAsWebsocket(token, socketUser, out user, allowSocketlessWebsocket) ||
                VerifyBotTokenAsWebsocket(token, socketUser, out user, allowSocketlessWebsocket) ||
                VerifyAsRest(token, socketUser, out user) ||
                VerifyBeatKhanaTokenAsWebsocket(token, socketUser, out user, allowSocketlessWebsocket) ||
                VerifyAsMockPlayer(token, socketUser, out user);

            if (!anySucceeded)
            {
                Logger.Error($"All validation methods failed.");
            }

            return anySucceeded;
        }

        private bool VerifyAsWebsocket(string token, ConnectedUser socketUser, out User user, bool allowSocketlessWebsocket = false)
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

                bool.TryParse(claims.FirstOrDefault(x => x.Type == "ta:is_rest")?.Value, out var isRest);
                var discordId = claims.First(x => x.Type == "ta:discord_id").Value;
                var discordUsername = claims.First(x => x.Type == "ta:discord_name").Value;
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{claims.First(x => x.Type == "ta:discord_id").Value}/{claims.First(x => x.Type == "ta:discord_avatar").Value}.png";

                // If this has the rest claim, it's a rest token and should be checked as such
                if (isRest || (socketUser == null && !allowSocketlessWebsocket))
                {
                    user = null;
                    return false;
                }

                // If the discordId is a guid, this is a bot token and should be checked as such
                if (Guid.TryParse(discordId, out var _)) {
                    user = null;
                    return false;
                }

                if (string.IsNullOrEmpty(discordId))
                {
                    avatarUrl = null;
                }

                user = new User
                {
                    // Moon's note: specifically for allowSocketlessWebsocket, we will assign a random guid.
                    // This is only for validating a token before converting it to a REST token
                    Guid = socketUser?.id.ToString() ?? Guid.NewGuid().ToString(),
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

        private bool VerifyBotTokenAsWebsocket(string token, ConnectedUser socketUser, out User user, bool allowSocketlessWebsocket = false)
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
                    IssuerSigningKey = new X509SecurityKey(_pluginCert),
#if DEBUG
                    ClockSkew = TimeSpan.Zero
#endif
                };

                // Verify the token and extract the claims
                IdentityModelEventSource.ShowPII = true;
                IdentityModelEventSource.LogCompleteSecurityArtifact = true;
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                bool.TryParse(claims.FirstOrDefault(x => x.Type == "ta:is_rest")?.Value, out var isRest);
                var discordId = claims.First(x => x.Type == "ta:discord_id").Value;
                var discordUsername = claims.First(x => x.Type == "ta:discord_name").Value;

                // If there's no socket connected, this is probably a REST token. We shouldn't check the database for that
                // If this has the rest claim, it's a rest token and should be checked as such
                if (isRest || (socketUser == null && !allowSocketlessWebsocket))
                {
                    user = null;
                    return false;
                }

                user = new User
                {
                    Guid = socketUser?.id.ToString() ?? Guid.NewGuid().ToString(),
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

        private bool VerifyAsRest(string token, ConnectedUser socketUser, out User user)
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

                bool.TryParse(claims.FirstOrDefault(x => x.Type == "ta:is_rest")?.Value, out var isRest);
                var discordId = claims.First(x => x.Type == "ta:discord_id").Value;
                var discordUsername = claims.First(x => x.Type == "ta:discord_name").Value;
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{claims.First(x => x.Type == "ta:discord_id").Value}/{claims.First(x => x.Type == "ta:discord_avatar").Value}.png";

                // If either of these are true, we can't be processing a REST token
                if (!isRest || socketUser != null)
                {
                    user = null;
                    return false;
                }

                if (string.IsNullOrEmpty(discordId))
                {
                    avatarUrl = null;
                }

                user = new User
                {
                    // A rest user will be getting a response directly, so we're setting this to null
                    // (empty guid already represents the server itself)
                    Guid = null,
                    ClientType = User.ClientTypes.RESTConnection,
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

        private bool VerifyBeatKhanaTokenAsWebsocket(string token, ConnectedUser socketUser, out User user, bool allowSocketlessWebsocket = false)
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

                bool.TryParse(claims.FirstOrDefault(x => x.Type == "ta:is_rest")?.Value, out var isRest);
                var discordId = claims.First(x => x.Type == "id").Value;
                var discordUsername = claims.First(x => x.Type == "username").Value;
                var avatarUrl = $"https://cdn.discordapp.com/avatars/{claims.First(x => x.Type == "id").Value}/{claims.First(x => x.Type == "avatar").Value}.png";

                // If this has the rest claim, it's a rest token and should be checked as such
                if (isRest || (socketUser == null && !allowSocketlessWebsocket))
                {
                    user = null;
                    return false;
                }

                if (string.IsNullOrEmpty(discordId))
                {
                    avatarUrl = null;
                }

                user = new User
                {
                    Guid = socketUser?.id.ToString() ?? Guid.NewGuid().ToString(),
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

                if (string.IsNullOrEmpty(discordId))
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
