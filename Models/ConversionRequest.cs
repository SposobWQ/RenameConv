namespace RenameConv.Models;

internal sealed record ConversionRequest(string TargetPath, string SourceExtension, string TargetExtension);
