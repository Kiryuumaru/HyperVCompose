﻿using Domain.VirtualMachine.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.VirtualMachine.Models;

public class VirtualMachineBuild
{
    public required string Id { get; init; }

    public required string Rev { get; init; }

    public required VirtualMachineOSType VirtualMachineOS { get; init; }

    public required Dictionary<string, string?> Labels { get; init; }
}
