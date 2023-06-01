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

            context.Response.ContentType =
                context.Request.Headers.ContentType =
                    MediaTypeNames.Application.Json;

            context.Request.Body =
                await TransformBody(context.Request.Body, context.RequestAborted);

            await next(context);
        }

        static async Task<Stream> TransformBody(Stream body, CancellationToken cancellationToken)
        {
            StreamReader reader = new(body);
            MemoryStream result = new();
            StreamWriter writer = new(result);

            var encodedBody = Base64Decode(await reader.ReadToEndAsync(cancellationToken));

            await writer.WriteAsync(encodedBody);
            await writer.FlushAsync();
            result.Seek(0, SeekOrigin.Begin);
            return result;
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
            var contextBody = context.Response.Body;

            await using MemoryStream bufferBody = new();

            context.Response.Body = bufferBody;
            await next(context);
            context.Response.Body = contextBody;

            bufferBody.Seek(0, SeekOrigin.Begin);
            if (context.Response.ContentType is not ContentType)
            {
                await bufferBody.CopyToAsync(contextBody, cancellationToken);
                return;
            }

            await using var newBody = await TransformBody(bufferBody, cancellationToken);
            await newBody.CopyToAsync(contextBody, cancellationToken);
        }

        static async Task<Stream> TransformBody(Stream body, CancellationToken cancellationToken)
        {
            MemoryStream result = new();
            StreamWriter writer = new(result);
            StreamReader reader = new(body);

            var decodedBody = Base64Encode(await reader.ReadToEndAsync(cancellationToken));

            await writer.WriteAsync(decodedBody);
            await writer.FlushAsync();
            result.Seek(0, SeekOrigin.Begin);
            return result;
        }
    }

    static string Base64Decode(string plain) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(plain));

    static string Base64Encode(string encoded) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(encoded));
}