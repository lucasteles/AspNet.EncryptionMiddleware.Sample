using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;

namespace CryptoMidSample;

public static class CryptoMiddleware
{
    public const string ContentType = "application/jose";
    public static readonly MediaTypeHeaderValue MediaTypeHeader = new(ContentType);

    public class Request
    {
        readonly RequestDelegate next;
        readonly byte[] key;

        public Request(RequestDelegate next, IConfiguration configuration)
        {
            this.next = next;
            key = Encoding.UTF8.GetBytes(configuration.GetValue<string>("PrivateKey")
                                         ?? throw new ArgumentNullException());
        }

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
                context.Request.Body, context.RequestAborted);

            await next(context)

            context.Response.Body = originalBody;
        }

        static async Task<Stream> TransformBody(
            Stream body,
            CancellationToken cancellationToken
        )
        {
            using ToBase64Transform base64Transform = new();
            MemoryStream result = new();
            CryptoStream base64Stream = new(result, base64Transform, mode);

            await body.CopyToAsync(base64Stream, cancellationToken);

            await base64Stream.FlushFinalBlockAsync(cancellationToken);

            result.Seek(0, SeekOrigin.Begin);
            return result;
        }
    }

    public class Response
    {
        readonly RequestDelegate next;
        readonly byte[] key;

        public Response(RequestDelegate next, IConfiguration configuration)
        {
            this.next = next;
            key = Encoding.UTF8.GetBytes(configuration.GetValue<string>("PrivateKey")
                                         ?? throw new ArgumentNullException());
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

            await using var newBody =
                await TransformBody2(bufferBody, key, CryptoStreamMode.Write, cancellationToken);

            await newBody.CopyToAsync(originalBody, cancellationToken);
        }
    }

    static async Task<Stream> TransformBody2(
        Stream body, byte[] key, CryptoStreamMode mode,
        CancellationToken cancellationToken
    )
    {
        // using var aes = Aes.Create();
        // aes.Key = key;
        //
        // using var transform = mode switch
        // {
        //     CryptoStreamMode.Write => aes.CreateEncryptor(),
        //     CryptoStreamMode.Read => aes.CreateDecryptor(),
        //     _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        // };

        using ToBase64Transform base64Transform = new();

        MemoryStream result = new();
        // CryptoStream aesStream = new(result, transform, mode);
        CryptoStream base64Stream = new(result, base64Transform, mode);

        await body.CopyToAsync(base64Stream, cancellationToken);

        await base64Stream.FlushFinalBlockAsync(cancellationToken);

        result.Seek(0, SeekOrigin.Begin);
        return result;
    }
}