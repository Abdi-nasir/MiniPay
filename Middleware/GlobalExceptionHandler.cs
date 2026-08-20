using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniApy.Api.Exceptions;
using Npgsql;

namespace MiniApy.Api.Middleware;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException &&
            httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation(
                "Request {TraceId} was cancelled by the client",
                httpContext.TraceIdentifier);

            return false;
        }

        var error = MapException(exception);

        if (error.StatusCode >= 500)
        {
            logger.LogError(
                exception,
                "Unhandled exception for request {Method} {Path}. " +
                "TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                "Request {Method} {Path} failed with {StatusCode}. " +
                "ErrorCode: {ErrorCode}. ExceptionType: " +
                "{ExceptionType}. TraceId: {TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                error.StatusCode,
                error.ErrorCode,
                exception.GetType().Name,
                httpContext.TraceIdentifier);
        }

        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{error.StatusCode}",
            Title = error.Title,
            Status = error.StatusCode,
            Detail = error.Detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["errorCode"] =
            error.ErrorCode;

        problemDetails.Extensions["traceId"] =
            httpContext.TraceIdentifier;

        problemDetails.Extensions["timestamp"] =
            DateTimeOffset.UtcNow;

        httpContext.Response.StatusCode =
            error.StatusCode;

        httpContext.Response.ContentType =
            "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);

        return true;
    }

    private static ErrorMapping MapException(
        Exception exception)
    {
        return exception switch
        {
            ResourceNotFoundException =>
                new ErrorMapping(
                    StatusCodes.Status404NotFound,
                    "Resource not found",
                    "resource_not_found",
                    exception.Message),

            ResourceConflictException =>
                new ErrorMapping(
                    StatusCodes.Status409Conflict,
                    "Resource conflict",
                    "resource_conflict",
                    exception.Message),

            BusinessRuleException =>
                new ErrorMapping(
                    StatusCodes.Status400BadRequest,
                    "Business rule violation",
                    "business_rule_violation",
                    exception.Message),

            DbUpdateException dbUpdateException =>
                MapDatabaseException(
                    dbUpdateException,
                    FindPostgresException(dbUpdateException)),

            PostgresException postgresException =>
                MapPostgresException(postgresException),

            _ =>
                new ErrorMapping(
                    StatusCodes.Status500InternalServerError,
                    "Internal server error",
                    "internal_server_error",
                    "An unexpected error occurred. " +
                    "Use the traceId when contacting support.")
        };
    }

    private static ErrorMapping MapDatabaseException(
        DbUpdateException exception,
        PostgresException? postgresException)
    {
        if (postgresException is not null)
        {
            return MapPostgresException(postgresException);
        }

        return new ErrorMapping(
            StatusCodes.Status500InternalServerError,
            "Database error",
            "database_error",
            "The database operation could not be completed.");
    }

    private static ErrorMapping MapPostgresException(
        PostgresException exception)
    {
        return exception.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation =>
                new ErrorMapping(
                    StatusCodes.Status409Conflict,
                    "Resource conflict",
                    "database_unique_constraint",
                    "A record with the same unique value " +
                    "already exists."),

            PostgresErrorCodes.ForeignKeyViolation =>
                new ErrorMapping(
                    StatusCodes.Status400BadRequest,
                    "Invalid relationship",
                    "database_foreign_key_violation",
                    "The referenced resource does not exist " +
                    "or cannot be modified."),

            PostgresErrorCodes.SerializationFailure =>
                new ErrorMapping(
                    StatusCodes.Status409Conflict,
                    "Concurrent modification",
                    "database_concurrency_conflict",
                    "The operation conflicted with another " +
                    "request. Retry the request."),

            PostgresErrorCodes.DeadlockDetected =>
                new ErrorMapping(
                    StatusCodes.Status409Conflict,
                    "Concurrent modification",
                    "database_deadlock",
                    "The operation conflicted with another " +
                    "request. Retry the request."),

            _ =>
                new ErrorMapping(
                    StatusCodes.Status500InternalServerError,
                    "Database error",
                    "database_error",
                    "The database operation could not be completed.")
        };
    }

    private static PostgresException? FindPostgresException(
        Exception exception)
    {
        Exception? current = exception;

        while (current is not null)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }

            current = current.InnerException;
        }

        return null;
    }

    private sealed record ErrorMapping(
        int StatusCode,
        string Title,
        string ErrorCode,
        string Detail);
}