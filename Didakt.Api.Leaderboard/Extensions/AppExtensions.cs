using Scalar.AspNetCore;

namespace Didakt.Api.Leaderboard.Extensions;

internal static class AppExtensions
{
    extension(WebApplication app)
    {
        internal WebApplication ConfigureApp()
        {
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
                app.UseHttpsRedirection();
            }

            app.UseCors(policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = 503;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new { error = "Service temporarily unavailable." });
                });
            });

            app.UseAuthentication();
            app.UseAuthorization();

            return app;
        }
    }
}
