using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace SIAT
{
    [Serializable]
    public class ProjectVariable : INotifyPropertyChanged
    {
        private string _variableName;
        public string VariableName
        {
            get => _variableName;
            set { _variableName = value; OnPropertyChanged(); }
        }

        private string _variableType;
        public string VariableType
        {
            get => _variableType;
            set { _variableType = value; OnPropertyChanged(); }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        private bool _isVisible;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(); }
        }

        private string _qualifiedValue;
        public string QualifiedValue
        {
            get => _qualifiedValue;
            set { _qualifiedValue = value; OnPropertyChanged(); }
        }

        private string _unit;
        public string Unit
        {
            get => _unit;
            set { _unit = value; OnPropertyChanged(); }
        }

        private bool _isRange;
        public bool IsRange
        {
            get => _isRange;
            set { _isRange = value; OnPropertyChanged(); }
        }

        private string _value;
        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public ProjectVariable()
        {
            _variableName = string.Empty;
            _variableType = "String";
            _description = string.Empty;
            _isVisible = true;
            _qualifiedValue = string.Empty;
            _unit = string.Empty;
            _isRange = false;
            _value = string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}