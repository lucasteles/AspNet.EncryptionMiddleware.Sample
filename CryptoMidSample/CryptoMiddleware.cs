using System.IO.Pipelines;
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

            var cancellationToken = context.RequestAborted;
            context.Request.EnableBuffering();
            context.Request.ContentType = MediaTypeNames.Application.Json;

            Pipe pipe = new();
            var bodyReader = context.Request.BodyReader;
            var originalBody = context.Request.Body;
            context.Request.Body = pipe.Reader.AsStream();

            try
            {
                await Task.WhenAll(
                    ParseBody(bodyReader, pipe.Writer, cancellationToken),
                    next(context)
                );
            }
            finally
            {
                context.Request.Body = originalBody;
                await pipe.Reader.CompleteAsync();
                await pipe.Writer.CompleteAsync();
            }
        }

        async Task ParseBody(PipeReader bodyReader, PipeWriter writer,
            CancellationToken cancellationToken)
        {
            using var aes = Aes.Create();
            aes.Key = options.Value.PrivateKey;
            aes.IV = options.Value.IV;

            using FromBase64Transform base64Transform = new();
            using var decryptor = aes.CreateDecryptor();

            await using CryptoStream base64Stream =
                new(bodyReader.AsStream(), base64Transform, CryptoStreamMode.Read);
            await using CryptoStream aesStream =
                new(base64Stream, decryptor, CryptoStreamMode.Read);

            var reader = PipeReader.Create(aesStream);

            while (!cancellationToken.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;

                foreach (var memory in buffer)
                    await writer.WriteAsync(memory, cancellationToken);

                reader.AdvanceTo(buffer.End);
                await writer.FlushAsync(cancellationToken);
                if (readResult.IsCompleted) break;
            }

            await reader.CompleteAsync();
            await writer.CompleteAsync();
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
            Pipe pipe = new();
            var cancellationToken = context.RequestAborted;
            var bodyWriter = context.Response.BodyWriter;
            var originalBody = context.Response.Body;
            context.Response.Body = pipe.Writer.AsStream();

            try
            {
                await next(context);
                await pipe.Writer.CompleteAsync();

                if (context.Response.ContentType is not ContentType)
                    await PassBody(pipe.Reader, bodyWriter, cancellationToken);
                else
                    await ParseBody(pipe.Reader, bodyWriter, cancellationToken);
            }
            finally
            {
                context.Response.Body = originalBody;
                await pipe.Reader.CompleteAsync();
                await pipe.Writer.CompleteAsync();
            }
        }

        static async Task PassBody(
            PipeReader reader,
            PipeWriter writer,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;

                foreach (var memory in buffer)
                    await writer.WriteAsync(memory, cancellationToken);

                reader.AdvanceTo(buffer.End);
                if (readResult.IsCompleted) break;
            }

            await writer.CompleteAsync();
        }

        async Task ParseBody(
            PipeReader reader,
            PipeWriter writer,
            CancellationToken cancellationToken)
        {
            using var aes = Aes.Create();
            aes.Key = options.Value.PrivateKey;
            aes.IV = options.Value.IV;

            using var encryptor = aes.CreateEncryptor();
            using ToBase64Transform base64Transform = new();

            await using CryptoStream base64Stream = new(
                writer.AsStream(), base64Transform, CryptoStreamMode.Write);
            await using CryptoStream cryptoStream = new(
                base64Stream, encryptor, CryptoStreamMode.Write);

            while (!cancellationToken.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;

                foreach (var memory in buffer)
                    await cryptoStream.WriteAsync(memory, cancellationToken);

                reader.AdvanceTo(buffer.End);
                await cryptoStream.FlushFinalBlockAsync(cancellationToken);
                if (readResult.IsCompleted) break;
            }

            await writer.CompleteAsync();
        }
    }
}