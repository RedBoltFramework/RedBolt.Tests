namespace RedBolt.Tests;

using System.Reflection;

using RedBolt;
using RedBolt.Core;

public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_returns_help_for_help_alias()
    {
        var parser = new CommandLineParser("RedBolt.Tests");

        var result = parser.Parse(["--help"]);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsHelpRequested);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Fact]
    public void Parse_applies_registered_flags_and_options()
    {
        var parser = new CommandLineParser("RedBolt.Tests");
        var flagTriggered = false;
        var optionValue = "unset";

        parser.AddFlag(["--flag"], "Enable flag", () => flagTriggered = true);
        parser.AddOption(["--name"], "value", "Set value", value => optionValue = value);

        var result = parser.Parse(["--flag", "--name="]);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsHelpRequested);
        Assert.True(flagTriggered);
        Assert.Equal(string.Empty, optionValue);
    }

    [Fact]
    public void Parse_reports_value_mismatches_and_usage_lists_help()
    {
        var parser = new CommandLineParser("RedBolt.Tests");
        parser.AddFlag(["--flag"], "Enable flag", () => { });
        parser.AddOption(["--name"], "value", "Set value", _ => { });

        var missingValue = parser.Parse(["--name"]);
        var unexpectedValue = parser.Parse(["--flag=true"]);
        var usage = parser.GetUsage();

        Assert.False(missingValue.IsSuccess);
        Assert.Equal("Option '--name' requires a value.", missingValue.ErrorMessage);
        Assert.False(unexpectedValue.IsSuccess);
        Assert.Equal("Option '--flag' does not accept a value.", unexpectedValue.ErrorMessage);
        Assert.Contains("Usage: RedBolt.Tests [options]", usage);
        Assert.Contains("--name <value>", usage);
        Assert.Contains("-h, --help", usage);
    }

    [Fact]
    public void Parse_applies_optional_positional_arguments_in_registration_order()
    {
        var parser = new CommandLineParser("RedBolt.Editor");
        var project = string.Empty;
        var command = string.Empty;

        parser.AddPositional("project", "Project to open or create on startup.", value => project = value);
        parser.AddPositional("command", "Command to execute.", value => command = value);

        Assert.True(parser.Parse([]).IsSuccess);
        Assert.True(parser.Parse(["Data/Project", "Build"]).IsSuccess);
        Assert.Equal("Data/Project", project);
        Assert.Equal("Build", command);
        Assert.Equal(
            "Unexpected positional argument 'extra'.",
            parser.Parse(["Data/Project", "Build", "extra"]).ErrorMessage);
    }

    [Fact]
    public void Positional_arguments_are_listed_in_usage_and_report_action_errors()
    {
        var parser = new CommandLineParser("RedBolt.Editor");
        parser.AddPositional("project", "Project to open or create on startup.",
            _ => throw new InvalidOperationException("Invalid project."));

        var result = parser.Parse(["Data/Project"]);
        var usage = parser.GetUsage();

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid project.", result.ErrorMessage);
        Assert.Contains("Usage: RedBolt.Editor [options] [project]", usage);
        Assert.Contains("Arguments:", usage);
        Assert.Contains("project  Project to open or create on startup.", usage);
    }

    [Fact]
    public void Command_line_option_base_operations_are_safe_for_the_wrong_option_shape()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var flagType = typeof(CommandLineParser).GetNestedType("FlagCommandLineOption", BindingFlags.NonPublic)!;
        var flag = flagType.GetConstructors(flags).Single().Invoke(
            [new[] { "--flag" }, "flag", (Action)(static () => { })]);
        flagType.GetMethod("Apply", flags, binder: null, [typeof(string)], modifiers: null)!
            .Invoke(flag, ["ignored"]);

        var valueType = typeof(CommandLineParser).GetNestedType("ValueCommandLineOption", BindingFlags.NonPublic)!;
        var value = valueType.GetConstructors(flags).Single().Invoke(
            [new[] { "--value" }, "value", "text", (Action<string>)(static _ => { })]);
        valueType.GetMethod("Apply", flags, binder: null, Type.EmptyTypes, modifiers: null)!
            .Invoke(value, null);
    }
}
