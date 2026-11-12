using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PLCSimUnity.Shared;

namespace PLCSimUnity.PLCSimUnityUI
{
    public class DashboardForm : Form
    {
        private MemoryMappedViewAccessor _view;
        private Timer _statusTimer;
        private Timer _scanTimer;
        private List<InstanceSummary> _allDiscoveredInstances = new List<InstanceSummary>();
        private bool _isScanning = false;
     
        // Telemetry UI Controls - PLCSim
        private Label lblPlcStatus;
        private Label lblPlcInstance;
        private Label lblPlcHeartbeat;

        // Telemetry UI Controls - Unity 3D
        private Label lblUnityStatus;
        private Label lblUnityIo;

        // Telemetry UI Controls - WinCC Runtime HMI
        private Label lblHmiStatus;
        private Label lblHmiDetails;

        // Controls - Instance Search & Grid
        private TextBox txtSearch;
        private DataGridView dgvInstances;
        private Button btnRefresh;
        private Button btnConnectSelected;
        private Button btnAutoDiscover;
        private Label lblScanStatus;

        public DashboardForm(MemoryMappedViewAccessor view)
        {
            _view = view;
            InitializeUI();

            // Telemetry Refresh Timer (10 Hz / 100ms)
            _statusTimer = new Timer { Interval = 100 };
            _statusTimer.Tick += OnStatusTimerTick;
            _statusTimer.Start();

            // Auto Instance Scan Timer (3 Seconds)
            _scanTimer = new Timer { Interval = 3000 };
            _scanTimer.Tick += async (s, e) => await ScanInstancesAsync();
            _scanTimer.Start();

            this.Load += async (s, e) => await ScanInstancesAsync();
        }

        private void InitializeUI()
        {
            this.Text = "PLCSimUnity - Control Center & Telemetry";
            this.Size = new Size(620, 640);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // ==========================================
            // GROUP 1: SYSTEM CONNECTION TELEMETRY
            // ==========================================
            GroupBox grpTelemetry = new GroupBox
            {
                Text = "System Connection States",
                Location = new Point(15, 12),
                Size = new Size(575, 160)
            };

            // 1. PLC Simulation Status Indicator
            lblPlcStatus = new Label { Text = "● PLCSim: Disconnected", Location = new Point(15, 25), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold), ForeColor = Color.Red };
            lblPlcInstance = new Label { Text = "Instance: [Auto-Discover]", Location = new Point(30, 48), AutoSize = true, ForeColor = Color.DimGray };
            lblPlcHeartbeat = new Label { Text = "Heartbeat: 0", Location = new Point(300, 48), AutoSize = true, ForeColor = Color.DimGray };

            // 2. Unity 3D Engine Status Indicator
            lblUnityStatus = new Label { Text = "● Unity 3D: Waiting for Client...", Location = new Point(15, 72), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold), ForeColor = Color.DarkOrange };
            lblUnityIo = new Label { Text = "I/O Buffer: Active (64 In / 64 Out)", Location = new Point(30, 95), AutoSize = true, ForeColor = Color.DimGray };

            // 3. WinCC HMI Runtime Status Indicator
            lblHmiStatus = new Label { Text = "● WinCC HMI Screen: Disconnected", Location = new Point(15, 118), AutoSize = true, Font = new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Bold), ForeColor = Color.Red };
            lblHmiDetails = new Label { Text = "PID: None | Screen Capture Stream: Idle", Location = new Point(30, 138), AutoSize = true, ForeColor = Color.DimGray };

            grpTelemetry.Controls.AddRange(new Control[] {
                lblPlcStatus, lblPlcInstance, lblPlcHeartbeat,
                lblUnityStatus, lblUnityIo,
                lblHmiStatus, lblHmiDetails
            });

            // ==========================================
            // GROUP 2: INSTANCE SELECTION & SEARCH
            // ==========================================
            GroupBox grpInstances = new GroupBox
            {
                Text = "Discovered PLC Instances",
                Location = new Point(15, 180),
                Size = new Size(575, 400)
            };

            Label lblSearch = new Label { Text = "Search / Filter:", Location = new Point(15, 25), AutoSize = true };
            txtSearch = new TextBox { Location = new Point(110, 22), Size = new Size(330, 22) };
            txtSearch.TextChanged += (s, e) => FilterInstanceGrid();

            btnRefresh = new Button { Text = "↻ Refresh", Location = new Point(450, 20), Size = new Size(110, 26) };
            btnRefresh.Click += async (s, e) => await ScanInstancesAsync();

            dgvInstances = new DataGridView
            {
                Location = new Point(15, 55),
                Size = new Size(545, 240),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };

            dgvInstances.Columns.Add("Name", "Instance Name");
            dgvInstances.Columns.Add("State", "Operating State");
            dgvInstances.Columns.Add("Type", "CPU Type");
            dgvInstances.CellDoubleClick += (s, e) => ConnectToSelectedInstance();

            lblScanStatus = new Label { Text = "Ready", Location = new Point(15, 302), AutoSize = true, ForeColor = Color.Gray };

            btnConnectSelected = new Button
            {
                Text = "Connect to Selected Instance",
                Location = new Point(15, 325),
                Size = new Size(265, 35),
                Font = new Font(FontFamily.GenericSansSerif, 9, FontStyle.Bold)
            };
            btnConnectSelected.Click += (s, e) => ConnectToSelectedInstance();

            btnAutoDiscover = new Button
            {
                Text = "Enable Auto-Discover Mode",
                Location = new Point(295, 325),
                Size = new Size(265, 35)
            };
            btnAutoDiscover.Click += (s, e) => EnableAutoDiscover();

            grpInstances.Controls.AddRange(new Control[] { lblSearch, txtSearch, btnRefresh, dgvInstances, lblScanStatus, btnConnectSelected, btnAutoDiscover });

            this.Controls.AddRange(new Control[] { grpTelemetry, grpInstances });
        }

        private async Task ScanInstancesAsync()
        {
            if (_isScanning) return;
            _isScanning = true;
            lblScanStatus.Text = "Scanning for active instances...";

            try
            {
                var instances = await Task.Run(() => PlcScanner.ScanForInstances(verbose: false));
                _allDiscoveredInstances = instances ?? new List<InstanceSummary>();
                FilterInstanceGrid();
                lblScanStatus.Text = $"Found {_allDiscoveredInstances.Count} instance(s) - Last scanned: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                lblScanStatus.Text = $"Scan error: {ex.Message}";
            }
            finally
            {
                _isScanning = false;
            }
        }

        private void FilterInstanceGrid()
        {
            string query = txtSearch.Text.Trim().ToLower();
            string selectedName = dgvInstances.SelectedRows.Count > 0
                ? dgvInstances.SelectedRows[0].Cells["Name"].Value?.ToString()
                : null;

            dgvInstances.Rows.Clear();

            var filtered = _allDiscoveredInstances
                .Where(x => string.IsNullOrEmpty(query) ||
                            (x.Name != null && x.Name.ToLower().Contains(query)) ||
                            (x.CpuType != null && x.CpuType.ToLower().Contains(query)))
                .ToList();

            foreach (var inst in filtered)
            {
                int rowIndex = dgvInstances.Rows.Add(inst.Name, inst.OperatingState.ToString(), inst.CpuType);
                if (inst.OperatingState.ToString().Equals("Run", StringComparison.OrdinalIgnoreCase))
                {
                    dgvInstances.Rows[rowIndex].Cells["State"].Style.ForeColor = Color.DarkGreen;
                    dgvInstances.Rows[rowIndex].Cells["State"].Style.Font = new Font(dgvInstances.Font, FontStyle.Bold);
                }

                if (selectedName != null && inst.Name == selectedName)
                {
                    dgvInstances.Rows[rowIndex].Selected = true;
                }
            }
        }

        private void ConnectToSelectedInstance()
        {
            if (dgvInstances.SelectedRows.Count > 0)
            {
                string selectedName = dgvInstances.SelectedRows[0].Cells["Name"].Value?.ToString();
                if (!string.IsNullOrEmpty(selectedName))
                {
                    WriteInstanceToShm(selectedName);
                }
            }
        }

        private void EnableAutoDiscover()
        {
            WriteInstanceToShm("");
            txtSearch.Clear();
        }

        private void WriteInstanceToShm(string name)
        {
            if (_view == null) return;

            byte[] bytes = string.IsNullOrEmpty(name) ? new byte[0] : System.Text.Encoding.UTF8.GetBytes(name);
            int len = Math.Min(bytes.Length, SharedMemoryConstants.MaxInstanceNameLength);

            _view.Write(SharedMemoryConstants.Offset_SelectedInstanceNameLength, len);
            byte[] clearBuffer = new byte[SharedMemoryConstants.MaxInstanceNameLength];
            _view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, clearBuffer, 0, clearBuffer.Length);

            if (len > 0)
            {
                _view.WriteArray(SharedMemoryConstants.Offset_SelectedInstanceName, bytes, 0, len);
            }
        }

        private void OnStatusTimerTick(object sender, EventArgs e)
        {
            if (_view == null) return;

            try
            {
                // --- 1. PLC STATUS TELEMETRY ---
                byte plcStatus = _view.ReadByte(SharedMemoryConstants.Offset_Status);
                byte heartbeat = _view.ReadByte(SharedMemoryConstants.Offset_Heartbeat);

                int nameLength = _view.ReadInt32(SharedMemoryConstants.Offset_SelectedInstanceNameLength);
                string instanceName = "[Auto-Discover]";
                if (nameLength > 0)
                {
                    byte[] buffer = new byte[nameLength];
                    _view.ReadArray(SharedMemoryConstants.Offset_SelectedInstanceName, buffer, 0, nameLength);
                    instanceName = System.Text.Encoding.UTF8.GetString(buffer);
                }

                lblPlcStatus.Text = $"● PLCSim: {((ServiceStatus)plcStatus).ToString()}";
                lblPlcStatus.ForeColor = plcStatus == (byte)ServiceStatus.Connected ? Color.Green : Color.Red;
                lblPlcInstance.Text = $"Active Instance: {instanceName}";
                lblPlcHeartbeat.Text = $"Heartbeat: {heartbeat}";

                // --- 2. UNITY 3D TELEMETRY ---
                int currentUnityTick = _view.ReadInt32(100); // Read Unity Heartbeat Timestamp
                bool isUnityConnected = (Environment.TickCount - currentUnityTick) < 2000 && currentUnityTick > 0;

                lblUnityStatus.Text = isUnityConnected ? "● Unity 3D Engine: Connected" : "● Unity 3D Engine: Disconnected";
                lblUnityStatus.ForeColor = isUnityConnected ? Color.Green : Color.DarkOrange;

                // --- 3. WINCC HMI RUNTIME TELEMETRY ---
                byte hmiStatus = _view.ReadByte(SharedMemoryConstants.Offset_RuntimeScreenStatus);
                int hmiPid = _view.ReadInt32(SharedMemoryConstants.Offset_BrowserProcessId);
                int framesCaptured = _view.ReadInt32(SharedMemoryConstants.Offset_TotalFramesCaptured);

                if (hmiStatus == (byte)RuntimeScreenStatus.Connected)
                {
                    lblHmiStatus.Text = "● WinCC HMI Screen: Connected";
                    lblHmiStatus.ForeColor = Color.Green;
                    lblHmiDetails.Text = $"PID: {hmiPid} | Total Frames Capture Stream: {framesCaptured}";
                }
                else
                {
                    lblHmiStatus.Text = "● WinCC HMI Screen: Disconnected (HmiRTm Not Found)";
                    lblHmiStatus.ForeColor = Color.Red;
                    lblHmiDetails.Text = "PID: None | Screen Capture Stream: Idle";
                }
            }
            catch { }
        }
    }
}