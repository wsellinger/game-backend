using Didakt.Api.Leaderboard.Endpoints;
using Didakt.Api.Leaderboard.Endpoints.Requests;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using StackExchange.Redis;
using System.Security.Claims;

namespace Didakt.Api.Leaderboard.UnitTests.Endpoints
{
    public class PostScoreTests
    {
        private readonly Mock<IValidator<PostScoreRequest>> _validator = new();
        private readonly Mock<IDatabase> _database = new();
        private readonly Mock<IConnectionMultiplexer> _connection = new();

        private readonly ValidationResult FailedValidation = new([new ValidationFailure("", "")]);

        public PostScoreTests()
        {
            _connection.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_database.Object);
        }

        [Fact]
        public async Task ValidInput_OK()
        {
            //Arrange
            var category = "testCategory";
            var player = "testPlayer";
            var score = 1234;
            var request = new PostScoreRequest(score);
            var context = new DefaultHttpContext() { 
                User = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, player)])) };

            _validator.Setup(x => x.ValidateAsync(It.IsAny<PostScoreRequest>())).ReturnsAsync(new ValidationResult());
            _database.Setup(x => x.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>()));

            //Act
            var result = await EndpointMethods.PostScore(category, request, _validator.Object, _connection.Object, context);

            //Assert
            _validator.Verify(x => x.ValidateAsync(request), Times.Once);
            _database.Verify(x => x.SortedSetAddAsync(It.Is<RedisKey>(x => x.ToString().Contains(category)), player, score), Times.Once);

            Assert.IsType<Ok>(result);
        }

        [Fact]
        public async Task InvalidInput_BadRequest()
        {
            //Arrange
            var category = "testCategory";
            var score = 1234;
            var request = new PostScoreRequest(score);
            var context = new DefaultHttpContext();

            _validator.Setup(x => x.ValidateAsync(It.IsAny<PostScoreRequest>())).ReturnsAsync(FailedValidation);
            _database.Setup(x => x.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>()));

            //Act
            var result = await EndpointMethods.PostScore(category, request, _validator.Object, _connection.Object, context);

            //Assert
            _validator.Verify(x => x.ValidateAsync(request), Times.Once);
            _database.Verify(x => x.SortedSetAddAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<double>()), Times.Never);

            var problem = Assert.IsType<ProblemHttpResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        }
    }
}
