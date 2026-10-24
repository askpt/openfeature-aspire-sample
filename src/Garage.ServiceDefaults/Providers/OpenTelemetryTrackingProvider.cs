using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenFeature;
using OpenFeature.Model;

namespace Garage.ServiceDefaults.Providers;

/// <summary>
/// Decorates a <see cref="FeatureProvider"/> so that OpenFeature tracking events are exported through
/// OpenTelemetry. Flag evaluations are forwarded untouched to the inner provider; <see cref="Track"/>
/// records a counter and a value histogram (for dashboards) and adds an event to the current span.
/// </summary>
/// <remarks>
/// Primitive entries of the <see cref="TrackingEventDetails"/> become metric attributes, so callers
/// should keep them low-cardinality (for example the flag key and the served variant).
/// </remarks>
internal sealed class OpenTelemetryTrackingProvider : FeatureProvider, IDisposable
{
    public const string MeterName = "Garage.OpenFeature.Tracking";

    private const string EventNameAttribute = "feature_flag.tracking.event_name";
    private const string ProviderNameAttribute = "feature_flag.provider.name";
    private const string ContextIdAttribute = "feature_flag.context.id";
    private const string ValueAttribute = "feature_flag.tracking.value";
    private const string SpanEventName = "feature_flag.tracking";

    private readonly FeatureProvider _inner;
    private readonly Counter<long> _events;
    private readonly Histogram<double> _values;

    public OpenTelemetryTrackingProvider(FeatureProvider inner, IMeterFactory meterFactory)
    {
        _inner = inner;

        var meter = meterFactory.Create(MeterName);
        _events = meter.CreateCounter<long>(
            "feature_flag.tracking.events",
            unit: "{event}",
            description: "Number of OpenFeature tracking events.");
        _values = meter.CreateHistogram<double>(
            "feature_flag.tracking.value",
            description: "Value attached to OpenFeature tracking events.");
    }

    public override Metadata? GetMetadata() => _inner.GetMetadata();

    public override IImmutableList<Hook> GetProviderHooks() => _inner.GetProviderHooks();

    public override Task InitializeAsync(EvaluationContext context, CancellationToken cancellationToken = default)
        => _inner.InitializeAsync(context, cancellationToken);

    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
        => _inner.ShutdownAsync(cancellationToken);

    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => _inner.ResolveBooleanValueAsync(flagKey, defaultValue, context, cancellationToken);

    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => _inner.ResolveStringValueAsync(flagKey, defaultValue, context, cancellationToken);

    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => _inner.ResolveIntegerValueAsync(flagKey, defaultValue, context, cancellationToken);

    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => _inner.ResolveDoubleValueAsync(flagKey, defaultValue, context, cancellationToken);

    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
        => _inner.ResolveStructureValueAsync(flagKey, defaultValue, context, cancellationToken);

    public override void Track(string trackingEventName, EvaluationContext? evaluationContext = null, TrackingEventDetails? trackingEventDetails = null)
    {
        var tags = new TagList
        {
            { EventNameAttribute, trackingEventName },
            { ProviderNameAttribute, _inner.GetMetadata()?.Name },
        };

        if (trackingEventDetails is not null)
        {
            foreach (var (key, value) in trackingEventDetails)
            {
                if (TryGetPrimitive(value, out var primitive))
                {
                    tags.Add(key, primitive);
                }
            }
        }

        _events.Add(1, tags);
        if (trackingEventDetails?.Value is { } trackedValue)
        {
            _values.Record(trackedValue, tags);
        }

        if (Activity.Current is { } activity)
        {
            var spanTags = new ActivityTagsCollection(tags);
            if (evaluationContext?.TargetingKey is { } targetingKey)
            {
                spanTags[ContextIdAttribute] = targetingKey;
            }
            if (trackingEventDetails?.Value is { } value)
            {
                spanTags[ValueAttribute] = value;
            }
            activity.AddEvent(new ActivityEvent(SpanEventName, tags: spanTags));
        }

        _inner.Track(trackingEventName, evaluationContext, trackingEventDetails);
    }

    public void Dispose() => (_inner as IDisposable)?.Dispose();

    private static bool TryGetPrimitive(Value value, out object? primitive)
    {
        primitive = value switch
        {
            { IsString: true } => value.AsString,
            { IsNumber: true } => value.AsDouble,
            { IsBoolean: true } => value.AsBoolean,
            { IsDateTime: true } => value.AsDateTime?.ToString("O"),
            _ => null,
        };
        return primitive is not null;
    }
}
