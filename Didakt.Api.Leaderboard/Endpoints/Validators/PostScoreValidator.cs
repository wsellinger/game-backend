using Didakt.Api.Leaderboard.Endpoints.Requests;
using FluentValidation;

namespace Didakt.Api.Leaderboard.Endpoints.Validators;

public class PostScoreValidator : AbstractValidator<PostScoreRequest>
{
    public PostScoreValidator()
    {
        RuleFor(x => x.Score)
            .NotEmpty()
            .GreaterThan(0);
    }
}