using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

internal class CustomFeatureProvider : FeatureProvider
{
    public override Metadata? GetMetadata()
    {
        return new Metadata("CustomFeatureProvider");
    }

    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        var flagValue = Environment.GetEnvironmentVariable(flagKey);
        if (string.IsNullOrWhiteSpace(flagValue))
        {
            return Task.FromResult(new ResolutionDetails<bool>(flagKey, defaultValue, ErrorType.FlagNotFound));
        }

        // try to parse the flag value
        if (bool.TryParse(flagValue, out var value))
        {
            return Task.FromResult(new ResolutionDetails<bool>(flagKey, value, ErrorType.None));
        }
        else
        {
            return Task.FromResult(new ResolutionDetails<bool>(flagKey, defaultValue, ErrorType.ParseError));
        }
    }

    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        var flagValue = Environment.GetEnvironmentVariable(flagKey);
        if (string.IsNullOrWhiteSpace(flagValue))
        {
            return Task.FromResult(new ResolutionDetails<double>(flagKey, defaultValue, ErrorType.FlagNotFound));
        }

        // try to parse the flag value
        if (double.TryParse(flagValue, out var value))
        {
            return Task.FromResult(new ResolutionDetails<double>(flagKey, value, ErrorType.None));
        }
        else
        {
            return Task.FromResult(new ResolutionDetails<double>(flagKey, defaultValue, ErrorType.ParseError));
        }
    }

    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        var flagValue = Environment.GetEnvironmentVariable(flagKey);
        if (string.IsNullOrWhiteSpace(flagValue))
        {
            return Task.FromResult(new ResolutionDetails<int>(flagKey, defaultValue, ErrorType.FlagNotFound));
        }

        // try to parse the flag value
        if (int.TryParse(flagValue, out var value))
        {
            return Task.FromResult(new ResolutionDetails<int>(flagKey, value, ErrorType.None));
        }
        else
        {
            return Task.FromResult(new ResolutionDetails<int>(flagKey, defaultValue, ErrorType.ParseError));
        }
    }

    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        var flagValue = Environment.GetEnvironmentVariable(flagKey);
        if (string.IsNullOrWhiteSpace(flagValue))
        {
            return Task.FromResult(new ResolutionDetails<string>(flagKey, defaultValue, ErrorType.FlagNotFound));
        }

        return Task.FromResult(new ResolutionDetails<string>(flagKey, flagValue, ErrorType.None));
    }

    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
