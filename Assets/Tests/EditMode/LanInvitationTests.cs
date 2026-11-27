using System;
using System.Net;
using EachOneCoin.Networking;
using NUnit.Framework;

public class LanInvitationTests
{
    [Test]
    public void Invitation_RoundTripsAddressAndPort()
    {
        string text = LanInvitation.Encode(IPAddress.Parse("192.168.1.42"), 7777);
        Assert.That(LanInvitation.TryDecode(text, out var address, out var port), Is.True);
        Assert.That(address, Is.EqualTo(IPAddress.Parse("192.168.1.42")));
        Assert.That(port, Is.EqualTo(7777));
    }

    [TestCase("eachonecoin-lan:2:192.168.1.42:7777")]
    [TestCase("eachonecoin-lan:1:127.0.0.1:7777")]
    [TestCase("eachonecoin-lan:1:192.168.1.42:0")]
    [TestCase("eachonecoin-lan:1:192.168.1.42:65536")]
    [TestCase("eachonecoin-lan:1:example.com:7777")]
    [TestCase("eachonecoin-lan:1:224.0.0.1:7777")]
    [TestCase("eachonecoin-lan:1:192.168.1.42:7777:extra")]
    public void Invitation_RejectsInvalidPayload(string text)
    {
        Assert.That(LanInvitation.TryDecode(text, out _, out _), Is.False);
    }

    [Test]
    public void Encode_RejectsLoopback()
    {
        Assert.Throws<ArgumentException>(() => LanInvitation.Encode(IPAddress.Loopback, 7777));
    }
}
