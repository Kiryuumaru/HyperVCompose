using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.VirtualMachine.Models;

public class VirtualMachineBuild
{
    public required string Id { get; init; }

    public required string VagrantFileHash { get; init; }

    public required string Rev { get; init; }
}
