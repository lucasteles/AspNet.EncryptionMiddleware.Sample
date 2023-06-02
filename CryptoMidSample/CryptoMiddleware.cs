using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace CryptoMidSample;

public static class CryptoMiddleware
{
    public const string ContentType = "application/jose";
    public static readonly MediaTypeHeaderValue MediaTypeHeader = new(ContentType);

    public class Options
    {
        public required byte[] PrivateKey { get; init; }

        public required byte[] IV { get; init; }
    }

    public class Request
    {
        readonly RequestDelegate next;
        readonly IOptions<Options> options;

        public Request(RequestDelegate next, IOptions<Options> options)
        {
            this.next = next;
            this.options = options;
        }


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

        async Task<Stream> TransformBody(
            Stream body,
            CancellationToken cancellationToken
        )
        {
            using var aes = Aes.Create();
            aes.Key = options.Value.PrivateKey;
            aes.IV = options.Value.IV;

            using FromBase64Transform base64Transform = new();
            using var transform = aes.CreateDecryptor();

            CryptoStream base64Stream = new(body, base64Transform, CryptoStreamMode.Read);
            CryptoStream aesStream = new(base64Stream, transform, CryptoStreamMode.Read);

            MemoryStream result = new();
            await aesStream.CopyToAsync(result, cancellationToken);

            result.Seek(0, SeekOrigin.Begin);
            return result;
        }
    }

    public class Response
    {
        readonly RequestDelegate next;
        readonly IOptions<Options> options;

        public Response(RequestDelegate next, IOptions<Options> options)
        {
            this.next = next;
            this.options = options;
        }

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

        async Task<Stream> TransformBody(Stream body, CancellationToken cancellationToken)
        {
            using var aes = Aes.Create();
            aes.Key = options.Value.PrivateKey;
            aes.IV = options.Value.IV;

            using ToBase64Transform base64Transform = new();
            using var transform = aes.CreateEncryptor();

            MemoryStream result = new();
            CryptoStream base64Stream = new(result, base64Transform, CryptoStreamMode.Write);
            CryptoStream aesStream = new(base64Stream, transform, CryptoStreamMode.Write);

            await body.CopyToAsync(aesStream, cancellationToken);
            await aesStream.FlushFinalBlockAsync(cancellationToken);

            result.Seek(0, SeekOrigin.Begin);
            return result;
        }
    }
}