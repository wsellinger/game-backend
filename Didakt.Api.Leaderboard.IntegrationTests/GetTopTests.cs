using Didakt.Api.Leaderboard.Endpoints.Responses;
using System.Net;
using System.Net.Http.Json;

namespace Didakt.Api.Leaderboard.IntegrationTests
{
    public class GetTopTests : TestBase
    {
        private const string ScoreUri = "/leaderboard/category/score";
        private const string TopUri = "/leaderboard/category/top";

        [Fact]
        public async Task ValidRequest_ReturnsOk()
        {
            //Arrange
            var count = 3;
            var parameters = $"?count={count}";

            var entries = new[]
            {
                new { Player = "player_01", Score = 1234.0 },
                new { Player = "player_02", Score = 2345.0 },
                new { Player = "player_03", Score = 3456.0 },
                new { Player = "player_04", Score = 4567.0 }
            };

            var expected = new[]
            {
                new GetTopResponse(1, entries[3].Player, entries[3].Score),
                new GetTopResponse(2, entries[2].Player, entries[2].Score),
                new GetTopResponse(3, entries[1].Player, entries[1].Score)
            };

            foreach (var entry in entries)
            {
                Authenticate(entry.Player);
                await Client.PostAsJsonAsync(ScoreUri, new { entry.Score });
            }

            //Act
            var response = await Client.GetAsync(TopUri + parameters);
            var body = await response.Content.ReadFromJsonAsync<GetTopResponse[]>();

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(body);
            Assert.Equal(expected, body);
        }

        [Theory]
        [InlineData("")]
        [InlineData("?count=")]
        public async Task InvalidInput_ReturnsBadRequest(string parameters)
        {
            //Arrange
            //Act
            var response = await Client.GetAsync(TopUri + parameters);

            //Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
