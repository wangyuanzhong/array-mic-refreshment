using ArrayMicRefreshment.App.Web;
using Xunit;

namespace ArrayMicRefreshment.App.Tests;

public sealed class SettingsMetadataProviderTests
{
    [Fact]
    public void ListSpeakerUsers_without_enrollment_includes_none_option()
    {
        var users = SettingsMetadataProvider.ListSpeakerUsers(enrollment: null);

        Assert.Single(users);
        Assert.True(users[0].IsNone);
        Assert.Equal("无用户（不做声纹识别）", users[0].DisplayName);
    }
}
