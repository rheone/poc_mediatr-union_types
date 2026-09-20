using System.Diagnostics;

namespace MediatrUnionPoc.Api.Http;

/// <summary>
/// The single accessor for the request's trace id, shared by the <c>X-Trace-Id</c> header, the
/// problem-details <c>traceId</c> member and the logging scope so all three always agree.
/// </summary>
public static class HttpContextTraceExtensions
{
    /// <summary>The response header (and log-scope key's value source) carrying the trace id.</summary>
    public const string TraceIdHeaderName = "X-Trace-Id";

    /// <summary>The problem-details extension member and logging-scope property carrying the trace id.</summary>
    public const string TraceIdName = "traceId";

    extension(HttpContext http)
    {
        /// <summary>
        /// The W3C trace id of the ambient <see cref="Activity"/> when there is one (so it joins to
        /// distributed traces), otherwise <see cref="HttpContext.TraceIdentifier"/>. Never empty.
        /// </summary>
        public string TraceId
        {
            get
            {
                ArgumentNullException.ThrowIfNull(http);

                var activityTraceId = Activity.Current?.TraceId;

                return activityTraceId is { } id && id != default
                    ? id.ToHexString()
                    : http.TraceIdentifier;
            }
        }
    }
}
