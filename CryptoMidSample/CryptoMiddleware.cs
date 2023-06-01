using System.Buffers.Text;
using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Net.Mime;

namespace CryptoMidSample;

public static class CryptoMiddleware
{
    public const string ContentType = "application/jose";
    public static readonly MediaTypeHeaderValue MediaTypeHeader = new(ContentType);

    enum Mode
    {
        Encode,
        Decode,
        Pass,
    }

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
                    ParseBody(bodyReader, pipe.Writer, Mode.Decode, cancellationToken),
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
    }

    public class Response
    {
        readonly RequestDelegate next;

        public Response(RequestDelegate next) =>
            this.next = next;

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

                var mode = context.Response.ContentType is not ContentType
                    ? Mode.Pass
                    : Mode.Encode;

                await ParseBody(pipe.Reader, bodyWriter, mode, cancellationToken);
            }
            finally
            {
                context.Response.Body = originalBody;
                await pipe.Reader.CompleteAsync();
                await pipe.Writer.CompleteAsync();
            }
        }
    }

    static async Task ParseBody(
        PipeReader reader,
        PipeWriter writer,
        Mode mode,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var readResult = await reader.ReadAsync(cancellationToken);
            var buffer = readResult.Buffer;

            foreach (var memory in buffer)
            {
                int bytesWritten;
                switch (mode)
                {
                    case Mode.Decode:
                        Base64.DecodeFromUtf8(
                            memory.Span,
                            writer.GetSpan(memory.Length),
                            out _, out bytesWritten
                        );
                        break;
                    case Mode.Encode:
                        Base64.EncodeToUtf8(
                            memory.Span,
                            writer.GetSpan(memory.Length),
                            out _, out bytesWritten
                        );
                        break;
                    case Mode.Pass:
                    default:
                        await writer.WriteAsync(memory, cancellationToken);
                        bytesWritten = 0;
                        break;
                }

                writer.Advance(bytesWritten);
            }

            reader.AdvanceTo(buffer.End);
            await writer.FlushAsync(cancellationToken);
            if (readResult.IsCompleted) break;
        }

        await writer.CompleteAsync();
    }
}