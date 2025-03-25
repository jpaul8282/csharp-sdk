﻿using ModelContextProtocol.Client;
using Microsoft.Extensions.AI;
using OpenAI;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Protocol.Messages;
using System.Text.Json;
using ModelContextProtocol.Configuration;
using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Tests.Utils;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;

namespace ModelContextProtocol.Tests;

public class ClientIntegrationTests : LoggedTest, IClassFixture<ClientIntegrationTestFixture>
{
    private static readonly string? s_openAIKey = Environment.GetEnvironmentVariable("AI:OpenAI:ApiKey")!;

    public static bool NoOpenAIKeySet => string.IsNullOrWhiteSpace(s_openAIKey);

    private readonly ClientIntegrationTestFixture _fixture;

    public ClientIntegrationTests(ClientIntegrationTestFixture fixture, ITestOutputHelper testOutputHelper)
        : base(testOutputHelper)
    {
        _fixture = fixture;
        _fixture.Initialize(LoggerFactory);
    }

    public static IEnumerable<object[]> GetClients() =>
        ClientIntegrationTestFixture.ClientIds.Select(id => new object[] { id });

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task ConnectAndPing_Stdio(string clientId)
    {
        // Arrange

        // Act
        await using var client = await _fixture.CreateClientAsync(clientId);
        await client.PingAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(client);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task Connect_ShouldProvideServerFields(string clientId)
    {
        // Arrange

        // Act
        await using var client = await _fixture.CreateClientAsync(clientId);

        // Assert
        Assert.NotNull(client.ServerCapabilities);
        Assert.NotNull(client.ServerInfo);
        if (clientId != "everything")   // Note: Comment the below assertion back when the everything server is updated to provide instructions
            Assert.NotNull(client.ServerInstructions);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task ListTools_Stdio(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        var tools = await client.ListToolsAsync(TestContext.Current.CancellationToken);

        // assert
        Assert.NotEmpty(tools);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task CallTool_Stdio_EchoServer(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        var result = await client.CallToolAsync(
            "echo",
            new Dictionary<string, object?>
            {
                ["message"] = "Hello MCP!"
            },
            CancellationToken.None
        );

        // assert
        Assert.NotNull(result);
        Assert.False(result.IsError);
        var textContent = Assert.Single(result.Content, c => c.Type == "text");
        Assert.Equal("Echo: Hello MCP!", textContent.Text);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task CallTool_Stdio_ViaAIFunction_EchoServer(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        var aiFunctions = await client.ListToolsAsync(TestContext.Current.CancellationToken);
        var echo = aiFunctions.Single(t => t.Name == "echo");
        var result = await echo.InvokeAsync([new KeyValuePair<string, object?>("message", "Hello MCP!")], TestContext.Current.CancellationToken);

        // assert
        Assert.NotNull(result);
        Assert.Contains("Echo: Hello MCP!", result.ToString());
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task ListPrompts_Stdio(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        var prompts = await client.ListPromptsAsync(TestContext.Current.CancellationToken);

        // assert
        Assert.NotEmpty(prompts);
        // We could add specific assertions for the known prompts
        Assert.Contains(prompts, p => p.Name == "simple_prompt");
        Assert.Contains(prompts, p => p.Name == "complex_prompt");
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task GetPrompt_Stdio_SimplePrompt(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        var result = await client.GetPromptAsync("simple_prompt", null, CancellationToken.None);

        // assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Messages);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task GetPrompt_Stdio_ComplexPrompt(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        var arguments = new Dictionary<string, object?>
        {
            { "temperature", "0.7" },
            { "style", "formal" }
        };
        var result = await client.GetPromptAsync("complex_prompt", arguments, CancellationToken.None);

        // assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Messages);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task GetPrompt_NonExistent_ThrowsException(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        await Assert.ThrowsAsync<McpClientException>(() =>
            client.GetPromptAsync("non_existent_prompt", null, CancellationToken.None));
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task ListResourceTemplates_Stdio(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);

        IList<ResourceTemplate> allResourceTemplates = await client.ListResourceTemplatesAsync(TestContext.Current.CancellationToken);

        // The server provides a single test resource template
        Assert.Single(allResourceTemplates);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task ListResources_Stdio(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);

        IList<Resource> allResources = await client.ListResourcesAsync(TestContext.Current.CancellationToken);

        // The server provides 100 test resources
        Assert.Equal(100, allResources.Count);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task ReadResource_Stdio_TextResource(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        // Odd numbered resources are text in the everything server (despite the docs saying otherwise)
        // 1 is index 0, which is "even" in the 0-based index
        var result = await client.ReadResourceAsync("test://static/resource/1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Contents);
        Assert.NotNull(result.Contents[0].Text);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task ReadResource_Stdio_BinaryResource(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        // Even numbered resources are binary in the everything server (despite the docs saying otherwise)
        // 2 is index 1, which is "odd" in the 0-based index
        var result = await client.ReadResourceAsync("test://static/resource/2", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Contents);
        Assert.NotNull(result.Contents[0].Blob);
    }

    // Not supported by "everything" server version on npx
    [Fact]
    public async Task SubscribeResource_Stdio()
    {
        // arrange
        var clientId = "test_server";

        // act
        int counter = 0;
        await using var client = await _fixture.CreateClientAsync(clientId);
        client.AddNotificationHandler(NotificationMethods.ResourceUpdatedNotification, (notification) =>
        {
            var notificationParams = JsonSerializer.Deserialize<ResourceUpdatedNotificationParams>(notification.Params!.ToString() ?? string.Empty);
            ++counter;
            return Task.CompletedTask;
        });
        await client.SubscribeToResourceAsync("test://static/resource/1", CancellationToken.None);

        // notifications happen every 5 seconds, so we wait for 10 seconds to ensure we get at least one notification
        await Task.Delay(10000, TestContext.Current.CancellationToken);

        // assert
        Assert.True(counter > 0);
    }

    // Not supported by "everything" server version on npx
    [Fact]
    public async Task UnsubscribeResource_Stdio()
    {
        // arrange
        var clientId = "test_server";

        // act
        int counter = 0;
        await using var client = await _fixture.CreateClientAsync(clientId);
        client.AddNotificationHandler(NotificationMethods.ResourceUpdatedNotification, (notification) =>
        {
            var notificationParams = JsonSerializer.Deserialize<ResourceUpdatedNotificationParams>(notification.Params!.ToString() ?? string.Empty);
            ++counter;
            return Task.CompletedTask;
        });
        await client.SubscribeToResourceAsync("test://static/resource/1", CancellationToken.None);

        // notifications happen every 5 seconds, so we wait for 10 seconds to ensure we get at least one notification
        await Task.Delay(10000, TestContext.Current.CancellationToken);

        // reset counter
        int counterAfterSubscribe = counter;

        // unsubscribe
        await client.UnsubscribeFromResourceAsync("test://static/resource/1", CancellationToken.None);
        counter = 0;

        // notifications happen every 5 seconds, so we wait for 10 seconds to ensure we would've gotten at least one notification
        await Task.Delay(10000, TestContext.Current.CancellationToken);

        // assert
        Assert.True(counterAfterSubscribe > 0);
        Assert.Equal(0, counter);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task GetCompletion_Stdio_ResourceReference(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        var result = await client.GetCompletionAsync(new Reference
        {
            Type = "ref/resource",
            Uri = "test://static/resource/1"
        },
            "argument_name", "1",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Single(result.Completion.Values);
        Assert.Equal("1", result.Completion.Values[0]);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task GetCompletion_Stdio_PromptReference(string clientId)
    {
        // arrange

        // act
        await using var client = await _fixture.CreateClientAsync(clientId);
        var result = await client.GetCompletionAsync(new Reference
        {
            Type = "ref/prompt",
            Name = "irrelevant"
        },
            argumentName: "style", argumentValue: "fo",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Single(result.Completion.Values);
        Assert.Equal("formal", result.Completion.Values[0]);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task Sampling_Stdio(string clientId)
    {
        // Set up the sampling handler
        int samplingHandlerCalls = 0;
        await using var client = await _fixture.CreateClientAsync(clientId, new()
        {
            ClientInfo = new() { Name = "Sampling_Stdio", Version = "1.0.0" },
            Capabilities = new()
            {
                Sampling = new()
                {
                    SamplingHandler = (_, _) =>
                    {
                        samplingHandlerCalls++;
                        return Task.FromResult(new CreateMessageResult
                        {
                            Model = "test-model",
                            Role = "assistant",
                            Content = new Content
                            {
                                Type = "text",
                                Text = "Test response"
                            }
                        });
                    },
                },
            },
        });

        // Call the server's sampleLLM tool which should trigger our sampling handler
        var result = await client.CallToolAsync(
            "sampleLLM",
            new Dictionary<string, object?>
            {
                ["prompt"] = "Test prompt",
                ["maxTokens"] = 100
            },
            TestContext.Current.CancellationToken);

        // assert
        Assert.NotNull(result);
        var textContent = Assert.Single(result.Content);
        Assert.Equal("text", textContent.Type);
        Assert.False(string.IsNullOrEmpty(textContent.Text));
    }

    //[Theory]
    //[MemberData(nameof(GetClients))]
    //public async Task Roots_Stdio_EverythingServer(string clientId)
    //{       
    //    var rootsHandlerCalls = 0;
    //    var testRoots = new List<Root>
    //    {
    //        new() { Uri = "file:///test/root1", Name = "Test Root 1" },
    //        new() { Uri = "file:///test/root2", Name = "Test Root 2" }
    //    };

    //    await using var client = await _fixture.Factory.GetClientAsync(clientId);

    //    // Set up the roots handler
    //    client.SetRootsHandler((request, ct) =>
    //    {
    //        rootsHandlerCalls++;
    //        return Task.FromResult(new ListRootsResult
    //        {
    //            Roots = testRoots
    //        });
    //    });

    //    // Connect
    //    await client.ConnectAsync(CancellationToken.None);

    //    // assert
    //    // nothing to assert, no servers implement roots, so we if no exception is thrown, it's a success
    //    Assert.True(true);
    //}

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task Notifications_Stdio(string clientId)
    {
        await using var client = await _fixture.CreateClientAsync(clientId);

        // Verify we can send notifications without errors
        await client.SendNotificationAsync(NotificationMethods.RootsUpdatedNotification, cancellationToken: TestContext.Current.CancellationToken);
        await client.SendNotificationAsync("test/notification", new { test = true }, TestContext.Current.CancellationToken);

        // assert
        // no response to check, if no exception is thrown, it's a success
        Assert.True(true);
    }

    [Fact]
    public async Task CallTool_Stdio_MemoryServer()
    {
        // arrange
        McpServerConfig serverConfig = new()
        {
            Id = "memory",
            Name = "memory",
            TransportType = TransportTypes.StdIo,
            TransportOptions = new Dictionary<string, string>
            {
                ["command"] = "npx",
                ["arguments"] = "-y @modelcontextprotocol/server-memory"
            }
        };

        McpClientOptions clientOptions = new()
        {
            ClientInfo = new() { Name = "IntegrationTestClient", Version = "1.0.0" }
        };

        await using var client = await McpClientFactory.CreateAsync(
            serverConfig, 
            clientOptions, 
            loggerFactory: LoggerFactory, 
            cancellationToken: TestContext.Current.CancellationToken);

        // act
        var result = await client.CallToolAsync(
            "read_graph",
            new Dictionary<string, object?>(),
            TestContext.Current.CancellationToken);

        // assert
        Assert.NotNull(result);
        Assert.False(result.IsError);
        Assert.Single(result.Content, c => c.Type == "text");

        await client.DisposeAsync();
    }

    [Fact(Skip = "Requires OpenAI API Key", SkipWhen = nameof(NoOpenAIKeySet))]
    public async Task ListToolsAsync_UsingEverythingServer_ToolsAreProperlyCalled()
    {
        // Get the MCP client and tools from it.
        await using var client = await McpClientFactory.CreateAsync(
            _fixture.EverythingServerConfig, 
            _fixture.DefaultOptions, 
            cancellationToken: TestContext.Current.CancellationToken);
        var mappedTools = await client.ListToolsAsync(TestContext.Current.CancellationToken);

        // Create the chat client.
        using IChatClient chatClient = new OpenAIClient(s_openAIKey).AsChatClient("gpt-4o-mini")
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        // Create the messages.
        List<ChatMessage> messages = [new(ChatRole.System, "You are a helpful assistant.")];
        if (client.ServerInstructions is not null)
        {
            messages.Add(new(ChatRole.System, client.ServerInstructions));
        }
        messages.Add(new(ChatRole.User, "Please call the echo tool with the string 'Hello MCP!' and output the response ad verbatim."));

        // Call the chat client
        var response = await chatClient.GetResponseAsync(messages, new() { Tools = [.. mappedTools], Temperature = 0 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Echo: Hello MCP!", response.Text);
    }

    [Fact(Skip = "Requires OpenAI API Key", SkipWhen = nameof(NoOpenAIKeySet))]
    public async Task SamplingViaChatClient_RequestResponseProperlyPropagated()
    {
        await using var client = await McpClientFactory.CreateAsync(_fixture.EverythingServerConfig, new()
        {
            ClientInfo = new() { Name = nameof(SamplingViaChatClient_RequestResponseProperlyPropagated), Version = "1.0.0" },
            Capabilities = new()
            {
                Sampling = new()
                {
                    SamplingHandler = new OpenAIClient(s_openAIKey).AsChatClient("gpt-4o-mini").CreateSamplingHandler(),
                },
            },
        }, cancellationToken: TestContext.Current.CancellationToken);

        var result = await client.CallToolAsync("sampleLLM", new Dictionary<string, object?>()
        {
            ["prompt"] = "In just a few words, what is the most famous tower in Paris?",
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Content);
        Assert.Equal("text", result.Content[0].Type);
        Assert.Contains("LLM sampling result:", result.Content[0].Text);
        Assert.Contains("Eiffel", result.Content[0].Text);
    }

    [Theory]
    [MemberData(nameof(GetClients))]
    public async Task SetLoggingLevel_ReceivesLoggingMessages(string clientId)
    {
        // arrange
        JsonSerializerOptions jsonSerializerOptions = new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        int logCounter = 0;
        await using var client = await _fixture.CreateClientAsync(clientId);
        client.AddNotificationHandler(NotificationMethods.LoggingMessageNotification, (notification) =>
        {
            var loggingMessageNotificationParameters = JsonSerializer.Deserialize<LoggingMessageNotificationParams>(notification.Params!.ToString() ?? string.Empty,
                jsonSerializerOptions);
            if (loggingMessageNotificationParameters is not null)
            {
                ++logCounter;
            }
            return Task.CompletedTask;
        });

        // act
        await client.SetLoggingLevel(LoggingLevel.Debug, CancellationToken.None);
        await Task.Delay(16000, TestContext.Current.CancellationToken);

        // assert
        Assert.True(logCounter > 0);
    }
}
