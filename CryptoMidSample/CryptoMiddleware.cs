using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;

namespace CryptoMidSample;

public static class CryptoMiddleware
{
    public const string ContentType = "application/jose";
    public static readonly MediaTypeHeaderValue MediaTypeHeader = new(ContentType);

    public class Request
    {
        readonly RequestDelegate next;

        public Request(RequestDelegate next) =>
            this.next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.ContentType is not ContentType)
            {
                await next(context);
                return;
            }

            var originalBody = context.Response.Body;
            context.Request.ContentType = MediaTypeNames.Application.Json;

            context.Request.Body = await TransformBody(
                context.Request.Body, Mode.Decode, context.RequestAborted);

            await next(context);

            context.Response.Body = originalBody;
        }
    }

    public class Response
    {
        readonly RequestDelegate next;

        public Response(RequestDelegate next) =>
            this.next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var cancellationToken = context.RequestAborted;
            var originalBody = context.Response.Body;

            await using MemoryStream bufferBody = new();

            context.Response.Body = bufferBody;
            await next(context);
            context.Response.Body = originalBody;

            bufferBody.Seek(0, SeekOrigin.Begin);
            if (context.Response.ContentType is not ContentType)
            {
                await bufferBody.CopyToAsync(originalBody, cancellationToken);
                return;
            }

            await using var newBody =
                await TransformBody(bufferBody, Mode.Encode, cancellationToken);

            await newBody.CopyToAsync(originalBody, cancellationToken);
        }
    }

    enum Mode
    {
        Encode,
        Decode,
    }

    static async Task<Stream> TransformBody(
        Stream body, Mode mode,
        CancellationToken cancellationToken
    )
    {
        MemoryStream result = new();
        StreamWriter writer = new(result);
        StreamReader reader = new(body);

        var stringBody = await reader.ReadToEndAsync(cancellationToken);

        var decodedBody = mode switch
        {
            Mode.Encode => Convert.ToBase64String(Encoding.UTF8.GetBytes(stringBody)),
            Mode.Decode => Encoding.UTF8.GetString(Convert.FromBase64String(stringBody)),
            _ => stringBody,
        };

        await writer.WriteAsync(decodedBody);
        await writer.FlushAsync();
        result.Seek(0, SeekOrigin.Begin);
        return result;
    }
}