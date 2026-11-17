using System.Collections.Immutable;
using SebastianGuzmanMorla.Validator;
using SebastianGuzmanMorla.Validator.Interfaces;

(string Name, Func<Task> Run)[] checks =
[
    ("Empty and populated collections run the owning and later rules", CollectionContinues),
    ("Collection errors keep their indexes and honor StopProperty and StopAll", CollectionErrors),
    ("Scalar nesting preserves continuation and StopAll", ScalarNesting),
    ("Shared validator keeps dynamic messages local to each call", DynamicMessages),
    ("Direct dynamic calls return one result per invocation", DirectDynamicMessages),
    ("Contravariant dynamic rules retain their per-call message", ContravariantDynamicMessage),
    ("Pre-canceled validation completes as canceled through both overloads", PreCanceled),
    ("Cancellation during the final rule or a skipped property completes as canceled", CanceledCallbacks),
    ("Cancellation crosses nested and interface validation boundaries", CanceledNestedAndInterface),
    ("Successful fixed rules do not evaluate their error message", FixedRuleSuccess)
];

foreach ((string name, Func<Task> run) in checks)
{
    await run().WaitAsync(TimeSpan.FromSeconds(15));
    Console.WriteLine($"PASS {name}");
}

static async Task CollectionContinues()
{
    var validator = new CollectionValidator();
    var provider = new SingleServiceProvider(typeof(IValidator<Child>), new ChildValidator());

    var empty = await validator.Validate(new Parent([], null), provider);
    HasErrors(empty, "$.Children", "$.Title");

    var populated = await validator.Validate(new Parent([new Child("Valid")], null), provider);
    HasErrors(populated, "$.Title");
    Check((await validator.Validate(new Parent([new Child("Valid")], "Valid"), provider)).IsValid,
        "A wholly valid parent should pass.");
}

static async Task CollectionErrors()
{
    var validator = new CollectionValidator();
    var provider = new SingleServiceProvider(typeof(IValidator<Child>), new ChildValidator());

    var continued = await validator.Validate(new Parent([new Child(null), new Child(null)], null), provider);
    HasErrors(continued, "$.Children[0].Name", "$.Children[1].Name", "$.Title");
    Check(continued.Errors!["$.Children[0].Name"].SequenceEqual(["Name is required"]),
        "StopProperty should skip the next rule on the same child's property.");

    var stopped = await validator.Validate(new Parent([new Child(null, Stop: true), new Child(null)], null),
        provider);
    HasErrors(stopped, "$.Children[0].Name");
}

static async Task ScalarNesting()
{
    var validator = new ScalarValidator();
    var provider = new SingleServiceProvider(typeof(IValidator<Child>), new ChildValidator());

    HasErrors(await validator.Validate(new ScalarParent(new Child(null), null), provider),
        "$.Child.Name", "$.Title");
    HasErrors(await validator.Validate(new ScalarParent(new Child(null, Stop: true), null), provider),
        "$.Child.Name");
}

static async Task DynamicMessages()
{
    var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var validator = new TextValidator(async (_, text, _) =>
    {
        if (text.Value == "First")
        {
            started.SetResult();
            await release.Task;
        }

        return (text.Value == "Valid", text.Value);
    });
    var provider = new SingleServiceProvider();

    Task<ValidationResult> first = validator.Validate(new Text("First"), provider);
    await started.Task;
    var second = await validator.Validate(new Text("Second"), provider);
    Check((await validator.Validate(new Text("Valid"), provider)).IsValid, "The valid call should pass.");
    release.SetResult();

    Check((await first).Errors!["$.Value"].SequenceEqual(["First"]), "First message belongs to the first call.");
    Check(second.Errors!["$.Value"].SequenceEqual(["Second"]), "Second message belongs to the second call.");

    var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    int entered = 0;
    var concurrent = new TextValidator(async (_, text, _) =>
    {
        if (Interlocked.Increment(ref entered) == 32)
        {
            pending.SetResult();
        }

        await pending.Task;
        return (false, text.Value);
    });
    var subjects = Enumerable.Range(0, 32).Select(i => new Text($"Message {i}")).ToArray();
    var results = await Task.WhenAll(subjects.Select(text => concurrent.Validate(text, provider)));
    for (int i = 0; i < subjects.Length; i++)
    {
        Check(results[i].Errors!["$.Value"].SequenceEqual([subjects[i].Value]),
            "Each overlapping failure should carry its own message.");
    }
}

static async Task DirectDynamicMessages()
{
    IValidationRule<Text> rule = new ValidationRuleDynamic<Text>(
        (_, text, _) => Task.FromResult((false, (string?)text.Value)),
        ValidationErrorHandle.Continue);
    var provider = new SingleServiceProvider();

    Check(await rule.ValidateWithMessage(provider, new Text("First")) == (false, "First"),
        "Direct callers should get the result and message together.");
    Check(!await rule.Validate(provider, new Text("Second")), "The legacy boolean result remains available.");
    Check(rule.ValidationMessage(provider, new Text("Other")) == string.Empty,
        "The legacy message accessor must not expose previous calls.");
}

static async Task ContravariantDynamicMessage()
{
    IValidationRule<object> rule = new ValidationRuleDynamic<object>(
        (_, _, _) => Task.FromResult((false, (string?)"Base rule message")), ValidationErrorHandle.Continue);
    var result = await new RuleListValidator(rule).Validate(new Text("Value"), new SingleServiceProvider());
    Check(result.Errors!["$.Value"].SequenceEqual(["Base rule message"]),
        "A rule accepting a broader type should keep its message.");
}

static async Task PreCanceled()
{
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var validator = new EmptyValidator();
    var entity = new Text("Valid");
    var provider = new SingleServiceProvider();

    await IsCanceled(validator.Validate(entity, provider, cancellation.Token), cancellation.Token);
    await IsCanceled(((IValidator)validator).Validate(entity, provider, cancellation.Token), cancellation.Token);
}

static async Task CanceledCallbacks()
{
    using var ruleCancellation = new CancellationTokenSource();
    using var conditionCancellation = new CancellationTokenSource();
    var validator = new TextValidator((_, _, _) =>
    {
        ruleCancellation.Cancel();
        return Task.FromResult((true, (string?)null));
    });
    var conditional = new ConditionalValidator(conditionCancellation);
    var provider = new SingleServiceProvider();
    var entity = new Text("Valid");

    await IsCanceled(validator.Validate(entity, provider, ruleCancellation.Token), ruleCancellation.Token);
    await IsCanceled(conditional.Validate(entity, provider, conditionCancellation.Token), conditionCancellation.Token);
}

static async Task CanceledNestedAndInterface()
{
    using var listCancellation = new CancellationTokenSource();
    using var scalarCancellation = new CancellationTokenSource();
    using var interfaceCancellation = new CancellationTokenSource();
    var listProvider = new SingleServiceProvider(typeof(IValidator<Child>), new CancelingChild(listCancellation));
    var scalarProvider = new SingleServiceProvider(typeof(IValidator<Child>), new CancelingChild(scalarCancellation));

    await IsCanceled(new CollectionValidator().Validate(new Parent([new Child("Valid")], "Valid"),
        listProvider, listCancellation.Token), listCancellation.Token);
    await IsCanceled(new ScalarValidator().Validate(new ScalarParent(new Child("Valid"), "Valid"),
        scalarProvider, scalarCancellation.Token), scalarCancellation.Token);
    await IsCanceled(new InterfaceValidator(interfaceCancellation).Validate(new Text("Valid"),
        new SingleServiceProvider(), interfaceCancellation.Token), interfaceCancellation.Token);
}

static async Task FixedRuleSuccess()
{
    var result = await new FixedValidator().Validate(new Text("Valid"), new SingleServiceProvider());
    Check(result.IsValid, "A completed valid fixed rule should still succeed.");
}

static async Task IsCanceled(Task<ValidationResult> task, CancellationToken token)
{
    try
    {
        await task;
    }
    catch (OperationCanceledException error) when (task.IsCanceled && error.CancellationToken == token)
    {
        return;
    }

    throw new InvalidOperationException("Validation did not complete as a canceled task.");
}

static void HasErrors(ValidationResult result, params string[] paths)
{
    Check(!result.IsValid && result.Errors!.Keys.Order().SequenceEqual(paths.Order()),
        $"Expected errors for {string.Join(", ", paths)}, got {string.Join(", ", result.Errors?.Keys ?? Enumerable.Empty<string>())}.");
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed record Child(string? Name, bool Stop = false);
sealed record Parent(List<Child> Children, string? Title);
sealed record ScalarParent(Child Child, string? Title);
sealed record Text(string Value);

sealed class CollectionValidator : Validator<Parent>
{
    public CollectionValidator()
    {
        ValidateEntity(parent => parent.Children);
        RuleFor(parent => parent.Children)
            .Must((_, parent, _) => Task.FromResult(parent.Children.Count > 0), (_, _) => "Children are required");
        RuleFor(parent => parent.Title).NotEmpty((_, _) => "Title is required");
    }
}

sealed class ScalarValidator : Validator<ScalarParent>
{
    public ScalarValidator()
    {
        ValidateEntity(parent => parent.Child);
        RuleFor(parent => parent.Title).NotEmpty((_, _) => "Title is required");
    }
}

sealed class ChildValidator : Validator<Child>
{
    public ChildValidator()
    {
        RuleFor(child => child.Name)
            .Must((_, child, _) => Task.FromResult(!child.Stop),
                (_, _) => "Child stop", ValidationErrorHandle.StopAll)
            .NotEmpty((_, _) => "Name is required", ValidationErrorHandle.StopProperty)
            .Must((_, child, _) => Task.FromResult(child.Name is not null), (_, _) => "Name check repeated");
    }
}

sealed class TextValidator : Validator<Text>
{
    public TextValidator(Func<IServiceProvider, Text, CancellationToken, Task<(bool, string?)>> validate)
    {
        RuleFor(text => text.Value).Must(validate);
    }
}

sealed class EmptyValidator : Validator<Text>;

sealed class RuleListValidator : Validator<Text>
{
    public RuleListValidator(IValidationRule<Text> rule)
    {
        RuleFor(text => text.Value).Rules.Add(rule);
    }
}

sealed class ConditionalValidator : Validator<Text>
{
    public ConditionalValidator(CancellationTokenSource cancellation)
    {
        RuleForWhen(text => text.Value, (_, _, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(false);
        }).Must((_, _, _) => throw new InvalidOperationException("The condition was false."),
            (_, _) => "Unexpected failure");
    }
}

sealed class CancelingChild(CancellationTokenSource cancellation) : IValidator<Child>
{
    public Task<ValidationResult> Validate(object value, IServiceProvider provider, CancellationToken token = default)
        => Validate((Child)value, provider, token);

    public Task<ValidationResult> Validate(Child value, IServiceProvider provider, CancellationToken token = default)
    {
        cancellation.Cancel();
        return Task.FromResult(new ValidationResult());
    }
}

sealed class InterfaceValidator(CancellationTokenSource cancellation) : Validator<Text>
{
    protected override ImmutableArray<Func<IServiceProvider, Text, CancellationToken, Task<ValidationResult>>>
        InterfaceValidations =>
    [
        (_, _, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(new ValidationResult());
        }
    ];
}

sealed class FixedValidator : Validator<Text>
{
    public FixedValidator()
    {
        RuleFor(text => text.Value).Must((_, text, _) => Task.FromResult(text.Value.Length > 0),
            (_, _) => throw new InvalidOperationException("The rule passed."));
    }
}

sealed class SingleServiceProvider(Type? serviceType = null, object? service = null) : IServiceProvider
{
    public object? GetService(Type requested) => requested == serviceType ? service : null;
}
