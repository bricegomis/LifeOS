using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace LifeOS.Api.Validation;

public static class RequestValidationExtensions
{
    public static RouteGroupBuilder AddRequestValidation(this RouteGroupBuilder group)
    {
        return group.AddEndpointFilterFactory((context, next) =>
        {
            var validatedParameters = context.MethodInfo
                .GetParameters()
                .Select((parameter, index) => (parameter, index))
                .Where(item => HasValidationAttributes(item.parameter.ParameterType))
                .ToArray();

            if (validatedParameters.Length == 0)
            {
                return next;
            }

            return async invocationContext =>
            {
                var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

                foreach (var (parameter, index) in validatedParameters)
                {
                    var argument = invocationContext.Arguments[index];
                    if (argument is null)
                    {
                        continue;
                    }

                    var validationResults = new List<ValidationResult>();
                    if (Validator.TryValidateObject(
                            argument,
                            new ValidationContext(argument),
                            validationResults,
                            validateAllProperties: true))
                    {
                        continue;
                    }

                    foreach (var result in validationResults)
                    {
                        var memberNames = result.MemberNames.Any()
                            ? result.MemberNames
                            : [parameter.Name ?? "request"];

                        foreach (var memberName in memberNames)
                        {
                            var messages = errors.GetValueOrDefault(memberName) ?? [];
                            errors[memberName] = [.. messages, result.ErrorMessage ?? "The value is invalid."];
                        }
                    }
                }

                return errors.Count == 0
                    ? await next(invocationContext)
                    : Results.ValidationProblem(errors);
            };
        });
    }

    private static bool HasValidationAttributes(Type type)
    {
        return type.GetCustomAttributes<ValidationAttribute>(inherit: true).Any()
            || type.GetProperties()
                .Any(property => property.GetCustomAttributes<ValidationAttribute>(inherit: true).Any());
    }
}
