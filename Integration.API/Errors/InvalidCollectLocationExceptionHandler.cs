using Core.Kernel.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Integration.API.Errors;

/// <summary>
///   Turns <see cref="InvalidCollectLocationException" /> into a 400 with the
///   exception's user-facing message, e.g. when force-collecting an artifact
///   that was stored without a protocol. Every other exception falls through
///   to the generic 500 ProblemDetails response, which carries no details.
/// </summary>
public class InvalidCollectLocationExceptionHandler : IExceptionHandler {
  private readonly IProblemDetailsService problem_details_;

  /// <summary>
  ///   Initializes a new instance of the <see cref="InvalidCollectLocationExceptionHandler" /> class.
  /// </summary>
  /// <param name="problem_details">Writes ProblemDetails responses.</param>
  public InvalidCollectLocationExceptionHandler(
    IProblemDetailsService problem_details) {
    problem_details_ = problem_details;
  }

  /// <inheritdoc />
  public async ValueTask<bool> TryHandleAsync(HttpContext http_context,
                                              Exception exception,
                                              CancellationToken token) {
    if (exception is not InvalidCollectLocationException invalid) {
      return false;
    }

    http_context.Response.StatusCode = StatusCodes.Status400BadRequest;
    return await problem_details_.TryWriteAsync(
             new ProblemDetailsContext {
               HttpContext = http_context,
               Exception = exception,
               ProblemDetails = new ProblemDetails {
                 Status = StatusCodes.Status400BadRequest,
                 Title = "Invalid artifact location",
                 Detail = invalid.Message
               }
             }
           );
  }
}
