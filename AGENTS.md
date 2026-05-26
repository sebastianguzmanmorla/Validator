# AI Agent Integration Guide (`AGENTS.md`)

This document provides system prompt context, syntax, and guidelines for AI coding assistants (such as Cursor, Copilot, Antigravity, etc.) to correctly utilize, configure, and author validators using the `SebastianGuzmanMorla.Validator` library in C# .NET projects.

---

## 1. Overview & Setup

`SebastianGuzmanMorla.Validator` is a strongly-typed, extensible validation library for .NET 10.0+ that combines a Fluent API with a Roslyn Source Generator for automatic DI registration and cascading interface validation.

### DI Registration
To register validators, the consuming project must declare a partial class/method (typically `ConfigureServices`). The source generator automatically outputs the partial method implementation registering all validators as singletons.
```csharp
public partial class ConfigureServices
{
    public static IServiceCollection ConfigureDomain(this IServiceCollection services)
    {
        // The source generator implements this partial method automatically
        RegisterValidators(services);
        return services;
    }
}
```

---

## 2. Defining Validators

To create a validator for any model/class, inherit from `Validator<TEntity>` and define rules inside the constructor using `RuleFor`.

```csharp
using SebastianGuzmanMorla.Validator;

public class UserValidator : Validator<User>
{
    public UserValidator()
    {
        RuleFor(u => u.Name)
            .NotNull((_, _) => "Name cannot be null")
            .NotEmpty((_, _) => "Name is required");

        RuleFor(u => u.Email)
            .NotNull((_, _) => "Email cannot be null")
            .EmailAddress((_, _) => "Email must be valid");
    }
}
```

---

## 3. Dynamic & Localized Error Messages

Error messages are always specified as a lambda expression of type `Func<IServiceProvider, TEntity, string>`. This allows accessing registered services (such as localization repositories, database contexts, or configuration files) directly at validation time.

```csharp
RuleFor(x => x.DeviceId)
    .NotNull((serviceProvider, entity) => 
        serviceProvider.GetRequiredService<IGeneralLocalization>().NotNull(nameof(entity.DeviceId))
    );
```

---

## 4. Control Flow (`ValidationErrorHandle`)

Each chained rule accepts an optional `ValidationErrorHandle` parameter to determine how the validator behaves when that specific rule fails:

* **`ValidationErrorHandle.Continue` (Default)**: Continues executing all other rules on this property and subsequent properties.
* **`ValidationErrorHandle.StopProperty`**: Prevents executing any further rules on *the current property*, but continues validating other properties of the entity.
* **`ValidationErrorHandle.StopAll`**: Immediately halts all validation execution for this entity and returns the accumulated errors up to this point.

**Example:**
```csharp
RuleFor(x => x.Password)
    .NotNull((_, _) => "Password is required", ValidationErrorHandle.StopProperty)
    .MinimumLength(8, (_, _) => "Password must be at least 8 characters");
```

---

## 5. Conditional Validation (`RuleForWhen`)

To define validation rules that only run under specific conditions, use the `RuleForWhen` method in your validator's constructor. The condition is specified as an asynchronous predicate of type `Func<IServiceProvider, TEntity, CancellationToken, Task<bool>>`.

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
*Behind the scenes, the source generator will generate the partial implementation of `DeviceValidator` to execute `DeviceIdValidator` automatically when `Device` is validated.*

---

## 7. Nested Object & Collection Validation (`ValidateEntity`)

The library supports validating nested properties and collections of entities (such as `IList<TProperty>`) using `ValidateEntity`. Validators for these nested types are resolved dynamically from the dependency injection container.

When a validation fails on a nested entity, the errors are reported using nested dot-notation paths (e.g., `Address.Street` or `Phones[0].Number`).

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
        // Validates the nested Headquarters entity using IValidator<Address> resolved from DI
        ValidateEntity(c => c.Headquarters);

        // Validates each Employee in the list using IValidator<Employee> resolved from DI
        ValidateEntity(c => c.Employees);
    }
}
```

---

## 8. Standard API Reference for AI Agents

When writing validation rules, make use of the following chained methods on `ValidationPropertyRules`:

| Method | Description |
| :--- | :--- |
| `NotNull(msg, handle)` | Asserts the property is not `null`. |
| `NotEmpty(msg, handle)` | Asserts a string is not null/empty/whitespace, a collection/array is not empty (`string[]`), or a `Guid`/`Guid?` is not `Guid.Empty`. |
| `EmailAddress(msg, handle)` | Validates email format. |
| `Equal(value, msg, handle)` | Asserts property value equals constant `value`. |
| `Equal(expression, msg, handle)` | Asserts property value equals another property on the entity. |
| `NotEqual(value, msg, handle)` | Asserts property value does not equal constant `value`. |
| `NotEqual(expression, msg, handle)` | Asserts property value does not equal another property on the entity. |
| `Minimum(value, msg, handle)` | Asserts numeric/comparable values are `>= value`. |
| `Maximum(value, msg, handle)` | Asserts numeric/comparable values are `<= value`. |
| `MinimumLength(len, msg, handle)` | Asserts string length is `>= len`. |
| `MaximumLength(len, msg, handle)` | Asserts string length is `<= len`. |
| `Between(min, max, msg, handle)` | Asserts numeric/comparable values are between `min` and `max` (inclusive). |
| `Matches(regex, msg, handle)` | Validates the property against a regex string. |
| `Must(asyncPredicate, msg, handle)`| Executes a custom asynchronous predicate: `Func<IServiceProvider, TEntity, CancellationToken, Task<bool>>`. |
