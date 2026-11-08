using System.Windows;

namespace AIGRAV.RevitBridge;

public partial class AIGRAVWelcomeWindow : Window
{
    public AIGRAVWelcomeWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        // Closing the window does NOT turn AIGRAV OFF.
        // The Ribbon button controls the AIGRAV engine state.
        Hide();
    }

    private void LaunchButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Hide();
    }
}
