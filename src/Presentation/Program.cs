using CliFx;

return await new CliApplicationBuilder()
    .SetExecutableName("hvc")
    .AddCommandsFromThisAssembly()
    .Build()
    .RunAsync();
