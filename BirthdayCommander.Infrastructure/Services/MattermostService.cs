using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Models.Mattermost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BirthdayCommander.Infrastructure.Services;

public class MattermostService : IMattermostService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MattermostService> _logger;
    private readonly string _serverUrl;
    private readonly string _botToken;
    private readonly string _webhookSecret;
    private readonly JsonSerializerOptions? _jsonOptions;
    
    public MattermostService(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<MattermostService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        _serverUrl = configuration["Mattermost:ServerUrl"] 
                     ?? throw new InvalidOperationException("Mattermost:ServerUrl not configured");
        _botToken = configuration["Mattermost:BotToken"] 
                     ?? throw new InvalidOperationException("Mattermost:BotToken not configured");
        _webhookSecret = configuration["Mattermost:WebhookSecret"] 
                         ?? throw new InvalidOperationException("Mattermost:WebhookSecret not configured");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
    
    public async Task<MattermostUser> GetMeAsync() // bot service dependency
    {
        var response = await _httpClient.GetAsync("/api/v4/users/me");
            
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to get bot user info: {StatusCode} - {Error}", 
                response.StatusCode, error);
            throw new Exception($"Failed to get bot user info: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<MattermostUser>(content, _jsonOptions) 
                   ?? throw new ValidationException("Failed to deserialize bot user info");
        return user;
    }

    public async Task<MattermostUser?> GetUserByIdAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"/api/v4/users/{userId}");
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("User {UserId} not found in Mattermost", userId);
                    return null;
                }
                    
                _logger.LogError("Failed to get user {UserId}: {StatusCode}", userId, response.StatusCode);
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MattermostUser>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId} from Mattermost", userId);
            return null;
        }
    }

    public async Task<MattermostUser?> GetUserByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"/api/v4/users/email/{email}");
                
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("User with email {Email} not found in Mattermost", email);
                    return null;
                }
                    
                _logger.LogError("Failed to get user by email {Email}: {StatusCode}", email, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<MattermostUser>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by email {Email} from Mattermost", email);
            return null;
        }
    }

    public Task<string> CreateGroupChannelAsync(List<string> userIds, string channelName)
    {
        throw new NotImplementedException();
    }

    public Task<string> CreateDirectChannelAsync(string userId)
    {
        throw new NotImplementedException();
    }

    public Task SendMessageAsync(string channelId, string message)
    {
        throw new NotImplementedException();
    }

    public Task SendDirectMessageAsync(string userId, string message)
    {
        throw new NotImplementedException();
    }

    public Task<MattermostChannel> GetChannelAsync(string channelId)
    {
        throw new NotImplementedException();
    }

    public Task UpdateChannelDisplayNameAsync(string channelId, string displayName)
    {
        throw new NotImplementedException();
    }

    public bool ValidateWebhookToken(string token)
    {
        throw new NotImplementedException();
    }

    public string FormatBirthdayNotificationMessage(string birthdayPersonName, string wishlistUrl, int daysUntil)
    {
        throw new NotImplementedException();
    }
}