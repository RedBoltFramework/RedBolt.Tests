// Copyright (c) 2026 RedBolt and contributors.
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

internal static class Program
{
    private static readonly Regex ConditionCoveragePattern = new(
        @"(?<percent>[0-9]+(?:\.[0-9]+)?)%\s*\((?<covered>[0-9]+)\/(?<valid>[0-9]+)\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static int Main(string[] args)
    {
        if (!TryParseArguments(args, out var outputPath, out var inputPaths, out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 2;
        }

        try
        {
            var reports = ExpandReports(inputPaths).ToArray();
            if (reports.Length == 0)
                throw new InvalidOperationException("No Cobertura reports were found.");

            var outputFullPath = Path.GetFullPath(outputPath);
            if (reports.Any(report => PathsEqual(report, outputFullPath)))
                throw new InvalidOperationException("The output report must not also be an input report.");

            var merged = XDocument.Load(reports[0], LoadOptions.PreserveWhitespace);
            foreach (var report in reports.Skip(1))
            {
                var next = XDocument.Load(report, LoadOptions.PreserveWhitespace);
                MergeReports(merged, next);
            }

            RecalculateReportMetrics(merged);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath)!);
            var settings = new XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                NewLineChars = Environment.NewLine,
                NewLineHandling = NewLineHandling.Replace
            };
            using var writer = XmlWriter.Create(outputFullPath, settings);
            merged.Save(writer);

            Console.WriteLine($"Merged {reports.Length} Cobertura reports into {outputFullPath}");
            PrintSummary(merged.Root!);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Coverage merge failed: {exception.Message}");
            return 1;
        }
    }

    private static bool TryParseArguments(
        string[] args,
        out string outputPath,
        out List<string> inputPaths,
        out string error)
    {
        outputPath = string.Empty;
        inputPaths = [];
        error = string.Empty;

        for (var index = 0; index < args.Length; ++index)
        {
            switch (args[index])
            {
                case "--output" or "-o":
                    if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                    {
                        error = "--output requires a path.";
                        return false;
                    }

                    outputPath = args[index];
                    break;

                case "--help" or "-h":
                    error = string.Empty;
                    PrintUsage();
                    return false;

                default:
                    if (args[index].StartsWith("-", StringComparison.Ordinal))
                    {
                        error = $"Unknown option '{args[index]}'.";
                        return false;
                    }

                    inputPaths.Add(args[index]);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            error = "An output path is required.";
            return false;
        }

        if (inputPaths.Count == 0)
        {
            error = "At least one Cobertura report or report directory is required.";
            return false;
        }

        return true;
    }

    private static IEnumerable<string> ExpandReports(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (Path.GetFileName(path).Equals("coverage.cobertura.xml", StringComparison.OrdinalIgnoreCase))
                {
                    var fullPath = Path.GetFullPath(path);
                    if (seen.Add(fullPath))
                        yield return fullPath;
                }

                continue;
            }

            if (!Directory.Exists(path))
                throw new FileNotFoundException($"Coverage input was not found: {path}");

            foreach (var report in Directory.EnumerateFiles(
                         path, "coverage.cobertura.xml", SearchOption.AllDirectories).OrderBy(item => item,
                             StringComparer.Ordinal))
            {
                var fullPath = Path.GetFullPath(report);
                if (seen.Add(fullPath))
                    yield return fullPath;
            }
        }
    }

    private static void MergeReports(XDocument target, XDocument source)
    {
        var targetRoot = target.Root ?? throw new InvalidDataException("Target report has no root element.");
        var sourceRoot = source.Root ?? throw new InvalidDataException("Source report has no root element.");
        var targetPackages = targetRoot.Element("packages") ?? AddElement(targetRoot, "packages");
        var sourcePackages = sourceRoot.Element("packages");
        if (sourcePackages is null)
            return;

        foreach (var sourcePackage in sourcePackages.Elements("package"))
        {
            var packageName = (string?)sourcePackage.Attribute("name") ?? string.Empty;
            var targetPackage = targetPackages.Elements("package")
                .FirstOrDefault(package => string.Equals(
                    (string?)package.Attribute("name"), packageName, StringComparison.Ordinal));
            if (targetPackage is null)
            {
                targetPackages.Add(new XElement(sourcePackage));
                continue;
            }

            var targetClasses = targetPackage.Element("classes") ?? AddElement(targetPackage, "classes");
            var sourceClasses = sourcePackage.Element("classes");
            if (sourceClasses is null)
                continue;

            foreach (var sourceClass in sourceClasses.Elements("class"))
            {
                var className = (string?)sourceClass.Attribute("name") ?? string.Empty;
                var fileName = (string?)sourceClass.Attribute("filename") ?? string.Empty;
                var targetClass = targetClasses.Elements("class").FirstOrDefault(item =>
                    string.Equals((string?)item.Attribute("name"), className, StringComparison.Ordinal)
                    && string.Equals((string?)item.Attribute("filename"), fileName, StringComparison.Ordinal));
                if (targetClass is null)
                    targetClasses.Add(new XElement(sourceClass));
                else
                    MergeClasses(targetClass, sourceClass);
            }
        }
    }

    private static void MergeClasses(XElement targetClass, XElement sourceClass)
    {
        MergeLineCollections(GetOrCreateLines(targetClass), sourceClass.Element("lines"));

        var sourceMethods = sourceClass.Element("methods");
        if (sourceMethods is null)
            return;

        var targetMethods = targetClass.Element("methods") ?? AddElement(targetClass, "methods");
        foreach (var sourceMethod in sourceMethods.Elements("method"))
        {
            var methodName = (string?)sourceMethod.Attribute("name") ?? string.Empty;
            var signature = (string?)sourceMethod.Attribute("signature") ?? string.Empty;
            var targetMethod = targetMethods.Elements("method").FirstOrDefault(item =>
                string.Equals((string?)item.Attribute("name"), methodName, StringComparison.Ordinal)
                && string.Equals((string?)item.Attribute("signature"), signature, StringComparison.Ordinal));
            if (targetMethod is null)
                targetMethods.Add(new XElement(sourceMethod));
            else
                MergeLineCollections(GetOrCreateLines(targetMethod), sourceMethod.Element("lines"));
        }
    }

    private static void MergeLineCollections(XElement targetLines, XElement? sourceLines)
    {
        if (sourceLines is null)
            return;

        foreach (var sourceLine in sourceLines.Elements("line"))
        {
            var lineNumber = (string?)sourceLine.Attribute("number") ?? string.Empty;
            var targetLine = targetLines.Elements("line").FirstOrDefault(item =>
                string.Equals((string?)item.Attribute("number"), lineNumber, StringComparison.Ordinal));
            if (targetLine is null)
            {
                targetLines.Add(new XElement(sourceLine));
                continue;
            }

            MergeLine(targetLine, sourceLine);
        }
    }

    private static void MergeLine(XElement targetLine, XElement sourceLine)
    {
        var targetHits = ParseLong(targetLine.Attribute("hits"));
        var sourceHits = ParseLong(sourceLine.Attribute("hits"));
        targetLine.SetAttributeValue("hits", Math.Max(targetHits, sourceHits));

        if (IsTrue(targetLine.Attribute("branch")) || IsTrue(sourceLine.Attribute("branch")))
            targetLine.SetAttributeValue("branch", "True");

        var targetCoverage = ParseCoverage((string?)targetLine.Attribute("condition-coverage"));
        var sourceCoverage = ParseCoverage((string?)sourceLine.Attribute("condition-coverage"));
        if (targetCoverage.Valid == 0 && sourceCoverage.Valid == 0)
            return;

        // Cobertura does not expose stable IDs for each branch outcome. Its
        // condition number identifies the decision, not the individual edge.
        // Summing covered edges and capping at the shared denominator gives a
        // useful union for separate backend runs, while avoiding >100% rates.
        var valid = Math.Max(targetCoverage.Valid, sourceCoverage.Valid);
        var covered = Math.Min(valid, targetCoverage.Covered + sourceCoverage.Covered);
        targetLine.SetAttributeValue("condition-coverage", FormatCoverage(covered, valid));

        var sourceConditions = sourceLine.Element("conditions");
        if (sourceConditions is null)
            return;

        var targetConditions = targetLine.Element("conditions") ?? AddElement(targetLine, "conditions");
        foreach (var sourceCondition in sourceConditions.Elements("condition"))
        {
            var number = (string?)sourceCondition.Attribute("number") ?? string.Empty;
            var type = (string?)sourceCondition.Attribute("type") ?? string.Empty;
            var targetCondition = targetConditions.Elements("condition").FirstOrDefault(item =>
                string.Equals((string?)item.Attribute("number"), number, StringComparison.Ordinal)
                && string.Equals((string?)item.Attribute("type"), type, StringComparison.Ordinal));
            if (targetCondition is null)
            {
                targetConditions.Add(new XElement(sourceCondition));
                continue;
            }

            var targetPercent = ParsePercent((string?)targetCondition.Attribute("coverage"));
            var sourcePercent = ParsePercent((string?)sourceCondition.Attribute("coverage"));
            targetCondition.SetAttributeValue("coverage", FormatPercent(Math.Max(targetPercent, sourcePercent)));
        }
    }

    private static void RecalculateReportMetrics(XDocument document)
    {
        var root = document.Root ?? throw new InvalidDataException("Coverage report has no root element.");
        var classes = root.Element("packages")?.Elements("package")
            .SelectMany(package => package.Element("classes")?.Elements("class") ?? [])
            .ToArray() ?? [];

        foreach (var method in classes.SelectMany(item => item.Element("methods")?.Elements("method") ?? []))
            UpdateRateAttributes(method, AggregateLines(method.Element("lines")));
        foreach (var @class in classes)
            UpdateRateAttributes(@class, AggregateLines(@class.Element("lines")));

        foreach (var package in root.Element("packages")?.Elements("package") ?? [])
        {
            var packageClasses = package.Element("classes")?.Elements("class") ?? [];
            UpdateRateAttributes(package, AggregateLines(packageClasses.SelectMany(
                @class => @class.Element("lines")?.Elements("line") ?? [])));
        }

        var totals = AggregateLines(classes.SelectMany(
            @class => @class.Element("lines")?.Elements("line") ?? []));
        UpdateRootAttributes(root, totals);
    }

    private static CoverageTotals AggregateLines(XElement? lines)
    {
        return AggregateLines(lines?.Elements("line") ?? []);
    }

    private static CoverageTotals AggregateLines(IEnumerable<XElement> lines)
    {
        var lineList = lines.ToArray();
        var linesCovered = lineList.Count(line => ParseLong(line.Attribute("hits")) > 0);
        var branches = lineList
            .Select(line => ParseCoverage((string?)line.Attribute("condition-coverage")))
            .Where(coverage => coverage.Valid > 0)
            .ToArray();

        return new CoverageTotals(
            LinesCovered: linesCovered,
            LinesValid: lineList.Length,
            BranchesCovered: branches.Sum(coverage => coverage.Covered),
            BranchesValid: branches.Sum(coverage => coverage.Valid));
    }

    private static void UpdateRateAttributes(XElement element, CoverageTotals totals)
    {
        element.SetAttributeValue("line-rate", FormatRate(totals.LinesCovered, totals.LinesValid));
        element.SetAttributeValue("branch-rate", FormatRate(totals.BranchesCovered, totals.BranchesValid));
    }

    private static void UpdateRootAttributes(XElement root, CoverageTotals totals)
    {
        UpdateRateAttributes(root, totals);
        root.SetAttributeValue("lines-covered", totals.LinesCovered);
        root.SetAttributeValue("lines-valid", totals.LinesValid);
        root.SetAttributeValue("branches-covered", totals.BranchesCovered);
        root.SetAttributeValue("branches-valid", totals.BranchesValid);
        root.SetAttributeValue("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private static XElement GetOrCreateLines(XElement element) =>
        element.Element("lines") ?? AddElement(element, "lines");

    private static XElement AddElement(XElement parent, string name)
    {
        var child = new XElement(name);
        parent.Add(child);
        return child;
    }

    private static CoveragePair ParseCoverage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return default;

        var match = ConditionCoveragePattern.Match(value);
        if (!match.Success)
            return default;

        return new CoveragePair(
            int.Parse(match.Groups["covered"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["valid"].Value, CultureInfo.InvariantCulture));
    }

    private static double ParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0.0;
        var percent = value.Trim().TrimEnd('%');
        return double.TryParse(percent, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0.0;
    }

    private static long ParseLong(XAttribute? attribute) =>
        attribute is not null && long.TryParse(attribute.Value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static bool IsTrue(XAttribute? attribute) =>
        attribute is not null && attribute.Value.Equals("True", StringComparison.OrdinalIgnoreCase);

    private static string FormatCoverage(int covered, int valid) =>
        $"{FormatPercent(valid == 0 ? 0.0 : 100.0 * covered / valid)} ({covered}/{valid})";

    private static string FormatPercent(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string FormatRate(int covered, int valid) =>
        (valid == 0 ? 1.0 : (double)covered / valid).ToString("0.########", CultureInfo.InvariantCulture);

    private static bool PathsEqual(string first, string second) =>
        string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void PrintSummary(XElement root)
    {
        Console.WriteLine($"Line coverage: {FormatPercent(ParseDouble((string?)root.Attribute("line-rate")) * 100.0)} "
            + $"({(string?)root.Attribute("lines-covered") ?? "0"}/{(string?)root.Attribute("lines-valid") ?? "0"} lines)");
        Console.WriteLine($"Branch coverage: {FormatPercent(ParseDouble((string?)root.Attribute("branch-rate")) * 100.0)} "
            + $"({(string?)root.Attribute("branches-covered") ?? "0"}/{(string?)root.Attribute("branches-valid") ?? "0"} branches)");
    }

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0.0;

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: coverage-merge --output <coverage.cobertura.xml> <report.xml|report-dir> [...]");
    }

    private readonly record struct CoveragePair(int Covered, int Valid);

    private readonly record struct CoverageTotals(
        int LinesCovered,
        int LinesValid,
        int BranchesCovered,
        int BranchesValid);
}
