using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Tusk.Api.Middleware;

namespace Tusk.Api.Tests.Middleware
{
    public sealed class GlobalExceptionHandlerTests
    {
        public static TheoryData<Exception> SerializationFailures => new()
        {
            new PostgresException("could not serialize access", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure),
            new DbUpdateException("save failed",
                new PostgresException("could not serialize access", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure)),
            new InvalidOperationException("likely due to a transient failure",
                new DbUpdateException("save failed",
                    new PostgresException("could not serialize access", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure)))
        };

        [Fact]
        public async Task UnrelatedInvalidOperationException_StillReturns500()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
                .TryHandleAsync(context, new InvalidOperationException("No route matches the supplied values"), CancellationToken.None);

            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(SerializationFailures))]
        public async Task SerializationFailure_Returns409_WithRetryMessage(Exception exception)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
                .TryHandleAsync(context, exception, CancellationToken.None);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            var problem = await JsonDocument.ParseAsync(context.Response.Body);
            Assert.Contains("Try again", problem.RootElement.GetProperty("title").GetString());
        }

        [Fact]
        public async Task ConcurrencyException_Returns409_WithReloadMessage()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
                .TryHandleAsync(context, new DbUpdateConcurrencyException(), CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            var problem = await JsonDocument.ParseAsync(context.Response.Body);
            Assert.Contains("modified by someone else", problem.RootElement.GetProperty("title").GetString());
        }
    }
}
