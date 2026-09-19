using RenameConv;

ApplicationConfiguration.Initialize();
using var application = new RenameConvApplication(args);
return await application.RunAsync();
