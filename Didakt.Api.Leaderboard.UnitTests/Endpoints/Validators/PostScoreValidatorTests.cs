using Didakt.Api.Leaderboard.Endpoints.Requests;
using Didakt.Api.Leaderboard.Endpoints.Validators;

namespace Didakt.Api.Leaderboard.UnitTests.Endpoints.Validators
{
    public class PostScoreValidatorTests
    {
        [Fact]
        public async Task ValidInput_IsValid()
        {
            //Arrange
            var score = 1;
            var request = new PostScoreRequest(score);
            var validator = new PostScoreValidator();

            //Act
            var result = await validator.ValidateAsync(request);

            //Assert
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(null)] //Null Score
        [InlineData(0.0)] //Empty Score
        [InlineData(-1.0)] //Negative Score
        public async Task InvalidInput_IsNotValid(double? score)
        {
            //Arrange
            var request = new PostScoreRequest(score);
            var validator = new PostScoreValidator();

            //Act
            var result = await validator.ValidateAsync(request);

            //Assert
            Assert.False(result.IsValid);
        }
    }
}