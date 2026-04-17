namespace SebastianGuzmanMorla.Validator;

public sealed class ValidationResult
{
    internal ValidationErrorHandle ErrorHandle { get; set; } = ValidationErrorHandle.Continue;

    public bool IsValid => Errors is null || Errors.Count == 0;

    public Dictionary<string, List<string>>? Errors { get; set; }
}