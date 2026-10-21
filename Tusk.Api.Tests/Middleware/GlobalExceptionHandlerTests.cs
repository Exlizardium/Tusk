using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Tusk.Api.Middleware;

namespace Tusk.Api.Tests.Middleware
{
    public sealed class GlobalExceptionHandlerTests
    {
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
