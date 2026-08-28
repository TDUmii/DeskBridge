using System.Windows;
using DeskBridge.Core.Models;

namespace DeskBridge.App.Views;

public partial class PermissionDialog : Window
{
    public PermissionDialog(PermissionRequest request) { InitializeComponent(); DataContext = request; }
    private void Allow_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
}
