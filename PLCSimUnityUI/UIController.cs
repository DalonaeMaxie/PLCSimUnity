using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PLCSimUnity;
using PLCSimUnity.Shared;

namespace PLCSimUnity.UI
{
    public class UIController : Form
    {
        private ProcessManager _processManager;
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _view;
        private Thread _monitorThread;
        private bool _isClosing = false;
        private byte _lastHeartbeat = 0;
        private int _missedHeartbeats = 0;

        private List<InstanceSummary> _availableInstances = new List<InstanceSummary>();
        private System.Windows.Forms.Timer _scanTimer;

        public UIController()
        {
            InitializeComponent();
            this.Text = "PLCSimUnity Controller";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = new Icon(SystemIcons.Application, 32, 32);

            _processManager = new ProcessManager();
            _processManager.ServiceStarted += (s, e) => UpdateUI("Service started");
            _processManager.ServiceStopped += (s, e) => UpdateUI("Service stopped");
            _processManager.ServiceError += (s, msg) => UpdateUI($"Error: {msg}");

            SetupUI();
            SetupMonitoring();
        }

        private void SetupUI()
        {
            // Create main panel
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(10)
            };

            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Service Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); // Connection Status
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Instance Selection
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Instance List
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Control Buttons
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Bottom Buttons

            // Row 0: Service Status
            Panel serviceStatusPanel = CreateServiceStatusPanel();
            mainLayout.Controls.Add(serviceStatusPanel, 0, 0);

            // Row 1: Connection Status
            Panel connectionStatusPanel = CreateConnectionStatusPanel();
            mainLayout.Controls.Add(connectionStatusPanel, 0, 1);

            // Row 2: Instance Selection
            Panel instanceSelectionPanel = CreateInstanceSelectionPanel();
            mainLayout.Controls.Add(instanceSelectionPanel, 0, 2);

            // Row 3: Available Instances List
            Panel instanceListPanel = CreateInstanceListPanel();
            mainLayout.Controls.Add(instanceListPanel, 0, 3);
            mainLayout.SetRowSpan(instanceListPanel, 1);

            // Row 4: Control Buttons
            Panel controlButtonsPanel = CreateControlButtonsPanel();
            mainLayout.Controls.Add(controlButtonsPanel, 0, 4);

            // Row 5: Bottom Buttons
            FlowLayoutPanel bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };

            Button exitButton = new Button { Text = "Exit", Width = 80, Height = 30 };
            exitButton.Click += (s, e) => this.Close();
            bottomPanel.Controls.Add(exitButton);

            mainLayout.Controls.Add(bottomPanel, 0, 5);

            this.Controls.Add(mainLayout);
        }

        private Panel CreateServiceStatusPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            Label label = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Name = "ServiceStatusLabel",
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "● Service: Stopped",
                ForeColor = Color.Red,
                Font = new Font(FontFamily.GenericSansSerif, 11, FontStyle.Bold)
            };
            panel.Controls.Add(label);
            return panel;
        }

        private Panel CreateConnectionStatusPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };

            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(5)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));

            AddStatusRow(layout, 0, "Status:", "StatusValueLabel", "Disconnected");
            AddStatusRow(layout, 1, "Connected PLC:", "ConnectedPlcLabel", "None");
            AddStatusRow(layout, 2, "I/O State:", "IOStateLabel", "Idle");

            panel.Controls.Add(layout);
            return panel;
        }

        private void AddStatusRow(TableLayoutPanel layout, int row, string labelText, string controlName, string initialValue)
        {
            Label label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold)
            };

            Label value = new Label
            {
                Name = controlName,
                Text = initialValue,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DarkBlue
            };

            layout.Controls.Add(label, 0, row);
            layout.Controls.Add(value, 1, row);
        }

        private Panel CreateInstanceSelectionPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };
            FlowLayoutPanel layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            Label label = new Label
            {
                Text = "Select Instance:",
                AutoSize = true,
                Height = 25,
                TextAlign = ContentAlignment.MiddleLeft
            };

            ComboBox instanceDropdown = new ComboBox
            {
                Name = "InstanceDropdown",
                Width = 300,
                Height = 25,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            Button autoDiscoverButton = new Button
            {
                Name = "AutoDiscoverButton",
                Text = "Auto-Discover",
                Width = 120,
                Height = 25
            };
            autoDiscoverButton.Click += AutoDiscoverButton_Click;

            layout.Controls.Add(label);
            layout.Controls.Add(instanceDropdown);
            layout.Controls.Add(autoDiscoverButton);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateInstanceListPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };

            Label title = new Label
            {
                Text = "Available PLC Instances:",
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font(FontFamily.GenericSansSerif, 10, FontStyle.Bold)
            };

            DataGridView grid = new DataGridView
            {
                Name = "InstanceGrid",
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                MultiSelect = false
            };

            grid.Columns.Add("Name", "Instance Name");
            grid.Columns.Add("State", "State");
            grid.Columns.Add("Type", "CPU Type");

            grid.Columns[0].Width = 200;
            grid.Columns[1].Width = 100;
            grid.Columns[2].Width = 150;

            panel.Controls.Add(grid);
            panel.Controls.Add(title);

            return panel;
        }

        private Panel CreateControlButtonsPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };
            FlowLayoutPanel layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            Button startButton = new Button
            {
                Name = "StartServiceButton",
                Text = "Start Service",
                Width = 100,
                Height = 30,
                BackColor = Color.LightGreen
            };
            startButton.Click += StartServiceButton_Click;

            Button stopButton = new Button
            {
                Name = "StopServiceButton",
                Text = "Stop Service",
                Width = 100,
                Height = 30,
                BackColor = Color.LightCoral,
                Enabled = false
            };
            stopButton.Click += StopServiceButton_Click;

            Button connectButton = new Button
            {
                Name = "ConnectButton",
                Text = "Connect Instance",
                Width = 120,
                Height = 30,
                BackColor = Color.LightBlue,
                Enabled = false
            };
            connectButton.Click += ConnectButton_Click;

            layout.Controls.Add(startButton);
            layout.Controls.Add(stopButton);
            layout.Controls.Add(connectButton);

            panel.Controls.Add(layout);
            return panel;
        }

        private void SetupMonitoring()
        {
            // Monitor thread for SHM updates
            _monitorThread = new Thread(MonitorLoop) { IsBackground = true };
            _monitorThread.Start();

            // Timer for scanning instances
            _scanTimer = new System.Windows.Forms.Timer();
            _scanTimer.Interval = 2000; // Scan every 2 seconds
            _scanTimer.Tick += ScanTimer_Tick;
            _scanTimer.Start();
        }

        private void MonitorLoop()
        {
            while (!_isClosing)
            {
                try
                {
                    TryConnectToSharedMemory();
                    if (_view != null)
                    {
                        UpdateStatusFromSharedMemory();
                    }
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    UpdateUI($"Monitor error: {ex.Message}");
                    Thread.Sleep(1000);
                }
            }
        }

        private void TryConnectToSharedMemory()
        {
            if (_view != null)
                return;

            try
            {
                _mmf = MemoryMappedFile.OpenExisting(SharedMemoryConstants.DefaultShmName);
                _view = _mmf.CreateViewAccessor();
                UpdateUI("Connected to service shared memory");
            }
            catch
            {
                // Service hasn't started yet
                _view = null;
            }
        }

        private void UpdateStatusFromSharedMemory()
        {
            try
            {
                byte status = _view.ReadByte(SharedMemoryConstants.Offset_Status);
                byte state = _view.ReadByte(SharedMemoryConstants.Offset_State);
                byte heartbeat = _view.ReadByte(SharedMemoryConstants.Offset_Heartbeat);

                // Check if heartbeat is updating
                if (heartbeat == _lastHeartbeat)
                {
                    _missedHeartbeats++;
                }
                else
                {
                    _missedHeartbeats = 0;
                    _lastHeartbeat = heartbeat;
                }

                // Update UI with connection status
                UpdateConnectionStatus(status, state, _missedHeartbeats > 4);
            }
            catch
            {
                // Shared memory read failed
                _view = null;
            }
        }

        private void UpdateConnectionStatus(byte status, byte state, bool stalled)
        {
            try
            {
                this.Invoke(new Action(() =>
                {
                    Label statusLabel = this.Controls.Find("StatusValueLabel", true).FirstOrDefault() as Label;
                    if (statusLabel != null)
                    {
                        var statusEnum = (ServiceStatus)status;
                        string statusText = statusEnum.ToString();
                        statusLabel.Text = stalled ? $"{statusText} (STALLED)" : statusText;
                        statusLabel.ForeColor = stalled ? Color.Red : Color.DarkGreen;
                    }
                }));
            }
            catch { }
        }

        private void ScanTimer_Tick(object sender, EventArgs e)
        {
            if (_processManager.IsServiceRunning || _processManager.ServiceExistsInTaskManager())
            {
                BeginInvoke(new Action(RefreshInstanceList));
            }
        }

        private void RefreshInstanceList()
        {
            try
            {
                _availableInstances = PlcScanner.ScanForInstances(verbose: false);

                DataGridView grid = this.Controls.Find("InstanceGrid", true).FirstOrDefault() as DataGridView;
                if (grid != null)
                {
                    grid.Rows.Clear();
                    foreach (var instance in _availableInstances)
                    {
                        grid.Rows.Add(instance.Name, instance.OperatingState.ToString(), instance.CpuType);
                    }
                }

                ComboBox dropdown = this.Controls.Find("InstanceDropdown", true).FirstOrDefault() as ComboBox;
                if (dropdown != null)
                {
                    dropdown.DataSource = new List<InstanceSummary>(_availableInstances);
                    dropdown.DisplayMember = "Name";
                }
            }
            catch (Exception ex)
            {
                UpdateUI($"Error scanning instances: {ex.Message}");
            }
        }

        private void UpdateUI(string message)
        {
            try
            {
                this.Invoke(new Action(() =>
                {
                    Label statusLabel = this.Controls.Find("ServiceStatusLabel", true).FirstOrDefault() as Label;
                    if (statusLabel != null)
                    {
                        bool isRunning = _processManager.IsServiceRunning || _processManager.ServiceExistsInTaskManager();
                        statusLabel.Text = isRunning ? "● Service: Running" : "● Service: Stopped";
                        statusLabel.ForeColor = isRunning ? Color.Green : Color.Red;
                    }

                    Button stopButton = this.Controls.Find("StopServiceButton", true).FirstOrDefault() as Button;
                    if (stopButton != null)
                        stopButton.Enabled = isRunning;

                    Button startButton = this.Controls.Find("StartServiceButton", true).FirstOrDefault() as Button;
                    if (startButton != null)
                        startButton.Enabled = !isRunning;
                }));
            }
            catch { }
        }

        private void StartServiceButton_Click(object sender, EventArgs e)
        {
            _processManager.StartService();
            System.Threading.Thread.Sleep(500);
            UpdateUI("Service starting...");
            RefreshInstanceList();
        }

        private void StopServiceButton_Click(object sender, EventArgs e)
        {
            _processManager.StopService();
            UpdateUI("Service stopped");
        }

        private void AutoDiscoverButton_Click(object sender, EventArgs e)
        {
            if (_view != null)
            {
                try
                {
                    _view.Write(SharedMemoryConstants.Offset_SelectedInstanceIndex, -1); // -1 = auto-discover
                    UpdateUI("Auto-discover mode activated");
                }
                catch (Exception ex)
                {
                    UpdateUI($"Error: {ex.Message}");
                }
            }
        }

        private void ConnectButton_Click(object sender, EventArgs e)
        {
            ComboBox dropdown = this.Controls.Find("InstanceDropdown", true).FirstOrDefault() as ComboBox;
            if (dropdown != null && dropdown.SelectedItem is InstanceSummary selected)
            {
                if (_view != null)
                {
                    try
                    {
                        _view.SetSelectedInstanceName(selected.Name);
                        UpdateUI($"Connecting to {selected.Name}...");
                    }
                    catch (Exception ex)
                    {
                        UpdateUI($"Error: {ex.Message}");
                    }
                }
               else
                {
                    UpdateUI("Service not running - start service first");
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isClosing = true;
            _scanTimer?.Stop();
            _scanTimer?.Dispose();

            try
            {
                _view?.Dispose();
                _mmf?.Dispose();
            }
            catch { }

            base.OnFormClosing(e);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 500);
            this.ResumeLayout(false);
        }
    }
}
