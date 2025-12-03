using System.Windows;

namespace LLMClient.UI
{
    public partial class ExecutorSelectionWindow : Window
    {
        public BashEnvironment? SelectedEnvironment { get; private set; }

        public ExecutorSelectionWindow(List<BashEnvironment> environments)
        {
            InitializeComponent();
            LvExecutors.ItemsSource = environments;
        }

        private void LvExecutors_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LvExecutors.SelectedItem is BashEnvironment env)
            {
                SelectedEnvironment = env;
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}