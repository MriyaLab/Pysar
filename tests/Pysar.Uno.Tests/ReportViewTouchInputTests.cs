using Microsoft.UI.Input;
using Xunit;

namespace Pysar.Uno.Tests;

public class ReportViewTouchInputTests
{
    [Fact]
    public void CountsTowardTouchPinch_WhenTouch_IsTrue()
        => Assert.True(ReportView.CountsTowardTouchPinch(PointerDeviceType.Touch));

    [Fact]
    public void CountsTowardTouchPinch_WhenMouse_IsFalse()
        => Assert.False(ReportView.CountsTowardTouchPinch(PointerDeviceType.Mouse));

    [Fact]
    public void WheelZooms_OnIos_IsFalse()
        => Assert.False(ReportView.WheelZooms(isIos: true, isAndroid: false));

    [Fact]
    public void WheelZooms_OnDesktop_IsTrue()
        => Assert.True(ReportView.WheelZooms(isIos: false, isAndroid: false));
}
