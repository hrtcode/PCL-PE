'PCLPE：数据包识别、版本匹配与一键安装

Imports System.IO
Imports System.IO.Compression
Imports Newtonsoft.Json.Linq

''' <summary>
''' 一个数据包的信息。
''' </summary>
Public Class DataPackInfo
    ''' <summary>
    ''' 数据包 zip 路径。
    ''' </summary>
    Public Property Path As String
    ''' <summary>
    ''' 数据包名称（zip 文件名，不含扩展名）。
    ''' </summary>
    Public Property Name As String
    ''' <summary>
    ''' pack.mcmeta 里的 pack_format；读不到时为 -1。
    ''' </summary>
    Public Property PackFormat As Integer = -1
    ''' <summary>
    ''' supported_formats 支持的最小/最大格式；未声明时等于 PackFormat。
    ''' </summary>
    Public Property MinFormat As Integer = -1
    Public Property MaxFormat As Integer = -1
    ''' <summary>
    ''' 兼容的 MC 版本描述，例如 "1.21 ~ 1.21.1"。
    ''' </summary>
    Public Property VersionHint As String = "未知"
End Class

Public Module ModDataPack

    ''' <summary>
    ''' pack_format 与 MC 版本的对照表。数值来自各版本资源包/数据包格式版本。
    ''' </summary>
    Private ReadOnly FormatTable As New List(Of KeyValuePair(Of Integer, String)) From {
        New KeyValuePair(Of Integer, String)(4, "1.13 ~ 1.14.4"),
        New KeyValuePair(Of Integer, String)(5, "1.15 ~ 1.16.1"),
        New KeyValuePair(Of Integer, String)(6, "1.16.2 ~ 1.16.5"),
        New KeyValuePair(Of Integer, String)(7, "1.17 ~ 1.17.1"),
        New KeyValuePair(Of Integer, String)(8, "1.18 ~ 1.18.2"),
        New KeyValuePair(Of Integer, String)(9, "1.19 ~ 1.19.2"),
        New KeyValuePair(Of Integer, String)(10, "1.19.3"),
        New KeyValuePair(Of Integer, String)(12, "1.19.4"),
        New KeyValuePair(Of Integer, String)(15, "1.20 ~ 1.20.1"),
        New KeyValuePair(Of Integer, String)(18, "1.20.2"),
        New KeyValuePair(Of Integer, String)(26, "1.20.3 ~ 1.20.4"),
        New KeyValuePair(Of Integer, String)(41, "1.20.5 ~ 1.20.6"),
        New KeyValuePair(Of Integer, String)(48, "1.21 ~ 1.21.1"),
        New KeyValuePair(Of Integer, String)(57, "1.21.2 ~ 1.21.3"),
        New KeyValuePair(Of Integer, String)(61, "1.21.4"),
        New KeyValuePair(Of Integer, String)(71, "1.21.5")
    }

    ''' <summary>
    ''' 判断一个 zip 是不是数据包（含 pack.mcmeta 或 data/ 目录）。
    ''' </summary>
    Public Function IsDataPack(ZipPath As String) As Boolean
        Try
            Using Zip As ZipArchive = ZipFile.OpenRead(ZipPath)
                For Each Entry In Zip.Entries
                    If Entry.FullName.EndsWith("pack.mcmeta", StringComparison.OrdinalIgnoreCase) Then Return True
                    If Entry.FullName.StartsWith("data/", StringComparison.OrdinalIgnoreCase) Then Return True
                Next
            End Using
        Catch ex As Exception
            Logger.Warn(ex, $"读取压缩包失败：{ZipPath}")
        End Try
        Return False
    End Function

    ''' <summary>
    ''' 读取数据包的 pack.mcmeta。
    ''' </summary>
    Public Function ReadDataPack(ZipPath As String) As DataPackInfo
        Dim Info As New DataPackInfo With {.Path = ZipPath, .Name = Path.GetFileNameWithoutExtension(ZipPath)}
        Try
            Using Zip As ZipArchive = ZipFile.OpenRead(ZipPath)
                Dim MetaEntry = Zip.Entries.FirstOrDefault(Function(e) e.FullName.EndsWith("pack.mcmeta", StringComparison.OrdinalIgnoreCase))
                If MetaEntry Is Nothing Then Return Info
                Using Reader As New StreamReader(MetaEntry.Open())
                    Dim Json As JObject = JObject.Parse(Reader.ReadToEnd())
                    Dim Pack = TryCast(Json("pack"), JObject)
                    If Pack Is Nothing Then Return Info
                    If Pack("pack_format") IsNot Nothing Then Info.PackFormat = Pack("pack_format").ToObject(Of Integer)()
                    Info.MinFormat = Info.PackFormat
                    Info.MaxFormat = Info.PackFormat
                    Dim Supported = Pack("supported_formats")
                    If Supported IsNot Nothing Then
                        If TypeOf Supported Is JArray Then
                            Info.MinFormat = Supported(0).ToObject(Of Integer)()
                            Info.MaxFormat = Supported(1).ToObject(Of Integer)()
                        ElseIf TypeOf Supported Is JObject Then
                            Info.MinFormat = Supported("min_inclusive").ToObject(Of Integer)()
                            Info.MaxFormat = Supported("max_inclusive").ToObject(Of Integer)()
                        End If
                    End If
                End Using
            End Using
        Catch ex As Exception
            Logger.Warn(ex, $"读取数据包信息失败：{ZipPath}")
        End Try
        Info.VersionHint = GetVersionHint(Info.MinFormat, Info.MaxFormat)
        Return Info
    End Function

    ''' <summary>
    ''' 把 pack_format 区间翻译成 MC 版本描述。
    ''' </summary>
    Public Function GetVersionHint(MinFormat As Integer, MaxFormat As Integer) As String
        If MinFormat <= 0 Then Return "未知"
        Dim Names As New List(Of String)
        For Each Pair In FormatTable
            If Pair.Key >= MinFormat AndAlso Pair.Key <= MaxFormat Then Names.Add(Pair.Value)
        Next
        If Names.Count = 0 Then Return $"未知格式（{MinFormat} ~ {MaxFormat}）"
        Return String.Join(" / ", Names)
    End Function

    ''' <summary>
    ''' 把数据包装进存档的 datapacks 文件夹。
    ''' </summary>
    Public Function InstallToSave(ZipPath As String, Save As SaveInfo) As String
        Dim TargetFolder As String = Save.DataPackFolder
        If Not Directory.Exists(TargetFolder) Then Directory.CreateDirectory(TargetFolder)
        Dim Target As String = Path.Combine(TargetFolder, Path.GetFileName(ZipPath))
        Dim Index As Integer = 1
        While File.Exists(Target)
            Target = Path.Combine(TargetFolder, $"{Path.GetFileNameWithoutExtension(ZipPath)}_{Index}{Path.GetExtension(ZipPath)}")
            Index += 1
        End While
        File.Copy(ZipPath, Target)
        Logger.Info($"[DataPack] 已安装到存档「{Save.Name}」：{Target}")
        Return Target
    End Function

    ''' <summary>
    ''' 从"数据包"页面发起的本地安装：选文件 → 选版本 → 选存档。
    ''' </summary>
    Public Sub StartLocalInstall()
        Dim File = Dialogs.SelectFile("选择数据包文件", False, filter:={({"zip"}, "数据包压缩包")}).FirstOrDefault()
        If String.IsNullOrEmpty(File) Then Return
        InstallDataPackFlow(File)
    End Sub

    ''' <summary>
    ''' 可选的 MC 实例列表（已排除读取失败的版本）。
    ''' </summary>
    Private Function GetInstanceChoices() As List(Of McInstance)
        Try
            If McInstanceList Is Nothing OrElse Not McInstanceList.Any(Function(kv) kv.Value IsNot Nothing AndAlso kv.Value.Any) Then
                McInstanceListLoader.Start(Nothing)
                McInstanceListLoader.WaitForExit()
            End If
            If McInstanceList Is Nothing Then Return New List(Of McInstance)
            Return McInstanceList.Values.
                Where(Function(l) l IsNot Nothing).
                SelectMany(Function(l) l).
                Where(Function(i) i IsNot Nothing AndAlso i.State <> McInstance.McInstanceState.Error).
                OrderBy(Function(i) i.Name).
                ToList()
        Catch ex As Exception
            Logger.Warn(ex, "获取版本列表失败")
            Return New List(Of McInstance)
        End Try
    End Function

    ''' <summary>
    ''' 完整的"一键安装数据包"流程：识别 → 选版本 → 选存档 → 安装。
    ''' </summary>
    Public Sub InstallDataPackFlow(ZipPath As String)
        If Not File.Exists(ZipPath) Then
            Hint($"找不到文件：{ZipPath}", HintType.Red)
            Return
        End If
        If Not IsDataPack(ZipPath) Then
            Hint("这个压缩包里没有 pack.mcmeta 或 data 文件夹，看起来不是数据包。", HintType.Red)
            Return
        End If
        Dim Info = ReadDataPack(ZipPath)

        '先选版本
        Dim Instances = GetInstanceChoices()
        If Instances.Count = 0 Then
            Hint("没有找到任何 Minecraft 版本，请先在「下载」页安装一个版本。", HintType.Red)
            Return
        End If
        Dim InstanceItems As New List(Of IMyRadio)
        For Each Instance In Instances
            InstanceItems.Add(New MyRadioBox With {.Text = $"{Instance.Name}（{Instance.State}）"})
        Next
        Dim InstanceIndex As Integer? = MyMsgBoxSelect(InstanceItems,
            $"数据包「{Info.Name}」适用于 {Info.VersionHint}，装到哪个版本？", "下一步", "取消")
        If InstanceIndex Is Nothing Then Return
        Dim TargetInstance = Instances(InstanceIndex.Value)

        '再选存档
        Dim SavesFolder As String = TargetInstance.PathIndie & "saves\"
        Dim Saves = ModSaves.ScanSaves(SavesFolder)
        If Saves.Count = 0 Then
            Hint($"「{TargetInstance.Name}」还没有存档。请先启动一次游戏并创建世界。", HintType.Red)
            Return
        End If
        '构建存档选择列表
        Dim Items As New List(Of IMyRadio)
        For Each Save In Saves
            Dim VersionText As String = If(String.IsNullOrEmpty(Save.VersionName), "未知版本", Save.VersionName)
            Dim Box As New MyRadioBox With {.Text = $"{Save.Name}（{VersionText} · 已有 {Save.DataPackCount} 个数据包）"}
            Items.Add(Box)
        Next
        Dim Title As String = $"装到「{TargetInstance.Name}」的哪个存档？"
        Dim Selected As Integer? = MyMsgBoxSelect(Items, Title, "安装", "取消")
        If Selected Is Nothing Then Return
        Dim TargetSave = Saves(Selected.Value)
        Dim TargetFile = InstallToSave(ZipPath, TargetSave)
        Hint($"已安装到「{TargetSave.Name}」：{Path.GetFileName(TargetFile)}")
    End Sub

    ''' <summary>
    ''' 调试用：打印一个数据包的解析结果。
    ''' </summary>
    Public Sub DebugPrintDataPack(ZipPath As String)
        If Not File.Exists(ZipPath) Then
            Logger.Warn($"[DataPack] 文件不存在：{ZipPath}")
            Return
        End If
        Logger.Info($"[DataPack] 是否数据包：{IsDataPack(ZipPath)}")
        Dim Info = ReadDataPack(ZipPath)
        Logger.Info($"[DataPack] 名称={Info.Name} pack_format={Info.PackFormat} 区间={Info.MinFormat}~{Info.MaxFormat} 版本={Info.VersionHint}")
    End Sub

End Module
