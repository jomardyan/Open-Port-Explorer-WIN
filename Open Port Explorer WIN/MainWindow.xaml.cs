using Microsoft.Win32;
using Open_Port_Explorer_WIN.Helpers;
using Open_Port_Explorer_WIN.Interfaces;
using Open_Port_Explorer_WIN.Models;
using Open_Port_Explorer_WIN.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace Open_Port_Explorer_WIN
{
    public partial class MainWindow : Window
    {
        private readonly INetworkService _networkService;
        private readonly IRuleService _ruleService;
        private readonly IPreferencesService _preferencesService;
        private readonly IExportService _exportService;
        private readonly IFormatHelpers _formatHelpers;

        private readonly ObservableCollection<PortEntry> _ports = [];
        private readonly ObservableCollection<PortHistoryEntry> _historyView = [];
        private readonly DispatcherTimer _refreshTimer = new();
        private readonly DispatcherTimer _filterDebounceTimer = new() { Interval = TimeSpan.FromMilliseconds(180) };
        private ICollectionView? _portsView;
        private Dictionary<string, string> _previousConnectionStates = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, PortEntry> _previousEntriesByIdentity = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, PortEntry>? _baselineEntries;
        private readonly List<PortHistoryEntry> _history = [];
        private readonly Dictionary<string, ObservedConnectionState> _observedConnections = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _remoteHostNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<int> _trustedPorts = [];
        private readonly HashSet<int> _blockedPorts = [];
        private readonly HashSet<int> _watchedPorts = [];
        private readonly HashSet<string> _trustedProcesses = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _blockedProcesses = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _watchedProcesses = new(StringComparer.OrdinalIgnoreCase);
        private const int MaxHistoryEntries = 2000;
        private bool _isRunningAsAdmin;
        private bool _isRefreshing;
        private bool _suppressSelectionChanged;
        private bool _isDarkTheme;
        private int _detailsRequestVersion;
        private FilterCriteria _filterCriteria = FilterCriteria.Default;
        private const int PortAddBatchSize = 200;
        private int _refreshCount;

        public MainWindow()
            : this(
                new PortInfoService(),
                new PreferencesService(),
                new ExportService(),
                new FormatHelpers())
        {
        }

        public MainWindow(
            IPortInfoService portInfoService,
            IPreferencesService preferencesService,
            IExportService exportService,
            IFormatHelpers formatHelpers)
        {
            _networkService = new NetworkService(portInfoService);
            _ruleService = new RuleService(portInfoService, _networkService);
            _preferencesService = preferencesService;
            _exportService = exportService;
            _formatHelpers = formatHelpers;

            InitializeComponent();
            PortsDataGrid.ItemsSource = _ports;
            HistoryListBox.ItemsSource = _historyView;

            _portsView = CollectionViewSource.GetDefaultView(_ports);
            if (_portsView is not null)
            {
                _portsView.Filter = FilterPort;
            }

            UpdateFilterCriteria();

            _refreshTimer.Tick += RefreshTimer_Tick;
            _filterDebounceTimer.Tick += FilterDebounceTimer_Tick;
            SetTimerInterval();
            LoadPreferences();
            ApplyTheme(_isDarkTheme);
            SetAdminStatus();
            RefreshPortData();
            UpdateTimerState();
            UpdateDashboard();
            UpdateDetailsPanel();
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e) => RefreshPortData();

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshPortData();

        private void MenuRefresh_Click(object sender, RoutedEventArgs e) => RefreshPortData();

        private void MenuExportCsv_Click(object sender, RoutedEventArgs e) => ExportCsvButton_Click(sender, e);

        private void MenuExportJson_Click(object sender, RoutedEventArgs e) => ExportJsonButton_Click(sender, e);

        private void MenuWatchSelected_Click(object sender, RoutedEventArgs e) => WatchSelectedButton_Click(sender, e);

        private void MenuTrustSelected_Click(object sender, RoutedEventArgs e) => TrustSelectedButton_Click(sender, e);

        private void MenuBlockSelected_Click(object sender, RoutedEventArgs e) => BlockSelectedButton_Click(sender, e);

        private void MenuExportHistory_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"port-activity-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _exportService.WriteActivityLog(_history, saveDialog.FileName);
                AlertTextBlock.Text = $"Exported activity log: {saveDialog.FileName}";
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Unable to export activity log: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();

        private void MenuEndProcess_Click(object sender, RoutedEventArgs e) => TerminateProcessButton_Click(sender, e);

        private void MenuOpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            if (PortsDataGrid.SelectedItem is not PortEntry entry || string.IsNullOrWhiteSpace(entry.ProcessPath))
            {
                MessageBox.Show("Process path is unavailable.", "Open File Location", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(entry.ProcessPath);
                if (Directory.Exists(directory))
                {
                    Process.Start("explorer.exe", directory);
                }
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Unable to open file location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show($"Unable to open file location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuResetFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
            PortFilterTextBox.Clear();
            AddressFilterTextBox.Clear();
            ProcessFilterTextBox.Clear();
            PidFilterTextBox.Clear();
            ProtocolFilterComboBox.SelectedIndex = 0;
            FamilyFilterComboBox.SelectedIndex = 0;
            StateFilterComboBox.SelectedIndex = 0;
            RuleFilterComboBox.SelectedIndex = 0;
            SuspiciousOnlyCheckBox.IsChecked = false;
            WatchlistOnlyCheckBox.IsChecked = false;
            UpdateFilterCriteria();
            _portsView?.Refresh();
            UpdateCount();
        }

        private void MenuSetBaseline_Click(object sender, RoutedEventArgs e)
        {
            _baselineEntries = _ports
                .GroupBy(_formatHelpers.CreateConnectionIdentity, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);

            AlertTextBlock.Text = $"Snapshot baseline set with {_baselineEntries.Count} entries.";
        }

        private void MenuCompareBaseline_Click(object sender, RoutedEventArgs e)
        {
            if (_baselineEntries is null)
            {
                MessageBox.Show("Set a baseline first from View > Set Snapshot Baseline.", "Compare Baseline", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var current = _ports
                .GroupBy(_formatHelpers.CreateConnectionIdentity, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(static g => g.Key, static g => g.First(), StringComparer.OrdinalIgnoreCase);

            var opened = current.Keys.Except(_baselineEntries.Keys).Count();
            var closed = _baselineEntries.Keys.Except(current.Keys).Count();
            var suspiciousOpened = current
                .Where(kv => !_baselineEntries.ContainsKey(kv.Key) && kv.Value.IsSuspicious)
                .Count();

            MessageBox.Show(
                $"Compared with baseline:\nOpened: {opened}\nClosed: {closed}\nNew suspicious: {suspiciousOpened}",
                "Baseline Comparison",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void MenuCopyRow_Click(object sender, RoutedEventArgs e)
        {
            if (PortsDataGrid.SelectedItem is not PortEntry entry)
            {
                return;
            }

            var text = $"Port: {entry.PortNumber}\nService: {entry.ServiceName}\nProtocol: {entry.Protocol}/{entry.AddressFamily}\nState: {entry.State}\nLocal: {entry.LocalAddress}\nRemote: {entry.RemoteAddress}\nPID: {entry.ProcessId}\nProcess: {entry.ProcessName}\nRule: {entry.RuleStatus}\nSuspicious: {entry.IsSuspicious}\nReason: {entry.SuspicionReason}";
            Clipboard.SetText(text);
            AlertTextBlock.Text = "Selected row copied to clipboard.";
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Open Port Explorer\nReal-time port monitoring with process mapping, rules, watchlists, history, and snapshot comparison.", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AutoRefreshCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateTimerState();

        private void IntervalComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SetTimerInterval();
        }

        private void Filters_SelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateFilterCriteria();
            _portsView?.Refresh();
            UpdateCount();
        }

        private void Filters_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateFilterCriteria();
            _portsView?.Refresh();
            UpdateCount();
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            QueueFilterRefresh();
        }

        private void FilterDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _filterDebounceTimer.Stop();
            UpdateFilterCriteria();
            _portsView?.Refresh();
            UpdateCount();
        }

        private void QueueFilterRefresh()
        {
            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Start();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                RefreshPortData();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R)
            {
                MenuResetFilters_Click(sender, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
            {
                return;
            }

            if (Keyboard.FocusedElement is TextBox focusedTextBox && focusedTextBox.Parent is not null)
            {
                focusedTextBox.Clear();
                e.Handled = true;
            }
        }

        private void PortsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged)
            {
                return;
            }

            UpdateDetailsPanel();
            RefreshHistoryView();
        }

        private void WatchSelectedButton_Click(object sender, RoutedEventArgs e) => ToggleSelectedRule(RuleMode.Watched);

        private void TrustSelectedButton_Click(object sender, RoutedEventArgs e) => ToggleSelectedRule(RuleMode.Trusted);

        private void BlockSelectedButton_Click(object sender, RoutedEventArgs e) => ToggleSelectedRule(RuleMode.Blocked);

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDarkTheme = !_isDarkTheme;
            ApplyTheme(_isDarkTheme);
            SavePreferences();
        }

        private async void RefreshPortData()
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            try
            {
                var selectedIdentity = PortsDataGrid.SelectedItem is PortEntry selectedEntry
                    ? _formatHelpers.CreateConnectionIdentity(selectedEntry)
                    : null;
                var scanTimestamp = DateTime.Now;
                var entries = await Task.Run(_networkService.GetPortEntries).ConfigureAwait(true);

                ApplyObservationState(entries, scanTimestamp);

                var currentEntriesByIdentity = new Dictionary<string, PortEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                {
                    var identity = _formatHelpers.CreateConnectionIdentity(entry);
                    currentEntriesByIdentity.TryAdd(identity, entry);
                }

                var opened = 0;
                var stateChanged = 0;
                foreach (var kv in currentEntriesByIdentity)
                {
                    if (!_previousConnectionStates.TryGetValue(kv.Key, out var previousState))
                    {
                        opened++;
                    }
                    else if (!string.Equals(kv.Value.State, previousState, StringComparison.OrdinalIgnoreCase))
                    {
                        stateChanged++;
                    }
                }

                var closed = 0;
                foreach (var key in _previousConnectionStates.Keys)
                {
                    if (!currentEntriesByIdentity.ContainsKey(key))
                    {
                        closed++;
                    }
                }
                var alertedOpened = currentEntriesByIdentity
                    .Where(kv => !_previousEntriesByIdentity.ContainsKey(kv.Key) && (kv.Value.IsSuspicious || kv.Value.IsWatched || string.Equals(kv.Value.RuleStatus, "Blocked", StringComparison.OrdinalIgnoreCase)))
                    .Count();

                TrackHistory(currentEntriesByIdentity, scanTimestamp);

                var currentStates = new Dictionary<string, string>(currentEntriesByIdentity.Count, StringComparer.OrdinalIgnoreCase);
                foreach (var kv in currentEntriesByIdentity)
                {
                    currentStates[kv.Key] = kv.Value.State;
                }

                _previousConnectionStates = currentStates;
                _previousEntriesByIdentity = currentEntriesByIdentity;

                var sortedEntries = entries
                    .OrderByDescending(static e => e.IsSuspicious)
                    .ThenBy(static e => e.PortNumber)
                    .ThenBy(static e => e.Protocol)
                    .ThenBy(static e => e.ProcessName)
                    .ToArray();

                UpdateFilterCriteria();
                await ReplacePortsAsync(sortedEntries).ConfigureAwait(true);

                LastRefreshTextBlock.Text = $"Last refresh: {scanTimestamp:yyyy-MM-dd HH:mm:ss}";
                AlertTextBlock.Text = alertedOpened > 0
                    ? $"Opened: {opened}, Closed: {closed}, State changes: {stateChanged}, Alerts: {alertedOpened}"
                    : $"Opened: {opened}, Closed: {closed}, State changes: {stateChanged}";

                if (!string.IsNullOrWhiteSpace(selectedIdentity))
                {
                    var restoredSelection = _ports.FirstOrDefault(port => string.Equals(_formatHelpers.CreateConnectionIdentity(port), selectedIdentity, StringComparison.OrdinalIgnoreCase));
                    if (restoredSelection is not null)
                    {
                        PortsDataGrid.SelectedItem = restoredSelection;
                    }
                }

                UpdateCount();
                UpdateDashboard();
                UpdateDetailsPanel();
                RefreshHistoryView();
            }
            catch (InvalidOperationException ex)
            {
                AlertTextBlock.Text = $"Refresh error: {ex.Message}";
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                AlertTextBlock.Text = $"Unexpected refresh error: {ex.Message}";
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async Task ReplacePortsAsync(IReadOnlyList<PortEntry> entries)
        {
            _suppressSelectionChanged = true;
            try
            {
                using (_portsView is IEditableCollectionView ? _portsView?.DeferRefresh() : null)
                {
                    _ports.Clear();
                    if (entries.Count == 0)
                    {
                        return;
                    }

                    for (var i = 0; i < entries.Count; i += PortAddBatchSize)
                    {
                        var batchEnd = Math.Min(i + PortAddBatchSize, entries.Count);
                        for (var j = i; j < batchEnd; j++)
                        {
                            _ports.Add(entries[j]);
                        }

                        await Dispatcher.Yield(DispatcherPriority.Background);
                    }
                }
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        private bool FilterPort(object obj)
        {
            if (obj is not PortEntry entry)
            {
                return false;
            }

            if (_filterCriteria.SuspiciousOnly && !entry.IsSuspicious)
            {
                return false;
            }

            if (_filterCriteria.WatchlistOnly && !entry.IsWatched)
            {
                return false;
            }

            var search = _filterCriteria.Search;
            var portFilter = _filterCriteria.Port;
            var addressFilter = _filterCriteria.Address;
            var processFilter = _filterCriteria.Process;
            var pidFilter = _filterCriteria.Pid;
            var protocolFilter = _filterCriteria.Protocol;
            var familyFilter = _filterCriteria.Family;
            var stateFilter = _filterCriteria.State;
            var ruleFilter = _filterCriteria.Rule;

            if (!string.Equals(protocolFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.Protocol, protocolFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(familyFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.AddressFamily, familyFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(stateFilter, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entry.State, stateFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(ruleFilter, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(ruleFilter, "Unruled", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(entry.RuleStatus, "Unruled", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                else if (!string.Equals(entry.RuleStatus, ruleFilter, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(portFilter) &&
                !entry.PortNumber.ToString(CultureInfo.InvariantCulture).Contains(portFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(addressFilter) &&
                !entry.LocalAddress.Contains(addressFilter, StringComparison.OrdinalIgnoreCase) &&
                !entry.RemoteAddress.Contains(addressFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(processFilter) &&
                !entry.ProcessName.Contains(processFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(pidFilter) &&
                !entry.ProcessId.ToString(CultureInfo.InvariantCulture).Contains(pidFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return entry.PortNumber.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Protocol.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.State.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.LocalAddress.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.RemoteAddress.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.ProcessId.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.ServiceName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.RuleStatus.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.SuspicionReason.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateFilterCriteria()
        {
            _filterCriteria = new FilterCriteria(
                SearchTextBox.Text.Trim(),
                PortFilterTextBox.Text.Trim(),
                AddressFilterTextBox.Text.Trim(),
                ProcessFilterTextBox.Text.Trim(),
                PidFilterTextBox.Text.Trim(),
                GetSelectedComboValue(ProtocolFilterComboBox),
                GetSelectedComboValue(FamilyFilterComboBox),
                GetSelectedComboValue(StateFilterComboBox),
                GetSelectedComboValue(RuleFilterComboBox),
                SuspiciousOnlyCheckBox.IsChecked == true,
                WatchlistOnlyCheckBox.IsChecked == true);
        }

        private static string GetSelectedComboValue(System.Windows.Controls.ComboBox? comboBox)
        {
            if (comboBox?.SelectedItem is not System.Windows.Controls.ComboBoxItem item)
            {
                return "All";
            }

            return item.Content?.ToString() ?? "All";
        }

        private void ApplyObservationState(IEnumerable<PortEntry> entries, DateTime scanTimestamp)
        {
            var seenIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var identity = _formatHelpers.CreateConnectionIdentity(entry);
                seenIdentities.Add(identity);

                if (!_observedConnections.TryGetValue(identity, out var observation))
                {
                    observation = new ObservedConnectionState
                    {
                        FirstSeen = scanTimestamp,
                        LastSeen = scanTimestamp,
                        LastActivity = scanTimestamp,
                        LastState = entry.State
                    };
                }
                else
                {
                    observation.LastSeen = scanTimestamp;
                    if (!string.Equals(observation.LastState, entry.State, StringComparison.OrdinalIgnoreCase))
                    {
                        observation.LastActivity = scanTimestamp;
                        observation.LastState = entry.State;
                    }
                }

                _observedConnections[identity] = observation;

                entry.FirstSeen = observation.FirstSeen;
                entry.LastSeen = observation.LastSeen;
                entry.LastActivity = observation.LastActivity;
                entry.ObservedSeconds = Math.Max(0, (scanTimestamp - observation.FirstSeen).TotalSeconds);
                entry.ObservedDuration = _formatHelpers.FormatDuration(entry.ObservedSeconds);
                entry.RemoteHostName = _remoteHostNames.GetValueOrDefault(entry.RemoteAddressRaw, string.Empty);

                _ruleService.ApplyRules(entry, _trustedPorts, _blockedPorts, _watchedPorts, _trustedProcesses, _blockedProcesses, _watchedProcesses);
                _ruleService.EvaluateSuspicion(entry);
            }

            if (++_refreshCount % 10 == 0)
            {
                var staleThreshold = scanTimestamp.AddHours(-2);
                var staleKeys = _observedConnections
                    .Where(pair => pair.Value.LastSeen < staleThreshold)
                    .Select(static pair => pair.Key)
                    .ToArray();

                foreach (var staleKey in staleKeys)
                {
                    _observedConnections.Remove(staleKey);
                }
            }
        }

        private void UpdateDashboard()
        {
            var listening = 0;
            var established = 0;
            var flagged = 0;
            var watched = 0;

            foreach (var entry in _ports)
            {
                if (string.Equals(entry.State, "LISTENING", StringComparison.OrdinalIgnoreCase)) listening++;
                else if (string.Equals(entry.State, "ESTABLISHED", StringComparison.OrdinalIgnoreCase)) established++;
                if (entry.IsSuspicious || string.Equals(entry.RuleStatus, "Blocked", StringComparison.OrdinalIgnoreCase)) flagged++;
                if (entry.IsWatched) watched++;
            }

            CurrentRowsCardTextBlock.Text = _ports.Count.ToString(CultureInfo.InvariantCulture);
            ListeningCardTextBlock.Text = listening.ToString(CultureInfo.InvariantCulture);
            EstablishedCardTextBlock.Text = established.ToString(CultureInfo.InvariantCulture);
            FlaggedCardTextBlock.Text = flagged.ToString(CultureInfo.InvariantCulture);
            WatchlistCardTextBlock.Text = watched.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateCount()
        {
            if (CountTextBlock is null || StatsTextBlock is null)
            {
                return;
            }

            var count = 0;
            var listening = 0;
            var established = 0;
            var flagged = 0;
            var watched = 0;

            if (_portsView is not null)
            {
                foreach (PortEntry e in _portsView)
                {
                    count++;
                    if (string.Equals(e.State, "LISTENING", StringComparison.OrdinalIgnoreCase)) listening++;
                    else if (string.Equals(e.State, "ESTABLISHED", StringComparison.OrdinalIgnoreCase)) established++;
                    if (e.IsSuspicious || string.Equals(e.RuleStatus, "Blocked", StringComparison.OrdinalIgnoreCase)) flagged++;
                    if (e.IsWatched) watched++;
                }
            }
            else
            {
                count = _ports.Count;
                foreach (var e in _ports)
                {
                    if (string.Equals(e.State, "LISTENING", StringComparison.OrdinalIgnoreCase)) listening++;
                    else if (string.Equals(e.State, "ESTABLISHED", StringComparison.OrdinalIgnoreCase)) established++;
                    if (e.IsSuspicious || string.Equals(e.RuleStatus, "Blocked", StringComparison.OrdinalIgnoreCase)) flagged++;
                    if (e.IsWatched) watched++;
                }
            }

            CountTextBlock.Text = $"Rows: {count}";
            StatsTextBlock.Text = $"Listening: {listening} | Established: {established} | Flagged: {flagged} | Watched: {watched}";
        }

        private void SetTimerInterval()
        {
            var content = (IntervalComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "5 sec";
            var value = content.Split(' ')[0];
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
            {
                seconds = 5;
            }

            _refreshTimer.Interval = TimeSpan.FromSeconds(seconds);
        }

        private void UpdateTimerState()
        {
            if (AutoRefreshCheckBox.IsChecked == true)
            {
                _refreshTimer.Start();
            }
            else
            {
                _refreshTimer.Stop();
            }
        }

        private void LoadPreferences()
        {
            try
            {
                var preferences = _preferencesService.Load(PreferencesFilePath);

                _isDarkTheme = preferences.UseDarkTheme;
                _preferencesService.ApplySet(_trustedPorts, preferences.TrustedPorts);
                _preferencesService.ApplySet(_blockedPorts, preferences.BlockedPorts);
                _preferencesService.ApplySet(_watchedPorts, preferences.WatchedPorts);
                _preferencesService.ApplySet(_trustedProcesses, preferences.TrustedProcesses);
                _preferencesService.ApplySet(_blockedProcesses, preferences.BlockedProcesses);
                _preferencesService.ApplySet(_watchedProcesses, preferences.WatchedProcesses);
            }
            catch (InvalidOperationException ex)
            {
                AlertTextBlock.Text = $"Preferences could not be loaded: {ex.Message}";
            }
        }

        private void SavePreferences()
        {
            try
            {
                _preferencesService.Save(
                    PreferencesFilePath,
                    _isDarkTheme,
                    _trustedPorts,
                    _blockedPorts,
                    _watchedPorts,
                    _trustedProcesses,
                    _blockedProcesses,
                    _watchedProcesses);
            }
            catch (InvalidOperationException ex)
            {
                AlertTextBlock.Text = $"Preferences could not be saved: {ex.Message}";
            }
        }

        private void ApplyTheme(bool useDarkTheme)
        {
            _isDarkTheme = useDarkTheme;

            SetBrushColor("WindowBackgroundBrush", useDarkTheme ? "#0B1220" : "#F1F5F9");
            SetBrushColor("SurfaceBrush", useDarkTheme ? "#111827" : "#FFFFFF");
            SetBrushColor("SurfaceAltBrush", useDarkTheme ? "#1F2937" : "#F8FAFC");
            SetBrushColor("PrimaryTextBrush", useDarkTheme ? "#E5E7EB" : "#0F172A");
            SetBrushColor("SecondaryTextBrush", useDarkTheme ? "#94A3B8" : "#64748B");
            SetBrushColor("SoftBorderBrush", useDarkTheme ? "#334155" : "#E2E8F0");
            SetBrushColor("StatusBarBrush", useDarkTheme ? "#111827" : "#FFFFFF");
            ThemeButton.Content = useDarkTheme ? "Switch to Light" : "Switch to Dark";
        }

        private void SetBrushColor(string key, string colorCode)
        {
            if (ColorConverter.ConvertFromString(colorCode) is not Color color)
            {
                return;
            }

            if (TryFindResource(key) is SolidColorBrush brush)
            {
                if (brush.IsFrozen)
                {
                    Resources[key] = new SolidColorBrush(color);
                }
                else
                {
                    brush.Color = color;
                }

                return;
            }

            Resources[key] = new SolidColorBrush(color);
        }

        private void ToggleSelectedRule(RuleMode mode)
        {
            if (PortsDataGrid.SelectedItem is not PortEntry entry)
            {
                MessageBox.Show("Select a connection first.", "Rules", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var target = GetSelectedComboValue(RuleTargetComboBox);
            string message;

            if (string.Equals(target, "Process", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(entry.ProcessName) || string.Equals(entry.ProcessName, "N/A", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("A valid process name is required for a process rule.", "Rules", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                message = _ruleService.ToggleProcessRule(entry.ProcessName, mode, _watchedProcesses, _trustedProcesses, _blockedProcesses);
            }
            else
            {
                if (entry.PortNumber <= 0)
                {
                    MessageBox.Show("A valid port is required for a port rule.", "Rules", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                message = _ruleService.TogglePortRule(entry.PortNumber, mode, _watchedPorts, _trustedPorts, _blockedPorts);
            }

            SavePreferences();
            RefreshPortData();
            AlertTextBlock.Text = message;
        }

        private async void UpdateDetailsPanel()
        {
            if (PortsDataGrid.SelectedItem is not PortEntry entry)
            {
                DetailsServiceTextBlock.Text = "Select a connection";
                DetailsProcessTextBlock.Text = string.Empty;
                DetailsPortDescriptionTextBlock.Text = string.Empty;
                DetailsProcessDescriptionTextBlock.Text = string.Empty;
                DetailsProcessPathTextBlock.Text = string.Empty;
                DetailsProtocolTextBlock.Text = string.Empty;
                DetailsLocalTextBlock.Text = string.Empty;
                DetailsRemoteTextBlock.Text = string.Empty;
                DetailsRemoteHostTextBlock.Text = string.Empty;
                DetailsRuleTextBlock.Text = string.Empty;
                DetailsObservedTextBlock.Text = string.Empty;
                DetailsSnapshotTextBlock.Text = string.Empty;
                return;
            }

            DetailsServiceTextBlock.Text = $"{entry.ServiceName} ({entry.PortNumber})";
            DetailsProcessTextBlock.Text = $"{entry.ProcessName} (PID {entry.ProcessId})";
            DetailsPortDescriptionTextBlock.Text = entry.PortDescription;
            DetailsProcessDescriptionTextBlock.Text = entry.ProcessDescription;
            DetailsProcessPathTextBlock.Text = string.IsNullOrWhiteSpace(entry.ProcessPath) ? "Unavailable" : entry.ProcessPath;
            DetailsProtocolTextBlock.Text = $"{entry.Protocol} / {entry.AddressFamily} / {entry.State}";
            DetailsLocalTextBlock.Text = entry.LocalAddress;
            DetailsRemoteTextBlock.Text = entry.RemoteAddress;
            DetailsRuleTextBlock.Text = $"{entry.RuleStatus} | {entry.SuspicionReason}";
            DetailsObservedTextBlock.Text = $"First seen: {entry.FirstSeen:yyyy-MM-dd HH:mm:ss}\nLast change: {entry.LastActivity:yyyy-MM-dd HH:mm:ss}\nObserved duration: {entry.ObservedDuration}";
            DetailsSnapshotTextBlock.Text = _baselineEntries is null
                ? "No baseline set"
                : _baselineEntries.ContainsKey(_formatHelpers.CreateConnectionIdentity(entry)) ? "Present in baseline" : "Not in baseline";

            if (!_networkService.CanResolveHost(entry.RemoteAddressRaw))
            {
                DetailsRemoteHostTextBlock.Text = "Not applicable";
                return;
            }

            if (_remoteHostNames.TryGetValue(entry.RemoteAddressRaw, out var cachedHost))
            {
                DetailsRemoteHostTextBlock.Text = cachedHost;
                return;
            }

            var version = ++_detailsRequestVersion;
            DetailsRemoteHostTextBlock.Text = "Resolving...";
            var resolvedHost = await Task.Run(() => _networkService.ResolveHostName(entry.RemoteAddressRaw)).ConfigureAwait(true);
            _remoteHostNames[entry.RemoteAddressRaw] = resolvedHost;
            if (version == _detailsRequestVersion && ReferenceEquals(PortsDataGrid.SelectedItem, entry))
            {
                DetailsRemoteHostTextBlock.Text = resolvedHost;
            }
        }

        private void RefreshHistoryView()
        {
            _historyView.Clear();

            if (PortsDataGrid.SelectedItem is PortEntry selected)
            {
                var identity = _formatHelpers.CreateConnectionIdentity(selected);
                var count = 0;
                for (var i = _history.Count - 1; i >= 0 && count < 40; i--)
                {
                    if (string.Equals(_history[i].ConnectionId, identity, StringComparison.OrdinalIgnoreCase))
                    {
                        _historyView.Add(_history[i]);
                        count++;
                    }
                }

                if (count > 0)
                {
                    HistoryHeaderTextBlock.Text = "Connection History";
                    return;
                }

                HistoryHeaderTextBlock.Text = "Recent Activity";
            }
            else
            {
                HistoryHeaderTextBlock.Text = "Recent Activity";
            }

            var take = Math.Min(40, _history.Count);
            for (var i = _history.Count - 1; i >= _history.Count - take; i--)
            {
                _historyView.Add(_history[i]);
            }
        }

        private void SetAdminStatus()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                _isRunningAsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (SecurityException)
            {
                _isRunningAsAdmin = false;
            }
            catch (UnauthorizedAccessException)
            {
                _isRunningAsAdmin = false;
            }

            if (_isRunningAsAdmin)
            {
                AdminStatusTextBlock.Text = "Running with administrator privileges.";
                RequestAdminButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                AdminStatusTextBlock.Text = "Running without administrator privileges. Some process details may be unavailable.";
                RequestAdminButton.Visibility = Visibility.Visible;
            }
        }

        private void RequestAdminButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunningAsAdmin)
            {
                return;
            }

            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                MessageBox.Show("Unable to determine the application path.", "Run as administrator", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true,
                    Verb = "runas"
                });

                Close();
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                AlertTextBlock.Text = "Administrator permission request was canceled.";
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show($"Unable to restart as administrator (error {ex.NativeErrorCode}): {ex.Message}", "Run as administrator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Unable to restart as administrator: {ex.Message}", "Run as administrator", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"ports-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _exportService.WriteCsv(GetVisibleEntries(), saveDialog.FileName);
                AlertTextBlock.Text = $"Exported CSV: {saveDialog.FileName}";
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Unable to export CSV: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportJsonButton_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"ports-{DateTime.Now:yyyyMMdd-HHmmss}.json"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _exportService.WriteJson(GetVisibleEntries(), saveDialog.FileName);
                AlertTextBlock.Text = $"Exported JSON: {saveDialog.FileName}";
            }
            catch (IOException ex)
            {
                MessageBox.Show($"Unable to export JSON: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TerminateProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (PortsDataGrid.SelectedItem is not PortEntry entry || entry.ProcessId <= 0)
            {
                MessageBox.Show("Select a row with a valid process.", "End Process", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"End process '{entry.ProcessName}' (PID {entry.ProcessId})?",
                "Confirm process termination",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using var process = Process.GetProcessById(entry.ProcessId);
                process.Kill(true);
                AlertTextBlock.Text = $"Process {entry.ProcessName} (PID {entry.ProcessId}) terminated.";
                RefreshPortData();
            }
            catch (ArgumentException)
            {
                MessageBox.Show($"Process with PID {entry.ProcessId} is no longer running.", "End Process", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshPortData();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Unable to terminate process: {ex.Message}", "End Process", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show($"Unable to terminate process (access denied or insufficient privileges): {ex.Message}", "End Process", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<PortEntry> GetVisibleEntries()
        {
            if (_portsView is null)
            {
                return [.. _ports];
            }

            return [.. _portsView.Cast<PortEntry>()];
        }

        private string PreferencesFilePath => _preferencesService.GetDatabasePath();

        private void TrackHistory(IReadOnlyDictionary<string, PortEntry> currentEntriesByIdentity, DateTime timestamp)
        {
            foreach (var opened in currentEntriesByIdentity.Where(kv => !_previousEntriesByIdentity.ContainsKey(kv.Key)).Select(kv => kv.Value))
            {
                AddHistoryEvent("OPENED", opened, $"{opened.Protocol}/{opened.AddressFamily} port {opened.PortNumber} by {opened.ProcessName} (PID {opened.ProcessId})", timestamp);
                if (opened.IsSuspicious || opened.IsWatched || string.Equals(opened.RuleStatus, "Blocked", StringComparison.OrdinalIgnoreCase))
                {
                    AddHistoryEvent("ALERT", opened, $"{opened.PortNumber} flagged as {opened.RuleStatus}: {opened.SuspicionReason}", timestamp);
                }
            }

            foreach (var closed in _previousEntriesByIdentity.Where(kv => !currentEntriesByIdentity.ContainsKey(kv.Key)).Select(kv => kv.Value))
            {
                AddHistoryEvent("CLOSED", closed, $"{closed.Protocol}/{closed.AddressFamily} port {closed.PortNumber} from {closed.ProcessName} (PID {closed.ProcessId})", timestamp);
            }

            foreach (var current in currentEntriesByIdentity)
            {
                if (_previousEntriesByIdentity.TryGetValue(current.Key, out var previous)
                    && !string.Equals(previous.State, current.Value.State, StringComparison.OrdinalIgnoreCase))
                {
                    AddHistoryEvent("STATE", current.Value, $"{current.Value.Protocol}/{current.Value.AddressFamily} port {current.Value.PortNumber} state {previous.State} -> {current.Value.State}", timestamp);
                }
            }
        }

        private void AddHistoryEvent(string eventType, PortEntry entry, string details, DateTime timestamp)
        {
            _history.Add(new PortHistoryEntry
            {
                Timestamp = timestamp,
                EventType = eventType,
                ConnectionId = _formatHelpers.CreateConnectionIdentity(entry),
                Details = details
            });

            if (_history.Count > MaxHistoryEntries)
            {
                _history.RemoveRange(0, _history.Count - MaxHistoryEntries);
            }
        }

        private readonly record struct FilterCriteria(
            string Search,
            string Port,
            string Address,
            string Process,
            string Pid,
            string Protocol,
            string Family,
            string State,
            string Rule,
            bool SuspiciousOnly,
            bool WatchlistOnly)
        {
            public static FilterCriteria Default { get; } = new(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "All",
                "All",
                "All",
                "All",
                false,
                false);
        }
    }
}
