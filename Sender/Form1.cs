using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sender
{
    public partial class Form1 : Form
    {
        private UdpClient _sitlClient;
        private UdpClient _forwardClient;
        private IPEndPoint _sitlEndPoint;
        private IPEndPoint _forwardEndPoint;
        private bool _isConnected = false;
        private CancellationTokenSource _cancellationTokenSource;
        private int _packetsReceived = 0;
        private int _packetsSent = 0;

        private const int SITL_PORT = 14550;
        private const int FORWARD_PORT = 14562;
        private const string SITL_ADDRESS = "127.0.0.1";
        private const string FORWARD_ADDRESS = "127.0.0.1";

        public Form1()
        {
            InitializeComponent();
            InitializeNetworking();
            InitializeCustomComponents();
        }

        private void InitializeNetworking()
        {
            _sitlEndPoint = new IPEndPoint(IPAddress.Parse(SITL_ADDRESS), SITL_PORT);
            _forwardEndPoint = new IPEndPoint(IPAddress.Parse(FORWARD_ADDRESS), FORWARD_PORT);
        }

        private void InitializeCustomComponents()
        {
            this.Text = "MAVLink Sender - SITL to UDP Forwarder";
            this.Size = new System.Drawing.Size(600, 520);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            CreateControls();
        }

        private void CreateControls()
        {
            var btnConnect = new Button
            {
                Name = "btnConnect",
                Text = "Connect to SITL",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(100, 30)
            };
            btnConnect.Click += btnConnect_Click;
            this.Controls.Add(btnConnect);

            var btnDisconnect = new Button
            {
                Name = "btnDisconnect",
                Text = "Disconnect",
                Location = new System.Drawing.Point(130, 12),
                Size = new System.Drawing.Size(100, 30),
                Enabled = false
            };
            btnDisconnect.Click += btnDisconnect_Click;
            this.Controls.Add(btnDisconnect);

            var lblStatus = new Label
            {
                Name = "lblStatus",
                Text = "Status: Disconnected",
                Location = new System.Drawing.Point(250, 20),
                Size = new System.Drawing.Size(200, 13),
                AutoSize = true
            };
            this.Controls.Add(lblStatus);

            var lblPacketsReceived = new Label
            {
                Name = "lblPacketsReceived",
                Text = "Packets Received: 0",
                Location = new System.Drawing.Point(12, 60),
                Size = new System.Drawing.Size(120, 13),
                AutoSize = true
            };
            this.Controls.Add(lblPacketsReceived);

            var lblPacketsSent = new Label
            {
                Name = "lblPacketsSent",
                Text = "Packets Sent: 0",
                Location = new System.Drawing.Point(150, 60),
                Size = new System.Drawing.Size(100, 13),
                AutoSize = true
            };
            this.Controls.Add(lblPacketsSent);

            var lstPackets = new ListBox
            {
                Name = "lstPackets",
                Location = new System.Drawing.Point(12, 90),
                Size = new System.Drawing.Size(560, 200)
            };
            this.Controls.Add(lstPackets);

            var lblLog = new Label
            {
                Name = "lblLog",
                Text = "Log:",
                Location = new System.Drawing.Point(12, 300),
                Size = new System.Drawing.Size(25, 13),
                AutoSize = true
            };
            this.Controls.Add(lblLog);

            var rtbLog = new RichTextBox
            {
                Name = "rtbLog",
                Location = new System.Drawing.Point(12, 320),
                Size = new System.Drawing.Size(560, 150),
                ReadOnly = true
            };
            this.Controls.Add(rtbLog);
        }

        private async void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();

                _sitlClient = new UdpClient(SITL_PORT);
                _forwardClient = new UdpClient();

                _isConnected = true;

                var btnConnect = this.Controls["btnConnect"] as Button;
                var btnDisconnect = this.Controls["btnDisconnect"] as Button;
                var lblStatus = this.Controls["lblStatus"] as Label;

                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;
                lblStatus.Text = $"Status: Connected (SITL:{SITL_PORT} -> Forward:{FORWARD_PORT})";

                LogMessage($"Connected to SITL on port {SITL_PORT}");
                LogMessage($"Forwarding to {FORWARD_ADDRESS}:{FORWARD_PORT}");

                await Task.Run(() => ListenForPackets(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage($"Connection error: {ex.Message}");
                ResetConnection();
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            DisconnectFromSITL();
        }

        private void DisconnectFromSITL()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _isConnected = false;

                _sitlClient?.Close();
                _forwardClient?.Close();

                ResetConnection();
                LogMessage("Disconnected from SITL");
            }
            catch (Exception ex)
            {
                LogMessage($"Disconnect error: {ex.Message}");
            }
        }

        private void ResetConnection()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ResetConnection));
                return;
            }

            var btnConnect = this.Controls["btnConnect"] as Button;
            var btnDisconnect = this.Controls["btnDisconnect"] as Button;
            var lblStatus = this.Controls["lblStatus"] as Label;

            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            lblStatus.Text = "Status: Disconnected";
        }

        private async Task ListenForPackets(CancellationToken cancellationToken)
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _sitlClient.ReceiveAsync();
                    byte[] receivedData = result.Buffer;

                    if (receivedData.Length > 0)
                    {
                        _packetsReceived++;

                        string packetInfo = $"[{DateTime.Now:HH:mm:ss.fff}] Received MAVLink packet: {receivedData.Length} bytes from {result.RemoteEndPoint}";

                        Invoke(new Action(() =>
                        {
                            var lstPackets = this.Controls["lstPackets"] as ListBox;
                            var lblPacketsReceived = this.Controls["lblPacketsReceived"] as Label;

                            lstPackets.Items.Add(packetInfo);
                            if (lstPackets.Items.Count > 100)
                            {
                                lstPackets.Items.RemoveAt(0);
                            }
                            lstPackets.TopIndex = lstPackets.Items.Count - 1;
                            lblPacketsReceived.Text = $"Packets Received: {_packetsReceived}";
                        }));

                        await ForwardPacket(receivedData);

                        string hexData = BitConverter.ToString(receivedData).Replace("-", " ");
                        if (hexData.Length > 50) hexData = hexData.Substring(0, 50) + "...";

                        LogMessage($"Packet data (hex): {hexData}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        LogMessage($"Listen error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
        }

        private async Task ForwardPacket(byte[] packetData)
        {
            try
            {
                await _forwardClient.SendAsync(packetData, packetData.Length, _forwardEndPoint);
                _packetsSent++;

                Invoke(new Action(() =>
                {
                    var lblPacketsSent = this.Controls["lblPacketsSent"] as Label;
                    lblPacketsSent.Text = $"Packets Sent: {_packetsSent}";
                }));
            }
            catch (Exception ex)
            {
                LogMessage($"Forward error: {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(LogMessage), message);
                return;
            }

            var rtbLog = this.Controls["rtbLog"] as RichTextBox;
            string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            rtbLog.AppendText(logEntry + Environment.NewLine);
            rtbLog.ScrollToCaret();

            if (rtbLog.Lines.Length > 200)
            {
                var lines = rtbLog.Lines;
                var newLines = new string[100];
                Array.Copy(lines, lines.Length - 100, newLines, 0, 100);
                rtbLog.Lines = newLines;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isConnected)
            {
                DisconnectFromSITL();
            }
            base.OnFormClosing(e);
        }
    }
}