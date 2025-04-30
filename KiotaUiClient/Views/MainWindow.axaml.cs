using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KiotaUiClient.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    private void Exit_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}