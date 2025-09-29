using System;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sender
{
    public partial class Form1 : Form
    {
        private NetworkManager _networkManager;
        private Logger _logger;

        private Button _btnConnect;
        private Button _btnDisconnect;
        private Label _lblStatus;
        private Label _lblPacketsReceived;
        private Label _lblPacketsSent;
        private ListBox _lstPackets;
        private RichTextBox _rtbLog;
        private Label _lblLog;
        private Label _lblPacketsList;

        public Form1()
        {
            InitializeComponent();
            InitializeCustomComponents();
            InitializeManagers();
        }

        private void InitializeCustomComponents()
        {
            this.Text = "MAVLink Sender - SITL to UDP Forwarder";
            this.Size = new System.Drawing.Size(600, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            CreateControls();
        }

        private void CreateControls()
        {
            _btnConnect = new Button
            {
                Name = "btnConnect",
                Text = "Connect to SITL",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(120, 30),
                TabIndex = 0
            };
            _btnConnect.Click += BtnConnect_Click;
            this.Controls.Add(_btnConnect);

            _btnDisconnect = new Button
            {
                Name = "btnDisconnect",
                Text = "Disconnect",
                Location = new System.Drawing.Point(145, 12),
                Size = new System.Drawing.Size(120, 30),
                Enabled = false,
                TabIndex = 1
            };
            _btnDisconnect.Click += BtnDisconnect_Click;
            this.Controls.Add(_btnDisconnect);

            _lblStatus = new Label
            {
                Name = "lblStatus",
                Text = $"Status: Disconnected | SITL: {Config.SITL_ADDRESS}:{Config.SITL_PORT}",
                Location = new System.Drawing.Point(280, 20),
                Size = new System.Drawing.Size(300, 13),
                AutoSize = true
            };
            this.Controls.Add(_lblStatus);

            _lblPacketsReceived = new Label
            {
                Name = "lblPacketsReceived",
                Text = "Packets Received: 0",
                Location = new System.Drawing.Point(12, 60),
                Size = new System.Drawing.Size(150, 13),
                AutoSize = true
            };
            this.Controls.Add(_lblPacketsReceived);

            _lblPacketsSent = new Label
            {
                Name = "lblPacketsSent",
                Text = "Packets Forwarded: 0",
                Location = new System.Drawing.Point(180, 60),
                Size = new System.Drawing.Size(150, 13),
                AutoSize = true
            };
            this.Controls.Add(_lblPacketsSent);

            _lblPacketsList = new Label
            {
                Name = "lblPacketsList",
                Text = "Received Packets:",
                Location = new System.Drawing.Point(12, 85),
                Size = new System.Drawing.Size(100, 13),
                AutoSize = true
            };
            this.Controls.Add(_lblPacketsList);

            _lstPackets = new ListBox
            {
                Name = "lstPackets",
                Location = new System.Drawing.Point(12, 105),
                Size = new System.Drawing.Size(560, 180),
                TabIndex = 2
            };
            this.Controls.Add(_lstPackets);

            _lblLog = new Label
            {
                Name = "lblLog",
                Text = "Event Log:",
                Location = new System.Drawing.Point(12, 295),
                Size = new System.Drawing.Size(60, 13),
                AutoSize = true
            };
            this.Controls.Add(_lblLog);

            _rtbLog = new RichTextBox
            {
                Name = "rtbLog",
                Location = new System.Drawing.Point(12, 315),
                Size = new System.Drawing.Size(560, 230),
                ReadOnly = true,
                TabIndex = 3,
                Font = new System.Drawing.Font("Consolas", 9F)
            };
            this.Controls.Add(_rtbLog);
        }

        private void InitializeManagers()
        {
            try
            {
                _logger = new Logger(_rtbLog);
                _networkManager = new NetworkManager(_logger);

                _networkManager.PacketReceived += OnPacketReceived;
                _networkManager.PacketForwarded += OnPacketForwarded;
                _networkManager.ErrorOccurred += OnNetworkError;
                _networkManager.ConnectionLost += OnConnectionLost;

                _logger.LogInfo("Application initialized successfully");
                _logger.LogInfo($"Configuration: SITL={Config.SITL_ADDRESS}:{Config.SITL_PORT}, Forward={Config.FORWARD_ADDRESS}:{Config.FORWARD_PORT}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize application: {ex.Message}", "Initialization Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                _btnConnect.Enabled = false;
                _btnConnect.Text = "Connecting...";

                bool connected = await _networkManager.ConnectAsync();

                if (connected)
                {
                    UpdateConnectionStatus(true);
                }
                else
                {
                    _btnConnect.Enabled = true;
                    _btnConnect.Text = "Connect to SITL";
                    MessageBox.Show(
                        $"Failed to connect to SITL at {Config.SITL_ADDRESS}:{Config.SITL_PORT}\n\n" +
                        "Please ensure:\n" +
                        "1. SITL ArduPilot is running\n" +
                        "2. Port 14550 is not in use by another application\n" +
                        "3. No firewall is blocking the connection",
                        "Connection Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Connection error: {ex.Message}");
                MessageBox.Show($"Connection failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnConnect.Enabled = true;
                _btnConnect.Text = "Connect to SITL";
            }
        }

        private async void BtnDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                _btnDisconnect.Enabled = false;
                await _networkManager.DisconnectAsync();
                UpdateConnectionStatus(false);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Disconnect error: {ex.Message}");
            }
        }

        private void OnPacketReceived(byte[] data, IPEndPoint remoteEndPoint)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<byte[], IPEndPoint>(OnPacketReceived), data, remoteEndPoint);
                return;
            }

            try
            {
                string packetInfo = $"[{DateTime.Now:HH:mm:ss.fff}] Received: {data.Length} bytes from {remoteEndPoint}";

                _lstPackets.Items.Add(packetInfo);
                if (_lstPackets.Items.Count > Config.MAX_LISTBOX_ITEMS)
                {
                    _lstPackets.Items.RemoveAt(0);
                }
                _lstPackets.TopIndex = _lstPackets.Items.Count - 1;

                _lblPacketsReceived.Text = $"Packets Received: {_networkManager.PacketsReceived}";

                if (Config.SHOW_HEX_DATA && Config.ENABLE_DETAILED_LOGGING)
                {
                    string hexData = BitConverter.ToString(data, 0, Math.Min(Config.HEX_PREVIEW_LENGTH, data.Length)).Replace("-", " ");
                    if (data.Length > Config.HEX_PREVIEW_LENGTH) hexData += "...";
                    _logger.LogDebug($"Packet data: {hexData}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error displaying packet: {ex.Message}");
            }
        }

        private void OnPacketForwarded(byte[] data)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<byte[]>(OnPacketForwarded), data);
                return;
            }

            try
            {
                _lblPacketsSent.Text = $"Packets Forwarded: {_networkManager.PacketsForwarded}";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating forward counter: {ex.Message}");
            }
        }

        private void OnNetworkError(Exception ex)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Exception>(OnNetworkError), ex);
                return;
            }

            _logger.LogError($"Network error: {ex.Message}");
        }

        private async void OnConnectionLost()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(OnConnectionLost));
                return;
            }

            _logger.LogWarning("Connection to SITL lost");

            var result = MessageBox.Show(
                "Connection to SITL was lost.\n\nWould you like to reconnect?",
                "Connection Lost",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                await _networkManager.DisconnectAsync();
                await Task.Delay(500);
                BtnConnect_Click(null, null);
            }
            else
            {
                await _networkManager.DisconnectAsync();
                UpdateConnectionStatus(false);
            }
        }

        private void UpdateConnectionStatus(bool connected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(UpdateConnectionStatus), connected);
                return;
            }

            _btnConnect.Enabled = !connected;
            _btnConnect.Text = "Connect to SITL";
            _btnDisconnect.Enabled = connected;

            if (connected)
            {
                _lblStatus.Text = $"Status: Connected | Forwarding to {Config.FORWARD_ADDRESS}:{Config.FORWARD_PORT}";
                _lblStatus.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                _lblStatus.Text = $"Status: Disconnected | SITL: {Config.SITL_ADDRESS}:{Config.SITL_PORT}";
                _lblStatus.ForeColor = System.Drawing.Color.Red;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_networkManager?.IsConnected == true)
            {
                var result = MessageBox.Show(
                    "Connection is still active. Are you sure you want to exit?",
                    "Confirm Exit",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _networkManager?.DisconnectAsync().Wait();
            }

            _networkManager?.Dispose();
            base.OnFormClosing(e);
        }
    }
}