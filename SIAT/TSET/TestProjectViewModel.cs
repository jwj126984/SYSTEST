using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SIAT.TSET
{
    /// <summary>
    /// 测试项目视图模型（用于分组显示）
    /// </summary>
    public class TestProjectViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private int _order = 0;
        private bool _isExpanded = true;
        private TestStepStatus _overallStatus = TestStepStatus.Pending;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public TestStepStatus OverallStatus
        {
            get => _overallStatus;
            set { _overallStatus = value; OnPropertyChanged(); }
        }

        public ObservableCollection<TestStepConfig> Steps { get; set; } = new ObservableCollection<TestStepConfig>();
        public ObservableCollection<TestVariable> Variables { get; set; } = new ObservableCollection<TestVariable>();

        public int TotalSteps => Steps.Count;
        public int PassedSteps => Steps.Count(s => s.Status == TestStepStatus.Passed);
        public int FailedSteps => Steps.Count(s => s.Status == TestStepStatus.Failed);
        public int PendingSteps => Steps.Count(s => s.Status == TestStepStatus.Pending);
        public int RunningSteps => Steps.Count(s => s.Status == TestStepStatus.Running);
        
        // 项目总耗时
        private TimeSpan _totalDuration = TimeSpan.Zero;
        public TimeSpan TotalDuration 
        {
            get => _totalDuration;
            set { _totalDuration = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalDurationSeconds)); }
        }
        
        // 项目总耗时（秒），用于UI显示
        public double TotalDurationSeconds => _totalDuration.TotalSeconds;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public TestProjectViewModel() { }

        public TestProjectViewModel(TestProjectConfig projectConfig)
        {
            Name = projectConfig.Name;
            Description = projectConfig.Description;
            Order = projectConfig.Order;
            
            // 深拷贝步骤，确保每次测试的步骤对象独立
            foreach (var step in projectConfig.Steps.OrderBy(s => s.Order))
            {
                // 创建步骤的深拷贝
                var stepCopy = new TestStepConfig
                {
                    Name = step.Name,
                    Description = step.Description,
                    Order = step.Order,
                    IsVisible = step.IsVisible,
                    DeviceName = step.DeviceName,
                    ProtocolContent = step.ProtocolContent,
                    ProtocolType = step.ProtocolType,
                    ExpectedValue = step.ExpectedValue,
                    ActualValue = step.ActualValue,
                    Status = step.Status,
                    Duration = step.Duration,
                    WaitForResponse = step.WaitForResponse,
                    Variables = new ObservableCollection<TestVariable>(
                        step.Variables.Select(v => new TestVariable
                        {
                            Name = v.Name,
                            Type = v.Type,
                            Value = v.Value,
                            IsVisible = v.IsVisible,
                            Description = v.Description,
                            QualifiedValue = v.QualifiedValue,
                            Unit = v.Unit,
                            ActualValue = v.ActualValue,
                            Status = v.Status,
                            TestTime = v.TestTime,
                            Duration = v.Duration
                        })
                    ),
                    ResultVariables = new List<ResultVariable>(step.ResultVariables),
                    InputBindings = new List<BindingItem>(step.InputBindings),
                    OutputBindings = new List<BindingItem>(step.OutputBindings)
                };
                Steps.Add(stepCopy);
            }
            
            // 深拷贝变量，确保每次测试的变量对象独立
            foreach (var variable in projectConfig.Variables)
            {
                // 创建变量的深拷贝
                var variableCopy = new TestVariable
                {
                    Name = variable.Name,
                    Type = variable.Type,
                    Value = variable.Value,
                    IsVisible = variable.IsVisible,
                    Description = variable.Description,
                    QualifiedValue = variable.QualifiedValue,
                    Unit = variable.Unit,
                    ActualValue = variable.ActualValue,
                    Status = variable.Status,
                    TestTime = variable.TestTime,
                    Duration = variable.Duration
                };
                Variables.Add(variableCopy);
            }
        }

        /// <summary>
        /// 更新项目整体状态
        /// </summary>
        public void UpdateOverallStatus()
        {
            if (Steps.Count == 0)
            {
                OverallStatus = TestStepStatus.Pending;
                return;
            }

            if (Steps.Any(s => s.Status == TestStepStatus.Running))
            {
                OverallStatus = TestStepStatus.Running;
            }
            else if (Steps.Any(s => s.Status == TestStepStatus.Failed))
            {
                OverallStatus = TestStepStatus.Failed;
            }
            else if (Steps.All(s => s.Status == TestStepStatus.Passed))
            {
                OverallStatus = TestStepStatus.Passed;
            }
            else if (Steps.All(s => s.Status == TestStepStatus.Pending))
            {
                OverallStatus = TestStepStatus.Pending;
            }
            else
            {
                OverallStatus = TestStepStatus.Pending;
            }

            OnPropertyChanged(nameof(TotalSteps));
            OnPropertyChanged(nameof(PassedSteps));
            OnPropertyChanged(nameof(FailedSteps));
            OnPropertyChanged(nameof(PendingSteps));
            OnPropertyChanged(nameof(RunningSteps));
        }
    }

    /// <summary>
    /// 测试步骤分组集合
    /// </summary>
    public class TestProjectCollection : ObservableCollection<TestProjectViewModel>
    {
        public int TotalProjects => Count;
        public int TotalSteps => this.Sum(p => p.TotalSteps);
        public int TotalPassedSteps => this.Sum(p => p.PassedSteps);
        public int TotalFailedSteps => this.Sum(p => p.FailedSteps);
        public int TotalPendingSteps => this.Sum(p => p.PendingSteps);
        public int TotalRunningSteps => this.Sum(p => p.RunningSteps);

        public void UpdateAllStatus()
        {
            foreach (var project in this)
            {
                project.UpdateOverallStatus();
            }
        }
    }
}