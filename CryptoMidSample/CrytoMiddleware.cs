namespace CryptoMidSample;

public sealed class CrytoMiddleware
{
    readonly RequestDelegate next;

    public CrytoMiddleware(RequestDelegate next) =>
        this.next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
    }
}