
using Didakt.Api.Leaderboard.Endpoints.Requests;
using Didakt.Api.Leaderboard.Endpoints.Responses;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using System.Security.Claims;

namespace Didakt.Api.Leaderboard.Endpoints;

internal static class Endpoints
{
    //=== Mappings

    extension(WebApplication app)
    {
        internal WebApplication MapEndpoints()
        {
            var group = app.MapGroup("/leaderboard/{category}/");
            group.MapPost("score", EndpointMethods.PostScore).RequireAuthorization();
            group.MapGet("score", EndpointMethods.GetScore);
            group.MapGet("top", EndpointMethods.GetTop);

            return app;
        }
    }
}

internal static class EndpointMethods
{
    //=== Endpoints

    const string KeyBase = "leaderboard:";

    //Post Score
    internal static async Task<IResult> PostScore(string category, [FromBody] PostScoreRequest request, IValidator<PostScoreRequest> validator, IConnectionMultiplexer redis, HttpContext context)
    {
        //Validate
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        //Get Player
        var player = context.User.FindFirst(ClaimTypes.Name)?.Value;
        if (player is null)
            return Results.Unauthorized();

        //Store Score
        var db = redis.GetDatabase();
        await db.SortedSetAddAsync($"{KeyBase}{category}", player, request.Score!.Value);
        
        //Return
        return Results.Ok();
    }

    //Get Score
    internal static async Task<IResult> GetScore(string category, [AsParameters] GetScoreRequest request, IValidator<GetScoreRequest> validator, IConnectionMultiplexer redis)
    {
        //Validate
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        //Logic
        var db = redis.GetDatabase();
        double? score = await db.SortedSetScoreAsync($"{KeyBase}{category}", request.Player);
        return score is not null ? Results.Ok(new GetScoreResponse(score.Value)) : Results.NotFound();
    }

    //Get Top
    internal static async Task<IResult> GetTop(string category, [AsParameters] GetTopRequest request, IValidator<GetTopRequest> validator, IConnectionMultiplexer redis)
    {
        //Validate
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
            return Results.ValidationProblem(validationResult.ToDictionary());

        //Logic
        var rank = 0;
        var db = redis.GetDatabase();
        SortedSetEntry[] entries = await db.SortedSetRangeByRankWithScoresAsync($"{KeyBase}{category}", 0, request.Count!.Value - 1, Order.Descending);
        GetTopResponse[] result = [.. entries.Select(x => new GetTopResponse(++rank, x.Element.ToString(), x.Score))];
        return Results.Ok(result);
    }
}