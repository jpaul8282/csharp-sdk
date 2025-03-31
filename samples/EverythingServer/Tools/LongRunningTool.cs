﻿using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace EverythingServer.Tools;

[McpServerToolType]
public static class LongRunningTool
{
    [McpServerTool(Name = "longRunningOperation"), Description("Demonstrates a long running operation with progress updates")]
    public static async Task<string> LongRunningOperation(
        IMcpServer server,
        RequestContext<CallToolRequestParams> context,
        int duration = 10,
        int steps = 5)
    {
        var progressToken = context.Params?.Meta?.ProgressToken;
        var stepDuration = duration / steps;

        for (int i = 1; i <= steps + 1; i++)
        {
            await Task.Delay(stepDuration * 1000);
            
            if (progressToken is not null)
            {
                await server.SendMessageAsync(new JsonRpcNotification
                {
                    Method = "notifications/progress",
                    Params = new
                    {
                        Progress = i,
                        Total = steps,
                        progressToken
                    }
                });
            }
        }

        return $"Long running operation completed. Duration: {duration} seconds. Steps: {steps}.";
    }
}
