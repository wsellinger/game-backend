using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Testcontainers.Redis;

namespace Didakt.Api.Leaderboard.IntegrationTests
{
    public abstract class TestBase : IAsyncLifetime
    {
        private const string JwtSecret = "test-secret-key-min-32-chars-long!!";
        private const string JwtIssuer = "didakt-api";
        private const string JwtAudience = "didakt-client";

        protected readonly RedisContainer Redis = new RedisBuilder("redis:latest")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
            .Build();

        protected WebApplicationFactory<Program> Factory = null!;
        protected HttpClient Client = null!;

        public async Task InitializeAsync()
        {
            await Redis.StartAsync();
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = JwtSecret,
                        ["Jwt:Issuer"] = JwtIssuer,
                        ["Jwt:Audience"] = JwtAudience,
                        ["ConnectionStrings:Redis"] = Redis.GetConnectionString()
                    });
                });
            });
            Client = Factory.CreateClient();
        }

        public async Task DisposeAsync()
        {
            await Redis.DisposeAsync();
            await Factory.DisposeAsync();
        }

        protected void Authenticate(string username = "defaultUser")
        {
            var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                issuer: JwtIssuer,
                audience: JwtAudience,
                claims: [
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.NameIdentifier, "1")
                ],
                expires: DateTime.UtcNow.AddMinutes(15),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)),
                    SecurityAlgorithms.HmacSha256)));

            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}