# SebastianGuzmanMorla.Validator

Una librería de validación para .NET que permite crear validadores fuertemente tipados para entidades, con soporte para reglas de validación personalizadas, validaciones condicionales y generación automática de código.

## Características

- **Validación fuertemente tipada**: Crea validadores específicos para cada tipo de entidad.
- **Reglas de propiedad**: Define reglas de validación para propiedades individuales.
- **Validaciones condicionales**: Ejecuta validaciones solo cuando se cumplen ciertas condiciones.
- **Validaciones de interfaz**: Soporte para validaciones adicionales a través de interfaces.
- **Generador de código**: Genera automáticamente interfaces y configuraciones de servicios usando Roslyn.
- **Integración con DI**: Compatible con Microsoft.Extensions.DependencyInjection.
- **Manejo de errores flexible**: Controla cómo se manejan los errores de validación (continuar o detener).

## Instalación

Instala el paquete NuGet:

```bash
dotnet add package SebastianGuzmanMorla.Validator
```

## Uso Básico

### 1. Crear un validador

Primero, define la entidad que quieres validar:

```csharp
public class User
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public int? Age { get; set; }
}
```

Luego, crea una clase que herede de `Validator<TEntity>`:

```csharp
using SebastianGuzmanMorla.Validator;

public class UserValidator : Validator<User>
{
    public UserValidator()
    {
        RuleFor(u => u.Name)
            .NotNull((_, _) => "El nombre no puede ser nulo")
            .NotEmpty((_, _) => "El nombre es obligatorio");

        RuleFor(u => u.Email)
            .NotNull((_, _) => "El email no puede ser nulo")
            .NotEmpty((_, _) => "El email es obligatorio")
            .EmailAddress((_, _) => "El email debe ser válido");

        RuleFor(u => u.Age)
            .NotNull((_, _) => "La edad no puede ser nula")
            .Minimum(1, (_, _) => "La edad debe ser mayor a 0");
    }
}
```

### 2. Configuración de servicios

El generador de código automáticamente registra los validadores en el contenedor de DI. Solo necesitas tener una clase parcial `ConfigureServices`:

```csharp
public partial class ConfigureServices
{
    // El generador agregará el código aquí

    public static IServiceCollection ConfigureDomain(this IServiceCollection services)
    {
        ConfigureGenerated(services);

        return services;
    }
}
```

### 3. Usar el validador

En una aplicación ASP.NET Core con Minimal API, puedes validar directamente en los endpoints:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureDomain();

WebApplication app = builder.Build();

app.MapPost("/users", async (User user, [FromServices] IServiceProvider serviceProvider, [FromServices] IValidator<User> validator) =>
{
    var result = await validator.Validate(user, serviceProvider);
    
    if (!result.IsValid)
    {
        return Results.BadRequest(new 
        { 
            Errors = result.Errors 
        });
    }
    
    // Procesar el usuario válido
    return Results.Created($"/users/{user.Id}", user);
});

app.Run();
```

Este ejemplo muestra cómo:

- Inyectar `IValidator<User>` usando `[FromServices]` en el endpoint
- Usar el `IServiceProvider` inyectado para la validación
- Retornar errores de validación con `BadRequest`
- Continuar con la lógica si la validación es exitosa


## Ejemplos Avanzados

### Validación con interfaces y localización

Puedes crear validadores que implementen interfaces y usen servicios de localización para mensajes de error personalizados.

Primero, define la interfaz de validación:

```csharp
public interface IDeviceIdValidation : IEntityValidation
{
    public Guid DeviceId { get; set; }
}
```

Crea una entidad que implemente la interfaz:

```csharp
public class Device : IDeviceIdValidation
{
    public Guid DeviceId { get; set; }
    public string? Name { get; set; }
    public string? Model { get; set; }
}
```

Luego, crea el validador:

```csharp
public class DeviceIdValidator : Validator<IDeviceIdValidation>
{
    public DeviceIdValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotNull(
                (x, _) => x.GetRequiredService<IGeneralLocalization>().NotNull(nameof(IDeviceIdValidation.DeviceId)),
                ValidationErrorHandle.StopProperty)
            .NotEmpty(
                (x, _) => x.GetRequiredService<IGeneralLocalization>().NotEmpty(nameof(IDeviceIdValidation.DeviceId)),
                ValidationErrorHandle.StopProperty)
            .Must(DeviceIdExist, (x, _) =>
            {
                string label = x.GetRequiredService<IGeneralLocalization>().Device;

                return x.GetRequiredService<IGeneralLocalization>().NotExists(label);
            }, ValidationErrorHandle.StopAll);
    }

    private static Task<bool> DeviceIdExist(IServiceProvider provider, IDeviceIdValidation entity,
        CancellationToken cancellationToken = default)
    {
        return provider
            .GetRequiredService<IDeviceRepository>()
            .Any(entity.DeviceId, cancellationToken);
    }
}

public partial class DeviceValidator : Validator<Device>
{
    public DeviceValidator()
    {
        // Validaciones específicas para Device
        RuleFor(d => d.Name)
            .NotNull((_, _) => "El nombre del dispositivo no puede ser nulo")
            .NotEmpty((_, _) => "El nombre del dispositivo es obligatorio")
            .MinimumLength(2, (_, _) => "El nombre debe tener al menos 2 caracteres");

        RuleFor(d => d.Model)
            .NotNull((_, _) => "El modelo del dispositivo no puede ser nulo")
            .NotEmpty((_, _) => "El modelo del dispositivo es obligatorio");
    }
}
```

El generador de código automáticamente creará:

```csharp
// Código generado automáticamente para ConfigureServices
public static partial class ConfigureServices
{
    private static partial void ConfigureGenerated(IServiceCollection services)
    {
        services.AddSingleton(typeof(IValidator<IDeviceIdValidation>), typeof(DeviceIdValidator));
        services.AddSingleton(typeof(IValidator<Device>), typeof(DeviceValidator));
        // Otros validadores...
    }
}

// Código generado automáticamente para DeviceValidator
public partial class DeviceValidator
{
    protected override ImmutableArray<Func<IServiceProvider, Device, CancellationToken, Task<ValidationResult>>> InterfaceValidations =>
    [
        (serviceProvider, entity, cancellationToken) => serviceProvider.GetRequiredService<IValidator<IDeviceIdValidation>>().Validate(entity, serviceProvider, cancellationToken)
    ];
}
```

Este ejemplo muestra:
- Uso de interfaces (`IDeviceIdValidation`)
- Entidad concreta (`Device`) que implementa la interfaz
- Mensajes de error localizados usando servicios inyectados
- Validaciones personalizadas con `Must`
- Control de flujo de errores (`ValidationErrorHandle.StopProperty`, `ValidationErrorHandle.StopAll`)
- Código generado automáticamente por el generador

## API

### Métodos principales

- `RuleFor(propertyExpression)`: Define reglas para una propiedad.
- `NotNull(messageFunc)`: Valida que la propiedad no sea nula.
- `NotEmpty(messageFunc)`: Valida que la propiedad no esté vacía.
- `EmailAddress(messageFunc)`: Valida formato de email.
- `Minimum(value, messageFunc)`: Valida que el valor sea mayor o igual al mínimo.
- `Maximum(value, messageFunc)`: Valida que el valor sea menor o igual al máximo.
- `Must(predicate, messageFunc)`: Valida usando una función personalizada.
- `When(condition)`: Ejecuta la validación solo si se cumple la condición.

### Resultado de validación

`ValidationResult` contiene:
- `IsValid`: Indica si la validación fue exitosa.
- `Errors`: Diccionario de errores por propiedad.

## Generador de Código

La librería incluye generadores de Roslyn que:
- Registra automáticamente los validadores en el contenedor de DI.
- Genera código para ejecutar validaciones de interfaces en cascada.

## Requisitos

- .NET 10.0 o superior
- Microsoft.Extensions.DependencyInjection

## Contribución

¡Las contribuciones son bienvenidas! Por favor, abre un issue o envía un pull request.

## Licencia

Este proyecto está bajo la licencia MIT. Ver [LICENSE](LICENSE) para más detalles.
