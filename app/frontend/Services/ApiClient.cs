﻿// Copyright (c) Microsoft. All rights reserved.

using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClientApp.Services;

public sealed class ApiClient(HttpClient httpClient)
{
    public async Task<bool> ShowLogoutButtonAsync()
    {
        var response = await httpClient.GetAsync("api/enableLogout");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<bool>();
    }
    public async Task<UserInformation> GetUserAsync()
    {
        var response = await httpClient.GetAsync("api/user");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UserInformation>();
    }

    public async Task<UploadDocumentsResponse> UploadDocumentsAsync(
        IReadOnlyList<IBrowserFile> files,
        long maxAllowedSize,
        string cookie)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            foreach (var file in files)
            {
                // max allow size: 10mb
                var max_size = maxAllowedSize * 1024 * 1024;
#pragma warning disable CA2000 // Dispose objects before losing scope
                var fileContent = new StreamContent(file.OpenReadStream(max_size));
#pragma warning restore CA2000 // Dispose objects before losing scope
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                content.Add(fileContent, file.Name, file.Name);
            }

            // set cookie
            content.Headers.Add("X-CSRF-TOKEN-FORM", cookie);
            content.Headers.Add("X-CSRF-TOKEN-HEADER", cookie);

            var response = await httpClient.PostAsync("api/documents", content);

            response.EnsureSuccessStatusCode();

            var result =
                await response.Content.ReadFromJsonAsync<UploadDocumentsResponse>();

            return result
                ?? UploadDocumentsResponse.FromError(
                    "Unable to upload files, unknown error.");
        }
        catch (Exception ex)
        {
            return UploadDocumentsResponse.FromError(ex.ToString());
        }
    }

    public async IAsyncEnumerable<DocumentResponse> GetDocumentsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/documents", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await foreach (var document in
                JsonSerializer.DeserializeAsyncEnumerable<DocumentResponse>(stream, options, cancellationToken))
            {
                if (document is null)
                {
                    continue;
                }

                yield return document;
            }
        }
    }
    public async IAsyncEnumerable<FeedbackResponse> GetFeedbackAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/feedback", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            await foreach (var document in JsonSerializer.DeserializeAsyncEnumerable<FeedbackResponse>(stream, options, cancellationToken))
            {
                if (document is null)
                {
                    continue;
                }

                yield return document;
            }
        }
    }
    public async IAsyncEnumerable<ChatHistoryResponse> GetHistoryAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("api/chat/history", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var options = SerializerOptions.Default;
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await foreach (var document in JsonSerializer.DeserializeAsyncEnumerable<ChatHistoryResponse>(stream, options, cancellationToken))
            {
                if (document is null)
                {
                    continue;
                }

                yield return document;
            }
        }
    }

    public async Task ChatRatingAsync(ChatRatingRequest request)
    {
        await PostBasicAsync(request, "api/chat/rating");
    }

    public Task<AnswerResult<ChatRequest>> ChatConversationAsync(ChatRequest request) => PostRequestAsync(request, "api/chat");

    private async Task<AnswerResult<TRequest>> PostRequestAsync<TRequest>(TRequest request, string apiRoute) where TRequest : ApproachRequest
    {
        var result = new AnswerResult<TRequest>(
            IsSuccessful: false,
            Response: null,
            Approach: request.Approach,
            Request: request);

        var json = JsonSerializer.Serialize(request, SerializerOptions.Default);
        using var body = new StringContent(json, Encoding.UTF8, "application/json");


        await PostStreamingChatAsync(request);

        var response = await httpClient.PostAsync(apiRoute, body);
        if (response.IsSuccessStatusCode)
        {
            var answer = await response.Content.ReadFromJsonAsync<ApproachResponse>();
            return result with
            {
                IsSuccessful = answer is not null,
                Response = answer
            };
        }
        else
        {
            var answer = new ApproachResponse($"HTTP {(int)response.StatusCode} : {response.ReasonPhrase ?? "☹️ Unknown error..."}",
                null,
                [],
                "Unable to retrieve valid response from the server.", Guid.Empty, Guid.Empty, null);

            return result with
            {
                IsSuccessful = false,
                Response = answer
            };
        }
    }

    private async Task PostBasicAsync<TRequest>(TRequest request, string apiRoute) where TRequest : ApproachRequest
    {
        var json = JsonSerializer.Serialize(request,SerializerOptions.Default);
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(apiRoute, body);
    }

    private async Task PostStreamingChatAsync(ApproachRequest request)
    {
        var sb = new StringBuilder();
        var json = JsonSerializer.Serialize(request, SerializerOptions.Default);
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("api/chat/streaming", null);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();

        await foreach (var streamResponse in JsonSerializer.DeserializeAsyncEnumerable<string>(stream))
        {
            if (streamResponse is null)
            {
                continue;
            }

            sb.AppendLine(streamResponse);
        }
    }
}
