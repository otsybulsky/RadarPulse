namespace RadarPulse.Application.Product;

/// <summary>
/// Internal product query result that distinguishes missing data from successful lookup.
/// </summary>
/// <remarks>
/// Service methods use this shape before the HTTP-facing API response maps it
/// to status codes.
/// </remarks>
public sealed record RadarPulseProductQueryResult<T>(
    bool Found,
    T? Value,
    string Message)
{
    /// <summary>
    /// Creates a found result around a non-null value.
    /// </summary>
    public static RadarPulseProductQueryResult<T> FromValue(T value) =>
        new(true, value, string.Empty);

    /// <summary>
    /// Creates a not-found result with a caller-facing explanation.
    /// </summary>
    public static RadarPulseProductQueryResult<T> NotFound(string message) =>
        new(false, default, message);
}

/// <summary>
/// Product API response envelope shared by CLI and HTTP adapter surfaces.
/// </summary>
/// <remarks>
/// The HTTP adapter uses <see cref="StatusCode"/> directly, while in-process
/// callers can inspect <see cref="IsSuccess"/> and <see cref="Message"/>.
/// </remarks>
public sealed record RadarPulseProductApiResponse<T>(
    int StatusCode,
    bool IsSuccess,
    T? Body,
    string Message)
{
    /// <summary>
    /// Creates a successful response for an existing resource or query result.
    /// </summary>
    public static RadarPulseProductApiResponse<T> Ok(T body) =>
        new(200, true, body, string.Empty);

    /// <summary>
    /// Creates a successful response for a newly created run or control result.
    /// </summary>
    public static RadarPulseProductApiResponse<T> Created(T body) =>
        new(201, true, body, string.Empty);

    /// <summary>
    /// Creates a client error response for invalid product input.
    /// </summary>
    public static RadarPulseProductApiResponse<T> BadRequest(string message) =>
        new(400, false, default, message);

    /// <summary>
    /// Creates a missing-resource response for product lookup routes.
    /// </summary>
    public static RadarPulseProductApiResponse<T> NotFound(string message) =>
        new(404, false, default, message);
}

/// <summary>
/// Result of applying a product control action.
/// </summary>
public sealed record RadarPulseProductControlSummary(
    string RunId,
    string Action,
    RadarPulseProductOperatorSummary OperatorSummary,
    int CanceledOpenCount,
    int ReleasedCanceledCount,
    int DrainedProcessingCount,
    string Message);
