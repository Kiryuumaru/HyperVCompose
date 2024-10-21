using Domain.VirtualMachine.Enums;
using Domain.VirtualMachine.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Vagrant.Models;

public class VirtualMachineReplicaRuntime : VirtualMachineReplica
{
    public required string? IPAddress { get; init; }

    public required VirtualMachineReplicaState State { get; init; }
}
