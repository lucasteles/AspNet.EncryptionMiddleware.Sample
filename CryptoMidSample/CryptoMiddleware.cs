using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography;

namespace CryptoMidSample;

public static class CryptoMiddleware
{
    public const string ContentType = "application/jose";
    public static readonly MediaTypeHeaderValue MediaTypeHeader = new(ContentType);

    public class Request
    {
        readonly RequestDelegate next;
        public Request(RequestDelegate next) => this.next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.ContentType is not ContentType)
            {
                await next(context);
                return;
            }

            context.Request.ContentType = MediaTypeNames.Application.Json;

            var originalBody = context.Request.Body;
            context.Request.Body = await TransformBody(originalBody, context.RequestAborted);

            await next(context);
            context.Response.Body = originalBody;
        }

        static async Task<Stream> TransformBody(
            Stream body,
            CancellationToken cancellationToken
        )
        {
            using FromBase64Transform base64Transform = new();
            CryptoStream cryptoStream = new(body, base64Transform, CryptoStreamMode.Read);

            MemoryStream result = new();
            await cryptoStream.CopyToAsync(result, cancellationToken);

            result.Seek(0, SeekOrigin.Begin);
            return result;
        }
    }

    public class Response
    {
        readonly RequestDelegate next;
        public Response(RequestDelegate next) => this.next = next;

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

            await using var newBody = await TransformBody(bufferBody, cancellationToken);
            await newBody.CopyToAsync(originalBody, cancellationToken);
        }

        static async Task<Stream> TransformBody(Stream body, CancellationToken cancellationToken)
        {
            using ToBase64Transform base64Transform = new();

            MemoryStream result = new();
            CryptoStream cryptoStream = new(result, base64Transform, CryptoStreamMode.Write);

            await body.CopyToAsync(cryptoStream, cancellationToken);
            await cryptoStream.FlushFinalBlockAsync(cancellationToken);

            result.Seek(0, SeekOrigin.Begin);
            return result;
        }
    }
}