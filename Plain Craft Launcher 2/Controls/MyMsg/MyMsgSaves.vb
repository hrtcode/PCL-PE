'PCLPE：存档浏览窗口（显示世界名、版本、最后游玩、数据包数量，可打开文件夹）

Imports System.IO
Imports System.Windows.Media.Imaging

Public Class MyMsgSaves
    Inherits Grid

    Private ReadOnly Uuid As Integer = GetUuid()
    Private ReadOnly Saves As List(Of SaveInfo)
    Private ReadOnly Card As Border

    Public Sub New(Saves As List(Of SaveInfo), InstanceName As String)
        Me.Saves = If(Saves, New List(Of SaveInfo))
        Background = New SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
        HorizontalAlignment = HorizontalAlignment.Stretch
        VerticalAlignment = VerticalAlignment.Stretch

        '卡片
        Card = New Border With {
            .Background = New SolidColorBrush(Color.FromArgb(250, 255, 255, 255)),
            .CornerRadius = New CornerRadius(12),
            .Width = 760,
            .MaxHeight = 640,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center,
            .Padding = New Thickness(0),
            .Effect = New System.Windows.Media.Effects.DropShadowEffect With {
                .BlurRadius = 30, .ShadowDepth = 8, .Opacity = 0.28, .Color = Colors.Black}
        }
        Children.Add(Card)

        Dim Root As New Grid
        Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Auto)})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Auto)})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Auto)})
        Card.Child = Root

        '标题
        Dim Title As New TextBlock With {
            .Text = $"存档 · {InstanceName}（{Saves.Count} 个）",
            .FontSize = 22,
            .FontWeight = FontWeights.Bold,
            .Margin = New Thickness(28, 22, 28, 0)
        }
        Title.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Grid.SetRow(Title, 0)
        Root.Children.Add(Title)

        '分隔线
        Dim Line As New Border With {
            .Height = 1,
            .Margin = New Thickness(28, 14, 28, 0),
            .Background = New SolidColorBrush(Color.FromArgb(28, 0, 0, 0))
        }
        Grid.SetRow(Line, 1)
        Root.Children.Add(Line)

        '列表
        Dim ListPanel As New StackPanel With {.Margin = New Thickness(20, 12, 20, 8)}
        If Saves.Count = 0 Then
            Dim Empty As New TextBlock With {
                .Text = "这个版本还没有存档。启动一次游戏并创建世界后，存档会出现在这里。",
                .FontSize = 14,
                .Opacity = 0.6,
                .TextWrapping = TextWrapping.Wrap,
                .Margin = New Thickness(10, 20, 10, 24)
            }
            Empty.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            ListPanel.Children.Add(Empty)
        Else
            For Each Save In Saves
                ListPanel.Children.Add(BuildRow(Save))
            Next
        End If
        Dim Scroll As New ScrollViewer With {
            .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            .Content = ListPanel
        }
        Grid.SetRow(Scroll, 2)
        Root.Children.Add(Scroll)

        '底部按钮
        Dim Footer As New StackPanel With {
            .Orientation = Orientation.Horizontal,
            .HorizontalAlignment = HorizontalAlignment.Right,
            .Margin = New Thickness(28, 10, 28, 22)
        }
        Dim BtnOpenFolder As New MyButton With {.Text = "打开存档文件夹", .Height = 32, .MinWidth = 120, .Margin = New Thickness(0, 0, 10, 0)}
        AddHandler BtnOpenFolder.Click,
            Sub()
                Dim Folder As String = If(Saves.Count > 0,
                    Directory.GetParent(Saves(0).Folder).FullName,
                    ModSaves.GetSavesFolder())
                Directory.CreateDirectory(Folder)
                OpenExplorer(Folder)
            End Sub
        Dim BtnClose As New MyButton With {.Text = "关闭", .Height = 32, .MinWidth = 90, .ColorType = MyButton.ColorState.Highlight}
        AddHandler BtnClose.Click, Sub() CloseSelf()
        Footer.Children.Add(BtnOpenFolder)
        Footer.Children.Add(BtnClose)
        Grid.SetRow(Footer, 3)
        Root.Children.Add(Footer)
    End Sub

    ''' <summary>
    ''' 一行存档。
    ''' </summary>
    Private Function BuildRow(Save As SaveInfo) As UIElement
        Dim RowBorder As New Border With {
            .CornerRadius = New CornerRadius(8),
            .Padding = New Thickness(10, 8, 10, 8),
            .Margin = New Thickness(0, 2, 0, 2),
            .Background = Brushes.Transparent
        }
        Dim Grid_ As New Grid
        Grid_.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(56)})
        Grid_.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid_.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        RowBorder.Child = Grid_

        '图标
        Dim Icon As New Border With {
            .Width = 44, .Height = 44,
            .CornerRadius = New CornerRadius(8),
            .Background = New SolidColorBrush(Color.FromArgb(24, 0, 0, 0)),
            .ClipToBounds = True,
            .VerticalAlignment = VerticalAlignment.Center
        }
        If Save.IconPath IsNot Nothing Then
            Try
                Icon.Background = New ImageBrush(New BitmapImage(New Uri(Save.IconPath))) With {.Stretch = Stretch.UniformToFill}
            Catch
            End Try
        End If
        Grid.SetColumn(Icon, 0)
        Grid_.Children.Add(Icon)

        '文字
        Dim TextStack As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(10, 0, 10, 0)}
        Dim NameText As New TextBlock With {.Text = Save.Name, .FontSize = 15, .FontWeight = FontWeights.SemiBold, .TextTrimming = TextTrimming.CharacterEllipsis}
        NameText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Dim InfoText As New TextBlock With {
            .Text = $"{If(String.IsNullOrEmpty(Save.VersionName), "未知版本", Save.VersionName)} · " &
                    $"{If(Save.LastPlayed, DateTime.MinValue):yyyy-MM-dd HH:mm} · {Save.DataPackCount} 个数据包",
            .FontSize = 12,
            .Opacity = 0.62,
            .Margin = New Thickness(0, 3, 0, 0)
        }
        InfoText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        TextStack.Children.Add(NameText)
        TextStack.Children.Add(InfoText)
        Grid.SetColumn(TextStack, 1)
        Grid_.Children.Add(TextStack)

        '打开按钮
        Dim BtnOpen As New MyButton With {.Text = "打开", .Height = 28, .MinWidth = 64}
        AddHandler BtnOpen.Click, Sub() OpenExplorer(Save.Folder & "\")
        Grid.SetColumn(BtnOpen, 2)
        Grid_.Children.Add(BtnOpen)

        '悬停高亮
        AddHandler RowBorder.MouseEnter, Sub() RowBorder.Background = New SolidColorBrush(Color.FromArgb(14, 0, 0, 0))
        AddHandler RowBorder.MouseLeave, Sub() RowBorder.Background = Brushes.Transparent
        Return RowBorder
    End Function

    ''' <summary>
    ''' 挂到主窗口的弹窗层并播放入场动画。
    ''' </summary>
    Public Sub ShowDialog()
        FrmMain.PanMsg.Children.Add(Me)
        FrmMain.PanMsg.Visibility = Visibility.Visible
        Opacity = 0
        AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(90, 0, 0, 0) - FrmMain.PanMsg.Background, 180), "PanMsg Background")
        AniStart(AaOpacity(Me, 1, 140, 40), "MyMsgSaves " & Uuid)
        Logger.Info($"[Saves] 打开存档浏览：{Saves.Count} 个")
    End Sub

    Private Sub CloseSelf()
        AniStart({
            AaOpacity(Me, -Opacity, 90, 10),
            AaCode(
            Sub()
                CType(Parent, Grid).Children.Remove(Me)
                If Not CType(Parent, Grid).Children.OfType(Of MyMsgSaves).Any() Then
                    AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsg.Background, 160))
                    RunInUi(Sub()
                                If FrmMain.PanMsg.Children.Count = 0 Then FrmMain.PanMsg.Visibility = Visibility.Collapsed
                            End Sub, True)
                End If
            End Sub, , True)
        }, "MyMsgSaves " & Uuid)
    End Sub

End Class
