﻿using Application;
using CommandLine;
using Newtonsoft.Json.Linq;
using Presentation.Logger.Abstractions;
using Presentation.Logger.LogEventPropertyTypes;
using Serilog.Core;
using Serilog.Events;
using System;

namespace Presentation.Logger.Enrichers;

internal class LogGuidEnricher : ILogEventEnricher
{
    private const string ValueTypeIdentifier = "ValueProperty";
    private readonly Dictionary<string, ILogEventPropertyParser> _propertyParserMap = new()
    {
        [BooleanPropertyParser.Default.TypeIdentifier] = BooleanPropertyParser.Default,
        [DateTimeOffsetPropertyParser.Default.TypeIdentifier] = DateTimeOffsetPropertyParser.Default,
        [DateTimePropertyParser.Default.TypeIdentifier] = DateTimePropertyParser.Default,
        [GuidPropertyParser.Default.TypeIdentifier] = GuidPropertyParser.Default,
        [StringPropertyParser.Default.TypeIdentifier] = StringPropertyParser.Default,
    };

    private static bool HasHeadRuntimeLogs = false;

    public void Enrich(LogEvent evt, ILogEventPropertyFactory _)
    {
        AddProperty(evt, "EventGuid", Guid.NewGuid(), false);
        AddProperty(evt, "RuntimeGuid", Defaults.RuntimeGuid, false);
        if (!HasHeadRuntimeLogs)
        {
            AddProperty(evt, "IsHeadLog", true, false);
            HasHeadRuntimeLogs = true;
        }
        List<LogEventProperty> propsToAdd = [];
        foreach (var prop in evt.Properties)
        {
            propsToAdd.AddRange(GetPropertiesToAdd(evt, prop.Key, prop.Value));
        }
        foreach (var prop in propsToAdd)
        {
            evt.AddOrUpdateProperty(prop);
        }
    }

    private static void AddProperty<T>(LogEvent evt, string key, T value, bool addAndReplace)
    {
        if (key.StartsWith($"{ValueTypeIdentifier}__"))
        {
            return;
        }

        LogEventProperty logEventProperty = new(key, new ScalarValue(value));

        if (addAndReplace)
        {
            evt.AddOrUpdateProperty(logEventProperty);
        }
        else
        {
            evt.AddPropertyIfAbsent(logEventProperty);
        }
    }

    private List<LogEventProperty> GetPropertiesToAdd(LogEvent evt, string key, LogEventPropertyValue valueProp)
    {
        if (key.StartsWith($"{ValueTypeIdentifier}__"))
        {
            return [];
        }

        object? value = (valueProp as ScalarValue)?.Value;
        Type? valueType = value == null ? null : GetUnderlyingType(value);

        if (valueType == null)
        {
            return [];
        }
        if (valueType == typeof(string))
        {

        }

        LogEventProperty? logEventProperty = null;
        LogEventProperty? logEventIdentifierKey = null;

        string? realValuePropertyIdentifierKey = null;

        string valueTypeIdentifierKey = $"{ValueTypeIdentifier}__{key}";
        LogEventPropertyValue? existingTypeIdentifierKeyProp = evt.Properties.GetValueOrDefault(valueTypeIdentifierKey);

        if (existingTypeIdentifierKeyProp != null)
        {
            realValuePropertyIdentifierKey = existingTypeIdentifierKeyProp.Cast<ScalarValue>().Value?.ToString()!;
        }

        realValuePropertyIdentifierKey ??= valueType.Name;

        if (realValuePropertyIdentifierKey != null && _propertyParserMap.TryGetValue(realValuePropertyIdentifierKey, out var logEventPropertyParser))
        {
            if (existingTypeIdentifierKeyProp == null)
            {
                logEventIdentifierKey = new(valueTypeIdentifierKey, new ScalarValue(logEventPropertyParser.TypeIdentifier));
            }
            if (realValuePropertyIdentifierKey != valueType.Name)
            {
                logEventProperty = new(key, new ScalarValue(logEventPropertyParser.Parse(value?.ToString())));
            }
        }

        List<LogEventProperty> logEventProperties = [];
        if (logEventProperty != null)
        {
            logEventProperties.Add(logEventProperty);
        }
        if (logEventIdentifierKey != null)
        {
            logEventProperties.Add(logEventIdentifierKey);
        }
        return logEventProperties;
    }

    private static Type GetUnderlyingType(object obj)
    {
        Type valueType = obj.GetType();
        while (true)
        {
            var underlyingType = Nullable.GetUnderlyingType(valueType);
            if (underlyingType == null)
            {
                break;
            }
            valueType = underlyingType;
        }
        return valueType;
    }
}