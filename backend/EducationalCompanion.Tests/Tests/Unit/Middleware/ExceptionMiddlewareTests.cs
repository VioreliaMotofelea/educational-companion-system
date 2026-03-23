using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EducationalCompanion.Api.Common;
using EducationalCompanion.Api.Middleware;
using EducationalCompanion.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Middleware;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ValidationException_ReturnsBadRequest()
    {
        var middleware = new ExceptionMiddleware(
            next: _ => throw new ValidationException("bad request"),
            logger: NullLogger<ExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_NotFoundException_ReturnsNotFound()
    {
        var middleware = new ExceptionMiddleware(
            next: _ => throw new NotFoundException("User", "missing"),
            logger: NullLogger<ExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_GamificationRuleViolation_ReturnsInternalServerError()
    {
        var middleware = new ExceptionMiddleware(
            next: _ => throw new GamificationRuleViolationException("rule"),
            logger: NullLogger<ExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WritesJsonErrorResponse()
    {
        var middleware = new ExceptionMiddleware(
            next: _ => throw new ValidationException("bad request"),
            logger: NullLogger<ExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.False(string.IsNullOrWhiteSpace(body));

        var parsed = JsonSerializer.Deserialize<ErrorResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(parsed);
        Assert.Equal(StatusCodes.Status400BadRequest, parsed!.Status);
        Assert.Equal("bad request", parsed!.Error);
    }
}

