using Domain.VirtualMachine.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.VirtualMachine.Models;

public class VirtualMachineReplica
{
    public required VirtualMachineBuild VirtualMachineBuild { get; init; }

    public required string Id { get; init; }

    public required string Rev { get; init; }

    public required int Cpus { get; init; }

    public required int MemoryGB { get; init; }

    public required int StorageGB { get; init; }
}
