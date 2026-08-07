using FluentValidation;
using ValidationException = Watodoo.Shared.Exceptions.ValidationException;

namespace Watodoo.Shared.Validation;

public static class ValidatorExtensions
{
    public static async Task ValidateAndThrowCustomAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new ValidationException((IReadOnlyDictionary<string, string[]>)result.ToDictionary());
        }
    }
}
