using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Common.Infrastructure;

public static class ValidatedOptions
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to <paramref name="section"/> and checks its data
    /// annotations ([Required], [Range], ...) when the app starts, so a bad setting stops the deploy
    /// instead of failing the first request that reads it. Chain <c>.Validate(...)</c> for rules
    /// annotations can't express.
    /// </summary>
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(this IServiceCollection services, IConfiguration section)
        where TOptions : class =>
        services.AddOptions<TOptions>().Bind(section).ValidateDataAnnotations().ValidateOnStart();
}
