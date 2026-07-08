namespace Watodoo.Shared.Exceptions;

public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Une ou plusieurs erreurs de validation ont eu lieu.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
