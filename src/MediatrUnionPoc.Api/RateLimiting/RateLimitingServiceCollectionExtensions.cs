using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MediatrUnionPoc.Api.RateLimiting;

/// <summary>Registration, endpoint defaults and pipeline placement for the rate limiter.</summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RateLimitingOptions"/> (bound from <c>RateLimiting</c>, validated on start),
    /// the framework's rate limiter and one fixed-window policy per name in
    /// <see cref="RateLimitPolicyNames"/>, each partitioned by <see cref="RateLimitCaller"/>, and the
    /// <see cref="RateLimitRejectionHandler"/> that answers a refused request with a 429 problem. The
    /// budgets are read once, from <see cref="IOptions{TOptions}"/>: changing them takes a restart (see
    /// <see cref="RateLimitingOptions"/> for why). Call it after <c>AddApplication</c> and <c>AddAudit</c> (the handler needs the audit log)
    /// and after <c>AddApiProblemDetails</c>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<
            IValidateOptions<RateLimitingOptions>,
            RateLimitingOptionsValidator
        >();
        services.AddSingleton<RateLimitRejectionHandler>();

        services.AddRateLimiter();
        services
            .AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>, RateLimitRejectionHandler>(
                (limiter, settings, rejection) =>
                {
                    // The middleware sets this status before it calls OnRejected; its own default is 503.
                    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    limiter.OnRejected = rejection.OnRejectedAsync;

                    var budgets = settings.Value;
                    limiter.AddPolicy(
                        RateLimitPolicyNames.Reads,
                        context => PartitionFor(context, budgets.Reads)
                    );
                    limiter.AddPolicy(
                        RateLimitPolicyNames.Writes,
                        context => PartitionFor(context, budgets.Writes)
                    );
                    limiter.AddPolicy(
                        RateLimitPolicyNames.Impersonation,
                        context => PartitionFor(context, budgets.Impersonation)
                    );
                }
            );

        return services;
    }

    /// <summary>
    /// Adds the rate limiter. Place it after <c>UseAuthentication</c> (the partition is the caller, so the
    /// principal must exist) and after <c>UseCors</c> (a preflight is answered there and must neither
    /// consume nor be refused by a budget), and before <c>UseAuthorization</c>, so a request that is then
    /// answered 401 or 403 still spends budget.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns><paramref name="app"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    public static IApplicationBuilder UseApiRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseRateLimiter();
    }

    /// <summary>
    /// Makes every endpoint of <paramref name="builder"/> rate limited by default: an endpoint that carries
    /// neither <c>[EnableRateLimiting]</c> nor <c>[DisableRateLimiting]</c> (a controller action nobody
    /// annotated) gets the <see cref="RateLimitPolicyNames.Reads"/> policy. A declared policy always wins,
    /// because the default is only added when nothing was declared. Exemption is therefore always an
    /// explicit, visible <c>DisableRateLimiting</c>.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The builder returned by <c>MapControllers</c> (or any other endpoint mapping).</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static TBuilder WithDefaultRateLimiting<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(endpoint =>
        {
            var declared = endpoint.Metadata.Any(metadata =>
                metadata is EnableRateLimitingAttribute or DisableRateLimitingAttribute
            );

            if (!declared)
            {
                endpoint.Metadata.Add(new EnableRateLimitingAttribute(RateLimitPolicyNames.Reads));
            }
        });

        return builder;
    }

    private static RateLimitPartition<string> PartitionFor(
        HttpContext context,
        RateLimitPolicyOptions policy
    ) =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateLimitCaller.From(context).PartitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                QueueLimit = policy.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }
        );
}
