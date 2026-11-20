'PCLPE：存档扫描（读取 level.dat，供数据包安装、存档浏览使用）

Imports System.IO

''' <summary>
''' 一个存档的信息。
''' </summary>
Public Class SaveInfo
    ''' <summary>
    ''' 存档文件夹完整路径。
    ''' </summary>
    Public Property Folder As String
    ''' <summary>
    ''' 世界名称（level.dat 中的 LevelName，读不到时用文件夹名）。
    ''' </summary>
    Public Property Name As String
    ''' <summary>
    ''' 创建/最后一次使用的 MC 版本名（level.dat 中的 Version.Name）。
    ''' </summary>
    Public Property VersionName As String
    ''' <summary>
    ''' 最后游玩时间。
    ''' </summary>
    Public Property LastPlayed As DateTime?
    ''' <summary>
    ''' 存档图标（saves/世界/icon.png），不存在时为 Nothing。
    ''' </summary>
    Public Property IconPath As String
    ''' <summary>
    ''' datapacks 文件夹中的数据包数量。
    ''' </summary>
    Public Property DataPackCount As Integer
    ''' <summary>
    ''' 存档自身 datapacks 文件夹路径。
    ''' </summary>
    Public ReadOnly Property DataPackFolder As String
        Get
            Return Path.Combine(Folder, "datapacks")
        End Get
    End Property
End Class

Public Module ModSaves

    ''' <summary>
    ''' 当前选中实例的 saves 文件夹（已考虑版本隔离）。
    ''' </summary>
    Public Function GetSavesFolder() As String
        Try
            If McInstanceSelected Is Nothing Then Return Nothing
            Return Path.Combine(McInstanceSelected.PathIndie, "saves")
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 扫描一个 saves 文件夹下的全部存档，按最后游玩时间倒序。
    ''' </summary>
    Public Function ScanSaves(SavesFolder As String) As List(Of SaveInfo)
        Dim Result As New List(Of SaveInfo)
        If String.IsNullOrEmpty(SavesFolder) OrElse Not Directory.Exists(SavesFolder) Then Return Result
        For Each Folder In Directory.GetDirectories(SavesFolder)
            Try
                Result.Add(ReadSaveInfo(Folder))
            Catch ex As Exception
                Logger.Warn(ex, $"读取存档信息失败：{Folder}")
            End Try
        Next
        Return Result.
            OrderByDescending(Function(s) If(s.LastPlayed, DateTime.MinValue)).
            ThenBy(Function(s) s.Name).
            ToList()
    End Function

    ''' <summary>
    ''' 读取单个存档文件夹的信息。
    ''' </summary>
    Public Function ReadSaveInfo(Folder As String) As SaveInfo
        Dim Info As New SaveInfo With {.Folder = Folder, .Name = Path.GetFileName(Folder)}

        '图标
        Dim IconFile As String = Path.Combine(Folder, "icon.png")
        If File.Exists(IconFile) Then Info.IconPath = IconFile

        '数据包数量
        Dim DataPackDir As String = Path.Combine(Folder, "datapacks")
        If Directory.Exists(DataPackDir) Then
            Info.DataPackCount = Directory.GetFiles(DataPackDir, "*.zip").Count +
                                 Directory.GetDirectories(DataPackDir).Count
        End If

        'level.dat
        Dim LevelFile As String = Path.Combine(Folder, "level.dat")
        If File.Exists(LevelFile) Then
            Dim Nbt = ModNbt.ReadNbtFile(LevelFile)
            Dim Data = TryCast(Nbt?("Data"), Dictionary(Of String, Object))
            If Data IsNot Nothing Then
                Dim WorldName As String = TryCast(Data?("LevelName"), String)
                If Not String.IsNullOrEmpty(WorldName) Then Info.Name = WorldName
                Dim VersionTag = TryCast(Data?("Version"), Dictionary(Of String, Object))
                If VersionTag IsNot Nothing Then Info.VersionName = TryCast(VersionTag?("Name"), String)
                Dim LastPlayed As Object = Data?("LastPlayed")
                If TypeOf LastPlayed Is Long Then
                    Dim Stamp As Long = CLng(LastPlayed)
                    If Stamp > 0 Then Info.LastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(Stamp).LocalDateTime
                End If
            End If
        End If

        '兜底：文件夹修改时间
        If Info.LastPlayed Is Nothing Then Info.LastPlayed = Directory.GetLastWriteTime(Folder)
        Return Info
    End Function

    ''' <summary>
    ''' 把当前实例的存档列表打印到日志（M1 阶段的验证入口）。
    ''' </summary>
    Public Sub LogSaveList(Optional Folder As String = Nothing)
        Dim SavesFolder As String = If(String.IsNullOrEmpty(Folder), GetSavesFolder(), Folder)
        Logger.Info($"[Saves] 存档目录：{SavesFolder}")
        Dim Saves = ScanSaves(SavesFolder)
        Logger.Info($"[Saves] 共找到 {Saves.Count} 个存档：")
        For Each Save In Saves
            Logger.Info($"[Saves] - 「{Save.Name}」 版本={If(String.IsNullOrEmpty(Save.VersionName), "未知", Save.VersionName)} " &
                        $"最后游玩={If(Save.LastPlayed, DateTime.MinValue):yyyy-MM-dd HH:mm} " &
                        $"数据包={Save.DataPackCount} 图标={If(Save.IconPath Is Nothing, "无", "有")}")
        Next
    End Sub

End Module
