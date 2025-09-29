using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Sender
{
    public class NetworkManager : IDisposable
    {
        private UdpClient _sitlClient;
        private UdpClient _forwardClient;
        private readonly IPEndPoint _sitlEndPoint;
        private readonly IPEndPoint _forwardEndPoint;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isConnected;
        private readonly Logger _logger;

        public event Action<byte[], IPEndPoint> PacketReceived;
        public event Action<byte[]> PacketForwarded;
        public event Action<Exception> ErrorOccurred;
        public event Action ConnectionLost;

        public bool IsConnected => _isConnected;
        public int PacketsReceived { get; private set; }
        public int PacketsForwarded { get; private set; }

        public NetworkManager(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sitlEndPoint = new IPEndPoint(IPAddress.Parse(Config.SITL_ADDRESS), Config.SITL_PORT);
            _forwardEndPoint = new IPEndPoint(IPAddress.Parse(Config.FORWARD_ADDRESS), Config.FORWARD_PORT);
        }
        
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _logger.LogInfo($"Attempting to connect to SITL at {Config.SITL_ADDRESS}:{Config.SITL_PORT}...");

                if (!IsPortAvailable(Config.SITL_PORT))
                {
                    _logger.LogError($"Port {Config.SITL_PORT} is already in use");
                    return false;
                }

                _sitlClient = new UdpClient(Config.SITL_PORT)
                {
                    Client = { ReceiveTimeout = Config.RECEIVE_TIMEOUT_MS }
                };

                _forwardClient = new UdpClient();

                _cancellationTokenSource = new CancellationTokenSource();
                _isConnected = true;

                _logger.LogSuccess($"Connected to SITL on port {Config.SITL_PORT}");
                _logger.LogInfo($"Forwarding packets to {Config.FORWARD_ADDRESS}:{Config.FORWARD_PORT}");

                _ = Task.Run(() => ListenForPacketsAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (SocketException ex)
            {
                _logger.LogError($"Socket error during connection: {ex.Message} (Code: {ex.ErrorCode})");
                ErrorOccurred?.Invoke(ex);
                await DisconnectAsync();
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during connection: {ex.Message}");
                ErrorOccurred?.Invoke(ex);
                await DisconnectAsync();
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _logger.LogInfo("Disconnecting from SITL...");

                _isConnected = false;
                _cancellationTokenSource?.Cancel();

                await Task.Delay(100);

                _sitlClient?.Close();
                _forwardClient?.Close();

                _sitlClient?.Dispose();
                _forwardClient?.Dispose();

                _sitlClient = null;
                _forwardClient = null;

                _logger.LogSuccess("Disconnected from SITL");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error during disconnect: {ex.Message}");
            }
        }

        private async Task ListenForPacketsAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Started listening for MAVLink packets");

            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _sitlClient.ReceiveAsync();
                    byte[] receivedData = result.Buffer;

                    if (receivedData.Length > 0)
                    {
                        PacketsReceived++;

                        if (IsValidMavlinkPacket(receivedData))
                        {
                            _logger.LogDebug($"Received valid MAVLink packet: {receivedData.Length} bytes");

                            PacketReceived?.Invoke(receivedData, result.RemoteEndPoint);

                            await ForwardPacketAsync(receivedData);
                        }
                        else
                        {
                            _logger.LogWarning($"Received invalid data: {receivedData.Length} bytes (not MAVLink)");
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogDebug("UDP client disposed, stopping listener");
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }
                catch (SocketException ex)
                {
                    _logger.LogError($"Socket error while receiving: {ex.Message}");
                    ErrorOccurred?.Invoke(ex);

                    if (_isConnected)
                    {
                        _logger.LogWarning("Connection lost, attempting to reconnect...");
                        ConnectionLost?.Invoke();
                        await Task.Delay(Config.RECONNECT_DELAY_MS, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogError($"Unexpected error while receiving: {ex.Message}");
                        ErrorOccurred?.Invoke(ex);
                        await Task.Delay(Config.RECONNECT_DELAY_MS, cancellationToken);
                    }
                }
            }

            _logger.LogDebug("Stopped listening for packets");
        }

        private async Task ForwardPacketAsync(byte[] packetData)
        {
            try
            {
                if (_forwardClient == null || !_isConnected)
                {
                    _logger.LogWarning("Cannot forward packet: not connected");
                    return;
                }

                await _forwardClient.SendAsync(packetData, packetData.Length, _forwardEndPoint);
                PacketsForwarded++;

                _logger.LogDebug($"Forwarded packet to {Config.FORWARD_ADDRESS}:{Config.FORWARD_PORT}");
                PacketForwarded?.Invoke(packetData);
            }
            catch (SocketException ex)
            {
                _logger.LogError($"Socket error while forwarding: {ex.Message}");
                ErrorOccurred?.Invoke(ex);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while forwarding packet: {ex.Message}");
                ErrorOccurred?.Invoke(ex);
            }
        }

        private bool IsValidMavlinkPacket(byte[] data)
        {
            if (data == null || data.Length < 8)
                return false;

            return data[0] == 0xFE || data[0] == 0xFD;
        }

        private bool IsPortAvailable(int port)
        {
            try
            {
                using var client = new UdpClient(port);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            _cancellationTokenSource?.Dispose();
        }
    }
}