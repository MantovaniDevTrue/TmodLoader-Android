using Tml.Mobile;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Tml.ContentCheck <Terraria-PC/Content> <tModLoader/Content>");
    return 2;
}
var issues = ContentPreflight.Validate(args[0], args[1]);
foreach (var issue in issues) Console.Error.WriteLine($"{issue.Root}: {issue.Asset}: {issue.Reason}");
if (issues.Count != 0) return 1;
Console.WriteLine("Known startup assets found. This does not certify the full content tree or its version.");
return 0;
