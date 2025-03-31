﻿using ModelContextProtocol;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol.Types;
using EverythingServer.Tools;
using EverythingServer;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithListPromptsHandler((ctx, ct) =>
    {
        return Task.FromResult(new ListPromptsResult
        {
            Prompts =
            [
                new Prompt { Name= "simple_prompt", Description = "A prompt without arguments" },
                new Prompt { Name= "complex_prompt", Description = "A prompt with arguments", Arguments = [
                    new PromptArgument { Name = "temperature", Description = "Temperature setting", Required = true },
                    new PromptArgument { Name = "style", Description = "Output style", Required = false }
                ]
                }
            ]
        });
    })
    .WithGetPromptHandler((args, ct) =>
    {
        List<PromptMessage> messages = args.Params?.Name switch
        {
            "simple_prompt" => [new PromptMessage { Role = Role.User, Content = new Content { Type = "text", Text = "This is a simple prompt without arguments" } }],
            "complex_prompt" => [
                new PromptMessage { Role = Role.User, Content = new Content { Type = "text", Text = $"This is a complex prompt with arguments: temperature={args.Params?.Arguments?["temperature"]}, style={args.Params?.Arguments?["style"]}" } },
                new PromptMessage { Role = Role.Assistant, Content = new Content { Type = "text", Text = "I understand. You've provided a complex prompt with temperature and style arguments. How would you like me to proceed?" } },
                new PromptMessage { Role = Role.User, Content = new Content { Type = "image", Data = TinyImageTool.MCP_TINY_IMAGE, MimeType = "image/png" } }
                ]
            ,
            _ => throw new NotSupportedException($"Unknown prompt name: {args.Params?.Name}")
        };

        return Task.FromResult(new GetPromptResult
        {
            Messages = messages
        });
    })
    .WithListResourceTemplatesHandler((ctx, ct) =>
    {
        return Task.FromResult(new ListResourceTemplatesResult
        {
            ResourceTemplates =
            [
                new ResourceTemplate { Name = "Static Resource", Description = "A static resource with a numeric ID", UriTemplate = "test://static/resource/{id}" }
            ]
        });
    })
    .WithReadResourceHandler((ctx, ct) =>
    {
        var uri = ctx.Params?.Uri;

        if (uri is null || !uri.StartsWith("test://static/resource/"))
        {
            throw new NotSupportedException($"Unknown resource: {uri}");
        }

        int index = int.Parse(uri["test://static/resource/".Length..]) - 1;

        if (index < 0 || index >= ResourceGenerator.Resources.Count)
        {
            throw new NotSupportedException($"Unknown resource: {uri}");
        }

        var resource = ResourceGenerator.Resources[index];

        if (resource.MimeType == "text/plain")
        {
            return Task.FromResult(new ReadResourceResult
            {
                Contents = [new TextResourceContents
                {
                    Text = resource.Description!,
                    MimeType = resource.MimeType,
                    Uri = resource.Uri,
                }]
            });
        }
        else
        {
            return Task.FromResult(new ReadResourceResult
            {
                Contents = [new BlobResourceContents
                {
                    Blob = resource.Description!,
                    MimeType = resource.MimeType,
                    Uri = resource.Uri,
                }]
            });
        }
    })
    ;

await builder.Build().RunAsync();