using System;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Receiver
{
    public partial class Form1 : Form
    {
        private UdpClient _udpClient;
        private bool _isListening = false;
        private CancellationTokenSource _cancellationTokenSource;
        private DataTable _packetsDataTable;
        private int _totalPacketsReceived = 0;

        private const int LISTEN_PORT = 14562;

        public Form1()
        {
            InitializeComponent();
            InitializeDataTable();
            InitializeCustomComponents();
        }

        private void InitializeDataTable()
        {
            _packetsDataTable = new DataTable();
            _packetsDataTable.Columns.Add("№", typeof(int));
            _packetsDataTable.Columns.Add("Время получения", typeof(string));
            _packetsDataTable.Columns.Add("Имя пакета", typeof(string));
            _packetsDataTable.Columns.Add("Размер", typeof(string));
            _packetsDataTable.Columns.Add("Содержимое пакета", typeof(string));
        }

        private void InitializeCustomComponents()
        {
            this.Text = "MAVLink Receiver - UDP Packet Decoder";
            this.Size = new System.Drawing.Size(900, 600);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new System.Drawing.Size(800, 500);

            CreateControls();
        }

        private void CreateControls()
        {
            var btnStart = new Button
            {
                Name = "btnStart",
                Text = "Start Listening",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(120, 30)
            };
            btnStart.Click += btnStart_Click;
            this.Controls.Add(btnStart);

            var btnStop = new Button
            {
                Name = "btnStop",
                Text = "Stop Listening",
                Location = new System.Drawing.Point(150, 12),
                Size = new System.Drawing.Size(120, 30),
                Enabled = false
            };
            btnStop.Click += btnStop_Click;
            this.Controls.Add(btnStop);

            var btnClear = new Button
            {
                Name = "btnClear",
                Text = "Clear Table",
                Location = new System.Drawing.Point(290, 12),
                Size = new System.Drawing.Size(100, 30)
            };
            btnClear.Click += btnClear_Click;
            this.Controls.Add(btnClear);

            var lblStatus = new Label
            {
                Name = "lblStatus",
                Text = $"Status: Not listening (Port: {LISTEN_PORT})",
                Location = new System.Drawing.Point(420, 20),
                Size = new System.Drawing.Size(300, 13),
                AutoSize = true
            };
            this.Controls.Add(lblStatus);

            var lblPacketsCount = new Label
            {
                Name = "lblPacketsCount",
                Text = "Packets Received: 0",
                Location = new System.Drawing.Point(12, 55),
                Size = new System.Drawing.Size(150, 13),
                AutoSize = true
            };
            this.Controls.Add(lblPacketsCount);

            var dgvPackets = new DataGridView
            {
                Name = "dgvPackets",
                Location = new System.Drawing.Point(12, 80),
                Size = new System.Drawing.Size(860, 480),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                DataSource = _packetsDataTable,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };

            dgvPackets.DataBindingComplete += (s, e) =>
            {
                if (dgvPackets.Columns.Count >= 5)
                {
                    dgvPackets.Columns[0].Width = 50;
                    dgvPackets.Columns[1].Width = 120;
                    dgvPackets.Columns[2].Width = 150;
                    dgvPackets.Columns[3].Width = 80;
                    dgvPackets.Columns[4].Width = 400;
                }
            };

            this.Controls.Add(dgvPackets);

            this.Resize += (s, e) =>
            {
                dgvPackets.Size = new System.Drawing.Size(
                    this.ClientSize.Width - 24,
                    this.ClientSize.Height - 100
                );
            };
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _udpClient = new UdpClient(LISTEN_PORT);
                _isListening = true;

                var btnStart = this.Controls["btnStart"] as Button;
                var btnStop = this.Controls["btnStop"] as Button;
                var lblStatus = this.Controls["lblStatus"] as Label;

                if (btnStart != null) btnStart.Enabled = false;
                if (btnStop != null) btnStop.Enabled = true;
                if (lblStatus != null) lblStatus.Text = $"Status: Listening on port {LISTEN_PORT}";

                LogMessage($"Started listening on UDP port {LISTEN_PORT}");

                await Task.Run(() => ListenForPackets(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start listening: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogMessage($"Start error: {ex.Message}");
                ResetListening();
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            StopListening();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            _packetsDataTable.Clear();
            _totalPacketsReceived = 0;
            UpdatePacketsCount();
            LogMessage("Packets table cleared");
        }

        private void StopListening()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _isListening = false;
                _udpClient?.Close();

                ResetListening();
                LogMessage("Stopped listening");
            }
            catch (Exception ex)
            {
                LogMessage($"Stop error: {ex.Message}");
            }
        }

        private void ResetListening()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ResetListening));
                return;
            }

            var btnStart = this.Controls["btnStart"] as Button;
            var btnStop = this.Controls["btnStop"] as Button;
            var lblStatus = this.Controls["lblStatus"] as Label;

            if (btnStart != null) btnStart.Enabled = true;
            if (btnStop != null) btnStop.Enabled = false;
            if (lblStatus != null) lblStatus.Text = $"Status: Not listening (Port: {LISTEN_PORT})";
        }

        private async Task ListenForPackets(CancellationToken cancellationToken)
        {
            while (_isListening && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    byte[] receivedData = result.Buffer;

                    if (receivedData.Length > 0)
                    {
                        _totalPacketsReceived++;

                        ProcessMavlinkPacket(receivedData, result.RemoteEndPoint);
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

        private void ProcessMavlinkPacket(byte[] packetData, EndPoint remoteEndPoint)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string packetName = "Unknown MAVLink";
                string packetSize = $"{packetData.Length} bytes";
                string packetContent = "";

                if (packetData.Length >= 8)
                {
                    if (packetData[0] == 0xFE)
                    {
                        packetName = "MAVLink v1.0";
                        if (packetData.Length >= 6)
                        {
                            byte msgId = packetData[5];
                            packetName = $"MAVLink v1.0 (ID: {msgId})";
                            packetContent = GetMavlinkMessageName(msgId);
                        }
                    }
                    else if (packetData[0] == 0xFD)
                    {
                        packetName = "MAVLink v2.0";
                        if (packetData.Length >= 10)
                        {
                            uint msgId = (uint)(packetData[7] | (packetData[8] << 8) | (packetData[9] << 16));
                            packetName = $"MAVLink v2.0 (ID: {msgId})";
                            packetContent = GetMavlinkMessageName((byte)(msgId & 0xFF));
                        }
                    }
                    else
                    {
                        packetName = "Non-MAVLink Data";
                    }
                }

                string hexPreview = BitConverter.ToString(packetData, 0, Math.Min(16, packetData.Length)).Replace("-", " ");
                if (packetData.Length > 16) hexPreview += "...";

                if (!string.IsNullOrEmpty(packetContent))
                {
                    packetContent += $" | Hex: {hexPreview}";
                }
                else
                {
                    packetContent = $"Hex: {hexPreview}";
                }

                Invoke(new Action(() =>
                {
                    _packetsDataTable.Rows.Add(
                        _totalPacketsReceived,
                        timestamp,
                        packetName,
                        packetSize,
                        packetContent
                    );

                    var dgvPackets = this.Controls["dgvPackets"] as DataGridView;
                    if (dgvPackets != null && dgvPackets.Rows.Count > 0)
                    {
                        dgvPackets.FirstDisplayedScrollingRowIndex = dgvPackets.Rows.Count - 1;
                    }

                    UpdatePacketsCount();

                    if (_packetsDataTable.Rows.Count > 1000)
                    {
                        _packetsDataTable.Rows.RemoveAt(0);
                    }
                }));

                LogMessage($"Processed packet #{_totalPacketsReceived}: {packetName}");
            }
            catch (Exception ex)
            {
                LogMessage($"Packet processing error: {ex.Message}");
            }
        }

        private string GetMavlinkMessageName(byte messageId)
        {
            return messageId switch
            {
                0 => "HEARTBEAT",
                1 => "SYS_STATUS",
                2 => "SYSTEM_TIME",
                4 => "PING",
                11 => "SET_MODE",
                20 => "PARAM_REQUEST_READ",
                21 => "PARAM_REQUEST_LIST",
                22 => "PARAM_VALUE",
                24 => "GPS_RAW_INT",
                27 => "RAW_IMU",
                30 => "ATTITUDE",
                32 => "LOCAL_POSITION_NED",
                33 => "GLOBAL_POSITION_INT",
                74 => "VFR_HUD",
                76 => "COMMAND_LONG",
                77 => "COMMAND_ACK",
                87 => "POSITION_TARGET_GLOBAL_INT",
                109 => "RADIO_STATUS",
                116 => "SCALED_IMU2",
                147 => "BATTERY_STATUS",
                230 => "ESTIMATOR_STATUS",
                241 => "VIBRATION",
                253 => "STATUSTEXT",
                _ => $"MSG_ID_{messageId}"
            };
        }

        private void UpdatePacketsCount()
        {
            var lblPacketsCount = this.Controls["lblPacketsCount"] as Label;
            if (lblPacketsCount != null)
            {
                lblPacketsCount.Text = $"Packets Received: {_totalPacketsReceived}";
            }
        }

        private void LogMessage(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isListening)
            {
                StopListening();
            }
            base.OnFormClosing(e);
        }
    }
}