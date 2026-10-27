using System.Collections.Concurrent;
using System.Reflection;
using ErrorOr;

namespace Common.Application.Behaviors;

/// <summary>Builds an <c>ErrorOr&lt;T&gt;</c> failure when only the response type is known.</summary>
internal static class ErrorResponse
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> Factories = new();

    public static TResponse From<TResponse>(List<Error> errors)
        where TResponse : IErrorOr
    {
        var from = Factories.GetOrAdd(typeof(TResponse), type =>
            type.GetMethod(nameof(ErrorOr<object>.From), BindingFlags.Public | BindingFlags.Static, [typeof(List<Error>)])
            ?? throw new InvalidOperationException($"{type} has no From(List<Error>)."));
        return (TResponse)from.Invoke(null, [errors])!;
    }
}
