#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property PackAsTool=false
#:property GenerateDocumentationFile=true
#:property NoWarn=IDE0130;CA1812
#:package Spectre.Console.Cli
#:package CliWrap
#:package AWSSDK.DynamoDBv2
#:package AWSSDK.SecretsManager
#:package Microsoft.Extensions.Hosting
#:package Microsoft.Extensions.Http
#:package Spectre.Console.Cli.Extensions.DependencyInjection
#:package LocalStack.Client
#:package LocalStack.Client.Extensions
#:include Commands/**/*.cs
#:include Configuration/**/*.cs
#:include Infrastructure/**/*.cs
#:include Services/**/*.cs
#:include ../src/shared/Protocol/HmacCanonicalRequest.cs

return await BadgeSmith.Tools.BadgeSmithTool.RunAsync(args).ConfigureAwait(false);
