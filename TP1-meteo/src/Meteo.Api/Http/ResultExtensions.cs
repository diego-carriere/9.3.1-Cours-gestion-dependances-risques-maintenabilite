namespace Meteo.Api.Http;

/// <summary>Petit décorateur pour ajouter un en-tête à un <see cref="IResult"/> déjà construit.</summary>
internal static class ResultExtensions
{
    public static IResult WithHeader(this IResult result, string name, string value) =>
        new HeaderResult(result, name, value);

    private sealed class HeaderResult(IResult inner, string name, string value) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers[name] = value;
            return inner.ExecuteAsync(httpContext);
        }
    }
}
