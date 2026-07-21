---
name: SebastianGuzmanMorla.Validator
description: Rules and guidelines for creating and editing C# validators in the SebastianGuzmanMorla.Validator framework, including dynamic message validation rules.
---

# Validator Customization Skill (`SKILL.md`)

This skill defines the development guidelines and architectural constraints for coding assistants to follow when working with the `SebastianGuzmanMorla.Validator` library. It is a strongly-typed, extensible validation library for .NET 10.0+ that combines a Fluent API with a Roslyn Source Generator for automatic DI registration and cascading interface validation.

### DI Registration & Dependency Lifetimes (Captive Dependency Prevention)

> [!IMPORTANT]
> The source generator registers all validators in the DI container as **Singletons** (`services.AddSingleton`). 
> To prevent captive dependency issues, **NEVER** inject Scoped or Transient services (like DbContexts, Repositories, or context accessors) into the constructor of a Validator. Instead, always resolve them on-demand inside the validation rules/delegates at execution time using the provided `IServiceProvider`.

To register validators, the consuming project must declare a partial class/method (typically `ConfigureServices`). The source generator automatically outputs the partial method implementation registering all validators.

```csharp
public partial class ConfigureServices
{
    private static partial void RegisterValidators(IServiceCollection services);

    public static IServiceCollection ConfigureDomain(this IServiceCollection services)
    {
        // Registers all validators declared in this assembly
        RegisterValidators(services);
        return services;
    }
}
```

---

## 2. Defining Basic Validators

Validators must inherit from `Validator<TEntity>` where `TEntity` is the type being validated. Define rules sequentially
in the constructor using `RuleFor(expression)`:

```csharp
using SebastianGuzmanMorla.Validator;

public class UserValidator : Validator<User>
{
    public UserValidator()
    {
        RuleFor(u => u.Name)
            .NotNull((_, _) => "Name cannot be null")
            .NotEmpty((_, _) => "Name cannot be empty");
    }
}
```

---

## 3. Dynamic & Localized Error Messages

Error messages can be dynamically resolved from localized resource files or external dependencies using the
`Func<IServiceProvider, TEntity, string>` delegate:

```csharp
RuleFor(x => x.DeviceId)
    .NotNull((serviceProvider, entity) => 
        serviceProvider.GetRequiredService<IGeneralLocalization>().NotNull(nameof(entity.DeviceId))
    );
```

---

## 4. Complex Validation with Dynamic Predicates (Must Overload)

When validation logic depends on complex operations (such as database queries, tree structure checks, or external
service calls) and the returned error message needs to dynamically describe the failure:

Use the **dynamic overload of `Must`** which returns a tuple `(bool IsValid, string? ErrorMessage)` from the predicate
method.

```csharp
public class EntityRequestValidator : Validator<EntityRequest>
{
    public EntityRequestValidator()
    {
        RuleFor(x => x.PropertyIds)
            .Must(ValidateComplexLogic, ValidationErrorHandle.StopAll);
    }

    private static async Task<(bool IsValid, string? ErrorMessage)> ValidateComplexLogic(
        IServiceProvider provider, EntityRequest request, CancellationToken cancellationToken = default)
    {
        var externalService = provider.GetRequiredService<IExternalService>();
        var context = provider.GetRequiredService<IUserContext>();

        var result = await externalService.VerifyStructureAsync(context.TenantId, request.PropertyIds, cancellationToken);
        if (!result.IsValid)
        {
            var localization = provider.GetRequiredService<ILocalizationService>();
            var errorMessage = $"{localization.ValidationErrorTitle}:\n" + string.Join("\n", result.ValidationErrors);
            return (false, errorMessage);
        }

        return (true, null);
    }
}
```

---

## 5. Conditional Validation (`RuleForWhen`)

To define validation rules that only run under specific conditions, use `RuleForWhen(expression, whenPredicate)`. The condition is specified as an asynchronous predicate: `Func<IServiceProvider, TEntity, CancellationToken, Task<bool>>`.

```csharp
public class UserValidator : Validator<User>
{
    public UserValidator()
    {
        RuleForWhen(u => u.PromoCode, (serviceProvider, user, cancellationToken) => 
            Task.FromResult(user.HasOptedInForPromo)
        )
        .NotEmpty((_, _) => "Promo code is required when promo is opted-in");
    }
}
```

---

## 6. Interface Validation & Cascading

The library supports validating properties defined on interfaces (`IEntityValidation`). The Roslyn Source Generator automatically weaves these interface validations into the concrete class validators that implement them.

### Step 1: Define an Interface inheriting from `IEntityValidation`
```csharp
public interface IDeviceIdValidation : IEntityValidation
{
    public Guid DeviceId { get; set; }
}
```

### Step 2: Create a Validator for the Interface
```csharp
public class DeviceIdValidator : Validator<IDeviceIdValidation>
{
    public DeviceIdValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty((_, _) => "Device ID cannot be empty");
    }
}
```

### Step 3: Implement the Interface on your Model
Ensure your concrete class is declared as `partial` and inherits from `Validator<ConcreteClass>`.
```csharp
public class Device : IDeviceIdValidation
{
    public Guid DeviceId { get; set; }
    public string Name { get; set; }
}

public partial class DeviceValidator : Validator<Device>
{
    public DeviceValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty((_, _) => "Name is required");
    }
}
```

---

## 7. Nested Object & Collection Validation (`ValidateEntity`)

The library supports validating nested properties and collections of entities (such as `IList<TProperty>`) using `ValidateEntity`. Validators for these nested types are resolved dynamically from the dependency injection container.

```csharp
public class Company
{
    public Address Headquarters { get; set; }
    public List<Employee> Employees { get; set; }
}

public class CompanyValidator : Validator<Company>
{
    public CompanyValidator()
    {
        // Validates nested Headquarters using IValidator<Address> resolved from DI
        ValidateEntity(c => c.Headquarters);

        // Validates each Employee in the list using IValidator<Employee> resolved from DI
        ValidateEntity(c => c.Employees);
    }
}
```

---

## 8. Control Flow (`ValidationErrorHandle`)

Each validation method chains an optional `ValidationErrorHandle` parameter:

- `ValidationErrorHandle.Continue` (Default): Continues validating other properties and rules.
- `ValidationErrorHandle.StopProperty`: Halts executing further rules *on the current property*, but continues validating other properties.
- `ValidationErrorHandle.StopAll`: Instantly aborts validation of the entire request, returning the errors accumulated so far.

---

## 9. Standard API Reference for AI Agents

Ensure you utilize the correct rule methods available on the properties:

| Method                            | Description                                                                                                                       |
|:----------------------------------|:----------------------------------------------------------------------------------------------------------------------------------|
| `NotNull(msg, handle)`            | Property must not be null.                                                                                                        |
| `NotEmpty(msg, handle)`           | String must not be null/empty, collection must have items, Guid must not be empty.                                                |
| `EmailAddress(msg, handle)`       | Validates string matches standard email formatting.                                                                               |
| `Equal(val, msg, handle)`         | Property value must equal constant value.                                                                                         |
| `Equal(expr, msg, handle)`        | Property value must equal another property on the same entity.                                                                    |
| `NotEqual(val, msg, handle)`      | Property value must not equal constant value.                                                                                     |
| `NotEqual(expr, msg, handle)`     | Property value must not equal another property on the same entity.                                                                |
| `Minimum(val, msg, handle)`       | Property must be `>= val`.                                                                                                        |
| `Maximum(val, msg, handle)`       | Property must be `<= val`.                                                                                                        |
| `MinimumLength(len, msg, handle)` | String length must be `>= len`.                                                                                                   |
| `MaximumLength(len, msg, handle)` | String length must be `<= len`.                                                                                                   |
| `Between(min, max, msg, handle)`  | Property value must be between `min` and `max` (inclusive).                                                                       |
| `Matches(regex, msg, handle)`     | String must match specified regex pattern.                                                                                        |
| `Must(predicate, msg, handle)`    | Asynchronous predicate returning `Task<bool>` with a static/localization message.                                                 |
| `Must(dynamicPredicate, handle)`  | Asynchronous predicate returning `Task<(bool IsValid, string? ErrorMessage)>` with dynamic error details returned from execution. |
