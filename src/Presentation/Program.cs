using CliFx;

return await new CliApplicationBuilder()
    .SetExecutableName("hvcc")
    .AddCommandsFromThisAssembly()
    .Build()
    .RunAsync();
