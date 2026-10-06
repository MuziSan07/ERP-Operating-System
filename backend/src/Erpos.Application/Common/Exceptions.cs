namespace Erpos.Application.Common;

public class AppException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class NotFoundException(string what) : AppException($"{what} not found.", 404);

public class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(message, 403);

public class ValidationException(string message) : AppException(message, 400);

public class UnauthorizedException(string message = "Invalid credentials.") : AppException(message, 401);
