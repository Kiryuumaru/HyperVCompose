var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Presentation>("presentation-main")
    .WithEnvironment("MAKE_LOGS", "svc");

builder.AddProject<Projects.Presentation>("presentation-listen")
    .WithArgs(["logs", "-f"]);

builder.Build().Run();
