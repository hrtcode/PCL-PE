'PCLPE：基于 GitHub Releases 的自动更新

Imports System.IO
Imports System.Text
Imports Newtonsoft.Json.Linq

Public Module ModUpdate

    ''' <summary>
    ''' 更新仓库，格式 "owner/repo"（例如 "PCLPE-Team/PCLPE"）。
    ''' 留空则不检查更新；也可以用环境变量 PCLPE_UPDATE_REPO 临时覆盖。
    ''' </summary>
    Public Const UpdateRepoFallback As String = "hrtcode/PCL-PE"

    Public ReadOnly Property UpdateRepo As String
        Get
            Dim FromEnv As String = Environment.GetEnvironmentVariable("PCLPE_UPDATE_REPO")
            If Not String.IsNullOrEmpty(FromEnv) Then Return FromEnv.Trim()
            Return UpdateRepoFallback
        End Get
    End Property

    ''' <summary>
    ''' PCLPE 自己的版本号。与 GitHub Release 的 tag 比较（tag 可带 v 前缀，如 v1.2.0）。
    ''' 发版时记得同步改这里。
    ''' </summary>
    Public Const PeVersion As String = "1.0.0"

    ''' <summary>
    ''' 检查更新。Manual 为 True 时用户主动点击，会在没有更新/没配置仓库时给出提示。
    ''' </summary>
    Public Sub CheckUpdate(Manual As Boolean)
        Dim Repo As String = UpdateRepo
        If String.IsNullOrEmpty(Repo) Then
            If Manual Then Hint("还没有配置更新仓库。" & vbCrLf & "请在 ModUpdate.vb 里填写 owner/repo，或设置环境变量 PCLPE_UPDATE_REPO。")
            Return
        End If
        Try
            If Manual Then Hint("正在检查更新……")
            Dim Headers = New String(,) {{"Accept", "application/vnd.github+json"}, {"User-Agent", "PCLPE"}}
            Dim Json As String = NetRequestByClient($"https://api.github.com/repos/{Repo}/releases/latest",
                                                    Headers:=Headers, Timeout:=15000, RequireJson:=True)
            Dim Release As JObject = JObject.Parse(Json)
            Dim Tag As String = If(Release("tag_name")?.ToString(), "")
            If String.IsNullOrEmpty(Tag) Then Throw New Exception("Release 中没有 tag_name")

            Dim NewVersion As Version = ParseVersion(Tag)
            Dim CurrentVersion As Version = ParseVersion(PeVersion)
            If Not Manual AndAlso NewVersion <= CurrentVersion Then Return

            '找 exe 附件
            Dim DownloadUrl As String = Nothing
            Dim Assets = TryCast(Release("assets"), JArray)
            If Assets IsNot Nothing Then
                For Each Asset In Assets
                    Dim AssetName As String = If(Asset("name")?.ToString(), "")
                    If AssetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) Then
                        DownloadUrl = Asset("browser_download_url")?.ToString()
                        Exit For
                    End If
                Next
            End If
            If String.IsNullOrEmpty(DownloadUrl) Then
                Throw New Exception("该 Release 里没有找到 .exe 附件，请在 GitHub 上把编译好的 PCLPE.exe 上传到 Release")
            End If

            Dim Body As String = If(Release("body")?.ToString(), "")
            If Body.Length > 400 Then Body = Body.Substring(0, 400) & "……"
            Dim Message As String = $"发现新版本 {Tag}（当前 {PeVersion}）。{vbCrLf}{vbCrLf}{Body}{vbCrLf}{vbCrLf}是否现在更新？更新后会自动重启。"
            If MyMsgBox(Message, "PCLPE 更新", "立即更新", "稍后", IsWarn:=True) <> 1 Then Return

            '下载
            Dim NewExe As String = Path.Combine(PathTemp, "PCLPE_Update.exe")
            If File.Exists(NewExe) Then File.Delete(NewExe)
            Logger.Info($"[Update] 开始下载 {DownloadUrl}")
            NetDownloadByClient(DownloadUrl, NewExe)
            If Not File.Exists(NewExe) OrElse New FileInfo(NewExe).Length < 1024 Then Throw New Exception("下载的更新文件异常")
            Logger.Info($"[Update] 下载完成，{New FileInfo(NewExe).Length} 字节，准备替换")
            ApplyUpdate(NewExe)
        Catch ex As Exception
            Logger.Error(ex, "[Update] 检查更新失败")
            If Manual Then
                Hint($"无法从 {Repo} 获取更新信息（可能是还没有发布 Release，或网络问题）。" & vbCrLf & "详情见日志：{ex.Message}", HintType.Red)
            End If
        End Try
    End Sub

    ''' <summary>
    ''' 启动时静默检查，每天最多一次。
    ''' </summary>
    Public Sub AutoCheckUpdate()
        Try
            If String.IsNullOrEmpty(UpdateRepo) Then Return
            Dim Today As String = DateTime.Now.ToString("yyyy-MM-dd")
            If Settings.Get(Of String)("PeUpdateLastCheck") = Today Then Return
            Settings.Set("PeUpdateLastCheck", Today)
            RunInNewThread(Sub()
                               CheckUpdate(False)
                           End Sub, "自动检查更新", ThreadPriority.BelowNormal)
        Catch ex As Exception
            Logger.Warn(ex, "[Update] 自动检查更新失败")
        End Try
    End Sub

    ''' <summary>
    ''' 用批处理等待本进程退出后替换 exe 并重启。
    ''' </summary>
    Private Sub ApplyUpdate(NewExe As String)
        Dim BatFile As String = Path.Combine(PathTemp, "PCLPE_Update.bat")
        Dim Lines As New List(Of String) From {
            "@echo off",
            "timeout /t 2 /nobreak >nul",
            "move /y """ & PathExe & """ """ & PathExe & ".old"" >nul 2>&1",
            "move /y """ & NewExe & """ """ & PathExe & """ >nul 2>&1",
            "del """ & PathExe & ".old"" >nul 2>&1",
            "start """" """ & PathExe & """",
            "del ""%~f0"""
        }
        File.WriteAllText(BatFile, String.Join(vbCrLf, Lines), Encoding.Default)
        System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(BatFile) With {
            .WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            .UseShellExecute = True})
        FrmMain.EndProgram(False)
    End Sub

    ''' <summary>
    ''' 把 "v1.2.3" / "1.2.3-beta" 解析成可比较的 Version。
    ''' </summary>
    Private Function ParseVersion(Text As String) As Version
        If String.IsNullOrEmpty(Text) Then Return New Version(0, 0)
        Dim Clean As String = Text.Trim().TrimStart("v"c, "V"c)
        Dim Parts As String() = Clean.Split("."c)
        Dim Numbers As Integer() = {0, 0, 0, 0}
        For i = 0 To Math.Min(3, Parts.Length - 1)
            Dim Digits As New List(Of Char)
            For Each Ch In Parts(i)
                If [Char].IsDigit(Ch) Then Digits.Add(Ch) Else Exit For
            Next
            Dim Value As Integer
            Integer.TryParse(New String(Digits.ToArray()), Value)
            Numbers(i) = Value
        Next
        Return New Version(Numbers(0), Numbers(1), Numbers(2), Numbers(3))
    End Function

End Module
