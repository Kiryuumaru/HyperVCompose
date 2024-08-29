var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Presentation>("presentation-main")
    .WithEnvironment("MAKE_LOGS", "svc");

//builder.AddProject<Projects.Presentation>("presentation-listen1")
//    .WithArgs(["logs", "-f"]);

builder.AddProject<Projects.Presentation>("presentation-listen2")
    .WithArgs(["logs", "-t", "100"]);

builder.Build().Run();
