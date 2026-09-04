using Minio.DataModel.Tracing;
using Minio.Handlers;

namespace Collector.Kernel.Logging;

public sealed class MinioTraceLogger : IRequestLogger
{
    public void LogRequest(RequestToLog request, ResponseToLog response, double durationMs)
    {
        Console.WriteLine($"{request.Method} {request.Uri}");
        foreach (RequestParameter? p in request.Parameters)
            Console.WriteLine($"  {p.Name}: {p.Value}");
        Console.WriteLine($"--> {response.StatusCode}");
        Console.WriteLine(response.Content);   // <-- the  XML you need
    }
}
