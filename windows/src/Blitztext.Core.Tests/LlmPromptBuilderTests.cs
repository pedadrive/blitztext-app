using Blitztext.Core.Models;
using Blitztext.Core.Services;
using Xunit;

namespace Blitztext.Core.Tests;

public class LlmPromptBuilderTests
{
    [Fact]
    public void ImprovePrompt_Default_ContainsEditorInstructions()
    {
        var prompt = LlmPromptBuilder.BuildImproveSystemPrompt(new TextImprovementSettings());
        Assert.Contains("Lektor und Schreibassistent", prompt);
        Assert.Contains("Verwende einen neutralen, klaren Ton", prompt);
    }

    [Theory]
    [InlineData(TextTone.Formal, "formellen, professionellen Ton")]
    [InlineData(TextTone.Casual, "lockeren, natuerlichen Ton")]
    public void ImprovePrompt_AppliesTone(TextTone tone, string expected)
    {
        var prompt = LlmPromptBuilder.BuildImproveSystemPrompt(new TextImprovementSettings { Tone = tone });
        Assert.Contains(expected, prompt);
    }

    [Fact]
    public void ImprovePrompt_CustomSystemPrompt_OverridesDefault_AndAppendsTerms()
    {
        var settings = new TextImprovementSettings
        {
            SystemPrompt = "Mein eigener Prompt",
            CustomTerms = { "Blitztext", "WhisperKit" }
        };
        var prompt = LlmPromptBuilder.BuildImproveSystemPrompt(settings);
        Assert.StartsWith("Mein eigener Prompt", prompt);
        Assert.Contains("Blitztext, WhisperKit", prompt);
        Assert.DoesNotContain("Lektor und Schreibassistent", prompt);
    }

    [Fact]
    public void ImprovePrompt_IncludesContextAndTerms()
    {
        var settings = new TextImprovementSettings
        {
            Context = "E-Mail an Kunden",
            CustomTerms = { "Acme GmbH" }
        };
        var prompt = LlmPromptBuilder.BuildImproveSystemPrompt(settings);
        Assert.Contains("Kontext: E-Mail an Kunden", prompt);
        Assert.Contains("Acme GmbH", prompt);
    }

    [Theory]
    [InlineData(EmojiDensity.Wenig, "maximal 1-2 pro Absatz")]
    [InlineData(EmojiDensity.Mittel, "etwa alle 1-2 Saetze")]
    [InlineData(EmojiDensity.Viel, "mehrere pro Satz")]
    public void EmojiPrompt_AppliesDensity(EmojiDensity density, string expected)
    {
        var prompt = LlmPromptBuilder.BuildEmojiSystemPrompt(density);
        Assert.Contains(expected, prompt);
        Assert.Contains("Gib NUR den Text mit Emojis zurueck", prompt);
    }
}
