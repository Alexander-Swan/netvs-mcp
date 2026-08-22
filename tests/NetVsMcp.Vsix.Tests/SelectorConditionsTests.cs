using System;
using System.Windows.Automation;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public sealed class SelectorConditionsTests
{
    [Theory]
    [InlineData("id=SubmitButton", "SubmitButton")]
    [InlineData("automationid=SubmitButton", "SubmitButton")]
    [InlineData("automation-id=SubmitButton", "SubmitButton")]
    [InlineData(" id = SubmitButton ", "SubmitButton")]
    public void BuildSelectorCondition_IdPrefix_MatchesAutomationIdProperty(string selector, string expectedValue)
    {
        var condition = Assert.IsType<PropertyCondition>(SelectorConditions.BuildSelectorCondition(selector));
        Assert.Equal(AutomationElement.AutomationIdProperty, condition.Property);
        Assert.Equal(expectedValue, condition.Value);
    }

    [Theory]
    [InlineData("name=Recent Files")]
    [InlineData("text=Recent Files")]
    public void BuildSelectorCondition_NamePrefix_MatchesNameProperty(string selector)
    {
        var condition = Assert.IsType<PropertyCondition>(SelectorConditions.BuildSelectorCondition(selector));
        Assert.Equal(AutomationElement.NameProperty, condition.Property);
        Assert.Equal("Recent Files", condition.Value);
    }

    [Theory]
    [InlineData("class=Button")]
    [InlineData("classname=Button")]
    [InlineData("class-name=Button")]
    public void BuildSelectorCondition_ClassPrefix_MatchesClassNameProperty(string selector)
    {
        var condition = Assert.IsType<PropertyCondition>(SelectorConditions.BuildSelectorCondition(selector));
        Assert.Equal(AutomationElement.ClassNameProperty, condition.Property);
        Assert.Equal("Button", condition.Value);
    }

    [Theory]
    [InlineData("type=button")]
    [InlineData("controltype=button")]
    [InlineData("control-type=button")]
    public void BuildSelectorCondition_TypePrefix_MatchesControlTypeProperty(string selector)
    {
        var condition = Assert.IsType<PropertyCondition>(SelectorConditions.BuildSelectorCondition(selector));
        Assert.Equal(AutomationElement.ControlTypeProperty, condition.Property);
        // PropertyCondition boxes a ControlType as its underlying UIA numeric id, not the
        // ControlType object itself - that's how AutomationElement.ControlTypeProperty is
        // registered, not something ControlTypeCondition controls.
        Assert.Equal(ControlType.Button.Id, condition.Value);
    }

    [Fact]
    public void BuildSelectorCondition_BareText_MatchesNameOrAutomationIdOrClassName()
    {
        var condition = Assert.IsType<OrCondition>(SelectorConditions.BuildSelectorCondition("SubmitButton"));
        var children = condition.GetConditions();
        Assert.Equal(3, children.Length);
        Assert.All(children, child => Assert.IsType<PropertyCondition>(child));
    }

    [Fact]
    public void BuildSelectorCondition_UnrecognizedKeyPrefix_FallsBackToTextCondition()
    {
        // A "key=value" shape with a key nobody recognizes (e.g. a value that happens to
        // contain "=") should degrade to matching just the value as free text, not throw.
        var condition = SelectorConditions.BuildSelectorCondition("nonsense=value");
        Assert.IsType<OrCondition>(condition);
    }

    [Theory]
    [InlineData("edit", "Edit")]
    [InlineData("textbox", "Edit")]
    [InlineData("ControlType.Button", "Button")]
    [InlineData("HYPERLINK", "Hyperlink")]
    [InlineData("link", "Hyperlink")]
    [InlineData("totally-unknown-type", "Custom")]
    public void ControlTypeCondition_MapsFriendlyNamesToControlTypes(string value, string expectedControlTypeName)
    {
        var expected = expectedControlTypeName switch
        {
            "Edit" => ControlType.Edit,
            "Button" => ControlType.Button,
            "Hyperlink" => ControlType.Hyperlink,
            "Custom" => ControlType.Custom,
            _ => throw new ArgumentOutOfRangeException(nameof(expectedControlTypeName))
        };

        var condition = Assert.IsType<PropertyCondition>(SelectorConditions.ControlTypeCondition(value));
        // PropertyCondition boxes a ControlType as its underlying UIA numeric id, not the
        // ControlType object itself.
        Assert.Equal(expected.Id, condition.Value);
    }

    [Fact]
    public void BuildSelectorCondition_EmptyOrNull_DoesNotThrow()
    {
        Assert.IsType<OrCondition>(SelectorConditions.BuildSelectorCondition(""));
        Assert.IsType<OrCondition>(SelectorConditions.BuildSelectorCondition(null!));
    }
}
