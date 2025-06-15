using System;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BirthdayCommander.Core.Interfaces;
using BirthdayCommander.Core.Interfaces.Handlers;
using BirthdayCommander.Core.Models.Mattermost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Websocket.Client;

namespace BirthdayCommander.Infrastructure.Services.BackgroundServices
{
    public class MattermostBotService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MattermostBotService> _logger;
        private readonly IConfiguration _configuration;
        private IWebsocketClient? _webSocket;
        private string _botUserId;
        private readonly string _botToken;
        private readonly string _serverUrl;
        private int _sequenceNumber = 1;
        private readonly TimeSpan _reconnectTimeout = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _errorReconnectTimeout = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(30);
        private Timer? _pingTimer;
        private readonly object _sequenceLock = new object();

        public MattermostBotService(
            IServiceProvider serviceProvider,
            ILogger<MattermostBotService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _botToken = configuration["Mattermost:BotToken"] 
                ?? throw new InvalidOperationException("No Mattermost bot token found");
            _serverUrl = configuration["Mattermost:ServerUrl"] 
                ?? throw new InvalidOperationException("No Mattermost server URL found");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Mattermost Bot Service starting...");

            try
            {
                // Get bot user ID first
                await GetBotUserId();

                // Setup and connect WebSocket
                await SetupWebSocket(stoppingToken);

                // Keep the service running
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
                _logger.LogInformation("Mattermost Bot Service cancellation requested");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in Mattermost Bot Service");
                throw;
            }
            finally
            {
                await CleanupAsync();
            }
        }

        private async Task SetupWebSocket(CancellationToken stoppingToken)
        {
            var wsUrl = _serverUrl.Replace("https://", "wss://");
            wsUrl = $"{wsUrl}/api/v4/websocket";

            var factory = new Func<ClientWebSocket>(() =>
            {
                var client = new ClientWebSocket();
                client.Options.SetRequestHeader("Authorization", $"Bearer {_botToken}");
                client.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                return client;
            });

            _webSocket = new WebsocketClient(new Uri(wsUrl), factory)
            {
                Name = "Mattermost Birthday Commander Bot",
                ReconnectTimeout = _reconnectTimeout,
                ErrorReconnectTimeout = _errorReconnectTimeout,
                IsTextMessageConversionEnabled = true
            };

            // Subscribe to events
            SubscribeToWebSocketEvents(stoppingToken);

            // Start the connection
            _logger.LogInformation("Connecting to Mattermost WebSocket at {Url}", wsUrl);
            await _webSocket.Start();

            // Wait for connection
            if (!_webSocket.IsRunning)
            {
                throw new InvalidOperationException("Failed to connect to Mattermost WebSocket");
            }
        }

        private void SubscribeToWebSocketEvents(CancellationToken stoppingToken)
        {
            // Connection opened
            _webSocket.ReconnectionHappened
                .Subscribe(async info =>
                {
                    _logger.LogInformation("WebSocket reconnection happened: {Type}", info.Type);
                    
                    try
                    {
                        // Send authentication
                        await SendAuthenticationAsync();
                        
                        // Start ping timer
                        StartPingTimer();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during WebSocket connection setup");
                    }
                });
            
            // Connection closed
            _webSocket.DisconnectionHappened
                .Subscribe(info =>
                {
                    _logger.LogWarning("WebSocket disconnected: {Type} - {CloseStatus} - {Exception}", 
                        info.Type, info.CloseStatus, info.Exception?.Message);
                    
                    // Stop ping timer
                    _pingTimer?.Dispose();
                    _pingTimer = null;
                });
            
            // Message received
            _webSocket.MessageReceived
                .Where(msg => msg.MessageType == WebSocketMessageType.Text)
                .Select(msg => msg.Text)
                .Subscribe(async message =>
                {
                    try
                    {
                        await ProcessWebSocketMessage(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing WebSocket message");
                    }
                });

            // Handle cancellation
            stoppingToken.Register(() =>
            {
                _logger.LogInformation("Stopping Mattermost Bot Service...");
                _ = CleanupAsync();
            });
        }

        private async Task SendAuthenticationAsync()
        {
            var authMessage = new
            {
                seq = GetNextSequence(),
                action = "authentication_challenge",
                data = new { token = _botToken }
            };

            await SendWebSocketMessage(authMessage);
            _logger.LogInformation("Sent authentication challenge");
        }

        private void StartPingTimer()
        {
            _pingTimer?.Dispose();
            _pingTimer = new Timer(async _ =>
            {
                try
                {
                    if (_webSocket?.IsRunning == true)
                    {
                        var pingMessage = new
                        {
                            seq = GetNextSequence(),
                            action = "ping"
                        };

                        await SendWebSocketMessage(pingMessage);
                        _logger.LogDebug("Sent WebSocket ping");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending ping");
                }
            }, null, _pingInterval, _pingInterval);
        }

        private async Task ProcessWebSocketMessage(string message)
        {
            try
            {
                _logger.LogDebug("Received WebSocket message: {Message}", message);

                var wsEvent = JsonSerializer.Deserialize<MattermostWebSocketEvent>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                switch (wsEvent?.Event)
                {
                    case "posted":
                        await HandlePostedEvent(wsEvent);
                        break;
                    
                    case "hello":
                        _logger.LogInformation("Received hello from Mattermost server");
                        break;
                    
                    case "authentication_challenge":
                        _logger.LogInformation("Authentication successful");
                        break;
                    
                    case "pong":
                        _logger.LogDebug("Received pong");
                        break;
                    
                    default:
                        if (!string.IsNullOrEmpty(wsEvent?.Event))
                        {
                            _logger.LogDebug("Received event: {Event}", wsEvent.Event);
                        }
                        break;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize WebSocket message");
            }
        }

        private async Task HandlePostedEvent(MattermostWebSocketEvent wsEvent)
        {
            if (wsEvent?.Data?.ChannelType != "D")
            {
                // Not a direct message, ignore
                return;
            }

            try
            {
                var post = JsonSerializer.Deserialize<MattermostPostFromWebSocket>(
                    wsEvent.Data.PostJson, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Ignore messages from the bot itself
                if (post?.UserId == _botUserId)
                {
                    _logger.LogDebug("Ignoring bot's own message");
                    return;
                }

                _logger.LogInformation("Received DM from user {UserId} in channel {ChannelId}: {Message}",
                    post.UserId, post.ChannelId, post.Message);

                // Process the message in a separate task to avoid blocking
                _ = Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IDirectMessageHandler>();

                    try
                    {
                        await handler.HandleDirectMessage(post.UserId, post.ChannelId, post.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling direct message from user {UserId}", post.UserId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing posted event");
            }
        }

        private async Task GetBotUserId()
        {
            using var scope = _serviceProvider.CreateScope();
            var mattermostService = scope.ServiceProvider.GetRequiredService<IMattermostService>();

            var maxRetries = 5;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    var botUser = await mattermostService.GetMeAsync();
                    _botUserId = botUser.Id;
                    _logger.LogInformation("Bot user ID: {BotUserId}, Username: {Username}",
                        _botUserId, botUser.Username);
                    return;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogError(ex, "Failed to get bot user ID, attempt {Attempt}/{MaxAttempts}",
                        retryCount, maxRetries);

                    if (retryCount < maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)));
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to get bot user ID after multiple attempts", ex);
                    }
                }
            }
        }

        private async Task SendWebSocketMessage(object message)
        {
            if (_webSocket?.IsRunning != true)
            {
                _logger.LogWarning("Cannot send message, WebSocket is not connected");
                return;
            }

            var json = JsonSerializer.Serialize(message);
            _logger.LogDebug("Sending WebSocket message: {Message}", json);

            await Task.Run(() => _webSocket.Send(json));
        }

        private int GetNextSequence()
        {
            lock (_sequenceLock)
            {
                return _sequenceNumber++;
            }
        }

        private async Task CleanupAsync()
        {
            try
            {
                _pingTimer?.Dispose();
                _pingTimer = null;

                await _webSocket.Stop(WebSocketCloseStatus.NormalClosure, "Service stopping");
                _webSocket.Dispose();
                _webSocket = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Mattermost Bot Service stopping...");
            await CleanupAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}