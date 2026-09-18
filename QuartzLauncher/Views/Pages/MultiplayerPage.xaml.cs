using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QuartzLauncher.Services;

namespace QuartzLauncher.Views.Pages;

/// <summary>
/// 联机大厅：展示公开房间、查看详情、加入房间（本地代理接入中继）。
/// </summary>
public partial class MultiplayerPage : Page
{
    private const string LockedMessage = "为防止滥用，请您达到累计游戏时间 24 小时后才可使用本功能。";

    private readonly RelayLobby _lobby = new();
    private readonly DispatcherTimer _refreshTimer;
    private LobbyRoom? _selected;
    private bool _busy;
    private List<LobbyRoom> _rooms = new();

    public MultiplayerPage()
    {
        InitializeComponent();
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await LoadRoomsAsync(silent: true);
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            _refreshTimer.Stop();
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!PlayTimeGate.IsUnlocked(App.Settings.Data))
        {
            GateCard.Visibility = Visibility.Visible;
            GateTitle.Text = "暂不可用";
            GateMessage.Text = LockedMessage;
            GateProgress.Value = PlayTimeGate.ProgressRatio(App.Settings.Data) * 100;
            GateDetail.Text =
                $"已累计 {PlayTimeGate.DescribeTotal(App.Settings.Data)}，还需 {PlayTimeGate.DescribeRemaining(App.Settings.Data)}";
            LobbyRoot.Visibility = Visibility.Collapsed;
            return;
        }

        GateCard.Visibility = Visibility.Collapsed;
        LobbyRoot.Visibility = Visibility.Visible;
        await LoadRoomsAsync();
        _refreshTimer.Start();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = LoadRoomsAsync();

    private async Task LoadRoomsAsync(bool silent = false)
    {
        if (_busy || DetailCard.Visibility == Visibility.Visible) return;
        _busy = true;
        if (!silent) LobbyStatus.Text = "正在刷新...";
        try
        {
            _rooms = await _lobby.GetRoomsAsync();
            RenderRooms();
        }
        catch
        {
            LobbyStatus.Text = "中继服务器不可达";
        }
        finally
        {
            _busy = false;
        }
    }

    // 按房间号搜索结果，重绘列表
    private void RenderRooms()
    {
        var keyword = SearchBox.Text.Trim();
        var filtered = string.IsNullOrEmpty(keyword)
            ? _rooms
            : _rooms.Where(room => room.Room.Contains(keyword, StringComparison.Ordinal)).ToList();

        RoomList.Children.Clear();
        foreach (var room in filtered)
            RoomList.Children.Add(BuildRoomCard(room));

        EmptyHint.Text = _rooms.Count == 0
            ? "联机大厅暂无联机房间"
            : "没有找到该房间号";
        EmptyHint.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        LobbyStatus.Text = _rooms.Count == 0 ? "" : $"{filtered.Count}/{_rooms.Count} 个房间";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || LobbyRoot.Visibility != Visibility.Visible) return;
        RenderRooms();
    }

    private Border BuildRoomCard(LobbyRoom room)
    {
        var card = new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = new CornerRadius(10),
            Cursor = Cursors.Hand
        };
        card.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        card.BorderThickness = new Thickness(1);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();
        var name = new TextBlock { Text = room.TitleText, FontSize = 14, FontWeight = FontWeights.SemiBold };
        name.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        left.Children.Add(name);

        var info = new TextBlock { FontSize = 11, Margin = new Thickness(0, 4, 0, 0) };
        info.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        info.Inlines.Add(new System.Windows.Documents.Run(
            $"{room.KindText} · {room.KindDetail} · MC {room.Mc} · {room.PlayerText}"
            + (room.Locked ? " · 🔒需密码" : "")));
        left.Children.Add(info);
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var code = new TextBlock
        {
            Text = room.Room,
            FontSize = 15,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        code.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");
        right.Children.Add(code);

        var addr = new TextBlock
        {
            Text = room.AddrText,
            FontSize = 11,
            FontFamily = new FontFamily("Consolas"),
            Margin = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        addr.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
        right.Children.Add(addr);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        card.Child = grid;
        card.MouseLeftButtonUp += (_, _) => ShowDetail(room);
        return card;
    }

    private void ShowDetail(LobbyRoom room)
    {
        _selected = room;
        RoomList.Visibility = Visibility.Collapsed;
        EmptyHint.Visibility = Visibility.Collapsed;
        LobbyStatus.Text = "";

        DetailCard.Visibility = Visibility.Visible;
        DetailName.Text = room.TitleText;

        // 第一行：房间版本 + 房主玩家 ID
        DetailMeta.Text = $"版本 {room.Mc}　玩家ID {room.OwnerText}\n"
                          + $"{room.KindText} · {room.KindDetail} · {room.PlayerText}";

        // 第二行：连接地址
        if (room.ConnectAddress is { Length: > 0 })
        {
            DetailAddrText.Text = $"连接地址 {room.ConnectAddress}";
            DetailAddrText.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");
            CopyAddrBtn.Content = "复制 IP";
            CopyAddrBtn.Visibility = Visibility.Visible;
        }
        else
        {
            DetailAddrText.Text = "密码房：点「加入房间」由启动器自动进入";
            DetailAddrText.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            CopyAddrBtn.Visibility = Visibility.Collapsed;
        }

        // 第三行：Mod 清单
        if (room.Mods.Count > 0)
        {
            var preview = string.Join("\n", room.Mods.Take(20).Select(mod => $"· {mod.Name}  ({mod.SizeText})"));
            if (room.Mods.Count > 20) preview += $"\n… 等共 {room.Mods.Count} 个";
            DetailMods.Text = $"Mod 清单（{room.Mods.Count}）：\n" + preview;
        }
        else
        {
            DetailMods.Text = room.Kind == "modded"
                ? $"加载器 {room.KindDetail}（房主未上报清单）"
                : "原版房间，无需同步 Mod";
        }

        PasswordLabel.Visibility = room.Locked ? Visibility.Visible : Visibility.Collapsed;
        PasswordBox.Visibility = room.Locked ? Visibility.Visible : Visibility.Collapsed;
        PasswordBox.Text = "";
        JoinHint.Text = "";
        JoinBtn.IsEnabled = true;
    }

    private void CopyAddr_Click(object sender, RoutedEventArgs e)
    {
        if (_selected?.ConnectAddress is not { Length: > 0 } address) return;
        try
        {
            Clipboard.SetText(address);
            CopyAddrBtn.Content = "已复制";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                CopyAddrBtn.Content = "复制 IP";
            };
            timer.Start();
        }
        catch
        {
            JoinHint.Text = "复制失败，请手动选择地址";
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        DetailCard.Visibility = Visibility.Collapsed;
        RoomList.Visibility = Visibility.Visible;
        _ = LoadRoomsAsync();
    }

    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null || _busy) return;
        var room = _selected;
        if (room.Locked && string.IsNullOrWhiteSpace(PasswordBox.Text))
        {
            JoinHint.Text = "该房间需要联机密码";
            return;
        }

        _busy = true;
        JoinBtn.IsEnabled = false;
        JoinHint.Text = "正在加入...";
        try
        {
            var result = await _lobby.JoinAsync(room.Room, PasswordBox.Text.Trim());
            if (!result.Ok)
            {
                JoinHint.Text = result.Message;
                return;
            }

            if (!_lobby.StartProxy(result))
            {
                JoinHint.Text = "本地代理启动失败";
                return;
            }

            // Mod 自动同步：与房主清单比对并下载缺失项
            var syncText = "";
            if (room.Mods.Count > 0)
            {
                var instance = (Window.GetWindow(this) as MainWindow)?.HomePage?.SelectedInstance;
                var gameDir = instance != null
                    ? InstancePathService.GetGameDirectory(App.Paths, App.Settings.Data, instance)
                    : App.Paths.MinecraftDir;
                var progress = new Progress<string>(text => JoinHint.Text = text);
                syncText = await _lobby.SyncModsAsync(room.Mods, gameDir, result.Mc, result.Loader, progress);
            }

            // 一键启动并加入：公开房直接连中继地址；密码房走本地代理（令牌握手）
            QuickPlayRequest.Address = room.ConnectAddress is { Length: > 0 }
                ? room.ConnectAddress
                : $"127.0.0.1:{_lobby.LocalPort}";
            JoinHint.Text = $"{(string.IsNullOrEmpty(syncText) ? "" : syncText + "；")}正在启动游戏并加入房主的世界…";

            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.StartQuickPlay();
        }
        catch (Exception ex)
        {
            JoinHint.Text = "加入失败：" + ex.Message;
        }
        finally
        {
            _busy = false;
            JoinBtn.IsEnabled = true;
        }
    }
}
