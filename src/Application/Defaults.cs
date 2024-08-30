using AbsolutePathHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application;

public static class Defaults
{
    public static AbsolutePath DataPath { get; } = AbsolutePath.Create("C:\\Users\\Administrator\\source\\repos\\Kiryuumaru\\HyperVCompose\\src\\Presentation") / ".data";
    //public static AbsolutePath DataPath { get; } = AbsolutePath.Create(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)) / "hyperv-compose" / ".data";
    //public static AbsolutePath DataPath { get; } = AbsolutePath.Create(Environment.CurrentDirectory) / ".data";

    public static Guid RuntimeGuid { get; } = Guid.NewGuid();
}
