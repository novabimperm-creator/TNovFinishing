using System.Windows;
using System.Windows.Input;

namespace TNovFinishing
{
    /// <summary>
    /// Логика взаимодействия для FloorWPF.xaml
    /// </summary>
    public partial class FloorWPF : Window
    {
        public FloorWPF(FloorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            this.SizeToContent = SizeToContent.Height;
            
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close(); // закрытие окна
        }
        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close(); // закрытие окна
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = @"https://portal.talan.group/knowledge/proektirovanie/poly/";
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
    }
}
