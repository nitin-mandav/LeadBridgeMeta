using System.Security.Cryptography;
using System.Text;
using LeadBridgeMeta.Api.Controllers;
using LeadBridgeMeta.Infrastructure.Meta;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LeadBridgeMeta.Tests.Webhooks;

public class MetaWebhookControllerTests
{
    private const string AppSecret = "test-app-secret";

    private static MetaWebhookController CreateController(Mock<IMetaWebhookQueue> queue)
    {
        var options = Options.Create(new MetaOptions { AppId = "app-id", AppSecret = AppSecret, WebhookVerifyToken = "verify-me" });
        var controller = new MetaWebhookController(options, queue.Object, NullLogger<MetaWebhookController>.Instance);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static void SetBody(MetaWebhookController controller, string json, bool withValidSignature)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(bytes);
        controller.ControllerContext.HttpContext.Request.ContentLength = bytes.Length;

        var signature = withValidSignature
            ? "sha256=" + ComputeHmac(json, AppSecret)
            : "sha256=" + ComputeHmac(json, "wrong-secret");

        controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = signature;
    }

    private static string ComputeHmac(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    [Fact]
    public void Verify_WithMatchingToken_ReturnsChallenge()
    {
        var controller = CreateController(new Mock<IMetaWebhookQueue>());

        var result = controller.Verify("subscribe", "verify-me", "the-challenge-value");

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("the-challenge-value", content.Content);
    }

    [Fact]
    public void Verify_WithWrongToken_ReturnsForbid()
    {
        var controller = CreateController(new Mock<IMetaWebhookQueue>());

        var result = controller.Verify("subscribe", "wrong-token", "the-challenge-value");

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Receive_WithValidSignature_EnqueuesLeadgenChangesAndReturnsOk()
    {
        var queue = new Mock<IMetaWebhookQueue>();
        var controller = CreateController(queue);
        var payload = """
        {
          "object": "page",
          "entry": [
            {
              "id": "page-123",
              "changes": [
                { "field": "leadgen", "value": { "form_id": "form-456", "leadgen_id": "lead-789" } }
              ]
            }
          ]
        }
        """;
        SetBody(controller, payload, withValidSignature: true);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        queue.Verify(q => q.EnqueueAsync(
            It.Is<LeadgenNotification>(n => n.PageId == "page-123" && n.FormId == "form-456" && n.LeadgenId == "lead-789"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Receive_WithInvalidSignature_ReturnsUnauthorizedAndDoesNotEnqueue()
    {
        var queue = new Mock<IMetaWebhookQueue>();
        var controller = CreateController(queue);
        var payload = """{ "object": "page", "entry": [] }""";
        SetBody(controller, payload, withValidSignature: false);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        queue.Verify(q => q.EnqueueAsync(It.IsAny<LeadgenNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Receive_WithMissingSignatureHeader_ReturnsUnauthorized()
    {
        var queue = new Mock<IMetaWebhookQueue>();
        var controller = CreateController(queue);
        var payload = """{ "object": "page", "entry": [] }""";
        var bytes = Encoding.UTF8.GetBytes(payload);
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(bytes);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Receive_IgnoresNonLeadgenChanges()
    {
        var queue = new Mock<IMetaWebhookQueue>();
        var controller = CreateController(queue);
        var payload = """
        {
          "object": "page",
          "entry": [
            { "id": "page-1", "changes": [ { "field": "feed", "value": { } } ] }
          ]
        }
        """;
        SetBody(controller, payload, withValidSignature: true);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        queue.Verify(q => q.EnqueueAsync(It.IsAny<LeadgenNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
