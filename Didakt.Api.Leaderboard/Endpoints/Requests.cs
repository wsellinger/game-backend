namespace Didakt.Api.Leaderboard.Endpoints
{
    namespace Requests
    {
        public record PostScoreRequest(double? Score);
        public record GetScoreRequest(string? Player);
        public record GetTopRequest(long? Count);
    }
}