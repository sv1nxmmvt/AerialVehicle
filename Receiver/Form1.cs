using System;
using System.Data;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Receiver
{
    public partial class Form1 : Form
    {
        private NetworkListener _networkListener;
        private MavlinkDecoder _mavlinkDecoder;
        private DataTable _packetsDataTable;

        private Button _btnStart;
        private Button _btnStop;
        private Button _btnClear;
        private Label _lblStatus;
        private Label _lblPacketsCount;
        private DataGridView _dgvPackets;

        public Form1()
        {
            InitializeComponent();
            InitializeDataTable();
            InitializeCustomComponents();
            InitializeManagers();
        }

        private void InitializeDataTable()
        {
            _packetsDataTable = new DataTable();
            _packetsDataTable.Columns.Add("№", typeof(int));
            _packetsDataTable.Columns.Add("Время получения", typeof(string));
            _packetsDataTable.Columns.Add("Версия", typeof(string));
            _packetsDataTable.Columns.Add("Имя пакета", typeof(string));
            _packetsDataTable.Columns.Add("Размер", typeof(string));
            _packetsDataTable.Columns.Add("Sys ID", typeof(string));
            _packetsDataTable.Columns.Add("Содержимое", typeof(string));
        }

        private void InitializeCustomComponents()
        {
            this.Text = "MAVLink Receiver - UDP Packet Decoder";
            this.Size = new System.Drawing.Size(1100, 650);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new System.Drawing.Size(900, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            CreateControls();
        }

        private void CreateControls()
        {
            _btnStart = new Button
            {
                Name = "btnStart",
                Text = "Start Listening",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(120, 30),
                TabIndex = 0
            };
            _btnStart.Click += BtnStart_Click;
            this.Controls.Add(_btnStart);

            _btnStop = new Button
            {
                Name = "btnStop",
                Text = "Stop Listening",
                Location = new System.Drawing.Point(145, 12),
                Size = new System.Drawing.Size(120, 30),
                Enabled = false,
                TabIndex = 1
            };
            _btnStop.Click += BtnStop_Click;
            this.Controls.Add(_btnStop);

            _btnClear = new Button
            {
                Name = "btnClear",
                Text = "Clear Table",
                Location = new System.Drawing.Point(280, 12),
                Size = new System.Drawing.Size(100, 30),
                TabIndex = 2
            };
            _btnClear.Click += BtnClear_Click;
            this.Controls.Add(_btnClear);

            _lblStatus = new Label
            {
                Name = "lblStatus",
                Text = $"Status: Not listening | Port: {Config.LISTEN_PORT}",
                Location = new System.Drawing.Point(400, 20),
                Size = new System.Drawing.Size(300, 13),
                AutoSize = true
            };
            this.Controls.Add(_lblStatus);

            _lblPacketsCount = new Label
            {
                Name = "lblPacketsCount",
                Text = "Packets Received: 0",
                Location = new System.Drawing.Point(12, 55),
                Size = new System.Drawing.Size(150, 13),
                AutoSize = true
            };
            this.Controls.Add(_lblPacketsCount);

            _dgvPackets = new DataGridView
            {
                Name = "dgvPackets",
                Location = new System.Drawing.Point(12, 80),
                Size = new System.Drawing.Size(1060, 520),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                DataSource = _packetsDataTable,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                Font = new System.Drawing.Font("Consolas", 9F),
                TabIndex = 3
            };

            _dgvPackets.DataBindingComplete += (s, e) =>
            {
                if (_dgvPackets.Columns.Count >= 7)
                {
                    _dgvPackets.Columns[0].Width = 50;
                    _dgvPackets.Columns[1].Width = 110;
                    _dgvPackets.Columns[2].Width = 110;
                    _dgvPackets.Columns[3].Width = 180;
                    _dgvPackets.Columns[4].Width = 70;
                    _dgvPackets.Columns[5].Width = 60;
                    _dgvPackets.Columns[6].Width = 450;
                    _dgvPackets.Columns[6].DefaultCellStyle.WrapMode = DataGridViewTriState.False;
                }
            };

            this.Controls.Add(_dgvPackets);
        }

        private void InitializeManagers()
        {
            try
            {
                _networkListener = new NetworkListener();
                _mavlinkDecoder = new MavlinkDecoder();

                _networkListener.PacketReceived += OnPacketReceived;
                _networkListener.ErrorOccurred += OnNetworkError;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Application initialized successfully");
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Configuration: Listen Port={Config.LISTEN_PORT}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize application: {ex.Message}", "Initialization Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnStart_Click(object sender, EventArgs e)
        {
            try
            {
                _btnStart.Enabled = false;
                _btnStart.Text = "Starting...";

                bool started = await _networkListener.StartListeningAsync();

                if (started)
                {
                    UpdateListeningStatus(true);
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Successfully started listening on port {Config.LISTEN_PORT}");
                }
                else
                {
                    _btnStart.Enabled = true;
                    _btnStart.Text = "Start Listening";
                    MessageBox.Show(
                        $"Failed to start listening on port {Config.LISTEN_PORT}\n\n" +
                        "Please ensure:\n" +
                        "1. The port is not in use by another application\n" +
                        "2. Sender application is running and forwarding packets\n" +
                        "3. No firewall is blocking the connection",
                        "Start Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Start error: {ex.Message}");
                MessageBox.Show($"Failed to start listening: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnStart.Enabled = true;
                _btnStart.Text = "Start Listening";
            }
        }

        private async void BtnStop_Click(object sender, EventArgs e)
        {
            try
            {
                _btnStop.Enabled = false;
                await _networkListener.StopListeningAsync();
                UpdateListeningStatus(false);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stopped listening");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Stop error: {ex.Message}");
                MessageBox.Show($"Error stopping listener: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnStop.Enabled = true;
            }
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to clear all {_packetsDataTable.Rows.Count} packets from the table?",
                    "Confirm Clear",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    _packetsDataTable.Clear();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Packets table cleared");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Clear error: {ex.Message}");
                MessageBox.Show($"Error clearing table: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                var packetInfo = _mavlinkDecoder.DecodePacket(data);

                string version = packetInfo.IsValid ? packetInfo.Version : "Invalid";
                string packetName = packetInfo.IsValid ? packetInfo.MessageName : "DECODE_ERROR";
                string packetSize = $"{data.Length}B";
                string sysId = packetInfo.IsValid ? $"{packetInfo.SystemId}:{packetInfo.ComponentId}" : "N/A";

                string content = "";
                if (packetInfo.IsValid)
                {
                    content = $"MsgID: {packetInfo.MessageIdExtended} | Payload: {packetInfo.PayloadLength}B";

                    if (Config.SHOW_HEX_DATA)
                    {
                        string hexPreview = BitConverter.ToString(data, 0, Math.Min(Config.HEX_PREVIEW_LENGTH, data.Length)).Replace("-", " ");
                        if (data.Length > Config.HEX_PREVIEW_LENGTH) hexPreview += "...";
                        content += $" | Hex: {hexPreview}";
                    }
                }
                else
                {
                    content = $"Error: {packetInfo.ErrorMessage}";
                }

                _packetsDataTable.Rows.Add(
                    _networkListener.PacketsReceived,
                    timestamp,
                    version,
                    packetName,
                    packetSize,
                    sysId,
                    content
                );

                if (Config.AUTO_SCROLL_ENABLED && _dgvPackets.Rows.Count > 0)
                {
                    _dgvPackets.FirstDisplayedScrollingRowIndex = _dgvPackets.Rows.Count - 1;
                }

                _lblPacketsCount.Text = $"Packets Received: {_networkListener.PacketsReceived}";

                if (_packetsDataTable.Rows.Count > Config.MAX_TABLE_ROWS)
                {
                    int rowsToRemove = _packetsDataTable.Rows.Count - Config.AUTO_CLEAR_THRESHOLD;
                    for (int i = 0; i < rowsToRemove; i++)
                    {
                        _packetsDataTable.Rows.RemoveAt(0);
                    }
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Auto-cleared {rowsToRemove} old rows");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error processing packet: {ex.Message}");
            }
        }

        private void OnNetworkError(Exception ex)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Exception>(OnNetworkError), ex);
                return;
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Network error: {ex.Message}");
            MessageBox.Show($"Network error: {ex.Message}", "Network Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void UpdateListeningStatus(bool listening)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(UpdateListeningStatus), listening);
                return;
            }

            _btnStart.Enabled = !listening;
            _btnStart.Text = "Start Listening";
            _btnStop.Enabled = listening;

            if (listening)
            {
                _lblStatus.Text = $"Status: Listening on port {Config.LISTEN_PORT} ?";
                _lblStatus.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                _lblStatus.Text = $"Status: Not listening | Port: {Config.LISTEN_PORT}";
                _lblStatus.ForeColor = System.Drawing.Color.Red;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_networkListener?.IsListening == true)
            {
                var result = MessageBox.Show(
                    "Listener is still active. Are you sure you want to exit?",
                    "Confirm Exit",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                _networkListener?.StopListeningAsync().Wait();
            }

            _networkListener?.Dispose();
            base.OnFormClosing(e);
        }
    }
}