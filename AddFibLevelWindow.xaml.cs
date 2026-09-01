using System.Globalization;
using System.Windows;
using FibRuler.Models;

namespace FibRuler;

public partial class AddFibLevelWindow : Window
{
    public AddFibLevelWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RatioTextBox.Focus();
    }

    public double Ratio { get; private set; }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (!double.TryParse(
                RatioTextBox.Text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double ratio)
            || !double.IsFinite(ratio))
        {
            MessageBox.Show(
                this,
                "有効な数値を入力してください。",
                "Invalid Ratio",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            RatioTextBox.SelectAll();
            RatioTextBox.Focus();
            return;
        }

        Ratio = FibLevel.NormalizeRatio(ratio);
        DialogResult = true;
    }
}
