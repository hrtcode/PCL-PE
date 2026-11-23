Public Class PageDownloadDataPack

    'PCLPE：本地数据包一键安装
    Private Sub BtnLocalInstall_Click() Handles BtnLocalInstall.Click
        Try
            ModDataPack.StartLocalInstall()
        Catch ex As Exception
            Logger.Error(ex, "本地安装数据包失败", LogBehavior.Toast)
        End Try
    End Sub

End Class
