using System.ComponentModel;

namespace SIAT
{
    /// <summary>
    /// 结果变量模型
    /// </summary>
    [Serializable]
    public class ResultVariableItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private bool _isBound;
        private ProjectVariable? _selectedVariable;

        public string Name
        {
            get { return _name; }
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public bool IsBound
        {
            get { return _isBound; }
            set { _isBound = value; OnPropertyChanged(nameof(IsBound)); }
        }

        public ProjectVariable? SelectedVariable
        {
            get { return _selectedVariable; }
            set { _selectedVariable = value; OnPropertyChanged(nameof(SelectedVariable)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}