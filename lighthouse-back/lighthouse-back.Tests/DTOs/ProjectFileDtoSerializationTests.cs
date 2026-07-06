using System.Text.Json;
using Lighthouse.DTOs;
using FluentAssertions;

namespace Lighthouse.Tests.DTOs;

/// <summary>
/// Guards the JSON contract of the project-file editing DTOs. The default camelCase policy turns
/// "ETag" into "eTag" (only the first character is lowercased), which the frontend reads as
/// undefined and sends back as null — making every save a false 409 conflict. The DTOs pin the
/// name to "etag"; these tests fail if that annotation is ever removed.
/// </summary>
public class ProjectFileDtoSerializationTests
{
    // Mirrors ASP.NET Core's default web serialization (camelCase property naming).
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ProjectFileDto_SerializesETagAsLowercaseEtag()
    {
        var dto = new ProjectFileDto("compose", "docker-compose.yml", "content", "abc123", true);

        string json = JsonSerializer.Serialize(dto, WebOptions);

        json.Should().Contain("\"etag\":\"abc123\"");
        json.Should().NotContain("\"eTag\"");
    }

    [Fact]
    public void UpdateProjectFileRequest_DeserializesLowercaseEtag()
    {
        const string json = "{\"kind\":\"compose\",\"content\":\"x\",\"etag\":\"abc123\"}";

        UpdateProjectFileRequest? request = JsonSerializer.Deserialize<UpdateProjectFileRequest>(json, WebOptions);

        request.Should().NotBeNull();
        request!.ETag.Should().Be("abc123");
    }
}
