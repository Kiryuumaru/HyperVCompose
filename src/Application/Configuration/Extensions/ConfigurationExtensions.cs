using AbsolutePathHelpers;
using Application.Common;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Configuration.Extensions;

public static class ConfigurationExtensions
{
    private static Guid? _runtimeGuid = null;
    private static AbsolutePath? _dataPath = null;

    public static Guid GetRuntimeGuid(this IConfiguration configuration)
    {
        if (_runtimeGuid == null)
        {
            var runtimeGuidStr = configuration.GetVarRefValueOrDefault("RUNTIME_GUID", null);
            if (string.IsNullOrEmpty(runtimeGuidStr))
            {
                _runtimeGuid = Guid.NewGuid();
            }
            else
            {
                _runtimeGuid = Guid.Parse(runtimeGuidStr);
            }
        }
        return _runtimeGuid.Value;
    }

    public static AbsolutePath GetDataPath(this IConfiguration configuration)
    {
        if (_dataPath == null)
        {
            var dataPath = configuration.GetVarRefValueOrDefault("DATA_PATH", null);
            if (string.IsNullOrEmpty(dataPath))
            {
                _dataPath = AbsolutePath.Create("C:\\Hypoer") / ".data";
                //_dataPath = AbsolutePath.Create(Environment.CurrentDirectory) / ".data";
            }
            else
            {
                _dataPath = AbsolutePath.Create(dataPath);
            }
        }
        return _dataPath;
    }
}
