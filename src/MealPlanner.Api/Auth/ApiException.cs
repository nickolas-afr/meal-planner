namespace MealPlanner.Api.Auth;

/// <summary>A failure that maps to a status code and an <c>{ error }</c> body.</summary>
public sealed class ApiException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
