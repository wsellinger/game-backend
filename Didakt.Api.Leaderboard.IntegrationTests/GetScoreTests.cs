using Didakt.Api.Leaderboard.Endpoints.Responses;
using System.Net;
using System.Net.Http.Json;

namespace Didakt.Api.Leaderboard.IntegrationTests
{
    public class GetScoreTests : TestBase
    {
        private const string RequestUri = "/leaderboard/category/score";

        [Fact]
        public async Task ValidRequest_ReturnsOk()
        {
            //Arrange
            var score = 1234.0;
            var postBody = new { score } ;
            var player = "testPlayer";

            Authenticate(player);
            await Client.PostAsJsonAsync(RequestUri, postBody);
            
            var parameters = $"?player={player}";

            //Act
            var response = await Client.GetAsync(RequestUri + parameters);
            var body = await response.Content.ReadFromJsonAsync<GetScoreResponse>();

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(body);
            Assert.Equal(score, body.Score);
        }

        [Theory]
        [InlineData("")]
        [InlineData("?player=")]
        public async Task InvalidInput_ReturnsBadRequest(string parameters)
        {
            //Arrange
            //Act
            var response = await Client.GetAsync(RequestUri + parameters);

            //Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}