namespace Filee.Web.Contracts;

/// <summary>Fixed API error envelope. Internal error keys and exception text never cross this boundary.</summary>
public sealed record ApiErrorEnvelope(ApiError Error)
{
    /// <summary>Creates the wire envelope from a public error code and a user-facing Chinese message.</summary>
    public static ApiErrorEnvelope Create(string code, string message) => new(new ApiError(code, message));
}

/// <summary>One public error: a stable code plus a fixed user-facing message.</summary>
public sealed record ApiError(string Code, string Message);
