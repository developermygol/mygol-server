using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using webapi.Models.Db;

namespace webapi
{
    public class AuthTokenManager
    {
        // http://www.blinkingcaret.com/2017/09/06/secure-web-api-in-asp-net-core/

        public SecurityKey Key
        {
            get { return mSecretTokenKey; }
        }

        public AuthTokenManager(TokenAuthConfig secrets)
        {
            mTokenGenConfig = secrets;
            mSecretTokenKey = CreateAuthenticationSecret(secrets.TokenSecret);
        }
        
        public string CreateToken(User user)
        {
            var claims = new Claim[] {
                new Claim(ClaimTypes.Sid, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Level.ToString()),
                new Claim(ClaimTypes.Locality, user.Lang ?? "es"),      // TODO: get config.defaultlocale
                //new Claim(ClaimTypes.GroupSid, user.IdTeam.ToString()),
                new Claim(JwtRegisteredClaimNames.Exp, $"{new DateTimeOffset(DateTime.Now.AddHours(mTokenGenConfig.ExpirationHours)).ToUnixTimeSeconds()}"),
                new Claim(JwtRegisteredClaimNames.Nbf, $"{new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds()}")
            };

            return CreateToken(claims);
        }

        public string CreateActivationToken(long userId, string email)
        {
            var claims = new Claim[] {
                new Claim("userid", userId.ToString()),
                new Claim("email", email)
            };

            return CreateToken(claims);
        }

        public string CreateToken(IEnumerable<Claim> claims)
        {
            var token = new JwtSecurityToken(
                new JwtHeader(
                    new SigningCredentials(Key, SecurityAlgorithms.HmacSha256)),
                new JwtPayload(claims));

            return GetToken(token);
        }

        public IEnumerable<Claim> DecryptToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                IssuerSigningKeys = new[] { Key },
                ValidateLifetime = false,
                ValidateActor = false, 
                ValidateAudience = false, 
                ValidateIssuer = false
            };

            handler.ValidateToken(token, validationParameters, out SecurityToken decrypted);
            var result = decrypted as JwtSecurityToken;

            return result.Claims;
        }

        public static string GetToken(JwtSecurityToken token)
        {
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static SecurityKey CreateAuthenticationSecret(string keyPhrase)
        {
            var secretBytes = Encoding.UTF8.GetBytes(keyPhrase);
            var secretKey = new SymmetricSecurityKey(secretBytes);
            return secretKey;
        }


        // __ Password hashing ________________________________________________


        public static HashedPassword HashPassword(string password)
        {
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return new HashedPassword
            {
                Salt = Convert.ToBase64String(salt),
                Hash = HashPassword(password, salt)
            };
        }

        public static string HashPassword(string password, byte[] salt)
        {
            byte[] hashed = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA1, 1000, 256 / 8);
            return Convert.ToBase64String(hashed);
        }
        

        private SecurityKey mSecretTokenKey;
        private TokenAuthConfig mTokenGenConfig;
    }

    public class TokenAuthConfig
    {
        public int ExpirationHours { get; set; } = 4320;  // 6 months
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string TokenSecret { get; set; } = "dk·Dqk$1kd.Z0;)2dJ9l";
    }

    public class HashedPassword
    {
        public string Hash;
        public string Salt;
    }


}
