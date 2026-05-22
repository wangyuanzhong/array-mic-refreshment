using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Core.Tests;

public class PrivacyConfirmationTests
{
    [Theory]
    [InlineData("http://127.0.0.1:11434/v1", null)]
    [InlineData("http://localhost:11434/v1", "localhost")]
    [InlineData("http://[::1]:8080/v1", "::1")]
    public void ShouldPromptForHost_returns_false_for_loopback(string url, string? accepted)
    {
        Assert.False(PrivacyConfirmation.ShouldPromptForHost(url, accepted));
    }

    [Fact]
    public void ShouldPromptForHost_returns_false_when_already_accepted()
    {
        Assert.False(PrivacyConfirmation.ShouldPromptForHost(
            "https://api.openai.com/v1",
            "api.openai.com"));
    }

    [Fact]
    public void ShouldPromptForHost_returns_true_when_host_changes()
    {
        Assert.True(PrivacyConfirmation.ShouldPromptForHost(
            "https://api.deepseek.com/v1",
            "api.openai.com"));
    }

    [Fact]
    public void ShouldPromptForHost_returns_true_for_new_remote_host()
    {
        Assert.True(PrivacyConfirmation.ShouldPromptForHost(
            "https://api.openai.com/v1",
            null));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldPromptForHost_returns_false_for_invalid_url(string url)
    {
        Assert.False(PrivacyConfirmation.ShouldPromptForHost(url, null));
    }

    [Fact]
    public void TryResolveHost_extracts_host()
    {
        Assert.True(PrivacyConfirmation.TryResolveHost("https://api.openai.com/v1/chat", out var host));
        Assert.Equal("api.openai.com", host);
    }

    [Theory]
    [InlineData("localhost", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("api.openai.com", false)]
    public void IsLoopbackHost_classifies_hosts(string host, bool expected)
    {
        Assert.Equal(expected, PrivacyConfirmation.IsLoopbackHost(host));
    }
}
