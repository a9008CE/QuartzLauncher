using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views;

public enum LanShareChoice
{
    Dismissed,
    Private,
    KeyOnly,
    Public
}

/// <summary>
/// 检测到局域网世界后的询问弹窗：不公开 / 仅密钥 / 公开。
/// </summary>
public sealed record LanShareResult(LanShareChoice Choice, string Password, bool AllowNonPremium);

public static class LanShareDialog
{
    public static LanShareResult Show(Window? owner, LanWorldInfo world, LocalModSnapshot snapshot,
        bool allowNonPremium = true)
    {
        var choice = LanShareChoice.Dismissed;
        var password = "";

        var dialog = new Window
        {
            Title = "检测到局域网世界",
            Width = 540,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
            Owner = owner,
            Topmost = true,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Background = Brushes.Transparent,
            AllowsTransparency = true
        };

        var card = new Border
        {
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(12)
        };
        card.SetResourceReference(Border.BackgroundProperty, "ReadablePanelBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        card.BorderThickness = new Thickness(1);

        var panel = new StackPanel();

        var title = new TextBlock
        {
            Text = "检测到局域网世界",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        panel.Children.Add(title);

        var detail = new TextBlock
        {
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22
        };
        detail.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        detail.Inlines.Add(new System.Windows.Documents.Run($"「{world.Motd}」已开启局域网（端口 {world.Port}）"));
        detail.Inlines.Add(new System.Windows.Documents.LineBreak());
        detail.Inlines.Add(new System.Windows.Documents.Run($"类型：{snapshot.KindText}　{snapshot.SummaryText}"));
        panel.Children.Add(detail);

        if (snapshot.Modded && snapshot.Mods.Count > 0)
        {
            var listBox = new TextBlock
            {
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 132,
                Margin = new Thickness(0, 10, 0, 0)
            };
            listBox.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            var preview = string.Join("\n", snapshot.Mods.Take(6).Select(mod => "· " + mod.FileName));
            if (snapshot.Mods.Count > 6) preview += $"\n… 等共 {snapshot.Mods.Count} 个";
            listBox.Text = preview;
            panel.Children.Add(listBox);
        }

        var hint = new TextBlock
        {
            Text = "公开后其他玩家可在「联机大厅」看到该房间；仅密钥则只分享给知道房间码的人。",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 14, 0, 0)
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        panel.Children.Add(hint);

        var switchRow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        switchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        switchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var switchLabel = new TextBlock
        {
            Text = "允许非正版玩家进入",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };
        switchLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        switchRow.Children.Add(switchLabel);

        var allowSwitch = new System.Windows.Controls.Primitives.ToggleButton
        {
            IsChecked = allowNonPremium,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "关闭后局域网世界将开启正版验证，只有正版账号能加入。改动在下次启动游戏时生效。"
        };
        try
        {
            allowSwitch.Style = (Style)Application.Current.FindResource("IosSwitch");
        }
        catch
        {
        }
        Grid.SetColumn(allowSwitch, 1);
        switchRow.Children.Add(allowSwitch);
        panel.Children.Add(switchRow);

        var passwordLabel = new TextBlock
        {
            Text = "联机密码（选「仅密钥」时填写，留空则不加密码）",
            FontSize = 11,
            Margin = new Thickness(0, 14, 0, 6)
        };
        passwordLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        panel.Children.Add(passwordLabel);

        var passwordBox = new TextBox
        {
            Height = 32,
            FontSize = 13,
            MaxLength = 16
        };
        passwordBox.SetResourceReference(Control.BackgroundProperty, "InputBgBrush");
        passwordBox.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        passwordBox.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        panel.Children.Add(passwordBox);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        Button MakeButton(string text, LanShareChoice result, bool primary)
        {
            var button = new Button
            {
                Content = text,
                Height = 36,
                Padding = new Thickness(16, 4, 16, 4),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, primary ? "BtnPrimary" : "BtnBase");
            button.Click += (_, _) =>
            {
                choice = result;
                password = passwordBox.Text.Trim();
                dialog.Close();
            };
            return button;
        }

        actions.Children.Add(MakeButton("不公开", LanShareChoice.Private, false));
        actions.Children.Add(MakeButton("仅密钥", LanShareChoice.KeyOnly, false));
        actions.Children.Add(MakeButton("公开", LanShareChoice.Public, true));
        panel.Children.Add(actions);

        card.Child = panel;
        dialog.Content = card;
        dialog.ShowDialog();
        return new LanShareResult(choice, password, allowSwitch.IsChecked == true);
    }
}
