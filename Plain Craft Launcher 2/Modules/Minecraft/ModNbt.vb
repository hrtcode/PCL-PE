'PCLPE：最小 NBT 读取器（只读，够读 level.dat 与世界信息即可）

Imports System.IO
Imports System.IO.Compression
Imports System.Text

Public Module ModNbt

    ''' <summary>
    ''' 读取一个 gzip 压缩的 NBT 文件（例如 level.dat），返回最外层的 Compound。
    ''' </summary>
    Public Function ReadNbtFile(File As String) As Dictionary(Of String, Object)
        Using Fs As New FileStream(File, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Using Gz As New GZipStream(Fs, CompressionMode.Decompress)
                Dim RootType As Byte = ReadByte(Gz)
                If RootType <> 10 Then Throw New Exception($"不是合法的 NBT 文件（根标签类型 {RootType}）")
                ReadString(Gz, ReadUInt16(Gz)) '根标签名
                Return CType(ReadTag(Gz, 10), Dictionary(Of String, Object))
            End Using
        End Using
    End Function

    Public Function ReadTag(Stream As Stream, TagType As Byte) As Object
        Select Case TagType
            Case 1
                Return ReadByte(Stream)
            Case 2
                Return ReadInt16(Stream)
            Case 3
                Return ReadInt32(Stream)
            Case 4
                Return ReadInt64(Stream)
            Case 5
                Return ReadSingle(Stream)
            Case 6
                Return ReadDouble(Stream)
            Case 7
                Dim ByteCount As Integer = ReadInt32(Stream)
                Dim Buffer(ByteCount - 1) As Byte
                ReadFull(Stream, Buffer, ByteCount)
                Return Buffer
            Case 8
                Return ReadString(Stream, ReadUInt16(Stream))
            Case 9
                Dim ItemType As Byte = ReadByte(Stream)
                Dim Count As Integer = ReadInt32(Stream)
                Dim Items As New List(Of Object)
                For i = 1 To Count
                    Items.Add(ReadTag(Stream, ItemType))
                Next
                Return Items
            Case 10
                Dim Compound As New Dictionary(Of String, Object)
                Do
                    Dim ChildType As Byte = ReadByte(Stream)
                    If ChildType = 0 Then Exit Do
                    Dim ChildName As String = ReadString(Stream, ReadUInt16(Stream))
                    Compound(ChildName) = ReadTag(Stream, ChildType)
                Loop
                Return Compound
            Case 11
                Dim Count As Integer = ReadInt32(Stream)
                Dim Array(Count - 1) As Integer
                For i = 0 To Count - 1
                    Array(i) = ReadInt32(Stream)
                Next
                Return Array
            Case 12
                Dim Count As Integer = ReadInt32(Stream)
                Dim Array(Count - 1) As Long
                For i = 0 To Count - 1
                    Array(i) = ReadInt64(Stream)
                Next
                Return Array
            Case Else
                Throw New Exception($"不支持的 NBT 标签类型 {TagType}")
        End Select
    End Function

    '读取工具

    Private Function ReadByte(Stream As Stream) As Byte
        Dim Value As Integer = Stream.ReadByte()
        If Value < 0 Then Throw New EndOfStreamException("NBT 数据意外结束")
        Return CByte(Value)
    End Function

    Private Sub ReadFull(Stream As Stream, Buffer() As Byte, Count As Integer)
        Dim Offset As Integer = 0
        While Offset < Count
            Dim ReadCount As Integer = Stream.Read(Buffer, Offset, Count - Offset)
            If ReadCount <= 0 Then Throw New EndOfStreamException("NBT 数据意外结束")
            Offset += ReadCount
        End While
    End Sub

    Private Function ReadUInt16(Stream As Stream) As Integer
        Dim Buffer(1) As Byte
        ReadFull(Stream, Buffer, 2)
        Return (CInt(Buffer(0)) << 8) Or CInt(Buffer(1))
    End Function

    Private Function ReadInt16(Stream As Stream) As Short
        Dim Buffer(1) As Byte
        ReadFull(Stream, Buffer, 2)
        Return CShort((CInt(Buffer(0)) << 8) Or CInt(Buffer(1)))
    End Function

    Private Function ReadInt32(Stream As Stream) As Integer
        Dim Buffer(3) As Byte
        ReadFull(Stream, Buffer, 4)
        Return (CInt(Buffer(0)) << 24) Or (CInt(Buffer(1)) << 16) Or (CInt(Buffer(2)) << 8) Or CInt(Buffer(3))
    End Function

    Private Function ReadInt64(Stream As Stream) As Long
        Dim Buffer(7) As Byte
        ReadFull(Stream, Buffer, 8)
        Dim Value As Long = 0
        For i = 0 To 7
            Value = (Value << 8) Or CLng(Buffer(i))
        Next
        Return Value
    End Function

    Private Function ReadSingle(Stream As Stream) As Single
        Dim Buffer(3) As Byte
        ReadFull(Stream, Buffer, 4)
        Array.Reverse(Buffer)
        Return BitConverter.ToSingle(Buffer, 0)
    End Function

    Private Function ReadDouble(Stream As Stream) As Double
        Dim Buffer(7) As Byte
        ReadFull(Stream, Buffer, 8)
        Array.Reverse(Buffer)
        Return BitConverter.ToDouble(Buffer, 0)
    End Function

    Private Function ReadString(Stream As Stream, ByteCount As Integer) As String
        If ByteCount <= 0 Then Return ""
        Dim Buffer(ByteCount - 1) As Byte
        ReadFull(Stream, Buffer, ByteCount)
        Return Encoding.UTF8.GetString(Buffer)
    End Function

End Module
