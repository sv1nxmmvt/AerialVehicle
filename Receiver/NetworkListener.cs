using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Receiver
{
    public class NetworkListener : IDisposable
    {
        private UdpClient _udpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isListening;

        public event Action<byte[], IPEndPoint> PacketReceived;
        public event Action<Exception> ErrorOccurred;

        public bool IsListening => _isListening;
        public int PacketsReceived { get; private set; }

        public async Task<bool> StartListeningAsync()
        {
            try
            {
                if (_isListening)
                {
                    throw new InvalidOperationException("Already listening");
                }

                if (!IsPortAvailable(Config.LISTEN_PORT))
                {
                    throw new InvalidOperationException($"Port {Config.LISTEN_PORT} is already in use");
                }

                _udpClient = new UdpClient(Config.LISTEN_PORT)
                {
                    Client = { ReceiveTimeout = Config.RECEIVE_TIMEOUT_MS }
                };

                _cancellationTokenSource = new CancellationTokenSource();
                _isListening = true;
                PacketsReceived = 0;

                _ = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));

                return true;
            }
            catch (SocketException ex)
            {
                ErrorOccurred?.Invoke(new Exception($"Socket error: {ex.Message} (Code: {ex.ErrorCode})", ex));
                return false;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
                return false;
            }
        }

        public async Task StopListeningAsync()
        {
            try
            {
                if (!_isListening)
                    return;

                _isListening = false;
                _cancellationTokenSource?.Cancel();

                await Task.Delay(100);

                _udpClient?.Close();
                _udpClient?.Dispose();
                _udpClient = null;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(new Exception($"Error stopping listener: {ex.Message}", ex));
            }
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            if (Config.ENABLE_CONSOLE_LOGGING)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Started listening on port {Config.LISTEN_PORT}");
            }

            while (_isListening && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    byte[] receivedData = result.Buffer;

                    if (receivedData.Length > 0)
                    {
                        PacketsReceived++;

                        if (Config.ENABLE_DETAILED_LOGGING)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received packet #{PacketsReceived}: {receivedData.Length} bytes from {result.RemoteEndPoint}");
                        }

                        PacketReceived?.Invoke(receivedData, result.RemoteEndPoint);
                    }
                }
                catch (ObjectDisposedException)
                {
                    if (Config.ENABLE_CONSOLE_LOGGING)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] UDP client disposed, stopping listener");
                    }
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }
                catch (SocketException ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Socket error: {ex.Message}");
                        ErrorOccurred?.Invoke(ex);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Unexpected error: {ex.Message}");
                        ErrorOccurred?.Invoke(ex);
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }

            if (Config.ENABLE_CONSOLE_LOGGING)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stopped listening");
            }
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
            StopListeningAsync().Wait();
            _cancellationTokenSource?.Dispose();
        }
    }
}