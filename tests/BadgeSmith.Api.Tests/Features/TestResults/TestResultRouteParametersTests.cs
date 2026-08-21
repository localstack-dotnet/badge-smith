using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using BadgeSmith.Api.Core.Infrastructure;
using BadgeSmith.Api.Core.Routing;
using BadgeSmith.Api.Features.TestResults.Models;
using BadgeSmith.Api.Tests.Testing;
using Xunit;

namespace BadgeSmith.Api.Tests.Features.TestResults;

[Trait("Category", TestCategories.Unit)]
public sealed class TestResultRouteParametersTests
{
    public static TheoryData<string, string> RequiredParameterCases => new()
    {
        { "platform", "Platform parameter is required" },
        { "owner", "Owner parameter is required" },
        { "repo", "Repo parameter is required" },
        { "branch", "Branch parameter is required" },
    };

    [Theory]
    [MemberData(nameof(RequiredParameterCases))]
    public void Extract_Should_Return_Missing_Route_Parameter_When_Parameter_Is_Absent(string routeKey, string expectedReason)
    {
        var result = TestResultRouteParameters.Extract(CreateRouteContext(values => values.Remove(routeKey)));

        AssertMissingRouteParameter(result, expectedReason, routeKey);
    }

    [Theory]
    [MemberData(nameof(RequiredParameterCases))]
    public void Extract_Should_Return_Missing_Route_Parameter_When_Value_Is_Empty(string routeKey, string expectedReason)
    {
        var result = TestResultRouteParameters.Extract(CreateRouteContext(values => values[routeKey] = string.Empty));

        AssertMissingRouteParameter(result, expectedReason, routeKey);
    }

    [Theory]
    [MemberData(nameof(RequiredParameterCases))]
    public void Extract_Should_Return_Missing_Route_Parameter_When_Value_Is_Whitespace(string routeKey, string expectedReason)
    {
        var result = TestResultRouteParameters.Extract(CreateRouteContext(values => values[routeKey] = "   "));

        AssertMissingRouteParameter(result, expectedReason, routeKey);
    }

    [Fact]
    public void Extract_Should_Return_Platform_Failure_First_When_Platform_And_Owner_Are_Both_Empty()
    {
        var result = TestResultRouteParameters.Extract(CreateRouteContext(values =>
        {
            values["platform"] = string.Empty;
            values["owner"] = string.Empty;
        }));

        Assert.False(result.IsSuccess);
        Assert.Equal("platform", result.Failure.ParameterName);
        Assert.Equal("Platform parameter is required", result.Failure.Reason);
    }

    [Fact]
    public void Extract_Should_Preserve_All_Four_Values_In_Public_Route_Order_When_All_Parameters_Are_Present()
    {
        var result = TestResultRouteParameters.Extract(CreateRouteContext(values =>
        {
            values["platform"] = "linux";
            values["owner"] = "localstack-dotnet";
            values["repo"] = "badge-smith";
            values["branch"] = "feature/ci pipeline";
        }));

        Assert.True(result.IsSuccess);
        Assert.Equal("linux", result.Parameters.Platform);
        Assert.Equal("localstack-dotnet", result.Parameters.Owner);
        Assert.Equal("badge-smith", result.Parameters.Repo);
        Assert.Equal("feature/ci pipeline", result.Parameters.Branch);
    }

    [Fact]
    public void Failure_ToErrorResponse_Should_Serialize_Standard_Error_Contract_When_Owner_Is_Missing()
    {
        var result = TestResultRouteParameters.Extract(CreateRouteContext(values => values.Remove("owner")));

        var errorResponse = result.Failure.ToErrorResponse();
        var body = JsonSerializer.Serialize(errorResponse, LambdaFunctionJsonSerializerContext.Default.ErrorResponse);

        Assert.Equal(
            """{"message":"Owner parameter is required","error_details":[{"error_code":"MISSING_ROUTE_PARAMETER","property_name":"owner"}]}""",
            body);
    }

    private static void AssertMissingRouteParameter(TestResultRouteParametersResult result, string expectedReason, string expectedParameterName)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedReason, result.Failure.Reason);
        Assert.Equal(expectedParameterName, result.Failure.ParameterName);
        Assert.Equal("MISSING_ROUTE_PARAMETER", result.Failure.Code);
    }

    private static RouteContext CreateRouteContext(Action<Dictionary<string, string>> configure)
    {
        var routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["platform"] = "linux",
            ["owner"] = "owner",
            ["repo"] = "repo",
            ["branch"] = "main",
        };

        configure(routeValues);

        return new RouteContext(new APIGatewayHttpApiV2ProxyRequest(), routeValues);
    }
}
