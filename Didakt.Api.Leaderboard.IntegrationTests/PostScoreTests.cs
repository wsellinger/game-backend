using System.Net;
using System.Net.Http.Json;

namespace Didakt.Api.Leaderboard.IntegrationTests
{
    public class PostScoreTests : TestBase
    {
        private const string RequestUri = "/leaderboard/category/score";

        [Fact]
        public async Task ValidRequest_ReturnsOk()
        {
            //Arrange
            Authenticate();
            var requestBody = new { Score = 1234.0 };

            //Act
            var response = await Client.PostAsJsonAsync(RequestUri, requestBody);

            //Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task NoToken_ReturnsUnauthorized()
        {
            //Arrange
            var requestBody = new { Score = 1234.0 };

            //Act
            var response = await Client.PostAsJsonAsync(RequestUri, requestBody);

            //Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task InvalidInput_ReturnsBadRequest()
        {
            //Arrange
            Authenticate();

            var requestBody = new { Score = -1234.0 };

            //Act
            var response = await Client.PostAsJsonAsync(RequestUri, requestBody);

            //Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
