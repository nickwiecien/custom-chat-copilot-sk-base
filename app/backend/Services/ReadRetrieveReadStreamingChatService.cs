﻿// Copyright (c) Microsoft. All rights reserved.
using ClientApp.Pages;
using Microsoft.SemanticKernel.ChatCompletion;
using MinimalApi.Extensions;
using MinimalApi.Services.Prompts;
using Shared.Models;

namespace MinimalApi.Services;

internal sealed class ReadRetrieveReadStreamingChatService
{
    private readonly ILogger<ReadRetrieveReadStreamingChatService> _logger;
    private readonly IConfiguration _configuration;
    private readonly OpenAIClientFacade _openAIClientFacade;

    public ReadRetrieveReadStreamingChatService(OpenAIClientFacade openAIClientFacade,
                                       ILogger<ReadRetrieveReadStreamingChatService> logger,
                                       IConfiguration configuration)
    {
        _openAIClientFacade = openAIClientFacade;
        _logger = logger;
        _configuration = configuration;
    }

    public async IAsyncEnumerable<string> ReplyAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {

        var sw = Stopwatch.StartNew();

        var kernel = _openAIClientFacade.GetKernel(request.OptionFlags.IsChatGpt4Enabled());

        var generateSearchQueryFunction = kernel.Plugins.GetFunction(DefaultSettings.GenerateSearchQueryPluginName, DefaultSettings.GenerateSearchQueryPluginQueryFunctionName);
        var documentLookupFunction = kernel.Plugins.GetFunction(DefaultSettings.DocumentRetrievalPluginName, DefaultSettings.DocumentRetrievalPluginQueryFunctionName);
        var chatFunction = kernel.Plugins.GetFunction(DefaultSettings.ChatPluginName, DefaultSettings.ChatPluginFunctionName);

        var context = new KernelArguments().AddUserParameters(request.History);

        await kernel.InvokeAsync(generateSearchQueryFunction, context);
        await kernel.InvokeAsync(documentLookupFunction, context);

        // Chat Step
        var chatGpt = kernel.Services.GetService<IChatCompletionService>();
        var systemMessagePrompt = PromptService.GetPromptByName(PromptService.ChatSystemPrompt);
        context["SystemMessagePrompt"] = systemMessagePrompt;

        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory(systemMessagePrompt).AddChatHistory(request.History);
        var userMessage = await PromptService.RenderPromptAsync(kernel, PromptService.GetPromptByName(PromptService.ChatUserPrompt), context);
        context["UserMessage"] = userMessage;
        chatHistory.AddUserMessage(userMessage);

        var sb = new StringBuilder();
        await foreach (StreamingChatMessageContent chatUpdate in chatGpt.GetStreamingChatMessageContentsAsync(chatHistory, DefaultSettings.AIChatRequestSettings))
        {
            yield return chatUpdate.Content;
        }

    }
}
