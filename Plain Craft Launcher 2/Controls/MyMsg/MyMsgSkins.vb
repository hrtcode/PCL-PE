'PCLPE：皮肤库（浏览本地皮肤、导入、打开文件夹）

Imports System.IO
Imports System.Windows.Media.Imaging

Public Class MyMsgSkins
    Inherits Grid

    Private ReadOnly Uuid As Integer = GetUuid()
    Private ReadOnly ListPanel As WrapPanel
    Private ReadOnly TitleText As TextBlock

    ''' <summary>
    ''' 皮肤库文件夹：PCL\Skins\
    ''' </summary>
    Public Shared ReadOnly Property LibraryFolder As String
        Get
            Return Paths.AppDataThenName & "Skins\"
        End Get
    End Property

    Public Sub New()
        Background = New SolidColorBrush(Color.FromArgb(0, 0, 0, 0))

        Dim Card As New Border With {
            .Background = New SolidColorBrush(Color.FromArgb(250, 255, 255, 255)),
            .CornerRadius = New CornerRadius(12),
            .Width = 820,
            .MaxHeight = 660,
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center,
            .Effect = New System.Windows.Media.Effects.DropShadowEffect With {.BlurRadius = 30, .ShadowDepth = 8, .Opacity = 0.28, .Color = Colors.Black}
        }
        Children.Add(Card)

        Dim Root As New Grid
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})
        Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        Card.Child = Root

        TitleText = New TextBlock With {
            .FontSize = 22, .FontWeight = FontWeights.Bold,
            .Margin = New Thickness(28, 22, 28, 0)
        }
        TitleText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Grid.SetRow(TitleText, 0)
        Root.Children.Add(TitleText)

        Dim Line As New Border With {
            .Height = 1,
            .Margin = New Thickness(28, 14, 28, 0),
            .Background = New SolidColorBrush(Color.FromArgb(28, 0, 0, 0))
        }
        Grid.SetRow(Line, 1)
        Root.Children.Add(Line)

        ListPanel = New WrapPanel With {.Margin = New Thickness(22, 14, 22, 10)}
        Dim Scroll As New ScrollViewer With {
            .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            .Content = ListPanel
        }
        Grid.SetRow(Scroll, 2)
        Root.Children.Add(Scroll)

        Dim Footer As New StackPanel With {
            .Orientation = Orientation.Horizontal,
            .HorizontalAlignment = HorizontalAlignment.Right,
            .Margin = New Thickness(28, 10, 28, 22)
        }
        Dim BtnImport As New MyButton With {.Text = "导入皮肤", .Height = 32, .MinWidth = 96, .Margin = New Thickness(0, 0, 10, 0)}
        AddHandler BtnImport.Click, Sub() ImportSkins()
        Dim BtnFolder As New MyButton With {.Text = "打开皮肤文件夹", .Height = 32, .MinWidth = 120, .Margin = New Thickness(0, 0, 10, 0)}
        AddHandler BtnFolder.Click,
            Sub()
                Directory.CreateDirectory(LibraryFolder)
                OpenExplorer(LibraryFolder)
            End Sub
        Dim BtnClose As New MyButton With {.Text = "关闭", .Height = 32, .MinWidth = 90, .ColorType = MyButton.ColorState.Highlight}
        AddHandler BtnClose.Click, Sub() CloseSelf()
        Footer.Children.Add(BtnImport)
        Footer.Children.Add(BtnFolder)
        Footer.Children.Add(BtnClose)
        Grid.SetRow(Footer, 3)
        Root.Children.Add(Footer)

        RefreshList()
    End Sub

    ''' <summary>
    ''' 重新扫描皮肤库。
    ''' </summary>
    Private Sub RefreshList()
        ListPanel.Children.Clear()
        Directory.CreateDirectory(LibraryFolder)
        Dim Files = Directory.GetFiles(LibraryFolder, "*.png").OrderBy(Function(f) f).ToList()
        TitleText.Text = $"皮肤库（{Files.Count} 个）"
        If Files.Count = 0 Then
            Dim Empty As New TextBlock With {
                .Text = "皮肤库还是空的。点「导入皮肤」选择 64×64（或旧版 64×32）的 PNG 皮肤文件。",
                .FontSize = 14, .Opacity = 0.6, .TextWrapping = TextWrapping.Wrap,
                .Width = 740, .Margin = New Thickness(6, 16, 6, 16)
            }
            Empty.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
            ListPanel.Children.Add(Empty)
            Return
        End If
        For Each File In Files
            ListPanel.Children.Add(BuildTile(File))
        Next
    End Sub

    ''' <summary>
    ''' 一个皮肤格子：头部预览 + 文件名。
    ''' </summary>
    Private Function BuildTile(File As String) As UIElement
        Dim Stack As New StackPanel With {
            .Width = 96, .Margin = New Thickness(6), .Cursor = Cursors.Hand
        }
        Dim Frame As New Border With {
            .Width = 84, .Height = 84, .CornerRadius = New CornerRadius(10),
            .Background = New SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
            .HorizontalAlignment = HorizontalAlignment.Center
        }
        Try
            Dim Full As BitmapSource = LoadPng(File)
            Dim HeadRect As New Int32Rect(8, 8, 8, 8)
            If Full.PixelWidth >= 16 AndAlso Full.PixelHeight >= 16 Then
                Dim Head As New CroppedBitmap(Full, HeadRect)
                Dim Img As New Image With {
                    .Source = Head, .Width = 72, .Height = 72,
                    .Stretch = Stretch.Uniform,
                    .Margin = New Thickness(6)
                }
                RenderOptions.SetBitmapScalingMode(Img, BitmapScalingMode.NearestNeighbor)
                Frame.Child = Img
            End If
        Catch
            '读不出来的文件就当占位
        End Try
        Stack.Children.Add(Frame)
        Dim Name As New TextBlock With {
            .Text = Path.GetFileNameWithoutExtension(File),
            .FontSize = 11, .Opacity = 0.75,
            .TextTrimming = TextTrimming.CharacterEllipsis,
            .TextAlignment = TextAlignment.Center,
            .Margin = New Thickness(0, 6, 0, 0)
        }
        Name.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1")
        Stack.Children.Add(Name)
        AddHandler Stack.MouseEnter, Sub() Frame.Background = New SolidColorBrush(Color.FromArgb(26, 60, 130, 255))
        AddHandler Stack.MouseLeave, Sub() Frame.Background = New SolidColorBrush(Color.FromArgb(20, 0, 0, 0))
        AddHandler Stack.MouseLeftButtonUp, Sub() OpenExplorer(File)
        Stack.ToolTip = "点击打开所在文件夹"
        Return Stack
    End Function

    Private Shared Function LoadPng(File As String) As BitmapSource
        Dim Img As New BitmapImage()
        Img.BeginInit()
        Img.CacheOption = BitmapCacheOption.OnLoad
        Img.CreateOptions = BitmapCreateOptions.IgnoreImageCache
        Img.UriSource = New Uri(File)
        Img.EndInit()
        Img.Freeze()
        Return Img
    End Function

    ''' <summary>
    ''' 导入皮肤文件到皮肤库。
    ''' </summary>
    Private Sub ImportSkins()
        Dim Files = Dialogs.SelectFile("选择皮肤图片（64×64 或 64×32 的 PNG）", True, filter:={({"png"}, "皮肤图片")})
        If Files Is Nothing OrElse Not Files.Any() Then Return
        Dim Success As Integer = 0
        For Each SkinFile In Files
            Try
                Dim Img = LoadPng(SkinFile)
                If Img.PixelWidth < 64 OrElse Img.PixelHeight < 32 Then
                    Hint($"{Path.GetFileName(SkinFile)} 尺寸不对（需要 64×64 或 64×32）", HintType.Red)
                    Continue For
                End If
                Dim Target As String = Path.Combine(LibraryFolder, Path.GetFileName(SkinFile))
                Dim Index As Integer = 1
                While IO.File.Exists(Target)
                    Target = Path.Combine(LibraryFolder, $"{Path.GetFileNameWithoutExtension(SkinFile)}_{Index}.png")
                    Index += 1
                End While
                IO.File.Copy(SkinFile, Target)
                Success += 1
            Catch ex As Exception
                Logger.Warn(ex, $"导入皮肤失败：{SkinFile}")
            End Try
        Next
        RefreshList()
        If Success > 0 Then Hint($"已导入 {Success} 个皮肤")
    End Sub

    Public Sub ShowDialog()
        FrmMain.PanMsg.Children.Add(Me)
        FrmMain.PanMsg.Visibility = Visibility.Visible
        Opacity = 0
        AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(90, 0, 0, 0) - FrmMain.PanMsg.Background, 180), "PanMsg Background")
        AniStart(AaOpacity(Me, 1, 140, 40), "MyMsgSkins " & Uuid)
        Logger.Info($"[Skins] 打开皮肤库：{LibraryFolder}")
    End Sub

    Private Sub CloseSelf()
        AniStart({
            AaOpacity(Me, -Opacity, 90, 10),
            AaCode(
            Sub()
                CType(Parent, Grid).Children.Remove(Me)
                RunInUi(Sub()
                            If FrmMain.PanMsg.Children.Count = 0 Then FrmMain.PanMsg.Visibility = Visibility.Collapsed
                        End Sub, True)
            End Sub, , True)
        }, "MyMsgSkins " & Uuid)
    End Sub

End Class
