using System.Windows.Controls;

namespace QuartzLauncher.Views.Pages;

public partial class PresetPage : Page
{
    public PresetPage(string presetName)
    {
        InitializeComponent();
        PresetTitle.Text = presetName;
    }
}
