using SIAT.TSET;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SIAT
{
    public partial class TestInterfaceWindow : Window, INotifyPropertyChanged
    {
      
        private DispatcherTimer? _testTimer;
        private DispatcherTimer? _clockTimer;
        private DispatcherTimer? _durationTimer;
        private Stopwatch? _testStopwatch;
        private bool _isTesting = false;

        // 统计信息
        private TestStatistics? _statistics;
        private string? _statisticsFilePath;

        private TimeSpan _totalTestDuration = TimeSpan.Zero;
        private StringBuilder _logBuilder = new StringBuilder();
        private string _currentBarcode = "";

        // 单次测试的总时间
        private TimeSpan _currentTestTotalDuration = TimeSpan.Zero;
        private Stopwatch _currentTestStopwatch = new Stopwatch();

        // 测试进度相关
        private double _testProgress = 0.0;
        private int _totalTestSteps = 0;
        private int _completedTestSteps = 0;

        // 编辑模式标志
        private bool _isEditMode = false;

        // 全屏相关字段
        private bool _isFullscreen = false;
        private WindowState _previousWindowState = WindowState.Normal;
        private WindowStyle _previousWindowStyle = WindowStyle.None;
        private bool _previousAllowsTransparency = true;
        private double _previousLeft = 0;
        private double _previousTop = 0;
        private double _previousWidth = 0;
        private double _previousHeight = 0;

        // 统计信息保存定时器
        private DispatcherTimer? _autoSaveTimer;
        
        // 测试设置相关
        private TestSettings _testSettings;
        private BarCodeScanningGun? _barcodeScanner;
        private CommunicationManagement.ICommunication? _toolingCanCommunication;
        private DispatcherTimer? _barcodeCheckTimer;
        private string _barcodeBuffer = string.Empty;
        
        // 治具卡串口相关
        private System.IO.Ports.SerialPort? _jigSerialPort;
        private bool _isJigPortConnected = false;

        // 用例文件相关
        private TestCaseConfig? _currentTestCase;
        private string _currentTestCaseFilePath = string.Empty; // 保存当前测试用例文件路径
        private List<TestProjectConfig> _loadedProjects = new List<TestProjectConfig>();

        // 测试步骤集合
        private ObservableCollection<TestStepConfig> _testSteps = new ObservableCollection<TestStepConfig>();

        // 测试项目集合（用于分组显示）
        private TestProjectCollection _testProjects = new TestProjectCollection();


        public int TotalTestCount
        {
            get => _statistics?.TotalTestCount ?? 0;
            set
            {
                if (_statistics != null)
                {
                    _statistics.TotalTestCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PassRate));
                    // 值变化时自动保存
                    SaveStatistics();
                }
            }
        }

        public int TotalPassedCount
        {
            get => _statistics?.TotalPassedCount ?? 0;
            set
            {
                if (_statistics != null)
                {
                    _statistics.TotalPassedCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PassRate));
                    // 值变化时自动保存
                    SaveStatistics();
                }
            }
        }

        public int TotalFailedCount
        {
            get => _statistics?.TotalFailedCount ?? 0;
            set
            {
                if (_statistics != null)
                {
                    _statistics.TotalFailedCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PassRate));
                    // 值变化时自动保存
                    SaveStatistics();
                }
            }
        }

        public double PassRate
        {
            get => _statistics?.CalculatePassRate() ?? 0.0;
        }

        // 测试进度属性
        public double TestProgress
        {
            get => _testProgress;
            set
            {
                _testProgress = value;
                OnPropertyChanged();
                UpdateProgressBar();
            }
        }

        public TimeSpan CurrentTestTotalDuration
        {
            get => _currentTestTotalDuration;
            set
            {
                _currentTestTotalDuration = value;
                OnPropertyChanged();
            }
        }

        // 测试步骤集合属性（用于数据绑定）
        public ObservableCollection<TestStepConfig> TestSteps
        {
            get => _testSteps;
            set
            {
                _testSteps = value;
                OnPropertyChanged();
               
            }
        }

        // 测试项目集合属性（用于分组显示）
        public TestProjectCollection TestProjects
        {
            get => _testProjects;
            set
            {
                _testProjects = value;
                OnPropertyChanged();
               
            }
        }

        public TestInterfaceWindow()
        {
            InitializeComponent();
            DataContext = this;

            // 初始化测试设置
            _testSettings = TestSettings.Instance;
            
            InitializeStatistics();
            InitializeUI();
            UpdateStartupModeLogic();
            this.MouseDown += Window_MouseDown;
            this.SizeChanged += Window_SizeChanged;
            this.Loaded += Window_Loaded;
        }

        public TestInterfaceWindow(string username, string userRole)
        {
            InitializeComponent();
            DataContext = this;
            
            // 初始化测试设置
            _testSettings = TestSettings.Instance;
            
            if (CurrentUserText != null)
                CurrentUserText.Text = $"{username} ({userRole})";

            InitializeStatistics();
            UpdateStatisticsDisplay();
            InitializeUI();
            UpdateStartupModeLogic();
            this.MouseDown += Window_MouseDown;
        }

        private void InitializeStatistics()
        {
            try
            {
                // 使用AppData目录保存统计信息，确保用户有写入权限
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string appFolder = Path.Combine(appDataPath, "SIAT_TestSystem");
                _statisticsFilePath = Path.Combine(appFolder, "TestStatistics.xml");

                _statistics = TestStatistics.LoadFromFile(_statisticsFilePath);

                // 更新界面显示
                OnPropertyChanged(nameof(TotalTestCount));
                OnPropertyChanged(nameof(TotalPassedCount));
                OnPropertyChanged(nameof(TotalFailedCount));
                OnPropertyChanged(nameof(PassRate));

                AddLog($"统计信息已加载，总测试次数: {TotalTestCount}", "系统");
            }
            catch (Exception ex)
            {
                AddLog($"初始化统计信息失败: {ex.Message}", "错误");
                _statistics = new TestStatistics();
            }
        }

        private void SaveStatistics()
        {
            if (_statistics != null && !string.IsNullOrEmpty(_statisticsFilePath))
            {
                _statistics.SaveToFile(_statisticsFilePath);
                System.Diagnostics.Debug.WriteLine($"统计信息已保存: 总测试={TotalTestCount}, 通过={TotalPassedCount}, 失败={TotalFailedCount}");
            }
        }

        private void InitializeUI()
        {
            // 初始化定时器
            _testTimer = new DispatcherTimer();
            _clockTimer = new DispatcherTimer();
            _durationTimer = new DispatcherTimer();
            _testStopwatch = new Stopwatch();

            // 自动保存定时器
            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMinutes(5); // 每5分钟自动保存一次
            _autoSaveTimer.Tick += (s, e) => SaveStatistics();
            _autoSaveTimer.Start();

            InitializeTimers();

            // 不加载默认测试项目，测试开始前清空显示
            ClearTestItems();

            // 更新通过率显示
            if (PassRateText != null)
                PassRateText.Text = $"{PassRate:F2}%";

            AddLog("测试系统已启动", "系统");
            AddLog("请点击启动按钮开始测试", "提示");

            // 添加双击编辑功能
            InitializeDoubleClickEditing();

            // 显示统计信息状态
            DisplayStatisticsStatus();

            // 初始化用例文件显示
            UpdateTestCaseDisplay(null);

            // 初始化进度条
            InitializeProgressBar();
        }

        private void InitializeProgressBar()
        {
            // 初始化进度条
            TestProgress = 0.0;
            _totalTestSteps = 0;
            _completedTestSteps = 0;
        }

        private void DisplayStatisticsStatus()
        {
            try
            {
                if (!string.IsNullOrEmpty(_statisticsFilePath) && File.Exists(_statisticsFilePath))
        {
            FileInfo fileInfo = new FileInfo(_statisticsFilePath);
            AddLog($"统计文件: {fileInfo.Name} ({fileInfo.Length} bytes, 最后修改: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm})", "系统");
        }
            else
            {
                AddLog("未找到现有统计文件，将创建新文件", "系统");
            }
            }
            catch
            {
                // 忽略文件状态显示错误
            }
        }

        private void InitializeDoubleClickEditing()
        {
            if (TotalTestsBorder != null)
            {
                TotalTestsBorder.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount == 2 && !_isEditMode)
                    {
                        ModifyStatsButton_Click(null, null);
                    }
                };
            }

            if (TotalPassedBorder != null)
            {
                TotalPassedBorder.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount == 2 && !_isEditMode)
                    {
                        ModifyStatsButton_Click(null, null);
                        if (TotalPassedEditBox != null)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                TotalPassedEditBox.Focus();
                                TotalPassedEditBox.SelectAll();
                            }), DispatcherPriority.Render);
                        }
                    }
                };
            }

            if (TotalFailedBorder != null)
            {
                TotalFailedBorder.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount == 2 && !_isEditMode)
                    {
                        ModifyStatsButton_Click(null, null);
                        if (TotalFailedEditBox != null)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                TotalFailedEditBox.Focus();
                                TotalFailedEditBox.SelectAll();
                            }), DispatcherPriority.Render);
                        }
                    }
                };
            }
        }

        private void InitializeTimers()
        {
            // 初始化定时器
            if (_clockTimer != null)
            {
                _clockTimer.Interval = TimeSpan.FromSeconds(1);
                _clockTimer.Tick += ClockTimer_Tick;
                _clockTimer.Start();
            }

            // 测试定时器 - 每个项目测试间隔
            if (_testTimer != null)
            {
                _testTimer.Interval = TimeSpan.FromSeconds(2); // 2秒测试一个项目
                _testTimer.Tick += TestTimer_Tick;
            }

            // 总耗时定时器
            if (_durationTimer != null)
            {
                _durationTimer.Interval = TimeSpan.FromSeconds(1);
                _durationTimer.Tick += DurationTimer_Tick;
            }
        }

        private void ClearTestItems()
        {
            // 清空测试步骤集合
            _testSteps.Clear();
           
        }

        

        private void UpdateStatisticsDisplay()
        {
            try
            {
                // 更新文本显示
                if (TotalTestsText != null && !_isEditMode)
                    TotalTestsText.Text = TotalTestCount.ToString();

                if (TotalPassedText != null && !_isEditMode)
                    TotalPassedText.Text = TotalPassedCount.ToString();

                if (TotalFailedText != null && !_isEditMode)
                    TotalFailedText.Text = TotalFailedCount.ToString();

                if (PassRateText != null)
                    PassRateText.Text = $"{PassRate:F2}%";

                // 更新绑定属性通知
                OnPropertyChanged(nameof(TotalTestCount));
                OnPropertyChanged(nameof(TotalPassedCount));
                OnPropertyChanged(nameof(TotalFailedCount));
                OnPropertyChanged(nameof(PassRate));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新统计显示失败: {ex.Message}");
            }
        }

        private void UpdateProgressBar()
        {
            try
            {
                if (ProgressBarFill != null && ProgressPercentageText != null)
                {
                    // 使用Dispatcher确保在UI线程执行
                    Dispatcher.InvokeAsync(() =>
                    {
                        // 确保进度值在有效范围内
                        double progress = Math.Max(0, Math.Min(_testProgress, 100));
                        
                        // 计算进度条宽度（基于容器宽度）
                        var progressBarContainer = ProgressBarFill.Parent as Border;
                        if (progressBarContainer != null && progressBarContainer.ActualWidth > 0)
                        {
                            double containerWidth = progressBarContainer.ActualWidth - 2; // 减去边框
                            double progressWidth = containerWidth * progress / 100.0;
                            ProgressBarFill.Width = Math.Max(0, Math.Min(progressWidth, containerWidth));
                        }
                        else
                        {
                            // 如果容器宽度为0，使用固定宽度计算
                            ProgressBarFill.Width = 200 * progress / 100.0;
                        }

                        // 更新百分比文本
                        ProgressPercentageText.Text = $"{progress:F1}%";

                        // 根据进度改变颜色
                        if (progress >= 100)
                        {
                            ProgressBarFill.Background = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // 绿色
                        }
                        else if (progress >= 70)
                        {
                            ProgressBarFill.Background = new SolidColorBrush(Color.FromRgb(23, 162, 184)); // 蓝色
                        }
                        else
                        {
                            ProgressBarFill.Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // 默认蓝色
                        }
                    }, DispatcherPriority.Render);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新进度条失败: {ex.Message}");
            }
        }

        private void ClockTimer_Tick(object? sender, EventArgs e)
        {
            if (CurrentTimeText != null)
                CurrentTimeText.Text = DateTime.Now.ToString("HH:mm:ss");

            // 更新本次测试耗时显示
            if (CurrentTestTimeText != null)
            {
                if (_isTesting)
                {
                    // 测试进行中，显示计时器时间
                    CurrentTestTimeText.Text = $"{_currentTestStopwatch.Elapsed.TotalSeconds:F3}s";
                }
                else
                {
                    // 测试已结束，显示所有项目耗时总和
                    CurrentTestTimeText.Text = $"{_totalTestDuration.TotalSeconds:F3}s";
                }
            }
        }

        private void DurationTimer_Tick(object? sender, EventArgs e)
        {
            if (_isTesting && _testStopwatch?.IsRunning == true)
            {
                // 测试进行中，更新计时器时间
                _totalTestDuration = _testStopwatch.Elapsed;
                CurrentTestTotalDuration = _currentTestStopwatch.Elapsed;

                // 状态栏信息
                if (StatusMessageText != null)
                    StatusMessageText.Text = $"测试进行中... 本次测试耗时: {CurrentTestTotalDuration.TotalSeconds:F3}s";
            }
        }

        private void TestTimer_Tick(object? sender, EventArgs e)
        {
            
        }

       
      
        private void AddLog(string message, string type = "信息")
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{type}] {message}";

                _logBuilder.AppendLine(logEntry);

                if (LogTextBlock != null)
                {
                    LogTextBlock.Text = LogTextBlock.Text + logEntry + "\n";

                    // 限制日志行数，保持界面整洁
                    var lines = LogTextBlock.Text.Split('\n');
                    if (lines.Length > 100)
                    {
                        LogTextBlock.Text = string.Join("\n", lines.Skip(lines.Length - 100));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"添加日志失败: {ex.Message}");
            }
        }

        private void SaveLogToFile()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentBarcode))
                {
                    _currentBarcode = "UNKNOWN";
                }

                // 创建日志目录
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestLogs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                // 生成文件名：条码_日期.txt
                string fileName = $"{_currentBarcode}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(logDir, fileName);

                // 写入日志文件
                File.WriteAllText(filePath, _logBuilder.ToString());
                
                // 记录保存成功
                AddLog($"测试日志已保存: {fileName}", "系统");

            }
            catch (Exception ex)
            {
                AddLog($"保存日志失败: {ex.Message}", "错误");
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 窗口大小变化时更新进度条
            UpdateProgressBar();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载完成后，延迟更新进度条以确保UI元素已完全加载
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateProgressBar();
            }), DispatcherPriority.ApplicationIdle);
        }

        private async void WindowCloseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_isTesting)
            {
                var result = MessageBox.Show("测试正在进行中，确定要关闭窗口吗？", "确认关闭",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            if (deviceCommunications.Count > 0)
            {
                AddLog("正在关闭设备连接...", "信息");
                await DisconnectAllDevicesAsync();
            }

            SaveStatistics();
            AddLog("应用程序关闭，统计信息已保存", "系统");

            this.Close();
        }

        private void WindowMinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            // 最小化窗口
            WindowState = WindowState.Minimized;
        }

        private void WindowMaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            // 切换窗口最大化状态
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                if (WindowMaximizeButton != null)
                    WindowMaximizeButton.Content = "🗖";
            }
            else
            {
                WindowState = WindowState.Normal;
                if (WindowMaximizeButton != null)
                    WindowMaximizeButton.Content = "🗗";
            }
        }

        private void TestSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var settings = TestSettings.Instance;
                var settingsWindow = new TestSettingsWindow(settings);
                if (settingsWindow.ShowDialog() == true)
                {
                    // 设置已保存，更新当前测试界面的启动方式逻辑
                    UpdateStartupModeLogic();
                }
            }
            catch (Exception ex)
            {
                AddLog($"打开测试设置失败: {ex.Message}", "错误");
            }
        }

        private void StartTestButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_isTesting)
            {
                MessageBox.Show("测试正在进行中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }


            // 检查是否已加载用例文件
            if (_currentTestCase == null)
            {
                MessageBox.Show("请先选择测试用例文件", "提示", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

           

            // 检查是否已扫描条码
            if (BarcodeText != null && (string.IsNullOrEmpty(BarcodeText.Text) || BarcodeText.Text == "请扫描条码..."))
            {
                var result = MessageBox.Show("未扫描产品条码，是否继续测试？", "确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
                _currentBarcode = "UNKNOWN";
            }
            else if (BarcodeText != null)
            {
                _currentBarcode = BarcodeText.Text;
            }

            StartTesting();
        }

        /// <summary>
        /// 添加测试项及其变量显示
        /// </summary>
        /// <param name="projectConfig">测试项目配置</param>
        /// <param name="isExpanded">是否展开显示</param>
        /// <param name="totalDuration">项目总耗时</param>
        private void AddTestItem(TestProjectConfig projectConfig, bool isExpanded = true, TimeSpan? totalDuration = null)
        {
            // 创建视图模型
            var projectViewModel = new TestProjectViewModel(projectConfig);
            projectViewModel.IsExpanded = isExpanded;
            
            // 使用存储的变量值更新项目变量
            foreach (var variable in projectViewModel.Variables)
            {
                // 使用项目名+变量名作为键查找存储的变量值
                string key = $"{projectConfig.Name}_{variable.Name}";
                if (_variableValues.TryGetValue(key, out var storedVariable))
                {
                    // 更新变量属性，使用测试过程中更新的值
                    UpdateVariableProperties(variable, storedVariable);
                    
                    // 评估变量结果
                    EvaluateAndUpdateVariableStatus(variable);
                }
                else
                {
                    // 如果没有存储的变量值，使用变量的初始值
                    if (!string.IsNullOrEmpty(variable.Value))
                    {
                        variable.ActualValue = variable.Value;
                        variable.Status = TestStepStatus.Passed;
                    }
                }
            }
            
            // 添加到集合
            _testProjects.Add(projectViewModel);
            
            // 设置项目总耗时（在添加到集合后设置，确保PropertyChanged事件能被UI捕获）
            if (totalDuration.HasValue)
            {
                projectViewModel.TotalDuration = totalDuration.Value;
            }
            
            // 更新UI
            OnPropertyChanged(nameof(TestProjects));
        }

     

        /// <summary>
        /// 初始化测试状态
        /// </summary>
        private void InitializeTestState()
        {
            // 开始测试，清除所有显示
            _isTesting = true;
            _testStopwatch?.Start();
            _currentTestStopwatch.Restart();
            _testTimer?.Start();
            _durationTimer?.Start();
            
            // 清除所有显示
            _testProjects.Clear();
            OnPropertyChanged(nameof(TestProjects));
            
            // 清空变量值存储字典，确保每次测试都是全新的状态
            _variableValues.Clear();

            // 重置测试进度
            InitializeProgressBar();
            _totalTestSteps = _loadedProjects.Sum(p => p.Steps.Count);

            // 重置所有项目和步骤的状态
            foreach (var projectConfig in _loadedProjects)
            {
                // 重置步骤状态
                foreach (var step in projectConfig.Steps)
                {
                    step.Status = TestStepStatus.Pending;
                    step.ActualValue = string.Empty;
                    step.Duration = TimeSpan.Zero;
                }
                
                // 重置变量状态
                foreach (var variable in projectConfig.Variables)
                {
                    variable.Status = TestStepStatus.Pending;
                    variable.ActualValue = string.Empty;
                    variable.TestTime = string.Empty;
                    variable.Duration = TimeSpan.Zero;
                }
            }

            // 更新UI状态
            if (TestStatusIndicator != null)
                TestStatusIndicator.Background = Brushes.Green;

            if (TestStatusText != null)
                TestStatusText.Text = "测试中";

            if (StartTestButton != null)
                StartTestButton.IsEnabled = false;

            if (StopTestButton != null)
                StopTestButton.IsEnabled = true;

            if (StatusMessageText != null)
                StatusMessageText.Text = "测试进行中...";
        }

        private void StartTesting()
        {
            try
            {
                // 初始化测试状态
                InitializeTestState();

                // 记录测试开始
                AddLog($"开始测试用例: {_currentTestCase?.Name}", "开始");
                AddLog($"产品条码: {_currentBarcode}", "信息");
                AddLog($"包含 {_testProjects.Count} 个项目", "信息");

                // 测试开始时停止工装测试流程
                _isToolingTestFlowEnabled = false;
                AddLog("测试开始，停止工装测试流程", "工装");

                // 启动测试执行任务
                Task.Run(() => ExecuteTestCase());
            }
            catch (Exception ex)
            {
                AddLog($"启动测试失败: {ex.Message}", "错误");
                StopTesting();
            }
        }

        private async Task ExecuteTestCase()
        {
            try
            {
                if (_currentTestCase == null || _loadedProjects.Count == 0)
                {
                    throw new Exception("未加载有效的测试用例");
                }

                int totalSteps = _loadedProjects.Sum(p => p.Steps.Count);
                int currentStep = 0;
                int passedSteps = 0;
                int failedSteps = 0;
                var stepResults = new List<TestStepResult>();
                var testStartTime = DateTime.Now;



                // 执行每个项目的测试步骤
                foreach (var projectConfig in _loadedProjects.OrderBy(p => p.Order))
                {
                    // 记录项目开始时间
                    var projectStartTime = DateTime.Now;
                
                    // 执行项目中的每个步骤
                    foreach (var step in projectConfig.Steps.OrderBy(s => s.Order))
                    {
                        currentStep++;
                        var stepResult = await ExecuteTestStep(step, currentStep, totalSteps);
                        stepResults.Add(stepResult);

                        // 统计步骤结果
                        if (stepResult.IsSuccess)
                        {
                            passedSteps++;
                        }
                        else
                        {
                            failedSteps++;
                        }

                        // 更新测试进度
                        _completedTestSteps++;
                        if (_totalTestSteps > 0)
                        {
                            TestProgress = (_completedTestSteps / (double)_totalTestSteps) * 100.0;
                        }
                        else
                        {
                            TestProgress = 0.0;
                        }

                        // 检查是否被停止
                        if (!_isTesting)
                        {
                            AddLog("测试被手动停止", "停止");
                        
                            // 计算所有已执行项目耗时的总和
                            TimeSpan partialDurationFromProjects = TimeSpan.Zero;
                            await Dispatcher.InvokeAsync(() =>
                            {
                                foreach (var project in _testProjects)
                                {
                                    partialDurationFromProjects += project.TotalDuration;
                                }
                            });
                            
                            // 生成部分测试报告，传递计算好的总耗时
                            await GenerateTestReport(stepResults, passedSteps, failedSteps, false, partialDurationFromProjects);
                            return;
                        }
                    }

                    // 计算项目总耗时
                    var projectDuration = DateTime.Now - projectStartTime;
                
                    // 在项目执行完成后，将该项目添加到界面
                    await Dispatcher.InvokeAsync(() =>
                    {
                        // 添加项目到界面，并传递项目总耗时
                        AddTestItem(projectConfig, true, projectDuration);
                    });

                    AddLog($"项目 {projectConfig.Name} 执行完成并显示到界面", "完成");
                }

                // 测试完成
                await Dispatcher.InvokeAsync(() =>
                {
                    // 处理测试结果，更新所有变量的状态和属性
                    ProcessTestResults();
                    
                    // 检查所有变量的状态，只要有一个变量失败，整个测试就失败
                    bool hasFailedVariables = false;
                    foreach (var project in _testProjects)
                    {
                        if (project.Variables.Any(v => v.Status == TestStepStatus.Failed))
                        {
                            hasFailedVariables = true;
                            break;
                        }
                    }
                    
                    // 测试通过条件：没有失败的步骤，也没有失败的变量
                    bool testPassed = failedSteps == 0 && !hasFailedVariables;
                    string resultText = testPassed ? "通过" : "失败";
                    
                    // 计算所有项目耗时的总和，并设置为总耗时
                    TimeSpan totalDurationFromProjects = TimeSpan.Zero;
                    foreach (var project in _testProjects)
                    {
                        totalDurationFromProjects += project.TotalDuration;
                    }
                    
                    // 更新总耗时为所有项目耗时的总和
                    _totalTestDuration = totalDurationFromProjects;
                    CurrentTestTotalDuration = totalDurationFromProjects;
                    
                    AddLog($"测试用例执行完成 - 结果: {resultText}", testPassed ? "完成" : "失败");
                    AddLog($"测试总耗时: {_totalTestDuration.TotalSeconds:F3}s (所有项目耗时总和)", "统计");
                    
                    if (StatusMessageText != null)
                        StatusMessageText.Text = $"测试完成 - {resultText}";

                    // 更新统计信息
                    TotalTestCount++;
                    if (testPassed)
                    {
                        TotalPassedCount++;
                    }
                    else
                    {
                        TotalFailedCount++;
                    }

                    // 记录详细统计
                    AddLog($"测试统计: 总步骤={totalSteps}, 通过={passedSteps}, 失败={failedSteps}", "统计");

                    // 生成完整测试报告，传递计算好的总耗时
                    Task.Run(() => GenerateTestReport(stepResults, passedSteps, failedSteps, testPassed, _totalTestDuration));

                    // 停止测试
                    StopTesting();
                    
                    // 显示测试结果弹窗
                    TestResultDialog resultDialog = new TestResultDialog(testPassed);
                    resultDialog.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"测试执行失败: {ex.Message}", "错误");
                    StopTesting();
                });
            }
        }

        private async Task<TestStepResult> ExecuteTestStep(TestStepConfig step, int currentStep, int totalSteps)
        {
            try
            {
                // 在_testProjects中查找对应的步骤引用
                TestStepConfig stepInProjects = null;
                await Dispatcher.InvokeAsync(() =>
                {
                    // 遍历所有项目，找到对应的步骤
                    foreach (var project in _testProjects)
                    {
                        stepInProjects = project.Steps.FirstOrDefault(s => s.Name == step.Name && s.Order == step.Order);
                        if (stepInProjects != null)
                            break;
                    }
                });

                // 更新步骤状态
                await Dispatcher.InvokeAsync(() =>
                {
                    if (StatusMessageText != null)
                        StatusMessageText.Text = $"执行步骤 {currentStep}/{totalSteps}: {step.Name}";

                    AddLog($"执行步骤: {step.Name}", "步骤");
                    
                    // 更新步骤状态为运行中
                    if (stepInProjects != null)
                    {
                        stepInProjects.Status = TestStepStatus.Running;
                    }
                    step.Status = TestStepStatus.Running;
                });

                // 创建测试执行引擎实例，并传递设备通讯实例
                var executionEngine = new TestExecutionEngine(deviceCommunications);
                
                // 注册进度事件
                executionEngine.StepProgress += (sender, args) =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AddLog($"步骤进度: {step.Name} - {args.Message} ({args.Progress}%)", "进度");
                    });
                };

                // 注册变量更新事件
                executionEngine.VariableUpdated += (sender, args) =>
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        UpdateVariableDisplay(args.Variable);
                    });
                };

                // 准备项目变量的Value属性值
                var variables = new Dictionary<string, object>();
                // 添加条码信息到变量字典，以便插件步骤使用
                variables["Barcode"] = _currentBarcode;
               
                
                // 执行测试步骤，传递项目变量的Value属性值
                var result = await executionEngine.ExecuteStepAsync(step, variables);
                
                return result;

            }
            catch (TimeoutException ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // 更新原始步骤状态
                    step.Status = TestStepStatus.Failed;
                    step.ActualValue = $"超时: {ex.Message}";

                    // 在_testProjects中查找对应的步骤引用并更新状态
                    foreach (var project in _testProjects)
                    {
                        var stepInProjects = project.Steps.FirstOrDefault(s => s.Name == step.Name && s.Order == step.Order);
                        if (stepInProjects != null)
                        {
                            stepInProjects.Status = TestStepStatus.Failed;
                            stepInProjects.ActualValue = $"超时: {ex.Message}";
                            break;
                        }
                    }

                    AddLog($"步骤执行超时: {step.Name} - {ex.Message}", "错误");
                    
                    // 显示超时弹窗
                    MessageBox.Show(ex.Message, "设备超时", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    // 停止测试
                    StopTesting();
                });

                // 返回失败的测试结果
                return new TestStepResult
                {
                    StepName = step.Name,
                    IsSuccess = false,
                    ActualValue = $"超时: {ex.Message}",
                    Duration = TimeSpan.Zero,
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // 更新原始步骤状态
                    step.Status = TestStepStatus.Failed;
                    step.ActualValue = $"异常: {ex.Message}";

                    // 在_testProjects中查找对应的步骤引用并更新状态
                    foreach (var project in _testProjects)
                    {
                        var stepInProjects = project.Steps.FirstOrDefault(s => s.Name == step.Name && s.Order == step.Order);
                        if (stepInProjects != null)
                        {
                            stepInProjects.Status = TestStepStatus.Failed;
                            stepInProjects.ActualValue = $"异常: {ex.Message}";
                            break;
                        }
                    }

                    AddLog($"步骤执行异常: {step.Name} - {ex.Message}", "错误");
                    
                    // 显示异常弹窗
                    MessageBox.Show($"执行步骤 {step.Name} 时发生错误: {ex.Message}", "测试错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    // 停止测试
                    StopTesting();
                });

                // 返回失败的测试结果
                return new TestStepResult
                {
                    StepName = step.Name,
                    IsSuccess = false,
                    ActualValue = $"异常: {ex.Message}",
                    Duration = TimeSpan.Zero,
                    ErrorMessage = ex.Message
                };
            }
        }

        private void StopTestButton_Click(object? sender, RoutedEventArgs e)
        {
            StopTesting();
            AddLog("测试已手动停止", "停止");
        }

        private void StopTesting()
        {
            if (!_isTesting) return;

            try
            {
                _isTesting = false;

                _testStopwatch?.Stop();
                _currentTestStopwatch.Stop();
                _testTimer?.Stop();
                _durationTimer?.Stop();

                AddLog("测试已停止", "停止");

                // 计算所有项目耗时的总和，并更新总耗时
                TimeSpan totalDurationFromProjects = TimeSpan.Zero;
                foreach (var project in _testProjects)
                {
                    totalDurationFromProjects += project.TotalDuration;
                }
                
                // 更新总耗时为所有项目耗时的总和
                _totalTestDuration = totalDurationFromProjects;
                CurrentTestTotalDuration = totalDurationFromProjects;
                
                // 更新界面上的本次测试耗时显示
                if (CurrentTestTimeText != null)
                {
                    CurrentTestTimeText.Text = $"{_totalTestDuration.TotalSeconds:F3}s";
                }
                
                // 更新状态栏信息
                if (StatusMessageText != null)
                {
                    StatusMessageText.Text = $"测试已停止 - 总耗时: {_totalTestDuration.TotalSeconds:F3}s";
                }

                // 更新UI状态，不修改测试数据
                if (TestStatusIndicator != null)
                    TestStatusIndicator.Background = Brushes.Gray;

                if (TestStatusText != null)
                    TestStatusText.Text = "就绪";

                if (StartTestButton != null)
                    StartTestButton.IsEnabled = true;

                if (StopTestButton != null)
                    StopTestButton.IsEnabled = false;

                // 测试完成后发送抬起指令（只在状态机处于Testing状态时发送）
                if (_currentToolingState == ToolingState.Testing)
                {
                    SendPressUpCommand();
                }
                
                // 重新启用工装测试流程
                _isToolingTestFlowEnabled = true;
                AddLog("测试完成，重新启用工装测试流程", "工装");

                SaveLogToFile();
                UpdateStatisticsDisplay();
            }
            catch (Exception ex)
            {
                AddLog($"停止测试失败: {ex.Message}", "错误");
            }
        }

        private void SimulateScanButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                string[] productTypes = { "型号A-100", "型号B-200", "型号C-300", "型号D-400" };
                string[] batchNumbers = { "2023-10-001", "2023-10-002", "2023-11-001", "2023-11-002" };

                Random random = new Random();
                string barcode = $"SN{random.Next(100000, 999999)}";
                string product = productTypes[random.Next(productTypes.Length)];
                string batch = batchNumbers[random.Next(batchNumbers.Length)];

                if (BarcodeText != null)
                    BarcodeText.Text = barcode;

                if (ProductNameText != null)
                    ProductNameText.Text = product;

                if (BatchNumberText != null)
                    BatchNumberText.Text = batch;

                
            }
            catch (Exception ex)
            {
                AddLog($"模拟扫描失败: {ex.Message}", "错误");
            }
        }



        // 选择用例文件按钮点击事件
        private void SelectTestCaseButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = TestCaseManager.GetTestCaseFilter(),
                    Title = "选择测试用例文件",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    LoadTestCaseFile(openFileDialog.FileName);
                }
            }
            catch (Exception ex)
            {
                AddLog($"选择用例文件失败: {ex.Message}", "错误");
                MessageBox.Show($"选择用例文件失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 用于存储设备通讯实例
        private Dictionary<string, CommunicationManagement.ICommunication> deviceCommunications = new Dictionary<string, CommunicationManagement.ICommunication>();



        // 加载用例文件
        private async void LoadTestCaseFile(string filePath)
        {
            // 保存当前状态，以便在加载失败时恢复
            var originalTestCase = _currentTestCase;
            var originalLoadedProjects = new List<TestProjectConfig>(_loadedProjects);
            var originalTestProjects = new ObservableCollection<TestProjectViewModel>(_testProjects);
            var originalDeviceCommunications = new Dictionary<string, CommunicationManagement.ICommunication>(deviceCommunications);
            var originalFilePath = _currentTestCase != null ? filePath : null;

            try
            {
                AddLog($"正在加载用例文件: {Path.GetFileName(filePath)}", "加载");

                // 加载用例配置
                _currentTestCase = TestCaseManager.LoadTestCase(filePath);

                // 验证用例文件
                if (!TestCaseManager.ValidateTestCase(_currentTestCase))
                {
                    throw new Exception("用例文件格式无效或项目文件不存在");
                }

                // 加载所有项目配置
                _loadedProjects.Clear();
                foreach (var project in _currentTestCase.Projects)
                {
                    try
                    {
                        var projectConfig = TestProjectConfig.LoadFromFile(project.ProjectPath);
                        
                        // 设置项目的Order属性
                        projectConfig.Order = project.Order;
                        
                        // 应用TestCaseProject中的Variables属性（如果存在）
                        if (project.Variables != null && project.Variables.Count > 0)
                        {
                            // 更新项目配置中的变量，而不是直接替换
                            foreach (var variable in project.Variables)
                            {
                                // 查找是否已存在同名变量
                                var existingVariable = projectConfig.Variables.FirstOrDefault(v => v.Name == variable.VariableName);
                                if (existingVariable != null)
                                {
                                    existingVariable.QualifiedValue = variable.QualifiedValue ?? existingVariable.QualifiedValue;
                                    existingVariable.IsVisible = variable.IsVisible;
                                    
                                }
                                else
                                {
                                    // 如果变量不存在，创建新变量
                                    var testVariable = new TestVariable
                                    {
                                        Name = variable.VariableName ?? "未命名变量",
                                        Type = variable.VariableType ?? "string",
                                        Value = variable.Value ?? "", // 包含Value属性的值
                                        IsVisible = variable.IsVisible,
                                        Description = variable.Description ?? "",
                                        QualifiedValue = variable.QualifiedValue ?? "",
                                        Unit = variable.Unit ?? "-"
                                    };
                                    projectConfig.Variables.Add(testVariable);
                                }
                            }
                            AddLog($"已应用项目变量配置: {project.Name} ({project.Variables.Count} 个变量)", "加载");
                        }
                        
                        _loadedProjects.Add(projectConfig);
                        AddLog($"已加载项目: {project.Name}", "加载");
                    }
                    catch (Exception ex)
                    {
                        AddLog($"加载项目失败 {project.Name}: {ex.Message}", "错误");
                    }
                }

                if (_loadedProjects.Count == 0)
                {
                    throw new Exception("未成功加载任何项目配置");
                }

                // 更新界面显示
                UpdateTestCaseDisplay(filePath);

                // 保存测试用例文件路径
                _currentTestCaseFilePath = filePath;

                // 清除当前测试项目显示，等待测试开始时动态添加
                _testProjects.Clear();
                OnPropertyChanged(nameof(TestProjects));

                // 断开之前所有的设备连接
                await DisconnectAllDevicesAsync();
                
                // 加载设备并建立通讯连接
                await ConnectDevicesAsync();

                // 设备连接成功后，根据当前的启动模式初始化资源
                UpdateStartupModeLogic();

                // 更新按钮状态
                UpdateButtonStates();

                AddLog($"用例文件加载成功: {_currentTestCase.Name}", "成功");
                AddLog($"包含 {_loadedProjects.Count} 个项目", "信息");

              
            }
            catch (Exception ex)
            {
                AddLog($"加载用例文件失败: {ex.Message}", "错误");
                MessageBox.Show($"加载用例文件失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 恢复到之前的状态
                _currentTestCase = originalTestCase;
                _loadedProjects.Clear();
                _loadedProjects.AddRange(originalLoadedProjects);
                _testProjects.Clear();
                foreach (var project in originalTestProjects)
                {
                    _testProjects.Add(project);
                }
                deviceCommunications.Clear();
                foreach (var device in originalDeviceCommunications)
                {
                    deviceCommunications.Add(device.Key, device.Value);
                }
                UpdateTestCaseDisplay(originalFilePath);
                OnPropertyChanged(nameof(TestProjects));
                UpdateButtonStates();
            }
        }

        // 断开所有设备连接
        private async Task DisconnectAllDevicesAsync()
        {
            // 断开所有设备连接
            foreach (var device in deviceCommunications)
            {
                try
                {
                    AddLog($"正在断开设备连接: {device.Key}");
                    await device.Value.DisconnectAsync();
                    AddLog($"设备断开成功: {device.Key}", "成功");
                }
                catch (Exception ex)
                {
                    AddLog($"设备断开失败 {device.Key}: {ex.Message}", "错误");
                }
            }
            
            // 清除设备通讯实例
            deviceCommunications.Clear();
            AddLog("所有设备连接已断开", "信息");
        }

        // 加载设备并建立通讯连接
        private async Task ConnectDevicesAsync()
        {
            // 从设备文件夹加载所有设备
            string devicesFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Devices");
            if (Directory.Exists(devicesFolderPath))
            {
                string[] deviceFiles = Directory.GetFiles(devicesFolderPath, "*.xml");
                foreach (string file in deviceFiles)
                {
                    var device = XmlHelper.DeserializeFromFile<ResourceManagement.Device>(file);
                    if (device != null && !string.IsNullOrEmpty(device.Name) && device.IsEnabled)
                    {
                        AddLog($"正在连接设备: {device.Name} ({device.CommunicationType})");
                        
                        // 创建通讯实例并连接
                        var communication = CommunicationManagement.CommunicationManager.CreateCommunication(device.CommunicationType, device.Params, device.DeviceType);
                        bool connected = await communication.ConnectAsync(device.Params);
                        
                        if (connected)
                        {
                            deviceCommunications.Add(device.Name, communication);
                            AddLog($"设备连接成功: {device.Name}", "成功");
                        }
                        else
                        {
                            // 设备连接失败，抛出异常
                            string errorMessage = $"设备连接失败: {device.Name} - {communication.ConnectionStatus}";
                            AddLog(errorMessage, "错误");
                            throw new Exception(errorMessage);
                        }
                    }
                }
            }
            
            AddLog($"设备连接完成，成功连接 {deviceCommunications.Count} 个设备", "信息");
        }

        // 更新用例文件显示
        private void UpdateTestCaseDisplay(string? filePath)
        {
            if (TestCaseFileText != null)
            {
                if (filePath == "默认测试用例")
                {
                    TestCaseFileText.Text = "默认测试用例";
                    TestCaseFileText.Foreground = Brushes.Black;
                }
                else
                {
                    TestCaseFileText.Text = filePath != null ? Path.GetFileName(filePath) : "未选择用例文件";
                    TestCaseFileText.Foreground = filePath != null ? Brushes.Black : Brushes.Gray;
                }
            }

            if (CurrentTestCaseText != null)
            {
                CurrentTestCaseText.Text = _currentTestCase != null ? _currentTestCase.Name : "";
            }

            if (TestCaseProjectsText != null)
            {
                if (_currentTestCase != null && _loadedProjects.Count > 0)
                {
                    int totalSteps = _loadedProjects.Sum(p => p.Steps?.Count ?? 0);
                    TestCaseProjectsText.Text = $"{_loadedProjects.Count} 个项目，{totalSteps} 个步骤";
                }
                else
                {
                    TestCaseProjectsText.Text = "";
                }
            }
        }

        // 修改统计按钮点击事件
        private void ModifyStatsButton_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                if (_isEditMode)
                {
                    // 如果已经在编辑模式，则保存修改
                    SaveModifications();
                    return;
                }

                // 进入编辑模式
                EnterEditMode();

                AddLog("进入统计编辑模式", "系统");
            }
            catch (Exception ex)
            {
                AddLog($"进入编辑模式失败: {ex.Message}", "错误");
            }
        }

        // 进入编辑模式
        private void EnterEditMode()
        {
            _isEditMode = true;

            // 隐藏显示文本，显示编辑框
            if (TotalTestsBorder != null) TotalTestsBorder.Visibility = Visibility.Collapsed;
            if (TotalTestsEditBox != null)
            {
                TotalTestsEditBox.Visibility = Visibility.Visible;
                TotalTestsEditBox.Text = TotalTestCount.ToString();
                TotalTestsEditBox.SelectAll();
                TotalTestsEditBox.Focus();
            }

            if (TotalPassedBorder != null) TotalPassedBorder.Visibility = Visibility.Collapsed;
            if (TotalPassedEditBox != null)
            {
                TotalPassedEditBox.Visibility = Visibility.Visible;
                TotalPassedEditBox.Text = TotalPassedCount.ToString();
            }

            if (TotalFailedBorder != null) TotalFailedBorder.Visibility = Visibility.Collapsed;
            if (TotalFailedEditBox != null)
            {
                TotalFailedEditBox.Visibility = Visibility.Visible;
                TotalFailedEditBox.Text = TotalFailedCount.ToString();
            }

            // 更新按钮状态
            if (ModifyStatsButton != null)
            {
                ModifyStatsButton.Content = "保存";
                ModifyStatsButton.Style = (Style)FindResource("SuccessButtonStyle");
            }

           
        }

        // 退出编辑模式
        private void ExitEditMode()
        {
            _isEditMode = false;

            // 显示文本，隐藏编辑框
            if (TotalTestsBorder != null) TotalTestsBorder.Visibility = Visibility.Visible;
            if (TotalTestsEditBox != null) TotalTestsEditBox.Visibility = Visibility.Collapsed;

            if (TotalPassedBorder != null) TotalPassedBorder.Visibility = Visibility.Visible;
            if (TotalPassedEditBox != null) TotalPassedEditBox.Visibility = Visibility.Collapsed;

            if (TotalFailedBorder != null) TotalFailedBorder.Visibility = Visibility.Visible;
            if (TotalFailedEditBox != null) TotalFailedEditBox.Visibility = Visibility.Collapsed;

            // 更新按钮状态
            if (ModifyStatsButton != null)
            {
                ModifyStatsButton.Content = "修改";
                ModifyStatsButton.Style = (Style)FindResource("PrimaryButtonStyle");
            }

          
        }

       

        // 保存修改的逻辑
        private void SaveModifications()
        {
            try
            {
                // 验证输入
                if (!int.TryParse(TotalTestsEditBox?.Text, out int newTotalCount) || newTotalCount < 0)
                {
                    MessageBox.Show("请输入有效的总测试数（非负整数）", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (TotalTestsEditBox != null)
                    {
                        TotalTestsEditBox.Focus();
                        TotalTestsEditBox.SelectAll();
                    }
                    return;
                }

                if (!int.TryParse(TotalPassedEditBox?.Text, out int newPassedCount) || newPassedCount < 0)
                {
                    MessageBox.Show("请输入有效的通过数（非负整数）", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (TotalPassedEditBox != null)
                    {
                        TotalPassedEditBox.Focus();
                        TotalPassedEditBox.SelectAll();
                    }
                    return;
                }

                if (!int.TryParse(TotalFailedEditBox?.Text, out int newFailedCount) || newFailedCount < 0)
                {
                    MessageBox.Show("请输入有效的失败数（非负整数）", "输入错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    if (TotalFailedEditBox != null)
                    {
                        TotalFailedEditBox.Focus();
                        TotalFailedEditBox.SelectAll();
                    }
                    return;
                }

                // 验证一致性：总测试数 = 通过数 + 失败数
                if (newTotalCount != newPassedCount + newFailedCount)
                {
                    var result = MessageBox.Show($"总测试数({newTotalCount})不等于通过数({newPassedCount}) + 失败数({newFailedCount})，是否自动调整总测试数？",
                        "数据不一致", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        newTotalCount = newPassedCount + newFailedCount;
                        if (TotalTestsEditBox != null)
                            TotalTestsEditBox.Text = newTotalCount.ToString();
                    }
                    else
                    {
                        return;
                    }
                }

                // 确认修改
                var confirmResult = MessageBox.Show($"确认修改统计信息？\n总测试数: {newTotalCount}\n通过数: {newPassedCount}\n失败数: {newFailedCount}",
                    "确认修改", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (confirmResult == MessageBoxResult.Yes)
                {
                    // 更新统计（属性设置器会自动调用SaveStatistics）
                    TotalTestCount = newTotalCount;
                    TotalPassedCount = newPassedCount;
                    TotalFailedCount = newFailedCount;
                    // 退出编辑模式
                    ExitEditMode();

                    // 更新显示
                    UpdateStatisticsDisplay();

                    AddLog($"修改统计信息: 总测试数={newTotalCount}, 通过={newPassedCount}, 失败={newFailedCount}", "修改");

                    MessageBox.Show("统计信息已修改并保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AddLog($"保存统计修改失败: {ex.Message}", "错误");
                MessageBox.Show($"保存失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加键盘事件处理
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_isEditMode)
            {
                // 在编辑模式下，Enter键保存，Esc键取消
                if (e.Key == Key.Enter)
                {
                    SaveModifications();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    ExitEditMode();
                    e.Handled = true;
                }
            }

            if (e.Key == Key.F1)
            {
                MessageBox.Show("测试界面快捷键:\n" +
                    "F1 - 显示帮助\n" +
                    "F5 - 刷新数据\n" +
                    "Ctrl+S - 模拟扫描\n" +
                    "Ctrl+R - 开始测试\n" +
                    "Ctrl+E - 停止测试\n" +
                    "Ctrl+M - 修改统计\n" +
                    "Esc - 关闭窗口",
                    "帮助", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (e.Key == Key.F5)
            {
                // 刷新显示
                ClearTestItems();
                AddLog("显示已刷新", "系统");
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SimulateScanButton_Click(null, new RoutedEventArgs());
            }
            else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
            {
                StartTestButton_Click(null, new RoutedEventArgs());
            }
            else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
            {
                StopTestButton_Click(null, new RoutedEventArgs());
            }
            else if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (ModifyStatsButton != null && ModifyStatsButton.IsEnabled && !_isEditMode)
                {
                    ModifyStatsButton_Click(null, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (_isEditMode)
                {
                    ExitEditMode();
                    e.Handled = true;
                }
                else
                {
                    WindowCloseButton_Click(null, new RoutedEventArgs());
                }
            }
        }

        protected override async void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_testTimer != null) _testTimer.Stop();
            if (_clockTimer != null) _clockTimer.Stop();
            if (_durationTimer != null) _durationTimer.Stop();
            if (_autoSaveTimer != null) _autoSaveTimer.Stop();

            // 清理启动资源，包括关闭CAN盒和条码扫描枪
            CleanupStartupResources();

            if (deviceCommunications.Count > 0)
            {
                AddLog("正在关闭设备连接...", "信息");
                await DisconnectAllDevicesAsync();
            }

            SaveStatistics();
        }

        /// <summary>
        /// 生成测试报告
        /// </summary>
        private async Task GenerateTestReport(List<TestStepResult> stepResults, 
            int passedSteps, int failedSteps, bool testPassed, TimeSpan totalDuration)
        {
            try
            {
                if (_currentTestCase == null)
                {
                    AddLog("无法生成测试报告：当前测试用例为空", "错误");
                    return;
                }
                
                // 生成详细测试报告
                string report = TestReportGenerator.GenerateDetailedReport(
                    _currentTestCase, stepResults, _currentBarcode, totalDuration, testPassed);

                // 保存报告到文件
                string reportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestReports");
                if (!Directory.Exists(reportDir))
                {
                    Directory.CreateDirectory(reportDir);
                }

                string fileName = $"{_currentBarcode}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(reportDir, fileName);
                
                await File.WriteAllTextAsync(filePath, report);
                
                // 记录报告生成
                await Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"测试报告已生成: {fileName}", "报告");
                });

                // 生成HTML报告
                string htmlReport = TestReportGenerator.GenerateHtmlReport(
                    _currentTestCase, stepResults, _currentBarcode, totalDuration, testPassed);
                
                string htmlFileName = $"{_currentBarcode}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                string htmlFilePath = Path.Combine(reportDir, htmlFileName);
                
                await File.WriteAllTextAsync(htmlFilePath, htmlReport);
                
                await Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"HTML测试报告已生成: {htmlFileName}", "报告");
                });

                // 生成Excel报告
                byte[] excelReport = TestReportGenerator.GenerateExcelReport(
                    _currentTestCase, stepResults, _currentBarcode, totalDuration, testPassed);
                
                string excelFileName = $"{_currentBarcode}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string excelFilePath = Path.Combine(reportDir, excelFileName);
                
                await File.WriteAllBytesAsync(excelFilePath, excelReport);
                
                await Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"Excel测试报告已生成: {excelFileName}", "报告");
                });

            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"生成测试报告失败: {ex.Message}", "错误");
                });
            }
        }

        /// <summary>
        /// 更新按钮状态
        /// </summary>
        private void UpdateButtonStates()
        {
            // 启用/禁用启动测试按钮
            if (StartTestButton != null)
            {
                // 启动按钮状态应取决于已加载的项目数量，而不是已执行的项目数量
                StartTestButton.IsEnabled = _currentTestCase != null && _loadedProjects.Count > 0;
            }
        }

        

       

        /// <summary>
        /// 更新变量显示
        /// </summary>
        private void UpdateVariableDisplay(TestVariable variable)
        {
            try
            {
                // 只处理项目变量，跳过结果变量
                if (IsProjectVariable(variable))
                {
                    // 在测试项目中查找并更新变量
                    bool variableUpdated = FindAndUpdateProjectVariable(variable);
                    
                    if (!variableUpdated)
                    {
                        AddLog($"未找到项目变量: {variable.Name}", "警告");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"更新变量显示失败: {ex.Message}", "错误");
            }
        }
        
        /// <summary>
        /// 检查是否为项目变量
        /// </summary>
        private bool IsProjectVariable(TestVariable variable)
        {
            // 项目变量是通过绑定关系从结果变量转换而来的
            // 这里简化处理，假设所有通过HandleProjectVariableBinding方法创建的变量都是项目变量
            // 可以根据实际情况添加更复杂的判断逻辑
            return true;
        }
        
        // 用于存储每个变量的最新值，键为变量名+项目名，值为变量对象
        private Dictionary<string, TestVariable> _variableValues = new Dictionary<string, TestVariable>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// 查找并更新项目变量
        /// </summary>
        private bool FindAndUpdateProjectVariable(TestVariable variable)
        {
            bool variableFound = false;
            
            // 为每个项目更新变量值
            foreach (var projectConfig in _loadedProjects.OrderBy(p => p.Name))
            {
                // 检查项目中是否包含该变量
                var existingVariable = projectConfig.Variables.FirstOrDefault(v => 
                    string.Equals(v.Name, variable.Name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(v.Description, variable.Description, StringComparison.OrdinalIgnoreCase));
                
                if (existingVariable != null)
                {
                    // 更新项目变量的值
                    existingVariable.Value = variable.Value;
                    existingVariable.ActualValue = variable.ActualValue;
                    
                    // 同时更新该项目中所有步骤的输入绑定中引用的变量值
                    foreach (var step in projectConfig.Steps)
                    {
                        if (step.InputBindings != null)
                        {
                            foreach (var inputBinding in step.InputBindings)
                            {
                                if (inputBinding.IsBound && inputBinding.SelectedVariable != null && 
                                    (string.Equals(inputBinding.SelectedVariable.VariableName, variable.Name, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(inputBinding.SelectedVariable.Description, variable.Description, StringComparison.OrdinalIgnoreCase)))
                                {
                                    // 更新输入绑定中变量的值
                                    inputBinding.SelectedVariable.Value = variable.Value;
                                }
                            }
                        }
                    }
                    
                    // 使用项目名+变量名作为键，确保每个项目的变量值独立存储
                    string key = $"{projectConfig.Name}_{variable.Name}";
                    _variableValues[key] = variable;
                    variableFound = true;
                }
            }
            
            return variableFound;
        }
        
        
        
        
        /// <summary>
        /// 更新变量属性
        /// </summary>
        private void UpdateVariableProperties(TestVariable existingVariable, TestVariable variable)
        {
            existingVariable.ActualValue = variable.ActualValue;
            existingVariable.TestTime = variable.TestTime;
            existingVariable.Duration = variable.Duration;
            existingVariable.Type = variable.Type;
        }

        /// <summary>
        /// 评估并更新变量状态
        /// </summary>
        private void EvaluateAndUpdateVariableStatus(TestVariable existingVariable)
        {
            existingVariable.Status = EvaluateVariableResult(existingVariable);
            
        }
        
        /// <summary>
        /// 添加变量更新日志
        /// </summary>
        private void AddVariableUpdateLog(TestProjectViewModel project, TestVariable variable)
        {
            AddLog($"变量更新成功: {project.Name}.{variable.Name} = {variable.ActualValue} {variable.Unit} ({variable.Status})", "变量");
        }
        
        /// <summary>
        /// 刷新UI和状态
        /// </summary>
        private void RefreshUIAndStatus(TestProjectViewModel project)
        {
            // 显式刷新TreeView控件，确保UI实时更新
            TestStepsTreeView.Items.Refresh();
            
            // 刷新项目状态
            project.OverallStatus = CalculateProjectStatus(project);
            
            // 更新所有项目状态
            _testProjects.UpdateAllStatus();
        }
        
        /// <summary>
        /// 计算项目状态
        /// </summary>
        private TestStepStatus CalculateProjectStatus(TestProjectViewModel project)
        {
            return project.Steps.All(s => s.Status == TestStepStatus.Passed) && 
                   project.Variables.All(v => v.Status == TestStepStatus.Passed || v.Status == TestStepStatus.Pending)
               ? TestStepStatus.Passed : TestStepStatus.Failed;
        }
        
        /// <summary>
        /// 评估变量结果，根据实际值和合格值判断变量状态
        /// </summary>
        private TestStepStatus EvaluateVariableResult(TestVariable variable)
        {
            try
            {
                // 如果没有实际值或合格值，返回待处理状态
                if (string.IsNullOrEmpty(variable.ActualValue) || string.IsNullOrEmpty(variable.QualifiedValue))
                {
                    return TestStepStatus.Pending;
                }
                
                // 根据变量类型进行不同的结果判断
                switch (variable.Type.ToUpper())
                {
                    case "INT":
                        return EvaluateNumericResult<int>(variable.ActualValue, variable.QualifiedValue);
                    case "DOUBLE":
                        return EvaluateNumericResult<double>(variable.ActualValue, variable.QualifiedValue);
                    case "BOOL":
                        return EvaluateBooleanResult(variable.ActualValue, variable.QualifiedValue);
                    case "STRING":
                    default:
                        return EvaluateStringResult(variable.ActualValue, variable.QualifiedValue);
                }
            }
            catch (Exception ex)
            {
                AddLog($"评估变量结果失败: {variable.Name} - {ex.Message}", "错误");
                return TestStepStatus.Failed;
            }
        }





        /// <summary>
        /// 处理测试结果，更新所有变量的状态和属性
        /// </summary>
        private void ProcessTestResults()
        {
            try
            {
                AddLog("开始处理测试结果", "结果处理");

                // 遍历所有测试项目的变量
                foreach (var project in _testProjects)
                {
                    foreach (var variable in project.Variables)
                    {
                        // 将TestVariable的_value值赋值给_actualValue属性
                        if (!string.IsNullOrEmpty(variable.Value) && string.IsNullOrEmpty(variable.ActualValue))
                        {
                            variable.ActualValue = variable.Value;
                        }

                        // 比较TestVariable的_value属性与_qualifiedValue属性，更新_status属性
                        variable.Status = EvaluateVariableResult(variable);

                        // 添加日志
                        AddLog($"处理变量结果: {project.Name}.{variable.Name} - 实际值: {variable.ActualValue}, 合格值: {variable.QualifiedValue}, 状态: {variable.Status}", "结果处理");
                    }
                }

                // 刷新UI，确保更新后的_actualValue和_status能正确显示在用户界面上
                TestStepsTreeView.Items.Refresh();
                AddLog("测试结果处理完成", "结果处理");
            }
            catch (Exception ex)
            {
                AddLog($"处理测试结果失败: {ex.Message}", "错误");
            }
        }
        
        /// <summary>
        /// 评估数值型变量结果
        /// </summary>
        private TestStepStatus EvaluateNumericResult<T>(string actualValue, string qualifiedValue) where T : IComparable<T>
        {
            if (!TryParseValue<T>(actualValue, out T actual))
            {
                return TestStepStatus.Failed;
            }
            
            // 支持范围判断和比较判断
            if (qualifiedValue.Contains("-"))
            {
                // 范围格式：min-max
                string[] rangeParts = qualifiedValue.Split('-');
                if (rangeParts.Length == 2 && 
                    TryParseValue<T>(rangeParts[0].Trim(), out T min) && 
                    TryParseValue<T>(rangeParts[1].Trim(), out T max))
                {
                    return actual.CompareTo(min) >= 0 && actual.CompareTo(max) <= 0 ? TestStepStatus.Passed : TestStepStatus.Failed;
                }
            }
            else if (qualifiedValue.StartsWith(">="))
            {
                // 大于等于格式
                if (TryParseValue<T>(qualifiedValue.Substring(2).Trim(), out T min))
                {
                    return actual.CompareTo(min) >= 0 ? TestStepStatus.Passed : TestStepStatus.Failed;
                }
            }
            else if (qualifiedValue.StartsWith("<="))
            {
                // 小于等于格式
                if (TryParseValue<T>(qualifiedValue.Substring(2).Trim(), out T max))
                {
                    return actual.CompareTo(max) <= 0 ? TestStepStatus.Passed : TestStepStatus.Failed;
                }
            }
            else if (qualifiedValue.StartsWith(">"))
            {
                // 大于格式
                if (TryParseValue<T>(qualifiedValue.Substring(1).Trim(), out T min))
                {
                    return actual.CompareTo(min) > 0 ? TestStepStatus.Passed : TestStepStatus.Failed;
                }
            }
            else if (qualifiedValue.StartsWith("<"))
            {
                // 小于格式
                if (TryParseValue<T>(qualifiedValue.Substring(1).Trim(), out T max))
                {
                    return actual.CompareTo(max) < 0 ? TestStepStatus.Passed : TestStepStatus.Failed;
                }
            }
            else
            {
                // 精确匹配格式
                if (TryParseValue<T>(qualifiedValue, out T expected))
                {
                    return actual.CompareTo(expected) == 0 ? TestStepStatus.Passed : TestStepStatus.Failed;
                }
            }
            
            return TestStepStatus.Failed;
        }
        
        /// <summary>
        /// 评估布尔型变量结果
        /// </summary>
        private TestStepStatus EvaluateBooleanResult(string actualValue, string qualifiedValue)
        {
            if (bool.TryParse(actualValue, out bool actual) && bool.TryParse(qualifiedValue, out bool expected))
            {
                return actual == expected ? TestStepStatus.Passed : TestStepStatus.Failed;
            }
            
            return TestStepStatus.Failed;
        }
        
        /// <summary>
        /// 评估字符串型变量结果
        /// </summary>
        private TestStepStatus EvaluateStringResult(string actualValue, string qualifiedValue)
        {
            // 字符串比较，忽略大小写
            return string.Equals(actualValue, qualifiedValue, StringComparison.OrdinalIgnoreCase) ? TestStepStatus.Passed : TestStepStatus.Failed;
        }
        
        /// <summary>
        /// 尝试解析值为指定类型
        /// </summary>
        private bool TryParseValue<T>(string value, out T result)
        {
            try
            {
                result = (T)Convert.ChangeType(value, typeof(T));
                return true;
            }
            catch
            {
                result = default(T);
                return false;
            }
        }

        /// <summary>
        /// 更新启动模式逻辑
        /// </summary>
        private void UpdateStartupModeLogic()
        {
            // 清理之前的资源
            CleanupStartupResources();
            
            switch (_testSettings.StartMode)
            {
                case StartMode.Barcode:
                    // 初始化条码启动逻辑
                    InitializeBarcodeStartup();
                    break;
                case StartMode.Tooling:
                    // 初始化工装启动逻辑
                    InitializeToolingStartup();
                    break;
                case StartMode.Software:
                default:
                    // 软件启动，不需要额外逻辑
                    break;
            }
            
            AddLog($"启动模式已更新为: {_testSettings.StartMode}", "设置");
        }

        /// <summary>
        /// 清理启动资源
        /// </summary>
        private void CleanupStartupResources()
        {
            // 停止条码检查定时器
            if (_barcodeCheckTimer != null)
            {
                _barcodeCheckTimer.Stop();
                _barcodeCheckTimer = null;
            }
            
            // 关闭条码扫描枪
            if (_barcodeScanner != null)
            {
                try
                {
                    _barcodeScanner.Close();
                }
                catch (Exception ex)
                {
                    AddLog($"关闭条码扫描枪失败: {ex.Message}", "错误");
                }
                finally
                {
                    _barcodeScanner = null;
                }
            }
            
            // 关闭治具卡串口
            if (_jigSerialPort != null)
            {
                try
                {
                    if (_jigSerialPort.IsOpen)
                    {
                        _jigSerialPort.Close();
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"关闭治具卡串口失败: {ex.Message}", "错误");
                }
                finally
                {
                    _jigSerialPort = null;
                    _isJigPortConnected = false;
                }
            }
            
            // 停止工装状态读取线程
            _isToolingThreadRunning = false;
            if (_toolingStatusThread != null && _toolingStatusThread.IsAlive)
            {
                _toolingStatusThread.Join(1000); // 等待线程终止，最多1秒
                _toolingStatusThread = null;
            }
            
            // 重置工装CAN通信引用（保留兼容旧版本）
            _toolingCanCommunication = null;
        }

        /// <summary>
        /// 初始化条码启动逻辑
        /// </summary>
        private void InitializeBarcodeStartup()
        {
            // 初始化条码缓冲区
            _barcodeBuffer = string.Empty;
            
            // 添加键盘事件处理
            this.PreviewKeyDown += TestInterfaceWindow_PreviewKeyDown;
            
            AddLog("条码启动模式已启用，等待扫描条码...", "设置");
        }

        // 工装状态读取线程标志
        private bool _isToolingThreadRunning = false;
        private System.Threading.Thread? _toolingStatusThread;
        
        // 工装状态枚举
        private enum ToolingState
        {
            Idle,           // 空闲状态
            ButtonPressed,  // 按钮按下
            PressingDown,   // 正在下压
            PressedDown,    // 下压到位
            Testing,        // 正在测试
            PressingUp      // 正在抬起
        }
        
        // 当前工装状态
        private ToolingState _currentToolingState = ToolingState.Idle;
        
        // 上次发送指令的时间
        private DateTime _lastCommandTime = DateTime.MinValue;
        
        // 指令间隔时间（毫秒），防止频繁发送指令
        private const int CommandIntervalMs = 1000;
        
        // 全局工装状态变量
        private bool _isButtonPressed = false; // 按钮是否按下
        private bool _isPressDownComplete = false; // 下压是否到位
        private bool _isEmergencyStop = false; // 急停状态
        private bool _isStatusUpdated = false; // 状态是否已更新
        private bool _isToolingTestFlowEnabled = true; // 是否启用工装测试流程
        
        /// <summary>
        /// 初始化工装启动逻辑
        /// </summary>
        private void InitializeToolingStartup()
        {
            try
            {
                // 初始化条码扫描枪（使用ToolingPort配置）
                if (!string.IsNullOrEmpty(_testSettings.ToolingPort))
                {
                    _barcodeScanner = new BarCodeScanningGun(_testSettings.ToolingPort, _testSettings.ToolingBaudRate);
                    
                    _barcodeScanner.Received += (data) =>
                    {
                        try
                        {
                            string barcode = System.Text.Encoding.Default.GetString(data.ToArray());
                            
                            if (!string.IsNullOrEmpty(barcode))
                            {
                                Dispatcher.InvokeAsync(() =>
                                {
                                    if (_currentToolingState == ToolingState.Idle)
                                    {
                                        if (BarcodeText != null)
                                        {
                                            BarcodeText.Text = barcode;
                                        }
                                        AddLog($"扫描到条码: {barcode}", "条码");
                                    }
                                    else
                                    {
                                        AddLog($"工装忙，忽略条码扫描: {barcode}", "条码");
                                    }
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.InvokeAsync(() =>
                            {
                                AddLog($"处理条码数据失败: {ex.Message}", "错误");
                            });
                        }
                    };
                    
                    AddLog($"条码扫描枪 {_testSettings.ToolingPort} 已初始化", "设置");
                }
                
                // 初始化治具卡串口（使用ToolingJigPort配置）
                if (!string.IsNullOrEmpty(_testSettings.ToolingJigPort))
                {
                    _jigSerialPort = new System.IO.Ports.SerialPort();
                    _jigSerialPort.PortName = _testSettings.ToolingJigPort;
                    _jigSerialPort.BaudRate = _testSettings.ToolingJigBaudRate;
                    _jigSerialPort.DataBits = 8;
                    _jigSerialPort.Parity = System.IO.Ports.Parity.None;
                    _jigSerialPort.StopBits = System.IO.Ports.StopBits.One;
                    _jigSerialPort.ReadTimeout = 1000;
                    _jigSerialPort.WriteTimeout = 1000;
                    
                    try
                    {
                        _jigSerialPort.Open();
                        _isJigPortConnected = true;
                        AddLog($"治具卡串口 {_testSettings.ToolingJigPort} 已连接", "设置");
                        
                        // 启动工装状态读取线程
                        _isToolingThreadRunning = true;
                        _toolingStatusThread = new System.Threading.Thread(ToolingStatusReadingThread);
                        _toolingStatusThread.IsBackground = true;
                        _toolingStatusThread.Start();
                    }
                    catch (Exception ex)
                    {
                        AddLog($"治具卡串口连接失败: {ex.Message}", "错误");
                        _jigSerialPort = null;
                        _isJigPortConnected = false;
                    }
                }
                else
                {
                    AddLog("工装启动模式已启用，但未配置治具卡串口", "警告");
                }
                
            }
            catch (Exception ex)
            {
                AddLog($"初始化工装启动失败: {ex.Message}", "错误");
            }
        }
        
        /// <summary>
        /// 工装状态读取线程
        /// 使用治具卡串口接收1101地址帧并解析状态
        /// </summary>
        private void ToolingStatusReadingThread()
        {
            while (_isToolingThreadRunning)
            {
                try
                {
                    if (_jigSerialPort == null || !_jigSerialPort.IsOpen)
                    {
                        System.Threading.Thread.Sleep(1000);
                        continue;
                    }
                    
                    try
                    {
                        // 持续读取治具卡回传数据
                        byte[] response = ReadJigSerialData();
                        if (response != null && response.Length > 0)
                        {
                            // 解析1101地址帧
                            ParseJigStatusFrame(response);
                        }
                    }
                    catch (TimeoutException)
                    {
                        // 超时是正常的，继续循环
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.InvokeAsync(() =>
                        {
                            AddLog($"状态读取失败: {ex.Message}", "错误");
                        });
                    }
                    
                    // 执行工装测试流程
                    if (_isToolingTestFlowEnabled)
                    {
                        ExecuteToolingTestFlow();
                    }
                    
                    System.Threading.Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AddLog($"工装状态读取失败: {ex.Message}", "错误");
                    });
                    System.Threading.Thread.Sleep(1000);
                }
            }
        }
        
        /// <summary>
        /// 读取治具卡串口数据
        /// </summary>
        private byte[]? ReadJigSerialData()
        {
            if (_jigSerialPort == null || !_jigSerialPort.IsOpen)
                return null;
            
            try
            {
                int bytesAvailable = _jigSerialPort.BytesToRead;
                if (bytesAvailable == 0)
                    return null;
                
                byte[] buffer = new byte[bytesAvailable];
                int bytesRead = _jigSerialPort.Read(buffer, 0, bytesAvailable);
                
                if (bytesRead > 0)
                {
                    byte[] result = new byte[bytesRead];
                    Array.Copy(buffer, result, bytesRead);
                    return result;
                }
            }
            catch (TimeoutException)
            {
                // 超时正常
            }
            catch (Exception)
            {
                // 其他异常忽略
            }
            
            return null;
        }
        
        /// <summary>
        /// 解析治具卡1101地址帧
        /// 上报帧格式: AA 55 11 01 起始地址 00 03 数量 00 00 I/O bit 11 22 NTC1 33 44 NTC3 55 66 CRC
        /// </summary>
        private void ParseJigStatusFrame(byte[] data)
        {
            try
            {
                if (data.Length < 20)
                    return;
                
                // 验证帧头 AA 55
                if (data[0] != 0xAA || data[1] != 0x55)
                    return;
                
                // 验证帧ID 11 01（Modbus读多个寄存器响应）
                if (data[2] != 0x11 || data[3] != 0x01)
                    return;
                
                // 起始地址
                int startAddr = (data[4] << 8) | data[5];
                
                // 根据地址判断处理方式
                if (startAddr == 0x0103)
                {
                    // I/O状态地址
                    ParseIOStatusFrame(data);
                }
                else if (startAddr == 0x0100)
                {
                    // 温度数据地址
                    ParseTemperatureFrame(data);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"解析治具卡帧失败: {ex.Message}", "错误");
                });
            }
        }
        
        /// <summary>
        /// 解析I/O状态帧
        /// I/O bit按位展开: 无效、无效、DH2、DH1、DL4、DL3、DL2、DL1、DIH2、DIH1、DI6、DI5、DI4、DI3、DI2、DI1
        /// </summary>
        private void ParseIOStatusFrame(byte[] data)
        {
            try
            {
                if (data.Length < 12)
                    return;
                
                // I/O bit数据在第9、10字节（索引8、9）
                byte ioBitLow = data[8];   // 低字节: DL1-DL4, DH1-DH2, 无效, 无效
                byte ioBitHigh = data[9];  // 高字节: DI1-DI6, DIH1-DIH2
                
                // 低字节位展开 (ioBitLow):
                // bit0: DL1 (低边输出1)
                // bit1: DL2 (低边输出2)
                // bit2: DL3 (低边输出3)
                // bit3: DL4 (低边输出4)
                // bit4: DH1 (高边输出1)
                // bit5: DH2 (高边输出2)
                // bit6: 无效
                // bit7: 无效
                
                // 高字节位展开 (ioBitHigh):
                // bit0: DI1 (低边输入1 - 启动按键1检测)
                // bit1: DI2 (低边输入2 - 启动按键2检测)
                // bit2: DI3 (低边输入3 - 到位检测)
                // bit3: DI4 (低边输入4 - 启动按键1)
                // bit4: DI5 (低边输入5 - 预留)
                // bit5: DI6 (低边输入6 - 预留)
                // bit6: DIH1 (高边输入1 - 急停开关检测)
                // bit7: DIH2 (高边输入2 - 治具下压到位检测)
                
                bool di1 = (ioBitHigh & 0x01) != 0; // DI1: 启动按键1检测
                bool di2 = (ioBitHigh & 0x02) != 0; // DI2: 启动按键2检测
                bool di3 = (ioBitHigh & 0x04) != 0; // DI3: 到位检测
                bool dih1 = (ioBitHigh & 0x40) != 0; // DIH1: 急停开关检测
                bool dih2 = (ioBitHigh & 0x80) != 0; // DIH2: 治具下压到位检测
                
                _isButtonPressed = di1 || di2;      // 启动按键1或2按下
                _isPressDownComplete = dih2;        // 治具下压到位
                _isEmergencyStop = !dih1;           // 急停开关状态（低电平表示急停）
                
                _isStatusUpdated = true;
                
                if (_isEmergencyStop)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AddLog("检测到急停信号，停止测试", "错误");
                        _currentToolingState = ToolingState.Idle;
                        StopTestButton_Click(null, new RoutedEventArgs());
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"解析I/O状态帧失败: {ex.Message}", "错误");
                });
            }
        }
        
        /// <summary>
        /// 解析温度数据帧
        /// </summary>
        private void ParseTemperatureFrame(byte[] data)
        {
            try
            {
                if (data.Length < 18)
                    return;
                
                // NTC1温度数据在第9-12字节（索引8-11）
                float ntc1 = BitConverter.ToSingle(data, 8);
                
                // NTC3温度数据在第13-16字节（索引12-15）
                float ntc3 = BitConverter.ToSingle(data, 12);
                
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"温度数据 - NTC1: {ntc1:F2}°C, NTC3: {ntc3:F2}°C", "治具卡");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"解析温度帧失败: {ex.Message}", "错误");
                });
            }
        }
        
        /// <summary>
        /// 更新全局状态变量（保留兼容旧版本）
        /// </summary>
        private void UpdateGlobalStatus(string response)
        {
            // 旧版本CAN通信已不再使用，此方法保留兼容
        }
        
        /// <summary>
        /// 执行工装测试流程
        /// 使用状态机管理不同状态，避免重复发送下压和抬起指令
        /// </summary>
        private void ExecuteToolingTestFlow()
        {
            try
            {
                switch (_currentToolingState)
                {
                    case ToolingState.Idle:
                        // 空闲状态，先检查条码是否已扫描，然后检查按钮是否按下
                        bool hasValidBarcode = false;
                        Dispatcher.Invoke(() =>
                        {
                            hasValidBarcode = BarcodeText != null && !string.IsNullOrEmpty(BarcodeText.Text) && BarcodeText.Text != "请扫描条码...";
                        });
                        
                        if (hasValidBarcode && _isButtonPressed)
                        {
                            Dispatcher.InvokeAsync(() =>
                            {
                                AddLog("检测到按钮按下，开始执行工装测试流程", "工装");
                            });
                            
                            // 发送下压指令
                            SendPressDownCommand();
                            // 切换到正在下压状态
                            _currentToolingState = ToolingState.PressingDown;
                        }
                        else if (!hasValidBarcode && _isButtonPressed)
                        {
                            Dispatcher.InvokeAsync(() =>
                            {
                                AddLog("未扫描条码，忽略按钮按下", "工装");
                            });
                        }
                        break;
                        
                    case ToolingState.PressingDown:
                        // 正在下压状态，检查是否到位
                        if (_isPressDownComplete)
                        {
                            Dispatcher.InvokeAsync(() =>
                            {
                                AddLog("工装下压到位，启动测试", "工装");
                            });
                            
                            // 切换到下压到位状态
                            _currentToolingState = ToolingState.PressedDown;
                            
                            // 启动测试
                            Dispatcher.InvokeAsync(() =>
                            {
                                StartTestButton_Click(null, new RoutedEventArgs());
                            });
                        }
                        break;
                        
                    case ToolingState.PressedDown:
                        // 下压到位状态，检查按钮是否释放
                        if (!_isButtonPressed)
                        {
                            Dispatcher.InvokeAsync(() =>
                            {
                                AddLog("按钮已释放", "工装");
                            });
                            // 切换到测试状态
                            _currentToolingState = ToolingState.Testing;
                        }
                        break;
                        
                    case ToolingState.Testing:
                        // 测试状态，检查测试是否正在进行
                        if (!_isTesting)
                        {
                            // 测试已完成，发送抬起指令
                            Dispatcher.InvokeAsync(() =>
                            {
                                AddLog("测试完成，发送抬起指令", "工装");
                            });
                            SendPressUpCommand();
                            // 切换到正在抬起状态
                            _currentToolingState = ToolingState.PressingUp;
                        }
                        break;
                        
                    case ToolingState.PressingUp:
                        // 正在抬起状态，检查是否抬起到位（如果有抬起到位检测的话）
                        // 目前没有抬起到位检测，所以检查是否已经过了足够的时间
                        TimeSpan timeSincePressUp = DateTime.Now - _lastCommandTime;
                        if (timeSincePressUp.TotalMilliseconds >= 500) // 等待500ms后切换到空闲状态
                        {
                            _currentToolingState = ToolingState.Idle;
                            Dispatcher.InvokeAsync(() =>
                            {
                                AddLog("工装已抬起，回到空闲状态", "工装");
                            });
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"执行工装测试流程失败: {ex.Message}", "错误");
                    // 发生异常时重置状态
                    _currentToolingState = ToolingState.Idle;
                });
            }
        }
        
        
        
        /// <summary>
        /// 检查是否可以发送指令
        /// </summary>
        /// <returns>是否可以发送指令</returns>
        private bool CanSendCommand()
        {
            TimeSpan timeSinceLastCommand = DateTime.Now - _lastCommandTime;
            return timeSinceLastCommand.TotalMilliseconds >= CommandIntervalMs;
        }
        
        /// <summary>
        /// 发送下压指令
        /// </summary>
        private void SendPressDownCommand()
        {
            try
            {
                if (!CanSendCommand())
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AddLog("指令发送过于频繁，忽略此次发送", "调试");
                    });
                    return;
                }
                
                // 标准Modbus RTU协议：从站地址+功能码+寄存器地址+寄存器值
                // 从站地址: 0x01
                // 功能码: 0x06（写单个寄存器）
                // 寄存器地址: 0x0100（大端序）
                // 下压值: 0x0001（设置DL1=1）
                byte[] pressDownData = { 0x01, 0x06, 0x01, 0x00, 0x00, 0x01 };
                SendJigCommand(pressDownData);
                
                _lastCommandTime = DateTime.Now;
                
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog("已发送工装下压指令", "工装");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"发送工装下压指令失败: {ex.Message}", "错误");
                });
            }
        }
        
        /// <summary>
        /// 发送抬起指令
        /// </summary>
        private void SendPressUpCommand()
        {
            try
            {
                if (_jigSerialPort == null || !_jigSerialPort.IsOpen)
                    return;
                
                if (!CanSendCommand())
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        AddLog("指令发送过于频繁，忽略此次发送", "调试");
                    });
                    return;
                }
                
                // 标准Modbus RTU协议：从站地址+功能码+寄存器地址+寄存器值
                // 从站地址: 0x01
                // 功能码: 0x06（写单个寄存器）
                // 寄存器地址: 0x0100（大端序）
                // 抬起值: 0x0000（设置DL1=0）
                byte[] pressUpData = { 0x01, 0x06, 0x01, 0x00, 0x00, 0x00 };
                SendJigCommand(pressUpData);
                
                _lastCommandTime = DateTime.Now;
                
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog("已发送工装抬起指令", "工装");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    AddLog($"发送工装抬起指令失败: {ex.Message}", "错误");
                    _currentToolingState = ToolingState.Idle;
                });
            }
        }
        
        /// <summary>
        /// 发送治具卡指令（标准Modbus RTU协议）
        /// </summary>
        private void SendJigCommand(byte[] data)
        {
            if (_jigSerialPort == null || !_jigSerialPort.IsOpen)
                return;
            
            try
            {
                byte[] crc = CalculateCRC16(data);
                byte[] frame = new byte[data.Length + 2];
                Array.Copy(data, frame, data.Length);
                Array.Copy(crc, 0, frame, data.Length, 2);
                
                _jigSerialPort.Write(frame, 0, frame.Length);
            }
            catch (Exception ex)
            {
                throw new Exception($"串口发送失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 计算Modbus CRC16校验
        /// </summary>
        private byte[] CalculateCRC16(byte[] data)
        {
            ushort crc = 0xFFFF;
            
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            
            byte[] result = new byte[2];
            result[0] = (byte)(crc & 0xFF);
            result[1] = (byte)((crc >> 8) & 0xFF);
            
            return result;
        }
        
        /// <summary>
        /// 检查条码并自动启动测试
        /// </summary>
        private void CheckBarcodeAndStartTest(string barcode)
        {
            if (_testSettings.StartMode != StartMode.Barcode)
                return;
                
            if (string.IsNullOrEmpty(barcode))
                return;
                
            if (barcode.Length == _testSettings.BarcodeLength)
            {
                _currentBarcode = barcode;
                if (BarcodeText != null && BarcodeText.Text != barcode)
                {
                    BarcodeText.Text = barcode;
                }
                AddLog($"有效条码: {barcode}，自动启动测试", "条码");
                
                // 发送条码命令到工装
                SendBarcodeCommandToTooling(barcode);
                
                // 启动测试
                StartTestButton_Click(null, new RoutedEventArgs());
            }
        }
        
        /// <summary>
        /// 发送条码命令到工装
        /// </summary>
        private void SendBarcodeCommandToTooling(string barcode)
        {
            try
            {
                // 条码扫描枪现在用于接收条码，不需要向其发送命令
                // 这里可以添加向工装发送条码的逻辑
                
                // 读取工装状态（通过CAN） - 现在由全局状态读取线程处理
                // ReadToolingStatus();
            }
            catch (Exception ex)
            {
                AddLog($"发送条码命令失败: {ex.Message}", "错误");
            }
        }
        
     

        /// <summary>
        /// 条码扫描键盘事件处理
        /// </summary>
        private void TestInterfaceWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_testSettings.StartMode != StartMode.Barcode)
                return;
            
            // 检查焦点是否在条码输入框上，如果是则不处理键盘事件，允许手动输入
            if (FocusManager.GetFocusedElement(this) == BarcodeText)
                return;
            
            // 处理条码扫描
            if (e.Key == Key.Enter)
            {
                // 条码扫描完成
                if (!string.IsNullOrEmpty(_barcodeBuffer))
                {
                    // 检查条码长度
                    if (_barcodeBuffer.Length == _testSettings.BarcodeLength)
                    {
                        // 条码长度匹配，启动测试
                        CheckBarcodeAndStartTest(_barcodeBuffer);
                    }
                    else
                    {
                        AddLog($"扫描到条码: {_barcodeBuffer}，长度不匹配（期望 {_testSettings.BarcodeLength}，实际 {_barcodeBuffer.Length}）", "警告");
                    }
                    
                    // 清空条码缓冲区
                    _barcodeBuffer = string.Empty;
                }
                e.Handled = true;
            }
            else if (e.Key >= Key.D0 && e.Key <= Key.D9 || e.Key >= Key.A && e.Key <= Key.Z || e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                // 添加字符到条码缓冲区
                string keyChar = new KeyConverter().ConvertToString(e.Key);
                if (!string.IsNullOrEmpty(keyChar))
                {
                    if (keyChar.StartsWith("NumPad"))
                        keyChar = keyChar.Substring(6);
                    _barcodeBuffer += keyChar;
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// 条码文本变化事件处理
        /// </summary>
        private void BarcodeText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (BarcodeText != null && !string.IsNullOrEmpty(BarcodeText.Text))
            {
                CheckBarcodeAndStartTest(BarcodeText.Text);
            }
        }



        public event PropertyChangedEventHandler? PropertyChanged = delegate { };

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}